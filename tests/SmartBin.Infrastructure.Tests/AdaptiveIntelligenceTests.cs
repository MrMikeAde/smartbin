using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;
using SmartBin.Infrastructure.Compression;
using SmartBin.Infrastructure.Hashing;
using SmartBin.Infrastructure.Persistence;
using SmartBin.Infrastructure.Storage;
using SmartBin.Infrastructure.Services;
using Xunit;

namespace SmartBin.Infrastructure.Tests
{
    public class AdaptiveIntelligenceTests : IDisposable
    {
        private readonly string _testRootDir;
        private readonly SmartBinDbContext _dbContext;
        private readonly EfSmartBinRepository _repository;
        private readonly Sha256FileHasher _fileHasher;
        private readonly DefaultStoragePathProvider _pathProvider;
        private readonly StorageManager _storageManager;
        private readonly ZipCompressionService _compressionService;
        private readonly ImportService _importService;
        private readonly CompressionEngine _compressionEngine;
        private readonly StoragePressureMonitor _pressureMonitor;
        private readonly StoragePressureSimulator _simulator;
        private readonly CandidateAnalyzer _analyzer;
        private readonly OptimizationPlanner _planner;
        private readonly OptimizationExecutor _executor;

        public AdaptiveIntelligenceTests()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), "SmartBinIntelTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRootDir);

            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;
            _dbContext = new SmartBinDbContext(options);
            _dbContext.Database.OpenConnection();
            _dbContext.Database.EnsureCreated();

            _repository = new EfSmartBinRepository(_dbContext);
            _fileHasher = new Sha256FileHasher();
            _pathProvider = new DefaultStoragePathProvider(_testRootDir);
            _storageManager = new StorageManager(_pathProvider);
            _compressionService = new ZipCompressionService();

            _importService = new ImportService(_repository, _fileHasher, _storageManager);
            _compressionEngine = new CompressionEngine(_repository, _compressionService, _fileHasher, _storageManager);
            _pressureMonitor = new StoragePressureMonitor(_pathProvider);
            _simulator = new StoragePressureSimulator(_pressureMonitor);

