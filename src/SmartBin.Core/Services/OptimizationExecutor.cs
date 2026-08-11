using System;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class OptimizationExecutor : IOptimizationExecutor
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly IStoragePressureMonitor _pressureMonitor;
        private readonly ICompressionEngine _compressionEngine;

        public OptimizationExecutor(
            ISmartBinRepository<SmartBinItem> repository,
            IStoragePressureMonitor pressureMonitor,
            ICompressionEngine compressionEngine)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _pressureMonitor = pressureMonitor ?? throw new ArgumentNullException(nameof(pressureMonitor));
            _compressionEngine = compressionEngine ?? throw new ArgumentNullException(nameof(compressionEngine));
        }

        public async Task<ExecutionResult> ExecutePlanAsync(OptimizationPlan plan, long targetFreeSpace, CancellationToken cancellationToken = default)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));

            var result = new ExecutionResult();

            foreach (var candidate in plan.ItemsToOptimize)
            {
                // Support cancellation
                if (cancellationToken.IsCancellationRequested)
                {
                    result.Interrupted = true;
                    result.Message = "Optimization run was cancelled.";
                    break;
                }

                // 1. Recheck storage available free space
                var metrics = await _pressureMonitor.GetStorageMetricsAsync(cancellationToken);
                if (metrics.AvailableFreeSpace >= targetFreeSpace)
                {
                    result.Message = "Sufficient free space has been recovered. Stopping early.";
                    break; // Target satisfied, stop early to avoid unnecessary work!
                }

                // 2. Revalidate candidate
                var item = await _repository.GetByIdAsync(candidate.ItemId, cancellationToken);
                if (item == null || item.CompressionStatus != CompressionStatus.Uncompressed)
                {
                    // Candidate is no longer valid, skip
                    continue;
                }

                result.TotalProcessedCount++;

                try
                {
                    // 3. Perform atomic compression & verification
                    await _compressionEngine.CompressItemAsync(item.Id, cancellationToken);

                    // Fetch updated item to get actual post-compression size
                    var updatedItem = await _repository.GetByIdAsync(item.Id, cancellationToken);
                    if (updatedItem != null && updatedItem.CompressionStatus == CompressionStatus.Compressed)
                    {
                        long spaceSaved = updatedItem.OriginalSize - updatedItem.CurrentStoredSize;
                        result.ActualReclaimedBytes += spaceSaved;
                        result.SuccessfulCount++;
                    }
                    else
                    {
                        // Did not compress (e.g. because it was not feasible)
                        result.SuccessfulCount++;
                    }
                }
                catch (Exception)
                {
                    // Failures in compression must never corrupt the safe state
                    result.FailureCount++;
                }
            }

            if (string.IsNullOrEmpty(result.Message))
            {
                result.Message = $"Optimization run completed. Reclaimed {result.ActualReclaimedBytes:N0} bytes.";
            }

            return result;
        }
    }
}
