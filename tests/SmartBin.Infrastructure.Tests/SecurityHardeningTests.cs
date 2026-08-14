using System;
using System.IO;
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
    public class SecurityHardeningTests : IDisposable
    {
        private readonly string _testRootDir;
        private readonly SmartBinDbContext _dbContext;
        private readonly EfSmartBinRepository _repository;
        private readonly Sha256FileHasher _fileHasher;
        private readonly DefaultStoragePathProvider _pathProvider;
        private readonly StorageManager _storageManager;
        private readonly ZipCompressionService _compressionService;
        private readonly WindowsRecycleBinMutationService _mutationService;
        private readonly ControlledExperimentEngine _experimentEngine;
        private readonly RestoreService _restoreService;

        public SecurityHardeningTests()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), "SmartBinSecurityTests_" + Guid.NewGuid().ToString("N"));
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
            _mutationService = new WindowsRecycleBinMutationService(_pathProvider);

            _experimentEngine = new ControlledExperimentEngine(
                _repository,
                _mutationService,
                _compressionService,
                _fileHasher,
                _storageManager);

            _restoreService = new RestoreService(
                _repository,
                _compressionService,
                _fileHasher,
                _storageManager);
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
        public void Settings_MalformedOrOutOfBounds_ForcesDefaultModeOff()
        {
            // Arrange malformed out-of-bounds configuration values
            var settings = new SmartBinSettings
            {
                Mode = AutoOptimizationMode.Automatic,
                LowPressureThresholdPercentage = -10.0, // Malformed (must be >0)
                CriticalPressureThresholdPercentage = 110.0, // Malformed (must be <100)
                TargetFreeSpacePercentage = 5.0, // Incompatible (must be > Low)
                MinimumSafetyMarginBytes = -1, // Negative floor
                MaxItemsPerSession = 5000 // Out of limits (max 1000)
            };

            // Act
            settings.ValidateAndNormalize();

            // Assert
            Assert.Equal(AutoOptimizationMode.Off, settings.Mode); // Safely forced OFF!
            Assert.Equal(15.0, settings.LowPressureThresholdPercentage);
            Assert.Equal(5.0, settings.CriticalPressureThresholdPercentage);
            Assert.Equal(20.0, settings.TargetFreeSpacePercentage);
            Assert.Equal(5L * 1024 * 1024 * 1024, settings.MinimumSafetyMarginBytes); // Safe margin reset!
            Assert.Equal(5, settings.MaxItemsPerSession);
        }

        [Fact]
        public void Settings_SafeConfiguration_RetainsValues()
        {
            // Arrange a valid, safe configuration
            var settings = new SmartBinSettings
            {
                Mode = AutoOptimizationMode.Automatic,
                LowPressureThresholdPercentage = 25.0,
                CriticalPressureThresholdPercentage = 10.0,
                TargetFreeSpacePercentage = 30.0,
                MinimumSafetyMarginBytes = 1024 * 1024,
                MaxItemsPerSession = 50
            };

            // Act
            settings.ValidateAndNormalize();

            // Assert
            Assert.Equal(AutoOptimizationMode.Automatic, settings.Mode); // Remains Automatic
            Assert.Equal(25.0, settings.LowPressureThresholdPercentage);
            Assert.Equal(10.0, settings.CriticalPressureThresholdPercentage);
            Assert.Equal(30.0, settings.TargetFreeSpacePercentage);
            Assert.Equal(1024 * 1024, settings.MinimumSafetyMarginBytes);
            Assert.Equal(50, settings.MaxItemsPerSession);
        }

        [Fact]
        public void Production_FailureInjector_IsNoOp()
        {
            // Arrange
            var injector = new NoOpFailureInjector();

            // Act & Assert
            // Checking any named checkpoint should never throw in production
            injector.Check("BeforeCommit");
            injector.Check("DuringCompression");
            injector.Check("AfterHashing");
        }

        [Fact]
        public async Task PathTraversal_StorageRootEscape_Blocked()
        {
            // Act & Assert
            // Try to secure/move a file outside the approved storage base directory using path traversal
            var outsidePath = Path.Combine(_testRootDir, "..", "escaped_file.txt");

            Assert.Throws<UnauthorizedAccessException>(() => _storageManager.EnsurePathIsSecure(outsidePath));
        }

        [Fact]
        public async Task PathTraversal_RestoringToSystemFolder_Blocked()
        {
            // Arrange: create a fake SmartBinItem
            var finalObjectsPath = Path.Combine(_testRootDir, "objects", Guid.NewGuid().ToString("N") + ".z");
            Directory.CreateDirectory(Path.GetDirectoryName(finalObjectsPath)!);
            File.WriteAllText(finalObjectsPath, "dummy");

            var dbItem = new SmartBinItem
            {
                Id = Guid.NewGuid(),
                OriginalPath = Path.Combine(_testRootDir, "original.txt"),
                OriginalFileName = "original.txt",
                OriginalSize = 100,
                CurrentStoredSize = 20,
                Sha256Hash = "somehash",
                CurrentStoragePath = finalObjectsPath,
                CompressionStatus = CompressionStatus.Compressed,
                RestorationStatus = RestorationStatus.Pending
            };
            await _repository.AddAsync(dbItem);

            // Attempt to restore file into a system-sensitive directory (directory traversal attack vector)
            var systemDirectory = System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "smartbin_malicious_restore.txt")
                : "/etc/smartbin_malicious_restore.txt";

            // Act & Assert
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => _restoreService.RestoreAsync(dbItem.Id, systemDirectory));
        }

        [Fact]
        public void WindowsPowerState_GracefulFallback_OnNonWindows()
        {
            // Arrange
            var provider = new WindowsPowerStateProvider();

            // Act
            bool isOnBattery = provider.IsOnBatteryPower();

            // Assert
            // On Linux/macOS headless/CI environment, must fallback gracefully and return false without crashing
            if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                Assert.False(isOnBattery);
            }
        }
    }
}
