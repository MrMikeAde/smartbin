using System;

namespace SmartBin.Contracts
{
    public class ControlledExperimentItem
    {
        public Guid ExperimentId { get; set; } = Guid.NewGuid();
        public string WindowsItemIdentifier { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public long OriginalSize { get; set; }
        public string OriginalSha256 { get; set; } = string.Empty;
        public long CompressedSize { get; set; }
        public double CompressionRatio { get; set; }
        public long ActualSavingsBytes { get; set; }
        public DateTime DeletionTimestamp { get; set; }
        public string Volume { get; set; } = string.Empty;
        public ExperimentState State { get; set; } = ExperimentState.Discovered;
        public bool DidWindowsMutationOccur { get; set; }
        public string RestorationResultPath { get; set; } = string.Empty;
        public string FinalVerificationHash { get; set; } = string.Empty;
        public string FailureMessage { get; set; } = string.Empty;
        public DateTime CreatedTimestamp { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedTimestamp { get; set; }
    }
}
