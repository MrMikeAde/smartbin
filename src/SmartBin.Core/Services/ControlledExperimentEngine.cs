using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class ControlledExperimentEngine
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly IRecycleBinMutationService _mutationService;
        private readonly ICompressionService _compressionService;
        private readonly IFileHasher _fileHasher;
        private readonly IStorageManager _storageManager;
        private readonly IFailureInjector _failureInjector;

        public ControlledExperimentEngine(
            ISmartBinRepository<SmartBinItem> repository,
            IRecycleBinMutationService mutationService,
            ICompressionService compressionService,
            IFileHasher fileHasher,
            IStorageManager storageManager,
            IFailureInjector? failureInjector = null)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mutationService = mutationService ?? throw new ArgumentNullException(nameof(mutationService));
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

        /// <summary>
        /// Runs the sequential acquisition, compression, and verification pipeline up to ReadyForCommit stage.
        /// Does NOT modify the Windows Recycle Bin item yet (respects commit boundary).
        /// </summary>
        public async Task<ControlledExperimentItem> PrepareAndVerifyAsync(
            WindowsRecycleBinItem item,
            Action<ExperimentState>? stateChangedCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (string.IsNullOrWhiteSpace(item.Id)) throw new ArgumentException("Item must have a valid ID.", nameof(item));

            // Validate eligibility
            if (item.Size <= 0)
            {
                throw new InvalidOperationException("Item is not eligible: File size must be greater than zero.");
            }
            if (string.IsNullOrWhiteSpace(item.OriginalPath))
            {
                throw new InvalidOperationException("Item is not eligible: Original path is unknown.");
            }

            // Validate working storage capacity (Working storage exhaustion safety)
            var availableSpace = await _storageManager.GetAvailableFreeSpaceAsync(cancellationToken);
            if (availableSpace < item.Size)
            {
                throw new InvalidOperationException($"Insufficient temporary storage. Required: {item.Size:N0} bytes, Available: {availableSpace:N0} bytes.");
            }

            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");
            Directory.CreateDirectory(tempDir);

            // Establish temporary paths
            var tempAcquiredPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".acq");
            var tempCompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip");
            var tempDecompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".unzip");
            var tempRestoredPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".dryrestore");

            // Ensure constructed paths do not violate trust boundaries
            EnsurePathIsSecure(tempAcquiredPath);
            EnsurePathIsSecure(tempCompressedPath);
            EnsurePathIsSecure(tempDecompressedPath);
            EnsurePathIsSecure(tempRestoredPath);

            var experiment = new ControlledExperimentItem
            {
                WindowsItemIdentifier = item.Id,
                OriginalPath = item.OriginalPath,
                OriginalSize = item.Size,
                DeletionTimestamp = item.DeletedTimestamp ?? DateTime.UtcNow,
                Volume = item.Volume,
                State = ExperimentState.Discovered,
                DidWindowsMutationOccur = false
            };

            bool pipelineSucceeded = false;

            try
            {
                // 1. STATE: Acquired
                experiment.State = ExperimentState.Acquired;
                stateChangedCallback?.Invoke(experiment.State);

                await _mutationService.ExtractItemContentAsync(item.Id, tempAcquiredPath, cancellationToken);
                _failureInjector.Check("AfterAcquisition");

                // 2. STATE: AcquisitionVerified
                experiment.State = ExperimentState.AcquisitionVerified;
                stateChangedCallback?.Invoke(experiment.State);

                var acqInfo = new FileInfo(tempAcquiredPath);
                if (!acqInfo.Exists || acqInfo.Length != item.Size)
                {
                    throw new InvalidOperationException("Acquisition failed: Copied file size does not match expected size or is missing.");
                }

                // Reparse point / Symlink check on acquired temp file
                if ((acqInfo.Attributes & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint)
                {
                    throw new InvalidOperationException("Reparse points are not supported for safety.");
                }

                var originalHash = await _fileHasher.ComputeHashAsync(tempAcquiredPath, cancellationToken);
                experiment.OriginalSha256 = originalHash;
                _failureInjector.Check("AfterHashing");

                // 3. STATE: Compressed
                experiment.State = ExperimentState.Compressed;
                stateChangedCallback?.Invoke(experiment.State);

                _failureInjector.Check("BeforeCompression");
                await _compressionService.CompressAsync(tempAcquiredPath, tempCompressedPath, cancellationToken);
                _failureInjector.Check("DuringCompression");
                _failureInjector.Check("AfterCompression");

                var compressedInfo = new FileInfo(tempCompressedPath);
                experiment.CompressedSize = compressedInfo.Length;
                experiment.CompressionRatio = (double)compressedInfo.Length / item.Size;
                experiment.ActualSavingsBytes = item.Size - compressedInfo.Length;

                // Verify compression savings threshold: MUST produce meaningful savings
                if (experiment.ActualSavingsBytes <= 1024 || experiment.CompressionRatio >= 0.98)
                {
                    throw new InvalidOperationException("Compression aborted: Content is not compressible enough to yield savings.");
                }

                // 4. STATE: CompressionVerified
                experiment.State = ExperimentState.CompressionVerified;
                stateChangedCallback?.Invoke(experiment.State);

                _failureInjector.Check("BeforeCompressionVerification");
                await _compressionService.DecompressAsync(tempCompressedPath, tempDecompressedPath, cancellationToken);
                var decompressedHash = await _fileHasher.ComputeHashAsync(tempDecompressedPath, cancellationToken);

                if (decompressedHash != originalHash)
                {
                    throw new InvalidOperationException("Compression verification failed: Decompressed content SHA-256 mismatch.");
                }
                _failureInjector.Check("AfterCompressionVerification");

                // 5. STATE: RestorationVerified (Simulated restoration dry-run)
                experiment.State = ExperimentState.RestorationVerified;
                stateChangedCallback?.Invoke(experiment.State);

                _failureInjector.Check("BeforeRestorationVerification");
                // Decompress to mock restore path
                await _compressionService.DecompressAsync(tempCompressedPath, tempRestoredPath, cancellationToken);
                var restoredHash = await _fileHasher.ComputeHashAsync(tempRestoredPath, cancellationToken);

                if (restoredHash != originalHash)
                {
                    throw new InvalidOperationException("Restoration dry-run verification failed: Restored hash mismatch.");
                }
                _failureInjector.Check("AfterRestorationVerification");

                experiment.RestorationResultPath = tempRestoredPath;
                experiment.FinalVerificationHash = restoredHash;

                // 6. STATE: ReadyForCommit (All checks succeeded! Ready for user confirmation)
                experiment.State = ExperimentState.ReadyForCommit;
                stateChangedCallback?.Invoke(experiment.State);

                pipelineSucceeded = true;
            }
            catch (Exception ex)
            {
                experiment.State = ExperimentState.Failed;
                experiment.FailureMessage = ex.Message;
                stateChangedCallback?.Invoke(experiment.State);

                // Clean up all temporary files immediately on failure
                CleanFile(tempAcquiredPath);
                CleanFile(tempCompressedPath);
                CleanFile(tempDecompressedPath);
                CleanFile(tempRestoredPath);

                throw;
            }
            finally
            {
                // Clean up non-necessary temporary files
                CleanFile(tempAcquiredPath);
                CleanFile(tempDecompressedPath);

                if (!pipelineSucceeded)
                {
                    CleanFile(tempCompressedPath);
                    CleanFile(tempRestoredPath);
                }
            }

            return experiment;
        }

        /// <summary>
        /// Commits the verified experiment: moves compressed representation to objects/, optionally invokes Windows mutation, and persists metadata.
        /// </summary>
        public async Task CommitExperimentAsync(
            ControlledExperimentItem experiment,
            bool executeWindowsMutation,
            Action<ExperimentState>? stateChangedCallback = null,
            CancellationToken cancellationToken = default)
        {
            if (experiment == null) throw new ArgumentNullException(nameof(experiment));
            if (experiment.State != ExperimentState.ReadyForCommit)
            {
                throw new InvalidOperationException("Cannot commit: Experiment is not in ReadyForCommit state.");
            }

            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");
            var objectsDir = Path.Combine(rootDir, "objects");

            // Look for expected compressed file from prep stage (we know its path pattern)
            // For simple retrieval, we can rebuild the path or find the restored dry-run file.
            // Since we know the compressed file is preserved, let's look in the temp folder.
            var tempFiles = Directory.GetFiles(tempDir, "*.zip");
            if (tempFiles.Length == 0)
            {
                throw new FileNotFoundException("Verified compressed file representation not found in temp storage.");
            }
            var tempCompressedFile = tempFiles[0];

            bool commitCompleted = false;
            var receiptPath = Path.Combine(tempDir, experiment.WindowsItemIdentifier + ".receipt");

            try
            {
                _failureInjector.Check("BeforeCommit");

                // Move compressed file to permanent objects folder
                var finalObjectsPath = Path.Combine(objectsDir, Guid.NewGuid().ToString("N") + ".z");

                // Security path traversal check
                EnsurePathIsSecure(finalObjectsPath);

                File.Move(tempCompressedFile, finalObjectsPath, overwrite: true);

                // Write commit receipt file
                var receiptContent = $"{experiment.OriginalPath}\n{experiment.OriginalSize}\n{experiment.CompressedSize}\n{experiment.OriginalSha256}\n{finalObjectsPath}\n{experiment.WindowsItemIdentifier}\n{experiment.DeletionTimestamp.Ticks}";
                File.WriteAllText(receiptPath, receiptContent);

                // Perform Windows Mutation if requested and supported
                if (executeWindowsMutation)
                {
                    _failureInjector.Check("DuringCommit");
                    await _mutationService.RemoveItemAsync(experiment.WindowsItemIdentifier, cancellationToken);
                    experiment.DidWindowsMutationOccur = true;
                }

                _failureInjector.Check("AfterCommit");
                _failureInjector.Check("BeforeActivityPersistence");

                // Persist as a standard recoverable item in the SmartBin repository
                var smartBinItem = new SmartBinItem
                {
                    Id = Guid.NewGuid(),
                    OriginalPath = experiment.OriginalPath,
                    OriginalFileName = Path.GetFileName(experiment.OriginalPath),
                    OriginalExtension = Path.GetExtension(experiment.OriginalPath),
                    OriginalSize = experiment.OriginalSize,
                    CurrentStoredSize = experiment.CompressedSize,
                    DeletedTimestamp = experiment.DeletionTimestamp,
                    Sha256Hash = experiment.OriginalSha256,
                    CurrentStoragePath = finalObjectsPath,
                    CompressionStatus = CompressionStatus.Compressed,
                    CompressionAlgorithm = CompressionAlgorithm.Zip,
                    CompressionTimestamp = DateTime.UtcNow,
                    RestorationStatus = RestorationStatus.Pending
                };

                await _repository.AddAsync(smartBinItem, cancellationToken);

                _failureInjector.Check("AfterActivityPersistence");

                // Clean up receipt upon successful DB persistence
                if (File.Exists(receiptPath))
                {
                    File.Delete(receiptPath);
                }

                experiment.State = ExperimentState.Committed;
                experiment.UpdatedTimestamp = DateTime.UtcNow;
                stateChangedCallback?.Invoke(experiment.State);

                commitCompleted = true;
            }
            catch (Exception ex)
            {
                experiment.State = ExperimentState.Failed;
                experiment.FailureMessage = ex.Message;
                stateChangedCallback?.Invoke(experiment.State);
                throw;
            }
            finally
            {
                // Ensure remaining restore-dryrun files are cleaned
                var dryRestoreFiles = Directory.GetFiles(tempDir, "*.dryrestore");
                foreach (var f in dryRestoreFiles) CleanFile(f);

                if (!commitCompleted)
                {
                    CleanFile(tempCompressedFile);
                }
            }
        }

        private static void CleanFile(string path)
        {
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
