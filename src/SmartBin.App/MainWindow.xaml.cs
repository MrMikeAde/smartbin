using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.EntityFrameworkCore;
using SmartBin.Contracts;
using SmartBin.Core.Models;
using SmartBin.Core.Services;
using SmartBin.Infrastructure.Compression;
using SmartBin.Infrastructure.Hashing;
using SmartBin.Infrastructure.Persistence;
using SmartBin.Infrastructure.Storage;
using SmartBin.Infrastructure.Services;

namespace SmartBin.App
{
    /// <summary>
    /// Interactive Production Dashboard supporting Importing, Listing, Compression, Restoring, and Adaptive Storage Intelligence.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private SmartBinDbContext? _dbContext;
        private EfSmartBinRepository? _repository;
        private ActivityRepository? _activityRepository;
        private Sha256FileHasher? _fileHasher;
        private DefaultStoragePathProvider? _pathProvider;
        private StorageManager? _storageManager;
        private ZipCompressionService? _compressionService;
        private ImportService? _importService;
        private CompressionEngine? _compressionEngine;
        private RestoreService? _restoreService;
        private StoragePressureMonitor? _pressureMonitor;
        private StoragePressureSimulator? _simulator;
        private CandidateAnalyzer? _candidateAnalyzer;
        private OptimizationPlanner? _planner;
        private OptimizationExecutor? _executor;

        // Phase 4 providers
        private IRecycleBinProvider? _realWinProvider;
        private SimulatedRecycleBinProvider? _simWinProvider;

        // Phase 5 services
        private WindowsRecycleBinMutationService? _mutationService;
        private ControlledExperimentEngine? _experimentEngine;
        private WindowsRecycleBinItem? _selectedWinItem;
        private ControlledExperimentItem? _currentExperiment;

        // Phase 6 background services
        private WindowsPowerStateProvider? _powerStateProvider;
        private StorageMonitor? _storageMonitor;
        private NotificationService? _notificationService;
        private AutomaticProtectionEngine? _autoEngine;

        public MainWindow()
        {
            this.InitializeComponent();
            InitializeBackend();
        }

        private void InitializeBackend()
        {
            try
            {
                // AppData folder path for local SmartBin data
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var storageRoot = Path.Combine(appData, "SmartBinStorage");
                Directory.CreateDirectory(storageRoot);

                var options = new DbContextOptionsBuilder<SmartBinDbContext>()
                    .UseSqlite($"Data Source={Path.Combine(storageRoot, "smartbin.db")}")
                    .Options;

                _dbContext = new SmartBinDbContext(options);
                _dbContext.Database.EnsureCreated();

                _repository = new EfSmartBinRepository(_dbContext);
                _activityRepository = new ActivityRepository(_dbContext);
                _fileHasher = new Sha256FileHasher();
                _pathProvider = new DefaultStoragePathProvider(storageRoot);
                _storageManager = new StorageManager(_pathProvider);
                _compressionService = new ZipCompressionService();

                _importService = new ImportService(_repository, _fileHasher, _storageManager);
                _compressionEngine = new CompressionEngine(_repository, _compressionService, _fileHasher, _storageManager);
                _restoreService = new RestoreService(_repository, _compressionService, _fileHasher, _storageManager);

                _pressureMonitor = new StoragePressureMonitor(_pathProvider);
                _simulator = new StoragePressureSimulator(_pressureMonitor);
                _candidateAnalyzer = new CandidateAnalyzer(_repository);
                _planner = new OptimizationPlanner();
                _executor = new OptimizationExecutor(_repository, _pressureMonitor, _compressionEngine);

                // Phase 4 providers
                _realWinProvider = new WindowsRecycleBinProvider();
                _simWinProvider = new SimulatedRecycleBinProvider();

                // Phase 5 mutation and engine
                _mutationService = new WindowsRecycleBinMutationService(_pathProvider);
                _experimentEngine = new ControlledExperimentEngine(
                    _repository,
                    _mutationService,
                    _compressionService,
                    _fileHasher,
                    _storageManager);

                // Phase 6 background protection
                _powerStateProvider = new WindowsPowerStateProvider();
                _notificationService = new NotificationService();
                _autoEngine = new AutomaticProtectionEngine(
                    _repository,
                    _activityRepository,
                    _pressureMonitor,
                    _powerStateProvider,
                    _simWinProvider, // Default to simulated for read-write safety
                    _candidateAnalyzer,
                    _planner,
                    _experimentEngine,
                    _notificationService);

                // Wire notification logger
                _notificationService.NotificationRaised += (msg, type) =>
                {
                    DispatcherQueue.TryEnqueue(() => LogToTerminal($"[NOTIFICATION - {type.ToUpperInvariant()}] {msg}"));
                };

                // Startup recovery check
                var recovery = new CrashRecoveryService(_storageManager);
                int sweptCount = recovery.PerformStartupRecoveryAndCleanup();
                if (sweptCount > 0)
                {
                    LogToTerminal($"[Crash Recovery] Swept {sweptCount} intermediate residual files.");
                }

                // Storage monitor loop
                _storageMonitor = new StorageMonitor(_pressureMonitor);
                _storageMonitor.PressureStateChanged += async (metrics) =>
                {
                    // Run automatic rules sequentially when metrics state changes!
                    if (_autoEngine != null)
                    {
                        await _autoEngine.RunAutomaticProtectionAsync();
                        DispatcherQueue.TryEnqueue(RefreshUI);
                    }
                };
                _storageMonitor.StartMonitoring(TimeSpan.FromSeconds(60)); // Standard 60 sec polling

                RefreshUI();
                RefreshWinRecycleBinUI();

                LogToTerminal("Adaptive Storage Dashboard Loaded.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Initialization Error: {ex.Message}");
            }
        }

