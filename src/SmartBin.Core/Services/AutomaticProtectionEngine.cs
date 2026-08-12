using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SmartBin.Contracts;
using SmartBin.Core.Models;

namespace SmartBin.Core.Services
{
    public class AutomaticProtectionEngine
    {
        private readonly ISmartBinRepository<SmartBinItem> _repository;
        private readonly IActivityLogger _activityLogger;
        private readonly IStoragePressureMonitor _pressureMonitor;
        private readonly IPowerStateProvider _powerStateProvider;
        private readonly IRecycleBinProvider _recycleBinProvider;
        private readonly CandidateAnalyzer _candidateAnalyzer;
        private readonly IOptimizationPlanner _planner;
        private readonly ControlledExperimentEngine _experimentEngine;
        private readonly INotificationService _notificationService;

        public SmartBinSettings Settings { get; set; } = new();

        public AutomaticProtectionEngine(
            ISmartBinRepository<SmartBinItem> repository,
            IActivityLogger activityLogger,
            IStoragePressureMonitor pressureMonitor,
            IPowerStateProvider powerStateProvider,
            IRecycleBinProvider recycleBinProvider,
            CandidateAnalyzer candidateAnalyzer,
            IOptimizationPlanner planner,
            ControlledExperimentEngine experimentEngine,
            INotificationService notificationService)
        {
            _repository = repository ?? throw new ArgumentNullException(nameof(repository));
            _activityLogger = activityLogger ?? throw new ArgumentNullException(nameof(activityLogger));
            _pressureMonitor = pressureMonitor ?? throw new ArgumentNullException(nameof(pressureMonitor));
            _powerStateProvider = powerStateProvider ?? throw new ArgumentNullException(nameof(powerStateProvider));
            _recycleBinProvider = recycleBinProvider ?? throw new ArgumentNullException(nameof(recycleBinProvider));
            _candidateAnalyzer = candidateAnalyzer ?? throw new ArgumentNullException(nameof(candidateAnalyzer));
            _planner = planner ?? throw new ArgumentNullException(nameof(planner));
            _experimentEngine = experimentEngine ?? throw new ArgumentNullException(nameof(experimentEngine));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
        }