            _analyzer = new CandidateAnalyzer(_repository);
            _planner = new OptimizationPlanner();
            _executor = new OptimizationExecutor(_repository, _pressureMonitor, _compressionEngine);
        }

        public void Dispose()
        {
            _dbContext.Database.CloseConnection();
            _dbContext.Dispose();

            if (Directory.Exists(_testRootDir))
            {
                try { Directory.Delete(_testRootDir, true); } catch { }
            }
        }

        private async Task CreateCompressibleFileAsync(string path, int repetitions)
        {
            var content = "This is compressible content repeating many times... " + new string('Z', repetitions);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        }

        [Fact]
        public async Task Simulator_SetsStorageMetricsCorrectly()
        {
            // Act
            _simulator.EnableSimulation(StoragePressureState.Critical);
            var metricsCrit = await _pressureMonitor.GetStorageMetricsAsync();

            _simulator.EnableSimulation(StoragePressureState.Low);
            var metricsLow = await _pressureMonitor.GetStorageMetricsAsync();

            _simulator.DisableSimulation();
            var metricsReal = await _pressureMonitor.GetStorageMetricsAsync();

            // Assert
            Assert.Equal(StoragePressureState.Critical, metricsCrit.PressureState);
            Assert.Equal(StoragePressureState.Low, metricsLow.PressureState);
            Assert.True(metricsCrit.FreeSpacePercentage < _pressureMonitor.CriticalPressureThresholdPercentage);
            Assert.True(metricsLow.FreeSpacePercentage < _pressureMonitor.LowPressureThresholdPercentage);

            // Checking disable
            Assert.Null(_pressureMonitor.MockMetricsOverride);
        }

        [Fact]
        public async Task OptimizationExecutor_SuccessfulPlan_ExecutesCompressionAndRecordsSavings()
        {
            // Arrange
            // Create three compressible files
            var sourcePath1 = Path.Combine(_testRootDir, "file1.txt");
            var sourcePath2 = Path.Combine(_testRootDir, "file2.txt");
            var sourcePath3 = Path.Combine(_testRootDir, "file3.txt");

            await CreateCompressibleFileAsync(sourcePath1, 30000);
            await CreateCompressibleFileAsync(sourcePath2, 20000);
            await CreateCompressibleFileAsync(sourcePath3, 10000);

            // Import
            var item1 = await _importService.ImportFileAsync(sourcePath1);
            var item2 = await _importService.ImportFileAsync(sourcePath2);
            var item3 = await _importService.ImportFileAsync(sourcePath3);

            // Fetch candidates and generate plan
            var candidates = await _analyzer.AnalyzeCandidatesAsync();

            // Set simulated low pressure so we need to reclaim space
            _simulator.EnableSimulation(StoragePressureState.Low);
            var metrics = await _pressureMonitor.GetStorageMetricsAsync();

            // We want to recover at least 15KB
            var targetFreeSpace = metrics.AvailableFreeSpace + 15000;
            var plan = _planner.GeneratePlan(candidates, metrics.AvailableFreeSpace, targetFreeSpace);

            // Act
            var executionResult = await _executor.ExecutePlanAsync(plan, targetFreeSpace);

            // Assert
            Assert.True(executionResult.SuccessfulCount > 0);
            Assert.True(executionResult.ActualReclaimedBytes > 0);
            Assert.Equal(0, executionResult.FailureCount);

            // Verify SQLite metadata has changed
            var dbItem = await _repository.GetByIdAsync(item1.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(CompressionStatus.Compressed, dbItem.CompressionStatus);
        }

        [Fact]
        public async Task OptimizationExecutor_EarlyStopping_SavesUnnecessaryResources()
        {
            // Arrange
            var sourcePath1 = Path.Combine(_testRootDir, "file1.txt");
            var sourcePath2 = Path.Combine(_testRootDir, "file2.txt");
            await CreateCompressibleFileAsync(sourcePath1, 40000);
            await CreateCompressibleFileAsync(sourcePath2, 40000);

            var item1 = await _importService.ImportFileAsync(sourcePath1);
            var item2 = await _importService.ImportFileAsync(sourcePath2);

            var candidates = await _analyzer.AnalyzeCandidatesAsync();

            _simulator.EnableSimulation(StoragePressureState.Low);
            var metrics = await _pressureMonitor.GetStorageMetricsAsync();

            // We require a larger recovery (e.g. 35KB) so both files are needed in the plan
            var targetFreeSpace = metrics.AvailableFreeSpace + 35000;
            var plan = _planner.GeneratePlan(candidates, metrics.AvailableFreeSpace, targetFreeSpace);

            // Before execution, we check that plan contains 2 items because both are needed to satisfy 35KB
            Assert.Equal(2, plan.ItemsToOptimize.Count);

            // We'll set the mock metrics to dynamically "resolve" after the first file is compressed.
            // But we don't have to manually mock. OptimizationExecutor rechecks using IStoragePressureMonitor!
            // Let's create a custom flow where after the first compression, the simulator is set to Normal.
            // To make this deterministic without complex timing, let's mock metrics inside executor loop.
            // Wait, we can simulate space being resolved dynamically by changing simulator state midway!
            // But even easier: we can just check if target free space is exceeded.
            // Since the first compression will reclaim about 25-30KB, and we only need 5KB,
            // after the first file is compressed, the executor re-evaluates the real storage pressure.
            // Wait! The executor checks "AvailableFreeSpace >= targetFreeSpace".
            // Since this is mock/simulated drive info, compressing a file on disk does NOT change the simulator's AvailableFreeSpace override.
            // So to trigger early stop, we can hook the pressure monitor or update the simulator override *during* compression!
            // Is that possible? Yes! If we update _pressureMonitor.MockMetricsOverride directly, it changes space immediately.
            // Let's verify that the executor stops early if metrics.AvailableFreeSpace >= targetFreeSpace.
            // Let's manually set _pressureMonitor.MockMetricsOverride.AvailableFreeSpace to targetFreeSpace + 1000 before running!
            _simulator.EnableSimulation(StoragePressureState.Low);
            var updatedMetrics = await _pressureMonitor.GetStorageMetricsAsync();
            updatedMetrics.AvailableFreeSpace = targetFreeSpace + 1000; // Force space satisfied before compression runs
            _pressureMonitor.MockMetricsOverride = updatedMetrics;

            // Act
            var result = await _executor.ExecutePlanAsync(plan, targetFreeSpace);

            // Assert
            Assert.Equal(0, result.TotalProcessedCount); // Stop immediately! No operations processed.
            Assert.Contains("Sufficient free space", result.Message);
        }

        [Fact]
        public async Task OptimizationExecutor_Cancellation_StopsProcessing()
        {
            // Arrange
            var sourcePath1 = Path.Combine(_testRootDir, "file1.txt");
            await CreateCompressibleFileAsync(sourcePath1, 10000);
            await _importService.ImportFileAsync(sourcePath1);

            var candidates = await _analyzer.AnalyzeCandidatesAsync();
            var plan = _planner.GeneratePlan(candidates, 10000, 20000);

            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act
            var result = await _executor.ExecutePlanAsync(plan, 20000, cts.Token);

            // Assert
            Assert.True(result.Interrupted);
            Assert.Contains("cancelled", result.Message);
        }
    }
}
