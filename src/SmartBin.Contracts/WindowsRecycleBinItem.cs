using System;

namespace SmartBin.Contracts
{
    public class WindowsRecycleBinItem
    {
        public string Id { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string OriginalPath { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime? DeletedTimestamp { get; set; }
        public string Volume { get; set; } = string.Empty;
        public bool IsSimulated { get; set; }
    }
}
