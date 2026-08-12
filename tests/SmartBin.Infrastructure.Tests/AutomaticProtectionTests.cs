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
    public class AutomaticProtectionTests : IDisposable
    {
        private readonly string _testRootDir;
        private readonly SmartBinDbContext _dbContext;
        private readonly EfSmartBinRepository _repository;
        private readonly ActivityRepository _activityRepository;
        private readonly Sha256FileHasher _fileHasher;
        private readonly DefaultStoragePathProvider _pathProvider;
        private readonly StorageManager _storageManager;
        private readonly ZipCompressionService _compressionService;
        private readonly WindowsRecycleBinMutationService _mutationService;
        private readonly ImportService _importService;
        private readonly CompressionEngine _compressionEngine;
        private readonly ControlledExperimentEngine _experimentEngine;
        private readonly StoragePressureMonitor _pressureMonitor;
        private readonly StoragePressureSimulator _pressureSimulator;
        private readonly NotificationService _notificationService;
        private readonly CandidateAnalyzer _candidateAnalyzer;
        private readonly OptimizationPlanner _planner;

        // Target system
        private readonly AutomaticProtectionEngine _autoEngine;

        public AutomaticProtectionTests()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), "SmartBinAutoTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRootDir);

            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;
            _dbContext = new SmartBinDbContext(options);
            _dbContext.Database.OpenConnection();
            _dbContext.Database.EnsureCreated();

            _repository = new EfSmartBinRepository(_dbContext);
            _activityRepository = new ActivityRepository(_dbContext);
            _fileHasher = new Sha256FileHasher();
            _pathProvider = new DefaultStoragePathProvider(_testRootDir);
            _storageManager = new StorageManager(_pathProvider);
            _compressionService = new ZipCompressionService();
            _mutationService = new WindowsRecycleBinMutationService(_pathProvider);

            _importService = new ImportService(_repository, _fileHasher, _storageManager);
            _compressionEngine = new CompressionEngine(_repository, _compressionService, _fileHasher, _storageManager);

            _experimentEngine = new ControlledExperimentEngine(
                _repository,
                _mutationService,
                _compressionService,
                _fileHasher,
                _storageManager);

            _pressureMonitor = new StoragePressureMonitor(_pathProvider);
            _pressureSimulator = new StoragePressureSimulator(_pressureMonitor);
            _notificationService = new NotificationService();
            _candidateAnalyzer = new CandidateAnalyzer(_repository);
            _planner = new OptimizationPlanner();

            // Setup the Automatic Protection Engine
            _autoEngine = new AutomaticProtectionEngine(
                _repository,
                _activityRepository,
                _pressureMonitor,
                new FakePowerStateProvider(isOnBattery: false), // Default AC
                new SimulatedRecycleBinProvider(), // Deterministic fake Windows items
                _candidateAnalyzer,
                _planner,
                _experimentEngine,
                _notificationService);
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

        [Fact]
        public async Task Run_WithModeOff_DoesNoOptimization()
        {
            // Arrange
            _autoEngine.Settings.Mode = AutoOptimizationMode.Off;
            _pressureSimulator.EnableSimulation(StoragePressureState.Critical);

            // Act
            await _autoEngine.RunAutomaticProtectionAsync();

            // Assert: No logs written, no items optimized
            var logs = await _activityRepository.GetLogsAsync();
            Assert.Empty(logs);

            var items = await _repository.GetAllAsync();
            Assert.Empty(items);
        }

        [Fact]
        public async Task Run_WithModeNotify_DoesNoOptimizationButAllowsScanning()
        {
            // Arrange
            _autoEngine.Settings.Mode = AutoOptimizationMode.NotifyMe;
            _pressureSimulator.EnableSimulation(StoragePressureState.Critical);

            // Act
            await _autoEngine.RunAutomaticProtectionAsync();

            // Assert: Scan notifications can be raised, but NO actual files optimized
            var items = await _repository.GetAllAsync();
            Assert.Empty(items);
        }

        [Fact]
        public async Task Run_WithModeAutomatic_ExecutesSequentialOneItemAtATime()
        {
            // Arrange
            _autoEngine.Settings.Mode = AutoOptimizationMode.Automatic;
            _autoEngine.Settings.MinimumSafetyMarginBytes = 1L * 1024 * 1024 * 1024; // 1 GB (simulate smaller safe floor)
            _pressureSimulator.EnableSimulation(StoragePressureState.Critical);

            // Act
            await _autoEngine.RunAutomaticProtectionAsync();

            // Assert: Highly compressible database.sql should be automatically optimized!
            var items = (await _repository.GetAllAsync()).ToList();
            Assert.Single(items); // Exactly ONE item per session limit
            Assert.Equal(@"C:\Users\User\Documents\database.sql", items[0].OriginalPath);
            Assert.Equal(CompressionStatus.Compressed, items[0].CompressionStatus);

            // Assert logged in ActivityLog
            var logs = await _activityRepository.GetLogsAsync();
            Assert.Single(logs);
            Assert.Equal("Automatic Optimization", logs[0].OperationType);
            Assert.Equal("Verified", logs[0].ResultState);
        }

        [Fact]
        public async Task Run_OnBatteryPower_SkipsOptimization()
        {
            // Arrange
            var batteryEngine = new AutomaticProtectionEngine(
                _repository, _activityRepository, _pressureMonitor,
                new FakePowerStateProvider(isOnBattery: true), // Running on battery!
                new SimulatedRecycleBinProvider(), _candidateAnalyzer, _planner, _experimentEngine, _notificationService);

            batteryEngine.Settings.Mode = AutoOptimizationMode.Automatic;
            batteryEngine.Settings.PauseOnBattery = true;
            _pressureSimulator.EnableSimulation(StoragePressureState.Critical);

            // Act
            await batteryEngine.RunAutomaticProtectionAsync();

            // Assert: No items optimized
            var items = await _repository.GetAllAsync();
            Assert.Empty(items);
        }

        [Fact]
        public async Task Run_BelowSafetyFloor_AbortsImmediately()
        {
            // Arrange
            _autoEngine.Settings.Mode = AutoOptimizationMode.Automatic;
            _autoEngine.Settings.MinimumSafetyMarginBytes = 100L * 1024 * 1024 * 1024; // Needs 100GB safety floor

            _pressureSimulator.EnableSimulation(StoragePressureState.Critical, totalCapacity: 50L * 1024 * 1024 * 1024); // Cap is 50GB, free is 4% (2GB)

            // Act
            await _autoEngine.RunAutomaticProtectionAsync();

            // Assert: Aborted via safety check
            var items = await _repository.GetAllAsync();
            Assert.Empty(items);

            var logs = await _activityRepository.GetLogsAsync();
            Assert.Single(logs);
            Assert.Equal("Safety check", logs[0].OperationType);
            Assert.Equal("Aborted", logs[0].ResultState);
        }

        private class FakePowerStateProvider : IPowerStateProvider
        {
            private readonly bool _isOnBattery;
            public FakePowerStateProvider(bool isOnBattery) => _isOnBattery = isOnBattery;
            public bool IsOnBatteryPower() => _isOnBattery;
        }
    }
}
