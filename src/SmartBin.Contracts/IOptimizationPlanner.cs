using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace SmartBin.Contracts
{
    public class OptimizationPlan
    {
        public List<CandidateItem> ItemsToOptimize { get; set; } = new();
        public long RequiredSpaceToReclaim { get; set; }
        public long ExpectedReclaimedBytes { get; set; }
    }

    public interface IOptimizationPlanner
    {
        /// <summary>
        /// Generates an optimization plan based on current free space, target free space, and available candidate items.
        /// </summary>
        OptimizationPlan GeneratePlan(List<CandidateItem> candidates, long currentFreeSpace, long targetFreeSpace);
    }
}
