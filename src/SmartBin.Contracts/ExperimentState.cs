namespace SmartBin.Contracts
{
    public enum ExperimentState
    {
        Discovered = 0,
        Acquired = 1,
        AcquisitionVerified = 2,
        Compressed = 3,
        CompressionVerified = 4,
        RestorationVerified = 5,
        ReadyForCommit = 6,
        Committed = 7,
        Restored = 8,
        Failed = 9,
        Cancelled = 10
    }
}
