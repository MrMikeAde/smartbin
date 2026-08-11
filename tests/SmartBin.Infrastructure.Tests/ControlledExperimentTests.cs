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
    public class ControlledExperimentTests : IDisposable
    {
        private readonly string _testRootDir;
        private readonly SmartBinDbContext _dbContext;
        private readonly EfSmartBinRepository _repository;
        private readonly Sha256FileHasher _fileHasher;
        private readonly DefaultStoragePathProvider _pathProvider;
        private readonly StorageManager _storageManager;
        private readonly ZipCompressionService _compressionService;
        private readonly WindowsRecycleBinMutationService _mutationService;
        private readonly ControlledExperimentEngine _engine;

        public ControlledExperimentTests()
        {
            _testRootDir = Path.Combine(Path.GetTempPath(), "SmartBinExpTests_" + Guid.NewGuid().ToString("N"));
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

            _engine = new ControlledExperimentEngine(
                _repository,
                _mutationService,
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
        public async Task PrepareAndVerify_WithCompressibleItem_Succeeds()
        {
            // Arrange
            // We use a mock/simulated Recycle Bin item
            var mockItem = new WindowsRecycleBinItem
            {
                Id = "win_0",
                FileName = "test_doc.txt",
                OriginalPath = Path.Combine(_testRootDir, "original_path.txt"),
                Size = 50036,
                Volume = "C:",
                IsSimulated = true
            };

            // In non-Windows execution/mock, ExtractItemContentAsync writes 50,000 highly compressible 'A's
            // Act
            var experiment = await _engine.PrepareAndVerifyAsync(mockItem);

            // Assert
            Assert.NotNull(experiment);
            Assert.Equal(ExperimentState.ReadyForCommit, experiment.State);
            Assert.Equal(50036, experiment.OriginalSize);
            Assert.True(experiment.CompressedSize < experiment.OriginalSize);
            Assert.True(experiment.ActualSavingsBytes > 0);
            Assert.Equal(experiment.OriginalSha256, experiment.FinalVerificationHash);

            // Assert original item is untouched (and no entries yet in database)
            var items = await _repository.GetAllAsync();
            Assert.Empty(items);
        }

        [Fact]
        public async Task PrepareAndVerify_WithIncompressibleItem_ThrowsInvalidOperationException()
        {
            // Arrange
            // A tiny item that will not yield compression savings
            var mockItem = new WindowsRecycleBinItem
            {
                Id = "win_0",
                FileName = "test_doc.txt",
                OriginalPath = Path.Combine(_testRootDir, "original_path.txt"),
                Size = 10, // Only 10 bytes size
                Volume = "C:",
                IsSimulated = true
            };

            // Act & Assert
            // It will fail compression savings check and throw
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.PrepareAndVerifyAsync(mockItem));

            // Verify temp directory contains no leaked files
            var tempFiles = Directory.GetFiles(Path.Combine(_testRootDir, "temp"));
            Assert.Empty(tempFiles);
        }

        [Fact]
        public async Task CommitExperiment_VerifyTransitionAndPersistence()
        {
            // Arrange
            var mockItem = new WindowsRecycleBinItem
            {
                Id = "win_0",
                FileName = "test_doc.txt",
                OriginalPath = Path.Combine(_testRootDir, "original_path.txt"),
                Size = 50036,
                Volume = "C:",
                IsSimulated = true
            };

            var experiment = await _engine.PrepareAndVerifyAsync(mockItem);

            // Act
            await _engine.CommitExperimentAsync(experiment, executeWindowsMutation: false);

            // Assert
            Assert.Equal(ExperimentState.Committed, experiment.State);

            // Verify persisted as a regular recoverable SmartBin item in SQLite
            var dbItems = (await _repository.GetAllAsync()).ToList();
            Assert.Single(dbItems);

            var savedItem = dbItems[0];
            Assert.Equal(mockItem.OriginalPath, savedItem.OriginalPath);
            Assert.Equal(CompressionStatus.Compressed, savedItem.CompressionStatus);
            Assert.Equal(experiment.OriginalSha256, savedItem.Sha256Hash);
        }

        [Fact]
        public async Task CommitExperiment_NotReadyForCommit_ThrowsInvalidOperationException()
        {
            // Arrange
            var experiment = new ControlledExperimentItem
            {
                State = ExperimentState.Acquired // Not ReadyForCommit
            };

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.CommitExperimentAsync(experiment, false));
        }
    }
}
