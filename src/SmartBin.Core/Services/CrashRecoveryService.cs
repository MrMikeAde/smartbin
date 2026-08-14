using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class CrashRecoveryService
    {
        private readonly IStorageManager _storageManager;
        private readonly ISmartBinRepository<SmartBinItem>? _repository;

        public CrashRecoveryService(IStorageManager storageManager, ISmartBinRepository<SmartBinItem>? repository = null)
        {
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
            _repository = repository;
        }

        /// <summary>
        /// Scans the controlled SmartBin storage temp/ folder on startup, processes any commit receipts to resolve DB-external inconsistencies, and cleans up unfinished temporary file artifacts.
        /// Returns the count of deleted residual files.
        /// </summary>
        public int PerformStartupRecoveryAndCleanup()
        {
            var rootDir = _storageManager.GetStoragePath();
            var tempDir = Path.Combine(rootDir, "temp");

            if (!Directory.Exists(tempDir))
            {
                return 0;
            }

            int cleanedCount = 0;

            // 1. Process commit receipts to resolve database inconsistencies (Committed but not persisted)
            if (_repository != null)
            {
                try
                {
                    var receiptFiles = Directory.GetFiles(tempDir, "*.receipt");
                    foreach (var receiptPath in receiptFiles)
                    {
                        try
                        {
                            var lines = File.ReadAllLines(receiptPath);
                            if (lines.Length >= 7)
                            {
                                var originalPath = lines[0];
                                var originalSize = long.Parse(lines[1]);
                                var compressedSize = long.Parse(lines[2]);
                                var sha256 = lines[3];
                                var storagePath = lines[4];
                                var winId = lines[5];
                                var ticks = long.Parse(lines[6]);
                                var deletionTime = new DateTime(ticks, DateTimeKind.Utc);

                                // Check if this item is already in the database
                                var dbItemsTask = _repository.GetAllAsync();
                                dbItemsTask.Wait(); // Blocking wait since this is synchronous startup recovery
                                var dbItems = dbItemsTask.Result;

                                var exists = dbItems.Any(item => item.Sha256Hash == sha256 || item.CurrentStoragePath == storagePath);

                                if (!exists && File.Exists(storagePath))
                                {
                                    // Inconsistency detected: Physical compressed file exists, Recycle Bin item is mutated, but DB record is missing.
                                    // Reconstruct the record to preserve user data!
                                    var recoveredItem = new SmartBinItem
                                    {
                                        Id = Guid.NewGuid(),
                                        OriginalPath = originalPath,
                                        OriginalFileName = Path.GetFileName(originalPath),
                                        OriginalExtension = Path.GetExtension(originalPath),
                                        OriginalSize = originalSize,
                                        CurrentStoredSize = compressedSize,
                                        DeletedTimestamp = deletionTime,
                                        Sha256Hash = sha256,
                                        CurrentStoragePath = storagePath,
                                        CompressionStatus = CompressionStatus.Compressed,
                                        CompressionAlgorithm = CompressionAlgorithm.Zip,
                                        CompressionTimestamp = DateTime.UtcNow,
                                        RestorationStatus = RestorationStatus.Pending
                                    };

                                    var addAsyncTask = _repository.AddAsync(recoveredItem);
                                    addAsyncTask.Wait();
                                }
                            }

                            // Delete receipt file after processing
                            File.Delete(receiptPath);
                            cleanedCount++;
                        }
                        catch
                        {
                            // Ignore individual receipt parsing failures
                        }
                    }
                }
                catch
                {
                    // Ignore broad receipt processing failures
                }
            }

            // 2. Scan for intermediate temporary file extensions and clean them up
            try
            {
                var extensions = new[] { "*.acq", "*.zip", "*.unzip", "*.restore", "*.dryrestore" };
                foreach (var ext in extensions)
                {
                    var files = Directory.GetFiles(tempDir, ext);
                    foreach (var file in files)
                    {
                        try
                        {
                            File.Delete(file);
                            cleanedCount++;
                        }
                        catch
                        {
                            // Ignore lock issues, try on next boot
                        }
                    }
                }
            }
            catch
            {
                // Safely ignore scan errors on startup
            }

            return cleanedCount;
        }
    }
}
