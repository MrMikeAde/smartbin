using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class CrashRecoveryService
    {
        private readonly IStorageManager _storageManager;

        public CrashRecoveryService(IStorageManager storageManager)
        {
            _storageManager = storageManager ?? throw new ArgumentNullException(nameof(storageManager));
        }

        /// <summary>
        /// Scans the controlled SmartBin storage temp/ folder on startup and cleans up unfinished temporary file artifacts.
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
            try
            {
                // Scan for intermediate temporary file extensions
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
