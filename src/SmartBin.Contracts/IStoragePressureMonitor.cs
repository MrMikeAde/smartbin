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
    }
}
