using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class CompressionEngine : ICompressionEngine
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly ICompressionService _compressionService;
        private readonly IFileHasher _fileHasher;
        private readonly IStorageManager _storageManager;

        // Configurable thresholds (sensible defaults: 5% saving, min 1024 bytes)
        public double MinSavingsRatioThreshold { get; set; } = 0.95; // 5% saving
        public long MinSavingsBytesThreshold { get; set; } = 1024; // 1 KB

        public CompressionEngine(
            ISmartBinRepository<SmartBinItem> repository,
            ICompressionService compressionService,
            IFileHasher fileHasher,
            IStorageManager storageManager)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _compressionService = compressionService ?? throw new ArgumentNullException(nameof(compressionService));
            _fileHasher = fileHasher ?? throw new ArgumentNullException(nameof(fileHasher));
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        }

        public async Task CompressItemAsync(Guid itemId, CancellationToken cancellationToken = default)
        {
            var item = await _repository.GetByIdAsync(itemId, cancellationToken);
            if (item == null)
            {
                throw new InvalidOperationException($"SmartBin item with ID {itemId} not found.");
            }

            if (item.CompressionStatus == CompressionStatus.Compressed)
            {
                return; // Already compressed
            }

            var originalFilePath = item.CurrentStoragePath;
            if (!File.Exists(originalFilePath))
            {
                throw new FileNotFoundException("Physical file representation not found in storage.", originalFilePath);
            }

            // 1. Check Extension Heuristics
            if (CompressionHeuristics.IsTypicallyCompressed(item.OriginalExtension))
            {
                item.CompressionStatus = CompressionStatus.NotFeasible;
                item.CompressionAlgorithm = CompressionAlgorithm.None;
                item.CurrentStoredSize = item.OriginalSize;
                item.CompressionTimestamp = DateTime.UtcNow;
                await _repository.UpdateAsync(item, cancellationToken);
                return;
            }

            // Define temporary file paths inside the storage manager temp folder
            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");
            Directory.CreateDirectory(tempDir);

            var tempCompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".zip");
            var tempDecompressedPath = Path.Combine(tempDir, Guid.NewGuid().ToString("N") + ".unzip");

            bool completedSuccessfully = false;

            try
            {
                // 2. Perform compression to temporary file
                await _compressionService.CompressAsync(originalFilePath, tempCompressedPath, cancellationToken);

                var tempCompressedInfo = new FileInfo(tempCompressedPath);
                var compressedSize = tempCompressedInfo.Length;

                // 3. Evaluate compression decision thresholds
                var spaceSaved = item.OriginalSize - compressedSize;
                double compressionRatio = item.OriginalSize > 0 ? (double)compressedSize / item.OriginalSize : 1.0;

                bool isWorthwhile = spaceSaved >= MinSavingsBytesThreshold && compressionRatio <= MinSavingsRatioThreshold;

                if (!isWorthwhile)
                {
                    // Clean up and mark as not feasible
                    if (File.Exists(tempCompressedPath))
                    {
                        File.Delete(tempCompressedPath);
                    }

                    item.CompressionStatus = CompressionStatus.NotFeasible;
                    item.CompressionAlgorithm = CompressionAlgorithm.None;
                    item.CurrentStoredSize = item.OriginalSize;
                    item.CompressionTimestamp = DateTime.UtcNow;
                    await _repository.UpdateAsync(item, cancellationToken);
                    return;
                }

                // 4. Verify Integrity: Decompress and compute SHA-256
                await _compressionService.DecompressAsync(tempCompressedPath, tempDecompressedPath, cancellationToken);

                var decompressedHash = await _fileHasher.ComputeHashAsync(tempDecompressedPath, cancellationToken);
                if (decompressedHash != item.Sha256Hash)
                {
                    throw new InvalidOperationException("Integrity check failed: Decompressed file hash does not match original hash.");
                }

                // 5. Commit atomic swap:
                // Move temporary compressed file to its permanent object storage name
                var finalCompressedPath = Path.Combine(Path.Combine(rootDir, "objects"), Guid.NewGuid().ToString("N") + ".z");
                File.Move(tempCompressedPath, finalCompressedPath, overwrite: true);

                // Update metadata first
                item.CompressionStatus = CompressionStatus.Compressed;
                item.CompressionAlgorithm = CompressionAlgorithm.Zip;
                item.CurrentStoragePath = finalCompressedPath;
                item.CurrentStoredSize = compressedSize;
                item.CompressionTimestamp = DateTime.UtcNow;

                // Save to database first to ensure atomicity
                await _repository.UpdateAsync(item, cancellationToken);

                // Only delete the original uncompressed file representation after metadata is successfully updated in DB
                if (File.Exists(originalFilePath))
                {
                    File.Delete(originalFilePath);
                }

                completedSuccessfully = true;
            }
            finally
            {
                // Clean up temporary leftovers
                if (!completedSuccessfully && File.Exists(tempCompressedPath))
                {
                    try { File.Delete(tempCompressedPath); } catch { /* Ignore */ }
                }
                if (File.Exists(tempDecompressedPath))
                {
                    try { File.Delete(tempDecompressedPath); } catch { /* Ignore */ }
                }
            }
        }
    }
}
