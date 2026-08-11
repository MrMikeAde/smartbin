using System;
using System.Collections.Generic;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;
using Xunit;

namespace SmartBin.Core.Tests
{
    public class CandidatePrioritizationTests
    {
        [Fact]
        public void StoragePressurePolicy_NormalState_RecommendsNoOptimization()
        {
            // Arrange
            var metrics = new StorageSpaceMetrics
            {
                TotalCapacity = 100000,
                AvailableFreeSpace = 25000, // 25% free
                UsedSpace = 75000,
                FreeSpacePercentage = 25.0,
                PressureState = StoragePressureState.Normal
            };

            // Act
            var recommendation = StoragePressurePolicy.Evaluate(metrics, targetFreePercent: 20.0);

            // Assert
            Assert.False(recommendation.IsOptimizationRecommended);
            Assert.Equal(0, recommendation.RequiredSpaceToReclaimBytes);
            Assert.Contains("No automatic optimization", recommendation.Rationale);
        }

        [Fact]
        public void StoragePressurePolicy_LowAndCriticalStates_RecommendsOptimization()
        {
            // Arrange
            var metricsLow = new StorageSpaceMetrics
            {
                TotalCapacity = 100000,
                AvailableFreeSpace = 12000, // 12% free (under 20% target, under 15% threshold)
                UsedSpace = 88000,
                FreeSpacePercentage = 12.0,
                PressureState = StoragePressureState.Low
            };

            // Act
            var recLow = StoragePressurePolicy.Evaluate(metricsLow, targetFreePercent: 20.0);

            // Assert
            Assert.True(recLow.IsOptimizationRecommended);
            Assert.Equal(8000, recLow.RequiredSpaceToReclaimBytes); // (100000 * 0.20) - 12000 = 8000 bytes needed
            Assert.Contains("pressure is Low", recLow.Rationale);
        }

        [Fact]
        public void CandidateAnalyzer_CalculatesScoresAndExplanationsCorrectly()
        {
            // Arrange
            var mockRepo = new MockRepository();
            var analyzer = new CandidateAnalyzer(mockRepo);

            // Item 1: Large, uncompressed, old deleted text file (Expected high priority)
            var item1 = new SmartBinItem
            {
                OriginalFileName = "huge_data.sql",
                OriginalExtension = ".sql",
                OriginalSize = 200 * 1024 * 1024, // 200 MB
                DeletedTimestamp = DateTime.UtcNow.AddDays(-40), // 40 days old
                CompressionStatus = CompressionStatus.Uncompressed,
                Sha256Hash = "hash"
            };

            // Item 2: Tiny, brand-new already compressed file (Expected lowest priority)
            var item2 = new SmartBinItem
            {
                OriginalFileName = "pic.png",
                OriginalExtension = ".png",
                OriginalSize = 5 * 1024, // 5 KB
                DeletedTimestamp = DateTime.UtcNow.AddMinutes(-5), // 5 minutes old
                CompressionStatus = CompressionStatus.Compressed,
                CurrentStoredSize = 5 * 1024,
                Sha256Hash = "hash"
            };

            // Act
            var cand1 = analyzer.AnalyzeItem(item1);
            var cand2 = analyzer.AnalyzeItem(item2);

            // Assert
            Assert.True(cand1.PriorityScore > cand2.PriorityScore);
            Assert.True(cand1.IsEligibleForOptimization);
            Assert.False(cand2.IsEligibleForOptimization); // Already optimized

            // Verify explainability
            Assert.Contains("Large file", cand1.PriorityExplaination);
            Assert.Contains("Not currently compressed", cand1.PriorityExplaination);
            Assert.Contains("40 days old", cand1.PriorityExplaination);

            Assert.Contains("Small file", cand2.PriorityExplaination);
            Assert.Contains("0 days old", cand2.PriorityExplaination);
        }

        [Fact]
        public void OptimizationPlanner_BuildsDeterministicOptimizedPlans()
        {
            // Arrange
            var planner = new OptimizationPlanner();

            var candA = new CandidateItem
            {
                ItemId = Guid.NewGuid(),
                OriginalFileName = "A.txt",
                EstimatedSavingsBytes = 7000,
                PriorityScore = 350.0,
                IsEligibleForOptimization = true
            };

            var candB = new CandidateItem
            {
                ItemId = Guid.NewGuid(),
                OriginalFileName = "B.txt",
                EstimatedSavingsBytes = 4000,
                PriorityScore = 320.0,
                IsEligibleForOptimization = true
            };

            var candC = new CandidateItem
            {
                ItemId = Guid.NewGuid(),
                OriginalFileName = "C.txt",
                EstimatedSavingsBytes = 1000,
                PriorityScore = 200.0,
                IsEligibleForOptimization = true
            };

            var candD = new CandidateItem
            {
                ItemId = Guid.NewGuid(),
                OriginalFileName = "D.txt",
                EstimatedSavingsBytes = 200,
                PriorityScore = 100.0,
                IsEligibleForOptimization = true
            };

            var candidates = new List<CandidateItem> { candD, candB, candC, candA };

            // Target needs 10,000 bytes
            // Act
            var plan = planner.GeneratePlan(candidates, currentFreeSpace: 10000, targetFreeSpace: 20000);

            // Assert
            Assert.Equal(10000, plan.RequiredSpaceToReclaim);
            // It should sort candidates by score DESC (A, B, C, D)
            // A (7000) + B (4000) = 11,000 >= 10,000. So C and D should NOT be in the plan.
            Assert.Equal(2, plan.ItemsToOptimize.Count);
            Assert.Equal(candA.ItemId, plan.ItemsToOptimize[0].ItemId);
            Assert.Equal(candB.ItemId, plan.ItemsToOptimize[1].ItemId);
            Assert.Equal(11000, plan.ExpectedReclaimedBytes);
        }

        [Fact]
        public void OptimizationPlanner_TargetSatisfied_ReturnsEmptyPlan()
        {
            // Arrange
            var planner = new OptimizationPlanner();
            var candidates = new List<CandidateItem> {
                new() { ItemId = Guid.NewGuid(), EstimatedSavingsBytes = 500, PriorityScore = 200.0, IsEligibleForOptimization = true }
            };

            // Act
            var plan = planner.GeneratePlan(candidates, currentFreeSpace: 15000, targetFreeSpace: 10000);

            // Assert
            Assert.Empty(plan.ItemsToOptimize);
            Assert.Equal(0, plan.ExpectedReclaimedBytes);
            Assert.Equal(0, plan.RequiredSpaceToReclaim);
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
