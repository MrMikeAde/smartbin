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
    /// Interactive Dashboard supporting Importing, Listing, Compression, Restoring, and Adaptive Storage Intelligence.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private SmartBinDbContext? _dbContext;
        private EfSmartBinRepository? _repository;
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
        private IRecycleBinProvider? _simWinProvider;

        // Phase 5 services
        private WindowsRecycleBinMutationService? _mutationService;
        private ControlledExperimentEngine? _experimentEngine;
        private WindowsRecycleBinItem? _selectedWinItem;
        private ControlledExperimentItem? _currentExperiment;

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
            if (_repository == null || _pressureMonitor == null || _candidateAnalyzer == null) return;

            try
            {
                var items = (await _repository.GetAllAsync()).ToList();
                ItemsListView.ItemsSource = items;

                long totalOriginalSize = items.Sum(i => i.OriginalSize);
                long totalStoredSize = items.Sum(i => i.CurrentStoredSize);
                long totalSpaceSaved = totalOriginalSize - totalStoredSize;

                OriginalSizeText.Text = $"{totalOriginalSize:N0} bytes";
                StoredSizeText.Text = $"{totalStoredSize:N0} bytes";
                SpaceSavedText.Text = $"{totalSpaceSaved:N0} bytes";

                FilesProtectedText.Text = items.Count.ToString();
                CompressedText.Text = items.Count(i => i.CompressionStatus == CompressionStatus.Compressed).ToString();
                OptimizedText.Text = items.Count(i => i.CompressionStatus == CompressionStatus.NotFeasible).ToString();

                // Compute potential additional recovery
                var candidates = await _candidateAnalyzer.AnalyzeCandidatesAsync();
                long potentialSaved = (long)candidates.Sum(c => c.IsEligibleForOptimization ? c.EstimatedSavingsBytes : 0);
                PotentialSavedText.Text = $"{potentialSaved:N0} bytes";

                // Update Storage Utilization Visualization
                var metrics = await _pressureMonitor.GetStorageMetricsAsync();
                StorageProgress.Value = Math.Clamp(metrics.FreeSpacePercentage, 0, 100);

                var isSim = _pressureMonitor.MockMetricsOverride != null ? " (SIMULATION)" : "";
                StorageStatusText.Text = $"{metrics.FreeSpacePercentage:F1}% Free Space ({metrics.PressureState}{isSim})";
            }
            catch (Exception ex)
            {
                LogToTerminal($"UI Refresh Error: {ex.Message}");
            }
        }

        private async void RefreshWinRecycleBinUI()
        {
            // Favour simulation provider when simulation toggle switch is checked
            var provider = (SimToggle != null && SimToggle.IsOn) ? _simWinProvider : _realWinProvider;
            if (provider == null || WinItemsListView == null) return;

            try
            {
                var items = (await provider.EnumerateItemsAsync()).ToList();
                WinItemsListView.ItemsSource = items;

                var stats = await provider.GetStatisticsAsync();
                WinStatsText.Text = $"{stats.TotalItems} items ({stats.TotalSize / (1024 * 1024):N0} MB)";
            }
            catch (Exception ex)
            {
                LogToTerminal($"Recycle Bin Refresh Error: {ex.Message}");
            }
        }

        private void LogToTerminal(string msg)
        {
            TerminalText.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}\n" + TerminalText.Text;
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
                ExpFileNameText.Text = selectedItem.FileName;
                ExpOrigPathText.Text = $"Original Path: {selectedItem.OriginalPath}";
                ExpSizeText.Text = $"Original Size: {selectedItem.Size:N0} bytes";

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
            Check1.Text = "PENDING"; Check1.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            Check2.Text = "PENDING"; Check2.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            Check3.Text = "PENDING"; Check3.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            Check4.Text = "PENDING"; Check4.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            Check5.Text = "PENDING"; Check5.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));
            Check6.Text = "PENDING"; Check6.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 128, 128, 128));

            CommitStatusText.Text = "Status: WAITING FOR PIPELINE CHECKS";
            CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 0, 139));

            CommitExpBtn.IsEnabled = false;
            KeepCopyBtn.IsEnabled = false;
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
                                Check1.Text = "✓ PASSED"; Check1.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                break;
                            case ExperimentState.AcquisitionVerified:
                                Check2.Text = "✓ PASSED"; Check2.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                Check3.Text = "✓ PASSED"; Check3.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                break;
                            case ExperimentState.Compressed:
                                Check4.Text = "✓ PASSED"; Check4.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                break;
                            case ExperimentState.CompressionVerified:
                                Check5.Text = "✓ PASSED"; Check5.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                break;
                            case ExperimentState.RestorationVerified:
                                Check6.Text = "✓ PASSED"; Check6.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                                break;
                        }
                    });
                });

                CommitStatusText.Text = "Status: READY FOR COMMIT";
                CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));

                CommitExpBtn.IsEnabled = true;
                KeepCopyBtn.IsEnabled = true;

                LogToTerminal($"✓ Pipeline completed successfully. Item is verified and ready for commit.");
                LogToTerminal($"Original: {_currentExperiment.OriginalSize:N0} bytes -> Compressed: {_currentExperiment.CompressedSize:N0} bytes.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"❌ Experiment Pipeline Failed: {ex.Message}");
                LogToTerminal("Rollback executed. Original Recycle Bin item remains completely untouched.");
                CommitStatusText.Text = "Status: PIPELINE FAILED / ROLLBACK COMPLETED";
                CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 139, 0, 0));
            }
        }

        private async void OnCommitExperimentClicked(object sender, RoutedEventArgs e)
        {
            if (_experimentEngine == null || _currentExperiment == null) return;

            try
            {
                bool mutationSelected = MutationCheck.IsOn;
                LogToTerminal("Committing experiment...");

                await _experimentEngine.CommitExperimentAsync(_currentExperiment, mutationSelected, state =>
                {
                    DispatcherQueue.TryEnqueue(() =>
                    {
                        if (state == ExperimentState.Committed)
                        {
                            CommitStatusText.Text = "Status: COMMITTED SUCCESSFULLY";
                            CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                            CommitExpBtn.IsEnabled = false;
                            KeepCopyBtn.IsEnabled = false;
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
                        if (state == ExperimentState.Committed)
                        {
                            CommitStatusText.Text = "Status: COMMITTED SUCCESSFULLY (NO MUTATION)";
                            CommitStatusText.Foreground = new Microsoft.UI.Xaml.Media.SolidColorBrush(Windows.UI.Color.FromArgb(255, 0, 128, 0));
                            CommitExpBtn.IsEnabled = false;
                            KeepCopyBtn.IsEnabled = false;
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
    }
}
