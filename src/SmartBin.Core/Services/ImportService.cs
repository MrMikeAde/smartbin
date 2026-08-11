using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class ImportService : IImportService
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly IFileHasher _fileHasher;
        private readonly IStorageManager _storageManager;

        public ImportService(
            ISmartBinRepository<SmartBinItem> repository,
            IFileHasher fileHasher,
            IStorageManager storageManager)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        }

        public async Task<ISmartBinItem> ImportFileAsync(string sourcePath, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(sourcePath))
            {
                throw new ArgumentException("Source path cannot be null or empty.", nameof(sourcePath));
            }

            if (!File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Source file does not exist.", sourcePath);
            }

            var fileInfo = new FileInfo(sourcePath);
            var originalSize = fileInfo.Length;

            // 1. Calculate original SHA-256 (using streaming)
            var originalHash = await _fileHasher.ComputeHashAsync(sourcePath, cancellationToken);

            // 2. Copy the source into controlled SmartBin storage (MoveToStorage)
            var storedPath = await _storageManager.MoveToStorageAsync(sourcePath, cancellationToken);

            try
            {
                // 3. Verify the copied representation (calculate hash and match)
                var copiedHash = await _fileHasher.ComputeHashAsync(storedPath, cancellationToken);
                if (originalHash != copiedHash)
                {
                    throw new InvalidOperationException("Integrity check failed: Copied file hash does not match original file hash.");
                }

                // 4. Capture metadata
                var item = new SmartBinItem
                {
                    Id = Guid.NewGuid(),
                    OriginalPath = Path.GetFullPath(sourcePath),
                    OriginalFileName = Path.GetFileName(sourcePath),
                    OriginalExtension = Path.GetExtension(sourcePath),
                    OriginalSize = originalSize,
                    CurrentStoredSize = originalSize,
                    DeletedTimestamp = DateTime.UtcNow,
                    OriginalCreationTimestamp = fileInfo.CreationTimeUtc,
                    OriginalModificationTimestamp = fileInfo.LastWriteTimeUtc,
                    Sha256Hash = originalHash,
                    CurrentStoragePath = storedPath,
                    CompressionStatus = CompressionStatus.Uncompressed,
                    CompressionAlgorithm = CompressionAlgorithm.None,
                    RestorationStatus = RestorationStatus.Pending
                };

                // Validate item
                if (!item.IsValid())
                {
                    throw new InvalidOperationException("Domain model validation failed: The created metadata is invalid.");
                }

                // 5. Persist metadata only after successful verification
                await _repository.AddAsync(item, cancellationToken);

                return item;
            }
            catch (Exception)
            {
                // Rollback file copy on exception to avoid stale files in controlled storage
                if (File.Exists(storedPath))
                {
                    try { File.Delete(storedPath); } catch { /* Ignore */ }
                }
                throw;
            }
        }
    }
}
