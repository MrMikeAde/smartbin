using System;

namespace SmartBin.Contracts
{
    public class CandidateItem
    {
        public Guid ItemId { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string OriginalExtension { get; set; } = string.Empty;
        public long OriginalSize { get; set; }
        public long CurrentStoredSize { get; set; }
        public DateTime DeletedTimestamp { get; set; }
        public int CompressionStatus { get; set; }
        public double EstimatedSavingsBytes { get; set; }
        public double EstimatedCompressionRatio { get; set; }
        public double PriorityScore { get; set; }
        public string PriorityExplaination { get; set; } = string.Empty;
        public bool IsEligibleForOptimization { get; set; }
    }
}
