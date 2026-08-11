using System;
using System.Collections.Generic;
using System.Linq;
using SmartBin.Contracts;

namespace SmartBin.Core.Services
{
    public class OptimizationPlanner : IOptimizationPlanner
    {
        public OptimizationPlan GeneratePlan(List<CandidateItem> candidates, long currentFreeSpace, long targetFreeSpace)
        {
            if (candidates == null) throw new ArgumentNullException(nameof(candidates));

            long requiredSpaceToReclaim = targetFreeSpace - currentFreeSpace;

            var plan = new OptimizationPlan
            {
                RequiredSpaceToReclaim = Math.Max(0, requiredSpaceToReclaim),
                ItemsToOptimize = new List<CandidateItem>(),
                ExpectedReclaimedBytes = 0
            };

            // If target free space is already satisfied, no planning is needed.
            if (requiredSpaceToReclaim <= 0)
            {
                return plan;
            }

            // Filter for eligible items and order by priority score (highest score first)
            var eligibleCandidates = candidates
                .Where(c => c.IsEligibleForOptimization)
                .OrderByDescending(c => c.PriorityScore)
                .ToList();

            long accumulatedSavings = 0;

            foreach (var candidate in eligibleCandidates)
            {
                plan.ItemsToOptimize.Add(candidate);
                accumulatedSavings += (long)candidate.EstimatedSavingsBytes;

                if (accumulatedSavings >= requiredSpaceToReclaim)
                {
                    break; // Target satisfied
                }
            }

            plan.ExpectedReclaimedBytes = accumulatedSavings;
            return plan;
        }
    }
}
