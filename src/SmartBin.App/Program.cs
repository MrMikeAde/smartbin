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
    /// Program class to bootstrap the application and run interactive headless Phase 6 background protection simulation.
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
            Console.WriteLine("\n[Running Headless Phase 6 Automatic Storage Protection Demo (Linux Sandbox)]\n");

            var demoRootDir = Path.Combine(Path.GetTempPath(), "SmartBinPhase6Demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(demoRootDir);

            // DB Setup
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite($"Data Source={Path.Combine(demoRootDir, "smartbin_p6_demo.db")}")
                .Options;

            using var dbContext = new SmartBinDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfSmartBinRepository(dbContext);
            var activityRepository = new ActivityRepository(dbContext);
            var fileHasher = new Sha256FileHasher();
            var pathProvider = new DefaultStoragePathProvider(demoRootDir);
            var storageManager = new StorageManager(pathProvider);
            var compressionService = new ZipCompressionService();
            var mutationService = new WindowsRecycleBinMutationService(pathProvider);

            var experimentEngine = new ControlledExperimentEngine(
                repository,
                mutationService,
                compressionService,
                fileHasher,
                storageManager);

            var pressureMonitor = new StoragePressureMonitor(pathProvider);
            var pressureSimulator = new StoragePressureSimulator(pressureMonitor);
            var notificationService = new NotificationService();
            var candidateAnalyzer = new CandidateAnalyzer(repository);
            var planner = new OptimizationPlanner();

            var fakePowerState = new FakePowerStateProvider(isOnBattery: false);

            var autoEngine = new AutomaticProtectionEngine(
                repository,
                activityRepository,
                pressureMonitor,
                fakePowerState,
                new SimulatedRecycleBinProvider(), // Deterministic fake Windows items
                candidateAnalyzer,
                planner,
                experimentEngine,
                notificationService);

            // Connect user notifications to the terminal logger
            notificationService.NotificationRaised += (msg, type) =>
            {
                Console.WriteLine($"\n[NOTIFICATION - {type.ToUpperInvariant()}] {msg}");
            };

            // 1. Startup Crash Recovery & Cleanup scan
            Console.WriteLine("--> Running Startup Crash Recovery...");
            var recoveryService = new CrashRecoveryService(storageManager);

            // Create some fake intermediate garbage files in temp/ directory to simulate cleanup
            var tempDir = Path.Combine(demoRootDir, "temp");
            Directory.CreateDirectory(tempDir);
            await File.WriteAllTextAsync(Path.Combine(tempDir, "temp_garbage.zip"), "unfinished zip data");
            await File.WriteAllTextAsync(Path.Combine(tempDir, "temp_garbage.acq"), "unfinished acq data");

            int cleanedCount = recoveryService.PerformStartupRecoveryAndCleanup();
            Console.WriteLine($"✓ Cleanup complete. Successfully swept {cleanedCount} intermediate residual files.");

            // 2. Configure user policy settings
            Console.WriteLine("\n--> Configuring User Policy Settings (Disabled by default)...");
            Console.WriteLine($"Policy State: Mode = {autoEngine.Settings.Mode}, Safety Floor = {autoEngine.Settings.MinimumSafetyMarginBytes / (1024 * 1024 * 1024):N0} GB, Pause on Battery = {autoEngine.Settings.PauseOnBattery}");

            // Trigger under disabled state to verify nothing happens
            Console.WriteLine("\n--> Scanning drive under OFF policy...");
            await autoEngine.RunAutomaticProtectionAsync();

            // 3. Turn on Automatic Protection policy
            Console.WriteLine("\n[AUTOMATIC PROTECTION ACTIVATED]");
            autoEngine.Settings.Mode = AutoOptimizationMode.Automatic;
            autoEngine.Settings.MinimumSafetyMarginBytes = 1L * 1024 * 1024 * 1024; // 1 GB safety floor for demo

            // 4. Simulate Critical Storage pressure
            Console.WriteLine("\n--> Simulating CRITICAL storage pressure...");
            pressureSimulator.EnableSimulation(StoragePressureState.Critical);

            var simulatedMetrics = await pressureMonitor.GetStorageMetricsAsync();
            var recommendation = StoragePressurePolicy.Evaluate(simulatedMetrics, autoEngine.Settings.TargetFreeSpacePercentage);

            // Display Current Dashboard View
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine("SMARTBIN");
            Console.WriteLine("-----------------------------------------");
            Console.WriteLine($"Free space:         {simulatedMetrics.AvailableFreeSpace / (1024 * 1024):N0} MB");
            Console.WriteLine($"Status:             {simulatedMetrics.PressureState} (SIMULATION)");
            Console.WriteLine($"Safety Floor:       {autoEngine.Settings.MinimumSafetyMarginBytes / (1024 * 1024 * 1024):N0} GB");
            Console.WriteLine($"Required Recovery:  {recommendation.RequiredSpaceToReclaimBytes / (1024 * 1024):N0} MB");
            Console.WriteLine("-----------------------------------------");

            // 5. Trigger Background Protection Loop
            Console.WriteLine("\n--> Triggering Background Storage Protection loop...");
            await autoEngine.RunAutomaticProtectionAsync();

            // 6. Print out final Activity Log History from Metadata SQLite DB
            var activityLogs = await activityRepository.GetLogsAsync();
            Console.WriteLine("\n=========================================");
            Console.WriteLine("             ACTIVITY HISTORY            ");
            Console.WriteLine("=========================================");
            foreach (var log in activityLogs)
            {
                Console.WriteLine($"[{log.Timestamp:HH:mm:ss}] {log.OperationType} - Result: {log.ResultState}");
                if (!string.IsNullOrEmpty(log.ItemName)) Console.WriteLine($"  File: {log.ItemName}");
                if (log.ReclaimedBytes > 0) Console.WriteLine($"  Reclaimed: {log.ReclaimedBytes:N0} bytes");
                Console.WriteLine($"  Rationale: {log.Rationale}");
                if (!string.IsNullOrEmpty(log.FailureReason)) Console.WriteLine($"  Failure Reason: {log.FailureReason}");
                Console.WriteLine("-----------------------------------------");
            }
            Console.WriteLine("=========================================\n");

            // Cleanup demo folder
            try
            {
                Directory.Delete(demoRootDir, true);
            }
            catch { }
        }

        private class FakePowerStateProvider : IPowerStateProvider
        {
            private readonly bool _isOnBattery;
            public FakePowerStateProvider(bool isOnBattery) => _isOnBattery = isOnBattery;
            public bool IsOnBatteryPower() => _isOnBattery;
        }
    }
}
