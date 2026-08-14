using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;

namespace SmartBin.Infrastructure.Services
{
    public class RestoreService : IRestoreService
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly ICompressionService _compressionService;
        private readonly IFileHasher _fileHasher;
        private readonly IStorageManager _storageManager;
        private readonly IFailureInjector _failureInjector;

        public RestoreService(
            ISmartBinRepository<SmartBinItem> repository,
            ICompressionService compressionService,
            IFileHasher fileHasher,
            IStorageManager storageManager,
            IFailureInjector? failureInjector = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
            _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
            _failureInjector = failureInjector ?? new NoOpFailureInjector();
        }

        private void EnsurePathIsSecure(string path)
        {
            var fullPath = Path.GetFullPath(path);
            var rootPath = Path.GetFullPath(_storageManager.GetStoragePath());
            if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Access denied: Path '{path}' is outside the authorized SmartBin storage root.");
            }
        }

        public async Task RestoreAsync(Guid itemId, string? targetPath = null, CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetByIdAsync(itemId, cancellationToken);
            if (item == null)
            {
                throw new SmartBinException($"SmartBin item with ID {itemId} not found.");
            }

            var storedFilePath = item.CurrentStoragePath;
            if (!File.Exists(storedFilePath))
            {
                throw new FileNotFoundException($"Stored file representation not found on disk: {storedFilePath}", storedFilePath);
            }

            // Symlink/Reparse Point Guard on stored file
            var storedAttributes = File.GetAttributes(storedFilePath);
            if ((storedAttributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
            {
                throw new InvalidOperationException("Reparse points are not supported for safety.");
            }

            // Determine final destination path
            var destination = string.IsNullOrWhiteSpace(targetPath) ? item.OriginalPath : targetPath;
            destination = Path.GetFullPath(destination);

            // Path Traversal Mitigation: Ensure we are not restoring to system sensitive folders
            var canonicalDest = Path.GetFullPath(destination);
            var winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (!string.IsNullOrWhiteSpace(winDir))
            {
                var systemRoot = Path.GetFullPath(winDir);
                if (canonicalDest.StartsWith(systemRoot, StringComparison.OrdinalIgnoreCase))
                {
                    throw new UnauthorizedAccessException($"Restoration to system directory '{destination}' is blocked for security.");
                }
            }

            // Cross-platform Unix system directories protection
            if (canonicalDest.StartsWith("/etc", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/bin", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/var", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/usr", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/sys", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/proc", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/dev", StringComparison.OrdinalIgnoreCase) ||
                canonicalDest.StartsWith("/boot", StringComparison.OrdinalIgnoreCase))
            {
                throw new UnauthorizedAccessException($"Restoration to system directory '{destination}' is blocked for security.");
            }

            // Overwrite protection: Return conflict result if destination exists
            if (File.Exists(destination))
            {
                throw new SmartBinConflictException($"Destination file already exists: {destination}", destination);
            }

            // Reparse Point / Symlink Guard on destination folder/file if it exists
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                var destAttributes = File.GetAttributes(destination);
                if ((destAttributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    throw new InvalidOperationException("Restoring to a reparse point (symlink/junction) is blocked for safety.");
                }
            }

            // Establish paths inside controlled temp directory
            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");
            Directory.CreateDirectory(tempDir);

            var tempRestorationFile = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".restore");

            // Secure path validation on temp restoration file
            EnsurePathIsSecure(tempRestorationFile);

            bool verifiedAndCommitted = false;

            try
            {
                _failureInjector.Check("BeforeRestoration");

                // Decompress or copy to temp restoration file
                if (item.CompressionStatus == CompressionStatus.Compressed)
                {
                    await _compressionService.DecompressAsync(storedFilePath, tempRestorationFile, cancellationToken);
                }
                else
                {
                    // Copy original uncompressed from storage to temp
                    using (var src = new FileStream(storedFilePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true))
                    using (var dst = new FileStream(tempRestorationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, useAsync: true))
                    {
                        await src.CopyToAsync(dst, cancellationToken);
                    }
                }

                _failureInjector.Check("BeforeRestorationVerification");

                // Calculate restored file's SHA-256 hash
                var restoredHash = await _fileHasher.ComputeHashAsync(tempRestorationFile, cancellationToken);

                // Verify with original recorded hash
                if (restoredHash != item.Sha256Hash)
                {
                    throw new SmartBinException("Integrity check failed: Restored file SHA-256 hash does not match original recorded hash.");
                }

                _failureInjector.Check("AfterRestorationVerification");

                _failureInjector.Check("BeforeCommit");

                // Ensure target directory exists
                var targetFolder = Path.GetDirectoryName(destination);
                if (!string.IsNullOrEmpty(targetFolder))
                {
                    Directory.CreateDirectory(targetFolder);
                }

                // Double check destination existence before moving (atomic check-then-act guard)
                if (File.Exists(destination))
                {
                    throw new SmartBinConflictException($"Destination file already exists just before commit: {destination}", destination);
                }

                _failureInjector.Check("DuringCommit");

                // Atomically move/rename from temp file to destination
                File.Move(tempRestorationFile, destination, overwrite: false);

                _failureInjector.Check("AfterCommit");

                // Update database state only after physical filesystem restoration succeeds
                item.RestorationStatus = RestorationStatus.Restored;
                await _repository.UpdateAsync(item, cancellationToken);

                verifiedAndCommitted = true;
            }
            finally
            {
                // Always clean up temp files
                if (!verifiedAndCommitted && File.Exists(tempRestorationFile))
                {
                    try { File.Delete(tempRestorationFile); } catch { /* Ignore */ }
                }
            }
        }
    }
}
