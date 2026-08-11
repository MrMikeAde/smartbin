namespace SmartBin.Contracts
{
    public enum StoragePressureState
    {
        Normal = 0,
        Low = 1,
        Critical = 2
    }

    public class StorageSpaceMetrics
    {
        public long TotalCapacity { get; set; }
        public long AvailableFreeSpace { get; set; }
        public long UsedSpace { get; set; }
        public double FreeSpacePercentage { get; set; }
        public StoragePressureState PressureState { get; set; }
    }
}
