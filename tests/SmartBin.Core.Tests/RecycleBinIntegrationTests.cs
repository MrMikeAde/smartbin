using System;
using System.Linq;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;
using Xunit;

namespace SmartBin.Core.Tests
{
    public class RecycleBinIntegrationTests
    {
        [Fact]
        public async Task SimulatedRecycleBinProvider_EnumerateItems_ReturnsDeterministicData()
        {
            // Arrange
            var provider = new SimulatedRecycleBinProvider();

            // Act
            var items = (await provider.EnumerateItemsAsync()).ToList();

            // Assert
            Assert.Equal(4, items.Count);

            var dbItem = items.First(i => i.FileName == "database.sql");
            Assert.Equal("C:", dbItem.Volume);
            Assert.True(dbItem.Size > 4L * 1024 * 1024 * 1024); // 4.2 GB
            Assert.True(dbItem.IsSimulated);

            var videoItem = items.First(i => i.FileName == "video.mp4");
            Assert.Equal("D:", videoItem.Volume);
        }

        [Fact]
        public async Task SimulatedRecycleBinProvider_GetStatistics_ReturnsCorrectAggregates()
        {
            // Arrange
            var provider = new SimulatedRecycleBinProvider();

            // Act
            var stats = await provider.GetStatisticsAsync();

            // Assert
            Assert.Equal(4, stats.TotalItems);
            Assert.True(stats.TotalSize > 0);
        }

        [Fact]
        public async Task SimulatedRecycleBinProvider_GetItem_ReturnsCorrectItemOrNull()
        {
            // Arrange
            var provider = new SimulatedRecycleBinProvider();

            // Act
            var item = await provider.GetItemAsync("sim_2");
            var missing = await provider.GetItemAsync("non_existent_id");

            // Assert
            Assert.NotNull(item);
            Assert.Equal("logs.txt", item.FileName);
            Assert.Null(missing);
        }

        [Fact]
        public void CandidateAnalyzer_AnalyzeWindowsItem_RanksAndEnsuresReadOnly()
        {
            // Arrange
            var mockRepo = new MockRepository();
            var analyzer = new CandidateAnalyzer(mockRepo);

            var winItem = new WindowsRecycleBinItem
            {
                Id = "win_test",
                FileName = "report.csv",
                OriginalPath = @"C:\Users\User\Desktop\report.csv",
                Size = 15L * 1024 * 1024, // 15 MB
                DeletedTimestamp = DateTime.UtcNow.AddDays(-15),
                Volume = "C:",
                IsSimulated = true
            };

            // Act
            var candidate = analyzer.AnalyzeWindowsItem(winItem);

            // Assert
            Assert.NotNull(candidate);
            Assert.Equal("report.csv", candidate.OriginalFileName);
            Assert.Equal(".csv", candidate.OriginalExtension);
            Assert.Equal(15L * 1024 * 1024, candidate.OriginalSize);

            // Expected score factors: size factor + age factor + benefit factor + uncompressed status factor
            // Size 15MB: 85 points. Age 15 days: 60 points. Benefit (csv ratio 0.35 -> 65% savings): 100 points. Status: 100 points.
            // Total expected score: 345
            Assert.Equal(345.0, candidate.PriorityScore);
            Assert.Contains("Large file", candidate.PriorityExplaination);
            Assert.Contains("15 days old", candidate.PriorityExplaination);

            // In Phase 5 & 6, real/simulated Windows Recycle Bin items are eligible for optimization if they are compressible.
            Assert.True(candidate.IsEligibleForOptimization);
        }

        private class MockRepository : ISmartBinRepository<SmartBinItem>
        {
            public Task AddAsync(SmartBinItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<IEnumerable<SmartBinItem>> GetAllAsync(CancellationToken cancellationToken = default) => Task.FromResult<IEnumerable<SmartBinItem>>(new List<SmartBinItem>());
            public Task<SmartBinItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SmartBinItem?>(null);
            public Task UpdateAsync(SmartBinItem item, CancellationToken cancellationToken = default) => Task.CompletedTask;
        }
    }
}
