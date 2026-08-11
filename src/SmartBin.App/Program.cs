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
    /// Program class to bootstrap the application and run interactive headless Phase 4 simulation on Linux.
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
            Console.WriteLine("\n[Running Headless Phase 4 Windows Recycle Bin Integration Demo (Linux Sandbox)]\n");

            var demoRootDir = Path.Combine(Path.GetTempPath(), "SmartBinPhase4Demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(demoRootDir);

            // DB Setup
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite($"Data Source={Path.Combine(demoRootDir, "smartbin_p4_demo.db")}")
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
            var pressureMonitor = new StoragePressureMonitor(pathProvider);
            var simulator = new StoragePressureSimulator(pressureMonitor);

            var candidateAnalyzer = new CandidateAnalyzer(repository);
            var planner = new OptimizationPlanner();
            var executor = new OptimizationExecutor(repository, pressureMonitor, compressionEngine);

            // Phase 4 Provider Setup (Simulated for Headless Environment)
            IRecycleBinProvider winProvider = new SimulatedRecycleBinProvider();

            // 1. Populate some SmartBin Controlled Items
            Console.WriteLine("--> Populating SmartBin Storage Files...");
            var textFile = Path.Combine(demoRootDir, "database_dump.sql");
            await File.WriteAllTextAsync(textFile, "DUMP: " + new string('S', 100000)); // 100KB compressible sql
            var textItem = await importService.ImportFileAsync(textFile);
            await compressionEngine.CompressItemAsync(textItem.Id);

            // 2. Fetch Windows Recycle Bin statistics and items
            Console.WriteLine("\n--> Querying Windows Recycle Bin (READ-ONLY)...");
            var winStats = await winProvider.GetStatisticsAsync();
            var winItems = (await winProvider.EnumerateItemsAsync()).ToList();

            // Display Dashboard UI Mock representation with Windows Recycle Bin section
            var items = (await repository.GetAllAsync()).ToList();
            long totalOriginal = items.Sum(i => i.OriginalSize);
            long totalStored = items.Sum(i => i.CurrentStoredSize);
            long actualSpaceReclaimed = totalOriginal - totalStored;

            Console.WriteLine("\n=========================================");
            Console.WriteLine("                SMARTBIN                 ");
            Console.WriteLine("=========================================");
            Console.WriteLine("Storage:");
            Console.WriteLine("42.3 GB free");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("SmartBin Storage (Controlled):");
            Console.WriteLine($"Recoverable items:      {items.Count}");
            Console.WriteLine($"Original size:          {totalOriginal:N0} bytes");
            Console.WriteLine($"Stored size:            {totalStored:N0} bytes");
            Console.WriteLine($"Actual space reclaimed: {actualSpaceReclaimed:N0} bytes");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("WINDOWS RECYCLE BIN");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Status:                 CONNECTED");
            Console.WriteLine($"Integration type:       READ-ONLY INTEGRATION");
            Console.WriteLine($"Total Items:            {winStats.TotalItems}");
            Console.WriteLine($"Aggregate Size:         {winStats.TotalSize / (1024 * 1024):N0} MB");
            Console.WriteLine("=========================================\n");

            // 3. Storage Intelligence Integration: Read-Only Analysis
            Console.WriteLine("--> Running Read-Only Storage Intelligence Analysis on Windows Recycle Bin Items...");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("Windows Recycle Bin Analysis (ANALYSIS ONLY)");
            Console.WriteLine("-----------------------------------------");

            var analyzedCandidates = winItems.Select(item => candidateAnalyzer.AnalyzeWindowsItem(item)).ToList();
            var highPriorityCount = analyzedCandidates.Count(c => c.PriorityScore >= 250);
            long totalPotentialReclaimable = (long)analyzedCandidates.Sum(c => c.EstimatedSavingsBytes);

            Console.WriteLine($"Potential candidates: {analyzedCandidates.Count}");
            Console.WriteLine($"Estimated reclaimable: {totalPotentialReclaimable / (1024 * 1024):N0} MB");
            Console.WriteLine("-----------------------------------------");

            // Output top 3 prioritized candidates for optimization planning simulation
            var topCandidates = analyzedCandidates.OrderByDescending(c => c.PriorityScore).Take(3).ToList();
            foreach (var cand in topCandidates)
            {
                var realItem = winItems.First(i => i.FileName == cand.OriginalFileName);
                Console.WriteLine($"\nFile: {cand.OriginalFileName}");
                Console.WriteLine($"Size: {realItem.Size / (1024 * 1024):N0} MB");
                Console.WriteLine($"Deleted: {realItem.DeletedTimestamp}");
                Console.WriteLine($"Volume: {realItem.Volume}");
                Console.WriteLine($"Priority: {(cand.PriorityScore >= 250 ? "HIGH" : "MEDIUM")} PRIORITY (Score: {cand.PriorityScore:F1})");
                Console.WriteLine("Why?");
                Console.WriteLine(cand.PriorityExplaination);
            }
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("NOTE: Modification, compression, and replacement of real Windows Recycle Bin items are intentionally not implemented.");
            Console.WriteLine("=========================================\n");

            // Cleanup demo directory
            try
            {
                Directory.Delete(demoRootDir, true);
            }
            catch { }
        }
    }
}
