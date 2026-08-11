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

                RefreshUI();
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
    }
}
