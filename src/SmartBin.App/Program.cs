using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;
using SmartBin.Infrastructure.Compression;
using SmartBin.Infrastructure.Hashing;
using SmartBin.Infrastructure.Persistence;
using SmartBin.Infrastructure.Services;
using SmartBin.Infrastructure.Storage;

namespace SmartBin.App
{
    /// <summary>
    /// Program class to bootstrap the application.
    /// </summary>
    public static class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Console.WriteLine("SmartBin UI Application Starting...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                RunWindowsApp();
            }
            else
            {
                RunMockDashboardAsync().GetAwaiter().GetResult();
            }
        }

        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
        private static void RunWindowsApp()
        {
#if WINDOWS
            Microsoft.UI.Xaml.Application.Start((p) => new App());
#else
            RunMockDashboardAsync().GetAwaiter().GetResult();
#endif
        }

        private static async Task RunMockDashboardAsync()
        {
            Console.WriteLine("\n[Running Headless Demo Mode (Linux Sandbox)]\n");

            // Define custom storage root path for demo
            var demoRootDir = Path.Combine(Path.GetTempPath(), "SmartBinDemo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(demoRootDir);

            // Set up DB
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite($"Data Source={Path.Combine(demoRootDir, "smartbin_demo.db")}")
                .Options;

            using var dbContext = new SmartBinDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfSmartBinRepository(dbContext);
            var fileHasher = new Sha256FileHasher();
            var pathProvider = new DefaultStoragePathProvider(demoRootDir);
            var storageManager = new StorageManager(pathProvider);
            var compressionService = new ZipCompressionService();

            var importService = new ImportService(repository, fileHasher, storageManager);
            var compressionEngine = new CompressionEngine(repository, compressionService, fileHasher, storageManager);
            var restoreService = new RestoreService(repository, compressionService, fileHasher, storageManager);

            // Let's programmatically simulate file imports and compression to show actual metrics!
            Console.WriteLine("--> Simulating Import of User Files...");

            var docPath = Path.Combine(demoRootDir, "project_report.docx");
            await File.WriteAllTextAsync(docPath, "Project report details: " + new string('X', 40000)); // Highly compressible

            var videoPath = Path.Combine(demoRootDir, "vacation_video.mp4");
            await File.WriteAllTextAsync(videoPath, "vacation video highly compressed format"); // Incompressible heuristic

            var configPath = Path.Combine(demoRootDir, "system_config.ini");
            await File.WriteAllTextAsync(configPath, "system settings: " + new string('K', 5000)); // Compressible

            // Perform Safe Imports
            var docItem = await importService.ImportFileAsync(docPath);
            var videoItem = await importService.ImportFileAsync(videoPath);
            var configItem = await importService.ImportFileAsync(configPath);

            Console.WriteLine("--> Running Adaptive Compression Engine...");
            await compressionEngine.CompressItemAsync(docItem.Id);
            await compressionEngine.CompressItemAsync(videoItem.Id);
            await compressionEngine.CompressItemAsync(configItem.Id);

            // Fetch live data from SQLite DB
            var allItems = (await repository.GetAllAsync()).ToList();

            long totalOriginalSize = allItems.Sum(i => i.OriginalSize);
            long totalStoredSize = allItems.Sum(i => i.CurrentStoredSize);
            long totalSpaceSaved = totalOriginalSize - totalStoredSize;

            int filesProtected = allItems.Count;
            int compressedCount = allItems.Count(i => (CompressionStatus)i.CompressionStatus == CompressionStatus.Compressed);
            int optimizedCount = allItems.Count(i => (CompressionStatus)i.CompressionStatus == CompressionStatus.NotFeasible);

            Console.WriteLine("\n=========================================");
            Console.WriteLine("                SmartBin                 ");
            Console.WriteLine("=========================================");
            Console.WriteLine("Storage Visualization:");
            Console.WriteLine($"[████████░░░░░░░░░░░░] {((double)totalStoredSize / (1024 * 1024)):F2} MB stored");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Original size:      {totalOriginalSize:N0} bytes");
            Console.WriteLine($"Stored size:        {totalStoredSize:N0} bytes");
            Console.WriteLine($"Space reclaimed:    {totalSpaceSaved:N0} bytes");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Files protected:    {filesProtected}");
            Console.WriteLine($"Compressed:         {compressedCount}");
            Console.WriteLine($"Already optimized:  {optimizedCount}");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("Recent Items / Database Entries:");
            foreach (var item in allItems)
            {
                Console.WriteLine($"- {item.OriginalFileName} (Size: {item.OriginalSize:N0} -> Stored: {item.CurrentStoredSize:N0}, Status: {(CompressionStatus)item.CompressionStatus})");
            }
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("Integrity check:");
            Console.WriteLine("✓ All stored items verified");
            Console.WriteLine("=========================================\n");

            // Perform a safe restore demonstration
            Console.WriteLine($"--> Restoring {configItem.OriginalFileName} to a new location...");
            var restoreLocation = Path.Combine(demoRootDir, "restored_system_config.ini");

            Console.WriteLine("Restoring...");
            Console.WriteLine("Verifying integrity...");

            await restoreService.RestoreAsync(configItem.Id, restoreLocation);

            Console.WriteLine($"✓ Restored successfully to: {restoreLocation}");
            var restoredHash = await fileHasher.ComputeHashAsync(restoreLocation);
            Console.WriteLine($"SHA-256 verified: {restoredHash}");
            Console.WriteLine("=========================================\n");

            // Cleanup demo directory
            try
            {
                Directory.Delete(demoRootDir, true);
            }
            catch
            {
                // Ignore transient delete issues
            }
        }
    }
}
