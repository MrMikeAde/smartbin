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
        /// Activates simulated storage pressure based on a custom used percentage.
        /// </summary>
        public void EnablePercentageUsed(double usedPercentage, long totalCapacity = 100 * 1024 * 1024 * 1024L)
        {
            double freePercentage = 100.0 - usedPercentage;
            EnablePercentageFree(freePercentage, totalCapacity);
        }

        /// <summary>
        /// Activates simulated storage pressure based on a custom free percentage.
        /// </summary>
        public void EnablePercentageFree(double freePercentage, long totalCapacity = 100 * 1024 * 1024 * 1024L)
        {
            long freeSpace = (long)(totalCapacity * (freePercentage / 100.0));
            long usedSpace = totalCapacity - freeSpace;

            var state = StoragePressureState.Normal;
            if (freePercentage < _pressureMonitor.CriticalPressureThresholdPercentage)
            {
                state = StoragePressureState.Critical;
            }
            else if (freePercentage < _pressureMonitor.LowPressureThresholdPercentage)
            {
                state = StoragePressureState.Low;
            }

            var metrics = new StorageSpaceMetrics
            {
                TotalCapacity = totalCapacity,
                AvailableFreeSpace = freeSpace,
                UsedSpace = usedSpace,
                FreeSpacePercentage = freePercentage,
                PressureState = state
            };

            _pressureMonitor.MockMetricsOverride = metrics;
        }

        /// <summary>
        /// Activates simulated storage pressure by setting available free space in bytes.
        /// </summary>
        public void SetFreeSpaceBytes(long freeBytes, long totalCapacity = 100 * 1024 * 1024 * 1024L)
        {
            double freePercentage = totalCapacity > 0 ? ((double)freeBytes / totalCapacity) * 100.0 : 100.0;
            EnablePercentageFree(freePercentage, totalCapacity);
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
