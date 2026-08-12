using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class CandidateAnalyzer
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;

        public CandidateAnalyzer(ISmartBinRepository<SmartBinItem> repository)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        }

        public async Task<List<CandidateItem>> AnalyzeCandidatesAsync(CancellationToken cancellationToken = default)
        {
            var dbItems = await _repository.GetAllAsync(cancellationToken);
            var candidates = new List<CandidateItem>();

            foreach (var item in dbItems)
            {
                var candidate = AnalyzeItem(item);
                candidates.Add(candidate);
            }

            return candidates;
        }

        public CandidateItem AnalyzeWindowsItem(WindowsRecycleBinItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var ext = Path.GetExtension(item.OriginalPath);
            var candidate = new CandidateItem
            {
                ItemId = Guid.Empty, // Windows Recycle Bin item (Id represented as string elsewhere)
                OriginalFileName = item.FileName,
                OriginalExtension = ext,
                OriginalSize = item.Size,
                CurrentStoredSize = item.Size,
                DeletedTimestamp = item.DeletedTimestamp ?? DateTime.UtcNow,
                CompressionStatus = (int)CompressionStatus.Uncompressed
            };

            // Estimate Compression Ratio and Savings (Read-only analysis)
            if (CompressionHeuristics.IsTypicallyCompressed(ext))
            {
                candidate.EstimatedCompressionRatio = 1.0;
                candidate.EstimatedSavingsBytes = 0;
            }
            else
            {
                var lowerExt = ext.ToLowerInvariant();
                double estimatedRatio = 0.70;
                if (lowerExt == ".txt" || lowerExt == ".csv" || lowerExt == ".log" || lowerExt == ".sql" || lowerExt == ".ini" || lowerExt == ".json" || lowerExt == ".xml")
                {
                    estimatedRatio = 0.35;
                }

                candidate.EstimatedCompressionRatio = estimatedRatio;
                candidate.EstimatedSavingsBytes = Math.Max(0, (long)(item.Size * (1.0 - estimatedRatio)));
            }

            // Calculate Scoring Factors
            double sizeScore = GetSizeFactor(item.Size);
            double ageScore = GetAgeFactor(candidate.DeletedTimestamp);
            double savingsScore = GetSavingsFactor(candidate.EstimatedCompressionRatio);
            double statusScore = GetStatusFactor(CompressionStatus.Uncompressed);

            candidate.PriorityScore = sizeScore + ageScore + savingsScore + statusScore;

            // Generate explainability rationale
            var ageDays = (DateTime.UtcNow - candidate.DeletedTimestamp).TotalDays;
            var bulletPoints = new List<string>();

            if (item.Size >= 10 * 1024 * 1024) bulletPoints.Add("• Large file");
            else if (item.Size >= 1024 * 1024) bulletPoints.Add("• Medium file");
            else bulletPoints.Add("• Small file");

            bulletPoints.Add($"• {(int)ageDays} days old");

            if (candidate.EstimatedSavingsBytes >= 1024 * 1024)
            {
                bulletPoints.Add($"• Estimated {((double)candidate.EstimatedSavingsBytes / (1024 * 1024)):F1} MB savings");
            }
            else
            {
                bulletPoints.Add($"• Estimated {candidate.EstimatedSavingsBytes:N0} bytes savings");
            }

            bulletPoints.Add("• Not currently compressed");
            bulletPoints.Add("• Location: Windows Recycle Bin (Read-only Analysis)");

            candidate.IsEligibleForOptimization = candidate.EstimatedSavingsBytes > 0;
            candidate.PriorityExplaination = string.Join("\n", bulletPoints);

            return candidate;
        }

        public CandidateItem AnalyzeItem(SmartBinItem item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));

            var candidate = new CandidateItem
            {
                ItemId = item.Id,
                OriginalFileName = item.OriginalFileName,
                OriginalExtension = item.OriginalExtension,
                OriginalSize = item.OriginalSize,
                CurrentStoredSize = item.CurrentStoredSize,
                DeletedTimestamp = item.DeletedTimestamp,
                CompressionStatus = (int)item.CompressionStatus
            };

            // Estimate Compression Ratio and Savings
            if (item.CompressionStatus == CompressionStatus.Compressed)
            {
                candidate.EstimatedCompressionRatio = item.OriginalSize > 0 ? (double)item.CurrentStoredSize / item.OriginalSize : 1.0;
                candidate.EstimatedSavingsBytes = Math.Max(0, item.OriginalSize - item.CurrentStoredSize);
            }
            else if (item.CompressionStatus == CompressionStatus.NotFeasible)
            {
                candidate.EstimatedCompressionRatio = 1.0;
                candidate.EstimatedSavingsBytes = 0;
            }
            else
            {
                // Heuristic based estimation for Uncompressed items
                if (CompressionHeuristics.IsTypicallyCompressed(item.OriginalExtension))
                {
                    candidate.EstimatedCompressionRatio = 1.0;
                    candidate.EstimatedSavingsBytes = 0;
                }
                else
                {
                    // Basic rule of thumb estimation:
                    // Text and configurations compress extremely well (avg 35% of original size)
                    // Others average 70% of original size
                    var ext = item.OriginalExtension.ToLowerInvariant();
                    double estimatedRatio = 0.70;
                    if (ext == ".txt" || ext == ".csv" || ext == ".log" || ext == ".sql" || ext == ".ini" || ext == ".json" || ext == ".xml")
                    {
                        estimatedRatio = 0.35;
                    }

                    candidate.EstimatedCompressionRatio = estimatedRatio;
                    candidate.EstimatedSavingsBytes = Math.Max(0, (long)(item.OriginalSize * (1.0 - estimatedRatio)));
                }
            }

            // Calculate Scoring Factors
            double sizeScore = GetSizeFactor(item.OriginalSize);
            double ageScore = GetAgeFactor(item.DeletedTimestamp);
            double savingsScore = GetSavingsFactor(candidate.EstimatedCompressionRatio);
            double statusScore = GetStatusFactor((CompressionStatus)item.CompressionStatus);

            candidate.PriorityScore = sizeScore + ageScore + savingsScore + statusScore;

            // Generate prediction/explainability rationale
            var ageDays = (DateTime.UtcNow - item.DeletedTimestamp).TotalDays;
            var bulletPoints = new List<string>();

            if (item.OriginalSize >= 10 * 1024 * 1024) bulletPoints.Add("• Large file");
            else if (item.OriginalSize >= 1024 * 1024) bulletPoints.Add("• Medium file");
            else bulletPoints.Add("• Small file");

            bulletPoints.Add($"• {(int)ageDays} days old");

            if (candidate.EstimatedSavingsBytes >= 1024 * 1024)
            {
                bulletPoints.Add($"• Estimated {((double)candidate.EstimatedSavingsBytes / (1024 * 1024)):F1} MB savings");
            }
            else
            {
                bulletPoints.Add($"• Estimated {candidate.EstimatedSavingsBytes:N0} bytes savings");
            }

            if (item.CompressionStatus == CompressionStatus.Uncompressed)
            {
                bulletPoints.Add("• Not currently compressed");
                candidate.IsEligibleForOptimization = candidate.EstimatedSavingsBytes > 0;
            }
            else
            {
                bulletPoints.Add($"• Compression status: {item.CompressionStatus}");
                candidate.IsEligibleForOptimization = false; // Already optimized or not feasible
            }

            candidate.PriorityExplaination = string.Join("\n", bulletPoints);

            return candidate;
        }

        private static double GetSizeFactor(long size)
        {
            if (size >= 100 * 1024 * 1024) return 100.0; // >100MB
            if (size >= 10 * 1024 * 1024) return 85.0;  // 10MB to 100MB
            if (size >= 1024 * 1024) return 60.0;       // 1MB to 10MB
            if (size >= 10 * 1024) return 30.0;         // 10KB to 1MB
            return 10.0;                                // <10KB
        }

        private static double GetAgeFactor(DateTime deletedTime)
        {
            var days = (DateTime.UtcNow - deletedTime).TotalDays;
            if (days >= 30) return 100.0;
            if (days >= 7) return 60.0;
            if (days >= 1) return 30.0;
            return 10.0;
        }

        private static double GetSavingsFactor(double ratio)
        {
            double savingsPercent = 1.0 - ratio;
            if (savingsPercent >= 0.50) return 100.0;
            if (savingsPercent >= 0.20) return 60.0;
            if (savingsPercent >= 0.05) return 35.0;
            return 0.0;
        }

        private static double GetStatusFactor(CompressionStatus status)
        {
            if (status == CompressionStatus.Uncompressed) return 100.0;
            if (status == CompressionStatus.NotFeasible) return 10.0;
            return 0.0; // Already compressed
        }
    }
}
