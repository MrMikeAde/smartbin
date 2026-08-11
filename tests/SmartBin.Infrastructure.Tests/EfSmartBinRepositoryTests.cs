using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SmartBin.Core.Models;
using SmartBin.Infrastructure.Persistence;
using Xunit;

namespace SmartBin.Infrastructure.Tests
{
    public class EfSmartBinRepositoryTests
    {
        private SmartBinDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                .UseSqlite("DataSource=:memory:")
                .Options;

            var context = new SmartBinDbContext(options);
            context.Database.OpenConnection();
            context.Database.EnsureCreated();

            return context;
        }

        [Fact]
        public async Task AddAsync_AddsItemToDatabase()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var repository = new EfSmartBinRepository(context);

            var item = new SmartBinItem
            {
                Id = Guid.NewGuid(),
                OriginalPath = @"C:\Test\file.txt",
                OriginalFileName = "file.txt",
                OriginalSize = 2048,
                CurrentStoredSize = 2048,
                Sha256Hash = "abc"
            };

            // Act
            await repository.AddAsync(item);

            // Assert
            var savedItem = await context.SmartBinItems.FindAsync(item.Id);
            Assert.NotNull(savedItem);
            Assert.Equal(item.OriginalPath, savedItem.OriginalPath);
            Assert.Equal(item.OriginalSize, savedItem.OriginalSize);
            Assert.Equal(CompressionStatus.Uncompressed, savedItem.CompressionStatus);
        }

        [Fact]
        public async Task GetAllAsync_ReturnsAllStoredItems()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var repository = new EfSmartBinRepository(context);

            var item1 = new SmartBinItem { OriginalPath = "P1", OriginalFileName = "F1", Sha256Hash = "H1" };
            var item2 = new SmartBinItem { OriginalPath = "P2", OriginalFileName = "F2", Sha256Hash = "H2" };

            await repository.AddAsync(item1);
            await repository.AddAsync(item2);

            // Act
            var results = (await repository.GetAllAsync()).ToList();

            // Assert
            Assert.Equal(2, results.Count);
            Assert.Contains(results, i => i.OriginalPath == "P1");
            Assert.Contains(results, i => i.OriginalPath == "P2");
        }

        [Fact]
        public async Task GetByIdAsync_ReturnsCorrectItem()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var repository = new EfSmartBinRepository(context);

            var itemId = Guid.NewGuid();
            var item = new SmartBinItem
            {
                Id = itemId,
                OriginalPath = "P1",
                OriginalFileName = "F1",
                Sha256Hash = "H1"
            };
            await repository.AddAsync(item);

            // Act
            var result = await repository.GetByIdAsync(itemId);

            // Assert
            Assert.NotNull(result);
            Assert.Equal("P1", result.OriginalPath);
        }

        [Fact]
        public async Task UpdateAsync_ModifiesExistingItem()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var repository = new EfSmartBinRepository(context);

            var item = new SmartBinItem
            {
                OriginalPath = "P1",
                OriginalFileName = "F1",
                OriginalSize = 100,
                Sha256Hash = "H1"
            };
            await repository.AddAsync(item);

            // Act
            item.UpdateCompressionResult(50, CompressionAlgorithm.Brotli);
            await repository.UpdateAsync(item);

            // Assert
            var updated = await repository.GetByIdAsync(item.Id);
            Assert.NotNull(updated);
            Assert.Equal(CompressionStatus.Compressed, updated.CompressionStatus);
            Assert.Equal(50, updated.CurrentStoredSize);
            Assert.Equal(CompressionAlgorithm.Brotli, updated.CompressionAlgorithm);
        }

        [Fact]
        public async Task DeleteAsync_RemovesItemFromDatabase()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var repository = new EfSmartBinRepository(context);

            var item = new SmartBinItem
            {
                OriginalPath = "P1",
                OriginalFileName = "F1",
                Sha256Hash = "H1"
            };
            await repository.AddAsync(item);

            // Act
            await repository.DeleteAsync(item.Id);

            // Assert
            var deleted = await repository.GetByIdAsync(item.Id);
            Assert.Null(deleted);
        }
    }
}
