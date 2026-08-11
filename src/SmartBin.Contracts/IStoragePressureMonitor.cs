using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IStoragePressureMonitor
    {
        /// <summary>
        /// Checks if storage pressure exceeds a certain threshold.
        /// </summary>
        Task<bool> IsStoragePressureHighAsync(double thresholdPercentage, CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets the current detailed storage space metrics.
        /// </summary>
        Task<StorageSpaceMetrics> GetStorageMetricsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Gets or sets the threshold percentage below which pressure is marked as Low.
        /// </summary>
        double LowPressureThresholdPercentage { get; set; }

        /// <summary>
        /// Gets or sets the threshold percentage below which pressure is marked as Critical.
        /// </summary>
        double CriticalPressureThresholdPercentage { get; set; }

        /// <summary>
        /// Gets or sets a simulated override for the storage space metrics.
        /// </summary>
        StorageSpaceMetrics? MockMetricsOverride { get; set; }
    }
}
