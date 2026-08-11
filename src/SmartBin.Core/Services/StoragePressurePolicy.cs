using System;
using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class StorageOptimizationRecommendation
    {
        public bool IsOptimizationRecommended { get; set; }
        public StoragePressureState CurrentState { get; set; }
        public double TargetFreeSpacePercentage { get; set; }
        public long TargetFreeSpaceBytes { get; set; }
        public long RequiredSpaceToReclaimBytes { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }

    public static class StoragePressurePolicy
    {
        /// <summary>
        /// Converts the current storage pressure metrics into a deterministic optimization recommendation.
        /// </summary>
        public static StorageOptimizationRecommendation Evaluate(StorageSpaceMetrics metrics, double targetFreePercent = 20.0)
        {
            if (metrics == null) throw new ArgumentNullException(nameof(metrics));

            var recommendation = new StorageOptimizationRecommendation
            {
                CurrentState = metrics.PressureState,
                TargetFreeSpacePercentage = targetFreePercent,
                TargetFreeSpaceBytes = (long)(metrics.TotalCapacity * (targetFreePercent / 100.0))
            };

            recommendation.RequiredSpaceToReclaimBytes = Math.Max(0, recommendation.TargetFreeSpaceBytes - metrics.AvailableFreeSpace);

            switch (metrics.PressureState)
            {
                case StoragePressureState.Normal:
                    recommendation.IsOptimizationRecommended = false;
                    recommendation.RequiredSpaceToReclaimBytes = 0; // No need to reclaim
                    recommendation.Rationale = "Storage pressure is Normal. No automatic optimization is recommended.";
                    break;

                case StoragePressureState.Low:
                    recommendation.IsOptimizationRecommended = true;
                    recommendation.Rationale = $"Storage pressure is Low (Free Space: {metrics.FreeSpacePercentage:F1}%). Optimization is recommended to reach the safety target of {targetFreePercent}%.";
                    break;

                case StoragePressureState.Critical:
                    recommendation.IsOptimizationRecommended = true;
                    // For Critical state, we could enforce an even more urgent rationale
                    recommendation.Rationale = $"Storage pressure is Critical (Free Space: {metrics.FreeSpacePercentage:F1}%). Prioritize aggressive compression immediately to reclaim storage and avoid system problems.";
                    break;
            }

            return recommendation;
        }
    }
}
