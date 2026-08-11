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

        public ControlledExperimentEngine(
            ISmartBinRepository<SmartBinItem> repository,
            IRecycleBinMutationService mutationService,
            ICompressionService compressionService,
            IFileHasher fileHasher,
            IStorageManager storageManager)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _mutationService = mutationService ?? throw new ArgumentNullException(nameof(mutationService));
            _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
            _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
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

            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");
            Directory.CreateDirectory(tempDir);

            // Establish temporary paths
            var tempAcquiredPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".acq");
            var tempCompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip");
            var tempDecompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".unzip");
            var tempRestoredPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".dryrestore");

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

                // 2. STATE: AcquisitionVerified
                experiment.State = ExperimentState.AcquisitionVerified;
                stateChangedCallback?.Invoke(experiment.State);

                var acqInfo = new FileInfo(tempAcquiredPath);
                if (!acqInfo.Exists || acqInfo.Length != item.Size)
                {
                    throw new InvalidOperationException("Acquisition failed: Copied file size does not match expected size or is missing.");
                }

                var originalHash = await _fileHasher.ComputeHashAsync(tempAcquiredPath, cancellationToken);
                experiment.OriginalSha256 = originalHash;

                // 3. STATE: Compressed
                experiment.State = ExperimentState.Compressed;
                stateChangedCallback?.Invoke(experiment.State);

                await _compressionService.CompressAsync(tempAcquiredPath, tempCompressedPath, cancellationToken);

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

                await _compressionService.DecompressAsync(tempCompressedPath, tempDecompressedPath, cancellationToken);
                var decompressedHash = await _fileHasher.ComputeHashAsync(tempDecompressedPath, cancellationToken);

                if (decompressedHash != originalHash)
                {
                    throw new InvalidOperationException("Compression verification failed: Decompressed content SHA-256 mismatch.");
                }

                // 5. STATE: RestorationVerified (Simulated restoration dry-run)
                experiment.State = ExperimentState.RestorationVerified;
                stateChangedCallback?.Invoke(experiment.State);

                // Decompress to mock restore path
                await _compressionService.DecompressAsync(tempCompressedPath, tempRestoredPath, cancellationToken);
                var restoredHash = await _fileHasher.ComputeHashAsync(tempRestoredPath, cancellationToken);

                if (restoredHash != originalHash)
                {
                    throw new InvalidOperationException("Restoration dry-run verification failed: Restored hash mismatch.");
                }

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

            try
            {
                // Move compressed file to permanent objects folder
                var finalObjectsPath = Path.Combine(objectsDir, Guid.NewGuid().ToString("N") + ".z");
                File.Move(tempCompressedFile, finalObjectsPath, overwrite: true);

                // Perform Windows Mutation if requested and supported
                if (executeWindowsMutation)
                {
                    await _mutationService.RemoveItemAsync(experiment.WindowsItemIdentifier, cancellationToken);
                    experiment.DidWindowsMutationOccur = true;
                }

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