        private async void RefreshUI()
        {
            if (_repository == null || _pressureMonitor == null || _candidateAnalyzer == null || _activityRepository == null) return;

            try
            {
                var items = (await _repository.GetAllAsync()).ToList();
                ItemsListView.ItemsSource = items;

                // Handle Empty States
                if (ItemsListViewEmptyState != null)
                {
                    ItemsListViewEmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                ItemsListView.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

                long totalOriginalSize = items.Sum(i => i.OriginalSize);
                long totalStoredSize = items.Sum(i => i.CurrentStoredSize);
                long totalSpaceSaved = totalOriginalSize - totalStoredSize;

                if (OriginalSizeText != null) OriginalSizeText.Text = $"{totalOriginalSize:N0} bytes";
                if (StoredSizeText != null) StoredSizeText.Text = $"{totalStoredSize:N0} bytes";
                if (SpaceSavedText != null) SpaceSavedText.Text = $"{totalSpaceSaved:N0} bytes";

                if (FilesProtectedText != null) FilesProtectedText.Text = items.Count.ToString();
                if (CompressedText != null) CompressedText.Text = items.Count(i => i.CompressionStatus == CompressionStatus.Compressed).ToString();
                if (OptimizedText != null) OptimizedText.Text = items.Count(i => i.CompressionStatus == CompressionStatus.NotFeasible).ToString();

                // Compute potential additional recovery
                var candidates = await _candidateAnalyzer.AnalyzeCandidatesAsync();
                long potentialSaved = (long)candidates.Sum(c => c.IsEligibleForOptimization ? c.EstimatedSavingsBytes : 0);
                if (PotentialSavedText != null) PotentialSavedText.Text = $"{potentialSaved:N0} bytes";

                // Update Storage Utilization Visualization
                var metrics = await _pressureMonitor.GetStorageMetricsAsync();
                double usedPercentage = 100.0 - metrics.FreeSpacePercentage;

                if (StorageProgress != null) StorageProgress.Value = Math.Clamp(usedPercentage, 0, 100);

                var isSim = _pressureMonitor.MockMetricsOverride != null ? " (SIMULATION)" : "";
                if (StorageStatusText != null) StorageStatusText.Text = $"{usedPercentage:F1}% Space Used ({metrics.PressureState}{isSim})";
                if (StoragePressureLabel != null) StoragePressureLabel.Text = $"System State: {metrics.PressureState.ToString().ToUpperInvariant()}";

                if (TotalCapacityText != null) TotalCapacityText.Text = $"{metrics.TotalCapacity / (1024.0 * 1024 * 1024):F1} GB";
                if (UsedStorageText != null) UsedStorageText.Text = $"{metrics.UsedSpace / (1024.0 * 1024 * 1024):F1} GB";
                if (AvailableStorageText != null) AvailableStorageText.Text = $"{metrics.AvailableFreeSpace / (1024.0 * 1024 * 1024):F1} GB ({metrics.FreeSpacePercentage:F1}% free)";

                if (_autoEngine != null)
                {
                    if (SafetyFloorText != null) SafetyFloorText.Text = $"{_autoEngine.Settings.MinimumSafetyMarginBytes / (1024.0 * 1024 * 1024):F1} GB";
                    if (TargetLevelText != null) TargetLevelText.Text = $"{_autoEngine.Settings.TargetFreeSpacePercentage:F1}%";
                    if (AutoProtectStatusText != null) AutoProtectStatusText.Text = _autoEngine.Settings.Mode.ToString().ToUpperInvariant();
                }

                // Populate Activity logs list
                var logs = await _activityRepository.GetLogsAsync();
                ActivityListView.ItemsSource = logs;

                if (ActivityListViewEmptyState != null)
                {
                    ActivityListViewEmptyState.Visibility = logs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                ActivityListView.Visibility = logs.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
            }
            catch (Exception ex)
            {
                LogToTerminal($"UI Refresh Error: {ex.Message}");
            }
        }

        private async void RefreshWinRecycleBinUI()
        {
            // Favour simulation provider when simulation toggle switch is checked
            IRecycleBinProvider? provider = (SimToggle != null && SimToggle.IsOn) ? _simWinProvider : _realWinProvider;
            if (provider == null || WinItemsListView == null) return;

            try
            {
                var items = (await provider.EnumerateItemsAsync()).ToList();
                WinItemsListView.ItemsSource = items;

                if (WinItemsListViewEmptyState != null)
                {
                    WinItemsListViewEmptyState.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                }
                WinItemsListView.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

                var stats = await provider.GetStatisticsAsync();
                if (WinStatsText != null) WinStatsText.Text = $"{stats.TotalItems} items ({stats.TotalSize / (1024 * 1024):N0} MB)";
            }
            catch (Exception ex)
            {
                LogToTerminal($"Recycle Bin Refresh Error: {ex.Message}");
            }
        }

        private void LogToTerminal(string msg)
        {
            if (TerminalText != null)
            {
                TerminalText.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + TerminalText.Text;
            }
        }

        private async void OnImportFileClicked(object sender, RoutedEventArgs e)
        {
            if (_importService == null) return;

            try
            {
                var picker = new Windows.Storage.Pickers.FileOpenPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.List;
                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                picker.FileTypeFilter.Add("*");

                var file = await picker.PickSingleFileAsync();
                if (file != null)
                {
                    LogToTerminal($"Importing file: {file.Path}...");
                    var item = await _importService.ImportFileAsync(file.Path);
                    LogToTerminal($"✓ Successfully imported: {item.OriginalFileName}");
                    RefreshUI();
                }
            }
            catch (Exception ex)
            {
                LogToTerminal($"Import Failed: {ex.Message}");
            }
        }

        private void OnSimulationToggled(object sender, RoutedEventArgs e)
        {
            if (_simulator == null || SimStateCombo == null) return;

            try
            {
                if (SimToggle.IsOn)
                {
                    var selectedIndex = SimStateCombo.SelectedIndex;
                    var state = selectedIndex switch
                    {
                        1 => StoragePressureState.Low,
                        2 => StoragePressureState.Critical,
                        _ => StoragePressureState.Normal
                    };

                    _simulator.EnableSimulation(state);
                    LogToTerminal($"Simulation Enabled: State set to {state}");
                }
                else
                {
                    _simulator.DisableSimulation();
                    LogToTerminal("Simulation Disabled. Using actual physical drive metrics.");
                }

                RefreshUI();
                RefreshWinRecycleBinUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Simulation Toggle Error: {ex.Message}");
            }
        }

        private async void OnGenerateDemoClicked(object sender, RoutedEventArgs e)
        {
            try
            {
                string demoType = "10mb_compressible";
                if (DemoFileTypeCombo != null)
                {
                    demoType = DemoFileTypeCombo.SelectedIndex switch
                    {
                        1 => "100mb_compressible",
                        2 => "500mb_mixed",
                        3 => "1gb_incompressible",
                        _ => "10mb_compressible"
                    };
                }

                var tempFolder = Path.Combine(Path.GetTempPath(), "SmartBinDemo");
                Directory.CreateDirectory(tempFolder);
                var filePath = Path.Combine(tempFolder, $"demo_test_{Guid.NewGuid().ToString("N").Substring(0, 6)}.txt");

                LogToTerminal($"Generating programmatic test file ({demoType})...");
                await TestFileGenerator.GenerateTestFileAsync(filePath, demoType);

                var fileInfo = new FileInfo(filePath);
                LogToTerminal($"✓ Generated demo test file ({fileInfo.Length:N0} bytes) at: {filePath}");

                // Register with simulated Recycle Bin for read-write safe testing
                if (_simWinProvider != null)
                {
                    var simItem = new WindowsRecycleBinItem
                    {
                        Id = "sim_gen_" + Guid.NewGuid().ToString("N").Substring(0, 8),
                        FileName = fileInfo.Name,
                        OriginalPath = filePath,
                        Size = fileInfo.Length,
                        DeletedTimestamp = DateTime.UtcNow,
                        Volume = "C:",
                        IsSimulated = true
                    };
                    _simWinProvider.AddItem(simItem);
                    LogToTerminal($"✓ Test item automatically made available in Recycle Bin tab.");
                }

                RefreshWinRecycleBinUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Generate Demo Failed: {ex.Message}");
            }
        }

        private async void OnAnalyzeStorageClicked(object sender, RoutedEventArgs e)
        {
            if (_candidateAnalyzer == null || _pressureMonitor == null) return;

            try
            {
                LogToTerminal("Analyzing drive storage pressure...");
                var metrics = await _pressureMonitor.GetStorageMetricsAsync();
                var recommendation = StoragePressurePolicy.Evaluate(metrics);

                LogToTerminal($"Pressure State: {metrics.PressureState}");
                LogToTerminal($"Recommendation: {recommendation.Rationale}");

                if (recommendation.IsOptimizationRecommended)
                {
                    LogToTerminal($"Required Space to Reclaim: {recommendation.RequiredSpaceToReclaimBytes:N0} bytes");
                }

                var candidates = await _candidateAnalyzer.AnalyzeCandidatesAsync();
                var eligibleCount = candidates.Count(c => c.IsEligibleForOptimization);
                LogToTerminal($"Found {candidates.Count} total items, with {eligibleCount} eligible for compression.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Analyze Failed: {ex.Message}");
            }
        }

        private async void OnOptimizeNowClicked(object sender, RoutedEventArgs e)
        {
            if (_candidateAnalyzer == null || _pressureMonitor == null || _planner == null || _executor == null) return;

            try
            {
                var metrics = await _pressureMonitor.GetStorageMetricsAsync();
                var recommendation = StoragePressurePolicy.Evaluate(metrics);

                if (!recommendation.IsOptimizationRecommended)
                {
                    LogToTerminal("Storage pressure is Normal. Automatic batch optimization is skipped.");
                    return;
                }

                LogToTerminal("Running storage optimization planner...");
                var candidates = await _candidateAnalyzer.AnalyzeCandidatesAsync();
                var targetSpace = metrics.AvailableFreeSpace + recommendation.RequiredSpaceToReclaimBytes;

                var plan = _planner.GeneratePlan(candidates, metrics.AvailableFreeSpace, targetSpace);

                if (plan.ItemsToOptimize.Count == 0)
                {
                    LogToTerminal("No uncompressed candidates found to satisfy target free space.");
                    return;
                }

                LogToTerminal($"Planner selected {plan.ItemsToOptimize.Count} candidate(s). Expected savings: {plan.ExpectedReclaimedBytes:N0} bytes.");
                LogToTerminal("Executing plan...");

                var result = await _executor.ExecutePlanAsync(plan, targetSpace);
                LogToTerminal($"Execution Result: {result.Message}");
                LogToTerminal($"Actual storage space reclaimed: {result.ActualReclaimedBytes:N0} bytes.");

                RefreshUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Optimization Failed: {ex.Message}");
            }
        }

        private async void OnRestoreSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (_restoreService == null || _fileHasher == null || ItemsListView.SelectedItem is not SmartBinItem selectedItem)
            {
                LogToTerminal("Please select an item from the list to restore.");
                return;
            }

            try
            {
                var picker = new Windows.Storage.Pickers.FolderPicker();
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);

                picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.Downloads;
                picker.FileTypeFilter.Add("*");

                var folder = await picker.PickSingleFolderAsync();
                if (folder != null)
                {
                    var destinationPath = Path.Combine(folder.Path, selectedItem.OriginalFileName);

                    LogToTerminal("Restoring...");
                    LogToTerminal("Verifying integrity...");

                    await _restoreService.RestoreAsync(selectedItem.Id, destinationPath);

                    LogToTerminal($"✓ Restored successfully: {selectedItem.OriginalFileName}");
                    var hash = await _fileHasher.ComputeHashAsync(destinationPath);
                    LogToTerminal($"SHA-256 verified: {hash}");
                    RefreshUI();
                }
            }
            catch (SmartBinConflictException confEx)
            {
                LogToTerminal($"Restore Conflict: {confEx.Message}");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Restore Failed: {ex.Message}");
            }
        }

