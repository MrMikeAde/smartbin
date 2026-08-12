using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public interface IStorageMonitor
    {
        /// <summary>
        /// Starts the background monitoring loop at a configurable interval.
        /// </summary>
        void StartMonitoring(TimeSpan interval);

        /// <summary>
        /// Stops the background monitoring loop.
        /// </summary>
        void StopMonitoring();

        /// <summary>
        /// Event triggered when storage pressure state changes (Normal, Low, Critical).
        /// </summary>
        event Action<StorageSpaceMetrics>? PressureStateChanged;

        /// <summary>
        /// Gets the current storage monitor status.
        /// </summary>
        bool IsMonitoringActive { get; }
    }
}