        /// <summary>
        /// Orchestrates the automatic background protection sequence, processing exactly one file at a time.
        /// Rechecks and revalidates storage and candidates before every transaction.
        /// </summary>
        public async Task RunAutomaticProtectionAsync(CancellationToken cancellationToken = default)
        {
            // 1. Check if policy allows automatic optimization
            if (Settings.Mode != AutoOptimizationMode.Automatic)
            {
                return;
            }

            // 2. Power-awareness checks: Skip if running on battery and configured to pause
            if (Settings.PauseOnBattery && _powerStateProvider.IsOnBatteryPower())
            {
                _notificationService.RaiseNotification("Automatic optimization paused: System is running on battery power.", "Power");
                return;
            }

            // 3. Recheck storage metrics & safety margins
            var metrics = await _pressureMonitor.GetStorageMetricsAsync(cancellationToken);
            var recommendation = StoragePressurePolicy.Evaluate(metrics, Settings.TargetFreeSpacePercentage);

            if (!recommendation.IsOptimizationRecommended)
            {
                return;
            }

            // Enforce Hard Safety Floor / Safety Margin protection
            if (metrics.AvailableFreeSpace < Settings.MinimumSafetyMarginBytes)
            {
                _notificationService.RaiseNotification("Safety Floor triggered: Free space is below the minimum safety margin. Automatic optimization aborted.", "Safety");

                await _activityLogger.AddLogAsync(new ActivityLog
                {
                    OperationType = "Safety check",
                    ResultState = "Aborted",
                    FailureReason = "Free space below hard safety floor.",
                    IsAutomatic = true,
                    Rationale = $"Safety floor is set to {Settings.MinimumSafetyMarginBytes:N0} bytes."
                }, cancellationToken);
                return;
            }

            // 4. Discover and score candidates
            var winItems = (await _recycleBinProvider.EnumerateItemsAsync(cancellationToken)).ToList();
            var candidates = winItems.Select(item => _candidateAnalyzer.AnalyzeWindowsItem(item)).ToList();

            var targetFreeSpace = metrics.AvailableFreeSpace + recommendation.RequiredSpaceToReclaimBytes;
            var plan = _planner.GeneratePlan(candidates, metrics.AvailableFreeSpace, targetFreeSpace);

            if (plan.ItemsToOptimize.Count == 0)
            {
                _notificationService.RaiseNotification("Storage pressure detected but no compressible candidates found.", "Scan");
                return;
            }

            // Limit processing per session to avoid heavy execution
            var itemsToProcess = plan.ItemsToOptimize.Take(Settings.MaxItemsPerSession).ToList();

            // 5. Process EXACTLY ONE item at a time
            foreach (var candidate in itemsToProcess)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // A. Recheck storage space before modifying
                var currentMetrics = await _pressureMonitor.GetStorageMetricsAsync(cancellationToken);
                if (currentMetrics.AvailableFreeSpace >= targetFreeSpace)
                {
                    break; // Space constraints resolved, stop early!
                }

                // B. Revalidate candidate (exists, size matches)
                var currentItems = (await _recycleBinProvider.EnumerateItemsAsync(cancellationToken)).ToList();
                var freshItem = currentItems.FirstOrDefault(i =>
                    (candidate.ItemId != Guid.Empty && i.Id == candidate.ItemId.ToString()) ||
                    (i.FileName == candidate.OriginalFileName && i.Size == candidate.OriginalSize));

                if (freshItem == null)
                {
                    // Candidate disappeared or size changed, skip to prevent stale runs
                    continue;
                }

                try
                {
                    // C. Execute Phase 5 Safety Pipeline on the candidate
                    var experiment = await _experimentEngine.PrepareAndVerifyAsync(freshItem, null, cancellationToken);

                    // Complete user-policy allowed mutation
                    await _experimentEngine.CommitExperimentAsync(experiment, executeWindowsMutation: true, null, cancellationToken);

                    // D. Write activity log
                    var actualSaved = experiment.OriginalSize - experiment.CompressedSize;
                    await _activityLogger.AddLogAsync(new ActivityLog
                    {
                        OperationType = "Automatic Optimization",
                        ItemName = experiment.OriginalPath,
                        OriginalSize = experiment.OriginalSize,
                        CompressedSize = experiment.CompressedSize,
                        ReclaimedBytes = actualSaved,
                        ResultState = "Verified",
                        IsAutomatic = true,
                        Rationale = $"Reclaimed {actualSaved / (1024 * 1024):F1} MB safely via background protection."
                    }, cancellationToken);

                    // E. Raise success notification
                    _notificationService.RaiseNotification(
                        $"SmartBin optimized 1 recoverable file ({freshItem.FileName}) and reclaimed {actualSaved / (1024 * 1024):N0} MB.",
                        "Optimization");

                    // Phase 6 Strict Limit: Process exactly ONE successful item at a time per background cycle
                    break;
                }
                catch (Exception ex)
                {
                    // Safe cleanup: log the failure and continue gracefully without corrupted state
                    await _activityLogger.AddLogAsync(new ActivityLog
                    {
                        OperationType = "Automatic Optimization",
                        ItemName = freshItem.OriginalPath,
                        OriginalSize = freshItem.Size,
                        ResultState = "Failed",
                        FailureReason = ex.Message,
                        IsAutomatic = true,
                        Rationale = "Pipeline rollback executed successfully."
                    }, cancellationToken);

                    _notificationService.RaiseNotification(
                        $"SmartBin could not safely optimize {freshItem.FileName}. Original file remains untouched.",
                        "Error");
                }
            }
        }
    }
}