        private void OnItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_candidateAnalyzer == null || ItemsListView.SelectedItem is not SmartBinItem selectedItem) return;

            try
            {
                // Showcase explainability rationale in the terminal box on list selection
                var candidate = _candidateAnalyzer.AnalyzeItem(selectedItem);
                LogToTerminal($"--- Explainability: {candidate.OriginalFileName} ---");
                LogToTerminal($"Priority Score: {candidate.PriorityScore:F1}");
                LogToTerminal(candidate.PriorityExplaination);
                LogToTerminal("--------------------------------------");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Selection Error: {ex.Message}");
            }
        }

        private void OnWinItemsSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_candidateAnalyzer == null || WinItemsListView.SelectedItem is not WindowsRecycleBinItem selectedItem) return;

            try
            {
                _selectedWinItem = selectedItem;

                // Update Phase 5 tab details dynamically
                if (ExpFileNameText != null) ExpFileNameText.Text = selectedItem.FileName;
                if (ExpOrigPathText != null) ExpOrigPathText.Text = $"Original Path: {selectedItem.OriginalPath}";
                if (ExpSizeText != null) ExpSizeText.Text = $"Original Size: {selectedItem.Size:N0} bytes";

                ResetChecklist();

                // Retrieve read-only intelligence scoring and explainability for Windows item
                var candidate = _candidateAnalyzer.AnalyzeWindowsItem(selectedItem);
                LogToTerminal($"--- READ-ONLY ANALYSIS: {candidate.OriginalFileName} ---");
                LogToTerminal($"Original path: {selectedItem.OriginalPath}");
                LogToTerminal($"Size:          {selectedItem.Size:N0} bytes");
                LogToTerminal($"Volume:        {selectedItem.Volume}");
                LogToTerminal($"Priority Score: {candidate.PriorityScore:F1}");
                LogToTerminal("Why?");
                LogToTerminal(candidate.PriorityExplaination);
                LogToTerminal("--------------------------------------");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Selection Error: {ex.Message}");
            }
        }

        private void OnRefreshWinClicked(object sender, RoutedEventArgs e)
        {
            LogToTerminal("Querying Windows Recycle Bin...");
            RefreshWinRecycleBinUI();
            LogToTerminal("Windows Recycle Bin Item list refreshed successfully.");
        }

        private void ResetChecklist()
        {
            if (Check1 != null) { Check1.Text = "PENDING"; Check1.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }
            if (Check2 != null) { Check2.Text = "PENDING"; Check2.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }
            if (Check3 != null) { Check3.Text = "PENDING"; Check3.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }
            if (Check4 != null) { Check4.Text = "PENDING"; Check4.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }
            if (Check5 != null) { Check5.Text = "PENDING"; Check5.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }
            if (Check6 != null) { Check6.Text = "PENDING"; Check6.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128)); }

            if (CommitStatusText != null)
            {
                CommitStatusText.Text = "Status: WAITING FOR PIPELINE CHECKS";
                CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 139));
            }

            if (CommitExpBtn != null) CommitExpBtn.IsEnabled = false;
            if (KeepCopyBtn != null) KeepCopyBtn.IsEnabled = false;
        }

        private async void OnBeginControlledTestClicked(object sender, RoutedEventArgs e)
        {
            if (_experimentEngine == null || _selectedWinItem == null)
            {
                LogToTerminal("Please select exactly ONE Windows Recycle Bin item to run the experiment.");
                return;
            }

            ResetChecklist();
            LogToTerminal($"--> Starting Controlled Experiment on {_selectedWinItem.FileName}...");

            try
            {
                _currentExperiment = await _experimentEngine.PrepareAndVerifyAsync(_selectedWinItem, state =>
                {
                    // Update checklist indicators dynamically in response to state transitions
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        switch (state)
                        {
                            case ExperimentState.Acquired:
                                if (Check1 != null) { Check1.Text = "✓ PASSED"; Check1.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                break;
                            case ExperimentState.AcquisitionVerified:
                                if (Check2 != null) { Check2.Text = "✓ PASSED"; Check2.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                if (Check3 != null) { Check3.Text = "✓ PASSED"; Check3.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                break;
                            case ExperimentState.Compressed:
                                if (Check4 != null) { Check4.Text = "✓ PASSED"; Check4.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                break;
                            case ExperimentState.CompressionVerified:
                                if (Check5 != null) { Check5.Text = "✓ PASSED"; Check5.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                break;
                            case ExperimentState.RestorationVerified:
                                if (Check6 != null) { Check6.Text = "✓ PASSED"; Check6.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0)); }
                                break;
                        }
                    });
                });

                if (CommitStatusText != null)
                {
                    CommitStatusText.Text = "Status: READY FOR COMMIT";
                    CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                }

                if (CommitExpBtn != null) CommitExpBtn.IsEnabled = true;
                if (KeepCopyBtn != null) KeepCopyBtn.IsEnabled = true;

                LogToTerminal($"✓ Pipeline completed successfully. Item is verified and ready for commit.");
                LogToTerminal($"Original: {_currentExperiment.OriginalSize:N0} bytes -> Compressed: {_currentExperiment.CompressedSize:N0} bytes.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"❌ Experiment Pipeline Failed: {ex.Message}");
                LogToTerminal("Rollback executed. Original Recycle Bin item remains completely untouched.");
                if (CommitStatusText != null)
                {
                    CommitStatusText.Text = "Status: PIPELINE FAILED / ROLLBACK COMPLETED";
                    CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 0, 0));
                }
            }
        }

        private async void OnCommitExperimentClicked(object sender, RoutedEventArgs e)
        {
            if (_experimentEngine == null || _currentExperiment == null) return;

            try
            {
                bool mutationSelected = MutationCheck != null && MutationCheck.IsOn;
                LogToTerminal("Committing experiment...");

                await _experimentEngine.CommitExperimentAsync(_currentExperiment, mutationSelected, state =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (state == ExperimentState.Committed && CommitStatusText != null)
                        {
                            CommitStatusText.Text = "Status: COMMITTED SUCCESSFULLY";
                            CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                            if (CommitExpBtn != null) CommitExpBtn.IsEnabled = false;
                            if (KeepCopyBtn != null) KeepCopyBtn.IsEnabled = false;
                        }
                    });
                });

                LogToTerminal("✓ Controlled Experiment Committed successfully.");
                if (mutationSelected)
                {
                    LogToTerminal("Original Windows Recycle Bin entry permanently removed.");
                }
                else
                {
                    LogToTerminal("Original Recycle Bin item remains completely untouched.");
                }

                RefreshUI();
                RefreshWinRecycleBinUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Commit Failed: {ex.Message}");
            }
        }

        private async void OnKeepCopyClicked(object sender, RoutedEventArgs e)
        {
            if (_experimentEngine == null || _currentExperiment == null) return;

            try
            {
                LogToTerminal("Committing experiment and keeping original untouched...");

                await _experimentEngine.CommitExperimentAsync(_currentExperiment, executeWindowsMutation: false, state =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (state == ExperimentState.Committed && CommitStatusText != null)
                        {
                            CommitStatusText.Text = "Status: COMMITTED SUCCESSFULLY (NO MUTATION)";
                            CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                            if (CommitExpBtn != null) CommitExpBtn.IsEnabled = false;
                            if (KeepCopyBtn != null) KeepCopyBtn.IsEnabled = false;
                        }
                    });
                });

                LogToTerminal("✓ Verified copy preserved inside SmartBin Storage. Original Windows Recycle Bin item remains untouched.");
                RefreshUI();
                RefreshWinRecycleBinUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Commit Failed: {ex.Message}");
            }
        }

        private void OnSettingsChanged(object sender, RoutedEventArgs e)
        {
            if (_autoEngine == null || LowThresholdInput == null || CritThresholdInput == null || TargetPercentInput == null || SafetyFloorInput == null) return;

            try
            {
                bool isValid = true;
                string errorMessage = "";

                // Parse and validate numeric settings inputs with immediate explainability
                if (!double.TryParse(LowThresholdInput.Text, out var low) || low <= 0 || low >= 100)
                {
                    isValid = false;
                    errorMessage = "Low Pressure Threshold must be a percentage between 1% and 99%.";
                }
                else if (!double.TryParse(CritThresholdInput.Text, out var crit) || crit <= 0 || crit >= low)
                {
                    isValid = false;
                    errorMessage = $"Critical Pressure Threshold must be between 1% and strictly less than Low Threshold ({low}%).";
                }
                else if (!double.TryParse(TargetPercentInput.Text, out var target) || target <= low || target >= 100)
                {
                    isValid = false;
                    errorMessage = $"Target Free-Space Percentage must be strictly greater than Low Threshold ({low}%).";
                }
                else if (!long.TryParse(SafetyFloorInput.Text, out var floorGb) || floorGb < 1 || floorGb > 1000)
                {
                    isValid = false;
                    errorMessage = "Hard Safety Floor must be a positive integer between 1 GB and 1000 GB.";
                }
                else if (MaxItemsInput != null && (!int.TryParse(MaxItemsInput.Text, out var maxItems) || maxItems < 1 || maxItems > 100))
                {
                    isValid = false;
                    errorMessage = "Max items per session must be between 1 and 100.";
                }

                if (!isValid)
                {
                    if (SettingsValidationBorder != null) SettingsValidationBorder.Visibility = Visibility.Visible;
                    if (SettingsValidationErrorText != null) SettingsValidationErrorText.Text = errorMessage;

                    // Automatically default mode to OFF if configuration is untrusted
                    _autoEngine.Settings.Mode = AutoOptimizationMode.Off;
                    if (ModeOffRadio != null) ModeOffRadio.IsChecked = true;
                    return;
                }

                if (SettingsValidationBorder != null) SettingsValidationBorder.Visibility = Visibility.Collapsed;

                // Apply verified valid settings
                _autoEngine.Settings.LowPressureThresholdPercentage = double.Parse(LowThresholdInput.Text);
                _autoEngine.Settings.CriticalPressureThresholdPercentage = double.Parse(CritThresholdInput.Text);
                _autoEngine.Settings.TargetFreeSpacePercentage = double.Parse(TargetPercentInput.Text);
                _autoEngine.Settings.MinimumSafetyMarginBytes = long.Parse(SafetyFloorInput.Text) * 1024L * 1024 * 1024;

                if (BatteryPauseToggle != null) _autoEngine.Settings.PauseOnBattery = BatteryPauseToggle.IsOn;
                if (MaxItemsInput != null) _autoEngine.Settings.MaxItemsPerSession = int.Parse(MaxItemsInput.Text);

                if (ModeOffRadio != null && ModeOffRadio.IsChecked == true) _autoEngine.Settings.Mode = AutoOptimizationMode.Off;
                else if (ModeNotifyRadio != null && ModeNotifyRadio.IsChecked == true) _autoEngine.Settings.Mode = AutoOptimizationMode.NotifyMe;
                else if (ModeAutoRadio != null && ModeAutoRadio.IsChecked == true) _autoEngine.Settings.Mode = AutoOptimizationMode.Automatic;

                LogToTerminal("✓ Settings updated and validated successfully.");
                RefreshUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Settings Validation Error: {ex.Message}");
            }
        }

        private void OnSettingsChanged(object sender, TextChangedEventArgs e)
        {
            OnSettingsChanged(sender, (RoutedEventArgs)null!);
        }
    }
}
