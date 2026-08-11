using System;
using SmartBin.Contracts;

namespace SmartBin.Core.Models
{
    public class SmartBinItem : ISmartBinItem
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // Use non-nullable or nullable appropriately. Since we're in nullable-enabled, initialize defaults.
        public string OriginalPath { get; set; } = string.Empty;
        public string OriginalFileName { get; set; } = string.Empty;
        public string OriginalExtension { get; set; } = string.Empty;
        public long OriginalSize { get; set; }
        public DateTime DeletedTimestamp { get; set; } = DateTime.UtcNow;
        public DateTime? OriginalCreationTimestamp { get; set; }
        public DateTime? OriginalModificationTimestamp { get; set; }
        public string Sha256Hash { get; set; } = string.Empty;
        public string CurrentStoragePath { get; set; } = string.Empty;
        public long CurrentStoredSize { get; set; }

        public CompressionStatus CompressionStatus { get; set; } = CompressionStatus.Uncompressed;
        public CompressionAlgorithm CompressionAlgorithm { get; set; } = CompressionAlgorithm.None;
        public DateTime? CompressionTimestamp { get; set; }
        public RestorationStatus RestorationStatus { get; set; } = RestorationStatus.Pending;

        // Implement ISmartBinItem properties explicitly or implicitly
        int ISmartBinItem.CompressionStatus => (int)CompressionStatus;
        int ISmartBinItem.CompressionAlgorithm => (int)CompressionAlgorithm;
        int ISmartBinItem.RestorationStatus => (int)RestorationStatus;

        /// <summary>
        /// Business rule validation for the model before any persist operation.
        /// </summary>
        public bool IsValid()
        {
            if (string.IsNullOrWhiteSpace(OriginalPath)) return false;
            if (string.IsNullOrWhiteSpace(OriginalFileName)) return false;
            if (OriginalSize < 0) return false;
            if (CurrentStoredSize < 0) return false;
            if (string.IsNullOrWhiteSpace(Sha256Hash)) return false;
            return true;
        }

        /// <summary>
        /// Transition the compression status of the item.
        /// </summary>
        public void UpdateCompressionResult(long compressedSize, CompressionAlgorithm algorithm)
        {
            if (compressedSize < 0)
            {
                throw new ArgumentException("Compressed size cannot be negative.", nameof(compressedSize));
            }

            // Compression is optional and only applied if strictly smaller than original size
            if (compressedSize < OriginalSize)
            {
                CompressionStatus = CompressionStatus.Compressed;
                CompressionAlgorithm = algorithm;
                CurrentStoredSize = compressedSize;
                CompressionTimestamp = DateTime.UtcNow;
            }
            else
            {
                CompressionStatus = CompressionStatus.NotFeasible;
                CompressionAlgorithm = CompressionAlgorithm.None;
                CurrentStoredSize = OriginalSize;
                CompressionTimestamp = DateTime.UtcNow;
            }
        }
    }
}
