using System;

namespace SmartBin.Contracts
{
    public class ActivityLog
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string OperationType { get; set; } = string.Empty; // e.g. "Optimization", "Restore", "Scan"
        public string ItemName { get; set; } = string.Empty;
        public long OriginalSize { get; set; }
        public long CompressedSize { get; set; }
        public long ReclaimedBytes { get; set; }
        public string ResultState { get; set; } = string.Empty; // e.g. "Verified", "Failed", "Cancelled"
        public string FailureReason { get; set; } = string.Empty;
        public bool IsAutomatic { get; set; }
        public string Rationale { get; set; } = string.Empty;
    }
}
