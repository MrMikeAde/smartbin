namespace SmartBin.Core.Models
{
    public enum CompressionAlgorithm
    {
        None = 0,
        Zip = 1,
        Brotli = 2,
        Gzip = 3,
        Zstandard = 4
    }

    public enum CompressionStatus
    {
        Uncompressed = 0,
        Compressed = 1,
        Failed = 2,
        NotFeasible = 3
    }

    public enum RestorationStatus
    {
        Pending = 0,
        Restored = 1,
        Failed = 2
    }
}
