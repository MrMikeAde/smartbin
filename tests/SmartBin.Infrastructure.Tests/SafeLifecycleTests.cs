using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
    public class SafeLifecycleTests : IDisposable
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
        private readonly RestoreService _restoreService;

        public SafeLifecycleTests()
        {
            // Set up an isolated temp folder for this test run
            _testRootDir = Path.Combine(Path.GetTempPath(), "SmartBinTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testRootDir);

            // In-memory SQLite Database context
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
            _restoreService = new RestoreService(_repository, _compressionService, _fileHasher, _storageManager);
        }

        public void Dispose()
        {
            _dbContext.Database.CloseConnection();
            _dbContext.Dispose();

            if (Directory.Exists(_testRootDir))
            {
                try
                {
                    Directory.Delete(_testRootDir, true);
                }
                catch
                {
                    // Ignore transient cleanup issues
                }
            }
        }

        // Helper to write deterministic data
        private async Task CreateDeterministicFileAsync(string path, string content)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
            await File.WriteAllTextAsync(path, content, new UTF8Encoding(false));
        }

        private async Task CreateIncompressibleFileAsync(string path, int size)
        {
            var folder = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);

            var bytes = new byte[size];
            Random.Shared.NextBytes(bytes);
            await File.WriteAllBytesAsync(path, bytes);
        }

        #region Import Tests

        [Fact]
        public async Task Import_ValidFile_Succeeds()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "source.txt");
            await CreateDeterministicFileAsync(sourcePath, "Highly repetitive string content which is compressible. " + new string('a', 2000));

            // Act
            var item = await _importService.ImportFileAsync(sourcePath);

            // Assert
            Assert.NotNull(item);
            Assert.True(item.Id != Guid.Empty);
            Assert.Equal("source.txt", item.OriginalFileName);
            Assert.Equal(CompressionStatus.Uncompressed, (CompressionStatus)item.CompressionStatus);
            Assert.True(File.Exists(item.CurrentStoragePath));
            Assert.True(File.Exists(sourcePath)); // Import should NOT delete original

            // Check metadata in DB
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(item.Sha256Hash, dbItem.Sha256Hash);
        }

        [Fact]
        public async Task Import_MissingFile_ThrowsFileNotFoundException()
        {
            // Arrange
            var missingPath = Path.Combine(_testRootDir, "missing.txt");

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => _importService.ImportFileAsync(missingPath));
        }

        [Fact]
        public async Task Import_EmptyFile_Succeeds()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "empty.txt");
            await CreateDeterministicFileAsync(sourcePath, string.Empty);

            // Act
            var item = await _importService.ImportFileAsync(sourcePath);

            // Assert
            Assert.NotNull(item);
            Assert.Equal(0, item.OriginalSize);
            Assert.Equal("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855", item.Sha256Hash);
        }

        [Fact]
        public async Task Import_Cancelled_AbortsSafely()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "source.txt");
            await CreateDeterministicFileAsync(sourcePath, "Some text");
            using var cts = new CancellationTokenSource();
            await cts.CancelAsync();

            // Act & Assert
            await Assert.ThrowsAsync<TaskCanceledException>(() => _importService.ImportFileAsync(sourcePath, cts.Token));
        }

        #endregion

        #region Hashing Tests

        [Fact]
        public async Task Hash_DifferentContentProducesDifferentHashes()
        {
            // Arrange
            var path1 = Path.Combine(_testRootDir, "file1.txt");
            var path2 = Path.Combine(_testRootDir, "file2.txt");
            await CreateDeterministicFileAsync(path1, "Content A");
            await CreateDeterministicFileAsync(path2, "Content B");

            // Act
            var hash1 = await _fileHasher.ComputeHashAsync(path1);
            var hash2 = await _fileHasher.ComputeHashAsync(path2);

            // Assert
            Assert.NotEqual(hash1, hash2);
        }

        #endregion

        #region Compression Tests

        [Fact]
        public async Task Compression_CompressibleFile_SavesSpace()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "compressible.txt");
            // Highly redundant data to guarantee massive space savings
            await CreateDeterministicFileAsync(sourcePath, new string('X', 50000));

            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);
            var uncompressedStoragePath = item.CurrentStoragePath;

            // Act
            await _compressionEngine.CompressItemAsync(item.Id);

            // Assert
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(CompressionStatus.Compressed, dbItem.CompressionStatus);
            Assert.Equal(CompressionAlgorithm.Zip, dbItem.CompressionAlgorithm);
            Assert.True(dbItem.CurrentStoredSize < dbItem.OriginalSize);
            Assert.True(File.Exists(dbItem.CurrentStoragePath));
            Assert.False(File.Exists(uncompressedStoragePath)); // Original is cleaned up
        }

        [Fact]
        public async Task Compression_IncompressibleFile_StatusNotFeasible()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "incompressible.bin");
            await CreateIncompressibleFileAsync(sourcePath, 5000); // 5KB of completely random bytes

            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);
            var uncompressedStoragePath = item.CurrentStoragePath;

            // Act
            await _compressionEngine.CompressItemAsync(item.Id);

            // Assert
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(CompressionStatus.NotFeasible, dbItem.CompressionStatus);
            Assert.Equal(CompressionAlgorithm.None, dbItem.CompressionAlgorithm);
            Assert.Equal(dbItem.OriginalSize, dbItem.CurrentStoredSize);
            Assert.True(File.Exists(uncompressedStoragePath)); // Uncompressed kept
        }

        [Fact]
        public async Task Compression_AlreadyCompressedExtension_SkippedByHeuristics()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "movie.mp4");
            await CreateDeterministicFileAsync(sourcePath, "mock video bytes");

            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);

            // Act
            await _compressionEngine.CompressItemAsync(item.Id);

            // Assert
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(CompressionStatus.NotFeasible, dbItem.CompressionStatus);
            Assert.Equal(CompressionAlgorithm.None, dbItem.CompressionAlgorithm);
        }

        [Fact]
        public async Task Compression_FailureOrCorruptedTemp_RollbackKeepsOriginal()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "compressible.txt");
            await CreateDeterministicFileAsync(sourcePath, new string('A', 10000));
            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);
            var uncompressedStoragePath = item.CurrentStoragePath;

            // Delete the stored file from disk to force a FileNotFoundException during compression
            File.Delete(uncompressedStoragePath);

            // Act & Assert
            await Assert.ThrowsAsync<FileNotFoundException>(() => _compressionEngine.CompressItemAsync(item.Id));

            // Verify metadata state in DB is still uncompressed and has not falsely claimed success
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(CompressionStatus.Uncompressed, dbItem.CompressionStatus);
        }

        #endregion

        #region Restore Tests

        [Fact]
        public async Task Restore_SuccessfulDecompressAndVerify_Succeeds()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "original.txt");
            var originalContent = "Highly compressible repeated content! " + new string('Y', 10000);
            await CreateDeterministicFileAsync(sourcePath, originalContent);

            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);
            await _compressionEngine.CompressItemAsync(item.Id);

            // Delete original file to simulate restoring it back to that location
            File.Delete(sourcePath);

            // Act
            await _restoreService.RestoreAsync(item.Id);

            // Assert
            Assert.True(File.Exists(sourcePath));
            var restoredContent = await File.ReadAllTextAsync(sourcePath);
            Assert.Equal(originalContent, restoredContent);

            // Verify database state
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(RestorationStatus.Restored, dbItem.RestorationStatus);
        }

        [Fact]
        public async Task Restore_DestinationConflict_ThrowsSmartBinConflictException()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "original.txt");
            await CreateDeterministicFileAsync(sourcePath, "Original Content");

            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);

            // Destination already exists because we did not delete 'sourcePath' (Import ≠ Delete)
            // Act & Assert
            await Assert.ThrowsAsync<SmartBinConflictException>(() => _restoreService.RestoreAsync(item.Id));
        }

        [Fact]
        public async Task Restore_CorruptedStoredFile_ThrowsAndCleansUpTemp()
        {
            // Arrange
            var sourcePath = Path.Combine(_testRootDir, "compressible.txt");
            await CreateDeterministicFileAsync(sourcePath, new string('Z', 20000));
            var item = (SmartBinItem)await _importService.ImportFileAsync(sourcePath);
            await _compressionEngine.CompressItemAsync(item.Id);

            // Corrupt the stored file by overwriting it with garbage bytes
            await File.WriteAllTextAsync(item.CurrentStoragePath, "garbage bytes corrupting the zip archive structure");

            File.Delete(sourcePath);

            // Act & Assert
            await Assert.ThrowsAnyAsync<Exception>(() => _restoreService.RestoreAsync(item.Id));

            // Verify original source is NOT corrupted or falsely created, and original stored file/metadata remains
            Assert.False(File.Exists(sourcePath));
            var dbItem = await _repository.GetByIdAsync(item.Id);
            Assert.NotNull(dbItem);
            Assert.Equal(RestorationStatus.Pending, dbItem.RestorationStatus);

            // Verify temp directory has no leaks
            var tempFiles = Directory.GetFiles(Path.Combine(_testRootDir, "temp"));
            Assert.Empty(tempFiles);
        }

        #endregion

        #region Phase 10 Validation & Heuristics Tests

        [Fact]
        public async Task Candidate_Revalidation_DisappearedItem_SafelyAborted()
        {
            // Arrange
            var mutationService = new WindowsRecycleBinMutationService(_pathProvider);
            var engine = new ControlledExperimentEngine(
                _repository,
                mutationService,
                _compressionService,
                _fileHasher,
                _storageManager);

            // A candidate pointing to a non-existent item ID
            var missingCandidate = new WindowsRecycleBinItem
            {
                Id = "win_nonexistent_9999",
                FileName = "disappeared.txt",
                OriginalPath = Path.Combine(_testRootDir, "disappeared.txt"),
                Size = 10000,
                Volume = "C:",
                IsSimulated = true
            };

            // Act & Assert
            // When candidate content cannot be extracted, engine safely aborts and throws
            await Assert.ThrowsAnyAsync<Exception>(() => engine.PrepareAndVerifyAsync(missingCandidate));
        }

        [Fact]
        public async Task Heuristics_SyntheticDataTypes_BehaveSensibly()
        {
            // Test compression ratios for synthetic representative data types
            var txtContent = new string('A', 50000);
            var jsonContent = "{\"data\": [" + string.Join(",", Enumerable.Repeat("{\"id\": 1, \"name\": \"sample\"}", 500)) + "]}";
            var csvContent = string.Join("\n", Enumerable.Repeat("1,John,Doe,john@example.com,555-0199,Developer", 1000));

            var txtPath = Path.Combine(_testRootDir, "data.txt");
            var jsonPath = Path.Combine(_testRootDir, "data.json");
            var csvPath = Path.Combine(_testRootDir, "data.csv");

            await CreateDeterministicFileAsync(txtPath, txtContent);
            await CreateDeterministicFileAsync(jsonPath, jsonContent);
            await CreateDeterministicFileAsync(csvPath, csvContent);

            var txtItem = (SmartBinItem)await _importService.ImportFileAsync(txtPath);
            var jsonItem = (SmartBinItem)await _importService.ImportFileAsync(jsonPath);
            var csvItem = (SmartBinItem)await _importService.ImportFileAsync(csvPath);

            await _compressionEngine.CompressItemAsync(txtItem.Id);
            await _compressionEngine.CompressItemAsync(jsonItem.Id);
            await _compressionEngine.CompressItemAsync(csvItem.Id);

            var txtDb = await _repository.GetByIdAsync(txtItem.Id);
            var jsonDb = await _repository.GetByIdAsync(jsonItem.Id);
            var csvDb = await _repository.GetByIdAsync(csvItem.Id);

            Assert.Equal(CompressionStatus.Compressed, txtDb!.CompressionStatus);
            Assert.Equal(CompressionStatus.Compressed, jsonDb!.CompressionStatus);
            Assert.Equal(CompressionStatus.Compressed, csvDb!.CompressionStatus);

            Assert.True(txtDb.CurrentStoredSize < txtDb.OriginalSize / 5); // >80% reduction
            Assert.True(jsonDb.CurrentStoredSize < jsonDb.OriginalSize / 3);
            Assert.True(csvDb.CurrentStoredSize < csvDb.OriginalSize / 3);
        }

        #endregion
    }
}
