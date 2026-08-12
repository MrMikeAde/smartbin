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
    }
}
