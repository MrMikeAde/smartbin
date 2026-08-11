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
    /// Program class to bootstrap the application and run interactive headless Phase 3 simulation on Linux.
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
            Console.WriteLine("\n[Running Headless Phase 3 Adaptive Storage Intelligence Demo (Linux Sandbox)]\n");

            var demoRootDir = Path.Combine(Path.GetTempPath(), "SmartBinPhase3Demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(demoRootDir);

            // DB Setup
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite($"Data Source={Path.Combine(demoRootDir, "smartbin_p3_demo.db")}")
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

            // 1. Setup mock recoverable files of different types to showcase scoring and explanation
            Console.WriteLine("--> Populating User Files into SmartBin...");

            var textFile = Path.Combine(demoRootDir, "database_dump.sql");
            await File.WriteAllTextAsync(textFile, "DUMP DATA: " + new string('S', 500000)); // 500KB highly compressible sql dump

            var movieFile = Path.Combine(demoRootDir, "vacation_video.mkv");
            await File.WriteAllTextAsync(movieFile, "compressed movie format bytes heuristic skip"); // 42 bytes mkv (pre-compressed)

            var recentFile = Path.Combine(demoRootDir, "system_config.ini");
            await File.WriteAllTextAsync(recentFile, "CONFIG: " + new string('C', 5000)); // 5KB compressible ini, very recent

            // Safe Import (Import ≠ Delete)
            var textItem = await importService.ImportFileAsync(textFile);
            var movieItem = await importService.ImportFileAsync(movieFile);
            var recentItem = await importService.ImportFileAsync(recentFile);

            // Backdate textItem's deletion timestamp to make it old (e.g. 42 days old) to show priority age factor
            var textDbItem = await dbContext.SmartBinItems.FindAsync(textItem.Id);
            if (textDbItem != null)
            {
                textDbItem.DeletedTimestamp = DateTime.UtcNow.AddDays(-42);
                await dbContext.SaveChangesAsync();
            }

            // 2. Storage Pressure Evaluation
            Console.WriteLine("--> Evaluating Initial Storage Status...");
            var initialMetrics = await pressureMonitor.GetStorageMetricsAsync();
            Console.WriteLine($"Default Space: {initialMetrics.AvailableFreeSpace / (1024 * 1024):N0} MB available, Status: {initialMetrics.PressureState}");

            // 3. Enable Simulator: State = Critical
            Console.WriteLine("\n[SIMULATION MODE ENABLED: CRITICAL STORAGE PRESSURE]");
            simulator.EnableSimulation(StoragePressureState.Critical);

            var simulatedMetrics = await pressureMonitor.GetStorageMetricsAsync();
            var recommendation = StoragePressurePolicy.Evaluate(simulatedMetrics);

            // Display Storage Pressure Details
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("SMARTBIN");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Free space:         {simulatedMetrics.AvailableFreeSpace / (1024 * 1024):N0} MB");
            Console.WriteLine($"Status:             {simulatedMetrics.PressureState} (SIMULATION)");
            Console.WriteLine($"Recommendation:     {(recommendation.IsOptimizationRecommended ? "OPTIMIZE RECOMMENDED" : "NO WORK RECOMMENDED")}");
            Console.WriteLine($"Required Recovery:  {recommendation.RequiredSpaceToReclaimBytes:N0} bytes");
            Console.WriteLine($"Rationale:          {recommendation.Rationale}");
            Console.WriteLine("-----------------------------------------");

            // 4. Candidate Analysis & Scoring (Explainability)
            Console.WriteLine("\n--> Analyzing Candidates for Optimization...");
            var candidates = await candidateAnalyzer.AnalyzeCandidatesAsync();

            Console.WriteLine("\nCandidates & Explainability Scores:");
            foreach (var cand in candidates)
            {
                Console.WriteLine($"\nFile: {cand.OriginalFileName} (Size: {cand.OriginalSize:N0} bytes)");
                Console.WriteLine($"Score: {cand.PriorityScore:F1}");
                Console.WriteLine("Why?");
                Console.WriteLine(cand.PriorityExplaination);
            }

            // 5. Optimization Planner
            Console.WriteLine("\n--> Triggering Optimization Planner...");
            // Let's set a target to satisfy simulated pressure target (recommendation.TargetFreeSpaceBytes)
            var targetFreeSpace = simulatedMetrics.AvailableFreeSpace + recommendation.RequiredSpaceToReclaimBytes;
            var plan = planner.GeneratePlan(candidates, simulatedMetrics.AvailableFreeSpace, targetFreeSpace);

            Console.WriteLine($"Planner Generated. Candidates chosen to compress: {plan.ItemsToOptimize.Count}");
            foreach (var plannedItem in plan.ItemsToOptimize)
            {
                Console.WriteLine($"- {plannedItem.OriginalFileName} (Priority: {plannedItem.PriorityScore:F1}, Estimated Savings: {plannedItem.EstimatedSavingsBytes:N0} bytes)");
            }
            Console.WriteLine($"Expected Space reclaimed: {plan.ExpectedReclaimedBytes:N0} bytes");

            // 6. Optimization Executor
            Console.WriteLine("\n--> Executing Plan...");
            var execResult = await executor.ExecutePlanAsync(plan, targetFreeSpace);
            Console.WriteLine($"Result: {execResult.Message}");
            Console.WriteLine($"Actual space reclaimed: {execResult.ActualReclaimedBytes:N0} bytes");

            // 7. Update Dashboard with Live Values
            var updatedItems = (await repository.GetAllAsync()).ToList();
            long totalOriginal = updatedItems.Sum(i => i.OriginalSize);
            long totalStored = updatedItems.Sum(i => i.CurrentStoredSize);
            long actualSpaceReclaimed = totalOriginal - totalStored;

            // Potential additional recovery
            var reanalyzedCandidates = await candidateAnalyzer.AnalyzeCandidatesAsync();
            long potentialAdditional = (long)reanalyzedCandidates.Sum(c => c.IsEligibleForOptimization ? c.EstimatedSavingsBytes : 0);

            Console.WriteLine("\n=========================================");
            Console.WriteLine("            UPDATED SMARTBIN             ");
            Console.WriteLine("=========================================");
            Console.WriteLine($"Recoverable items:             {updatedItems.Count}");
            Console.WriteLine($"Original size:                 {totalOriginal:N0} bytes");
            Console.WriteLine($"Stored size:                   {totalStored:N0} bytes");
            Console.WriteLine($"Actual space reclaimed:        {actualSpaceReclaimed:N0} bytes");
            Console.WriteLine($"Potential additional recovery: {potentialAdditional:N0} bytes");
            Console.WriteLine("=========================================\n");

            // Clean up demo folder
            try
            {
                Directory.Delete(demoRootDir, true);
            }
            catch { }
        }
    }
}
