namespace SmartBin.Contracts
{
    public enum AutoOptimizationMode
    {
        Off = 0,
        NotifyMe = 1,
        Automatic = 2
    }

    public class SmartBinSettings
    {
        public AutoOptimizationMode Mode { get; set; } = AutoOptimizationMode.Off;

        public double LowPressureThresholdPercentage { get; set; } = 15.0;
        public double CriticalPressureThresholdPercentage { get; set; } = 5.0;
        public double TargetFreeSpacePercentage { get; set; } = 20.0;

        // Hard safety margin (default 5 GB)
        public long MinimumSafetyMarginBytes { get; set; } = 5L * 1024 * 1024 * 1024;

        // Power and resource limits
        public bool PauseOnBattery { get; set; } = true;
        public int MaxItemsPerSession { get; set; } = 5;
        public long MaxStorageProcessedPerSessionBytes { get; set; } = 50L * 1024 * 1024 * 1024; // 50 GB

        /// <summary>
        /// Validates settings range bounds. If any values are untrusted or out-of-range,
        /// resets them to safe defaults and forces automatic optimization OFF for safety.
        /// </summary>
        public void ValidateAndNormalize()
        {
            bool isTrusted = true;

            if (LowPressureThresholdPercentage <= 0.0 || LowPressureThresholdPercentage >= 100.0)
            {
                LowPressureThresholdPercentage = 15.0;
                isTrusted = false;
            }

            if (CriticalPressureThresholdPercentage <= 0.0 || CriticalPressureThresholdPercentage >= 100.0 || CriticalPressureThresholdPercentage >= LowPressureThresholdPercentage)
            {
                CriticalPressureThresholdPercentage = 5.0;
                isTrusted = false;
            }

            if (TargetFreeSpacePercentage <= 0.0 || TargetFreeSpacePercentage >= 100.0 || TargetFreeSpacePercentage <= LowPressureThresholdPercentage)
            {
                TargetFreeSpacePercentage = 20.0;
                isTrusted = false;
            }

            if (MinimumSafetyMarginBytes < 0)
            {
                MinimumSafetyMarginBytes = 5L * 1024 * 1024 * 1024;
                isTrusted = false;
            }

            if (MaxItemsPerSession < 1 || MaxItemsPerSession > 1000)
            {
                MaxItemsPerSession = 5;
                isTrusted = false;
            }

            if (MaxStorageProcessedPerSessionBytes < 0)
            {
                MaxStorageProcessedPerSessionBytes = 50L * 1024 * 1024 * 1024;
                isTrusted = false;
            }

            // Safe Default Fallback
            if (!isTrusted)
            {
                Mode = AutoOptimizationMode.Off;
            }
        }
    }
}
