using System;
using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class StoragePressureSimulator
    {
        private readonly IStoragePressureMonitor _pressureMonitor;

        public StoragePressureSimulator(IStoragePressureMonitor pressureMonitor)
        {
            _pressureMonitor = pressureMonitor ?? throw new ArgumentNullException(nameof(pressureMonitor));
        }

        /// <summary>
        /// Activates simulated storage pressure by overriding the monitor's metrics.
        /// </summary>
        public void EnableSimulation(StoragePressureState state, long totalCapacity = 100 * 1024 * 1024 * 1024L)
        {
            // Determine a free space percentage that forces the chosen state
            double freePercent = 20.0; // Normal default

            if (state == StoragePressureState.Critical)
            {
                freePercent = _pressureMonitor.CriticalPressureThresholdPercentage - 1.0;
            }
            else if (state == StoragePressureState.Low)
            {
                freePercent = _pressureMonitor.LowPressureThresholdPercentage - 1.0;
            }
            else
            {
                freePercent = _pressureMonitor.LowPressureThresholdPercentage + 5.0;
            }

            long freeSpace = (long)(totalCapacity * (freePercent / 100.0));
            long usedSpace = totalCapacity - freeSpace;

            var metrics = new StorageSpaceMetrics
            {
                TotalCapacity = totalCapacity,
                AvailableFreeSpace = freeSpace,
                UsedSpace = usedSpace,
                FreeSpacePercentage = freePercent,
                PressureState = state
            };

            _pressureMonitor.MockMetricsOverride = metrics;
        }

        /// <summary>
        /// Disables the simulation, returning the pressure monitor to real physical drive checks.
        /// </summary>
        public void DisableSimulation()
        {
            _pressureMonitor.MockMetricsOverride = null;
        }
    }
}
