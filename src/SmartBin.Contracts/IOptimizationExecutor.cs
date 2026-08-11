using System;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public class ExecutionResult
    {
        public int TotalProcessedCount { get; set; }
        public int SuccessfulCount { get; set; }
        public int FailureCount { get; set; }
        public long ActualReclaimedBytes { get; set; }
        public bool Interrupted { get; set; }
        public string Message { get; set; } = string.Empty;
    }

    public interface IOptimizationExecutor
    {
        /// <summary>
        /// Orchestrates the execution of an optimization plan, verifying space and revalidating candidates continuously.
        /// </summary>
        Task<ExecutionResult> ExecutePlanAsync(OptimizationPlan plan, long targetFreeSpace, CancellationToken cancellationToken = default);
    }
}
