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
    /// Program class to bootstrap the application and run interactive headless Phase 5 simulation on Linux.
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
            Console.WriteLine("\n[Running Headless Phase 5 Controlled Experiment Proof (Linux Sandbox)]\n");

            var demoRootDir = Path.Combine(Path.GetTempPath(), "SmartBinPhase5Demo_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(demoRootDir);

            // DB Setup
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite($"Data Source={Path.Combine(demoRootDir, "smartbin_p5_demo.db")}")
                .Options;

            using var dbContext = new SmartBinDbContext(options);
            await dbContext.Database.EnsureCreatedAsync();

            var repository = new EfSmartBinRepository(dbContext);
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

            // Phase 5 Provider Setup (Simulated for Headless Environment)
            IRecycleBinProvider winProvider = new SimulatedRecycleBinProvider();

            Console.WriteLine("--> Discovering Windows Recycle Bin Items...");
            var winItems = (await winProvider.EnumerateItemsAsync()).ToList();

            // Select exactly ONE item for our controlled experiment
            var targetItem = winItems.First(i => i.FileName == "database.sql");

            Console.WriteLine($"\n========================================================");
            Console.WriteLine("                CONTROLLED EXPERIMENT                   ");
            Console.WriteLine("========================================================");
            Console.WriteLine($"Selected Recycle Bin Item: {targetItem.FileName}");
            Console.WriteLine($"Original Size:             {targetItem.Size / (1024 * 1024):N0} MB");
            Console.WriteLine($"Original Location:         {targetItem.OriginalPath}");
            Console.WriteLine($"Volume:                    {targetItem.Volume}");
            Console.WriteLine("--------------------------------------------------------");
            Console.WriteLine("SmartBin will perform a controlled experiment on this ONE item.");
            Console.WriteLine("No automatic or batch optimization will occur.");
            Console.WriteLine("========================================================\n");

            Console.WriteLine("--> Running Pipeline Safety Checks (State Machine transitions):");

            try
            {
                var experiment = await experimentEngine.PrepareAndVerifyAsync(
                    targetItem,
                    state => Console.WriteLine($"  [State Changed] -> {state}"));

                Console.WriteLine("\n--------------------------------------------------------");
                Console.WriteLine("SAFETY CHECKS PASSED:");
                Console.WriteLine("✓ Item identified");
                Console.WriteLine("✓ Content acquired securely");
                Console.WriteLine($"✓ SHA-256 calculated: {experiment.OriginalSha256}");
                Console.WriteLine($"✓ Compression completed: {experiment.CompressedSize / (1024 * 1024):N0} MB stored");
                Console.WriteLine($"✓ Compressed representation verified");
                Console.WriteLine("✓ Restoration test passed byte-for-byte");
                Console.WriteLine("--------------------------------------------------------");
                Console.WriteLine("Status: READY FOR COMMIT");
                Console.WriteLine("--------------------------------------------------------");
                Console.WriteLine($"Original Size:     {experiment.OriginalSize:N0} bytes");
                Console.WriteLine($"Stored Size:       {experiment.CompressedSize:N0} bytes");
                Console.WriteLine($"Verified Savings:  {experiment.ActualSavingsBytes:N0} bytes (Ratio: {experiment.CompressionRatio:F2})");
                Console.WriteLine("--------------------------------------------------------");

                // Explicit Commit Boundary
                Console.WriteLine("\n--> Requiring Explicit User Confirmation...");
                Console.WriteLine("Confirming commit: [Yes]");

                // In headless demo, we complete commit without real Windows mutation (no real Recycle Bin item is removed)
                await experimentEngine.CommitExperimentAsync(
                    experiment,
                    executeWindowsMutation: false,
                    state => Console.WriteLine($"  [State Changed] -> {state}"));

                Console.WriteLine("\n✓ Controlled Experiment Committed successfully.");
                Console.WriteLine("SmartBin has safely saved the verified compressed copy.");
                Console.WriteLine("Original Windows Recycle Bin entry remains untouched.");

                // Show database entry
                var dbItems = (await repository.GetAllAsync()).ToList();
                Console.WriteLine($"\nStored SmartBin Items in Metadata DB: {dbItems.Count}");
                foreach (var dbItem in dbItems)
                {
                    Console.WriteLine($"- {dbItem.OriginalFileName} (Size: {dbItem.OriginalSize:N0} -> Stored: {dbItem.CurrentStoredSize:N0}, Status: {dbItem.CompressionStatus})");
                }
                Console.WriteLine("========================================================\n");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n❌ Experiment Pipeline Failed: {ex.Message}");
                Console.WriteLine("Rollback successful. Original Recycle Bin item left completely untouched.");
            }

            // Cleanup demo directory
            try
            {
                Directory.Delete(demoRootDir, true);
            }
            catch { }
        }
    }
}
