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
    /// Interactive Dashboard supporting Importing, Listing, Compression, and Restoring.
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

                RefreshUI();
                LogToTerminal("Dashboard Loaded and Ready.");
            }
            catch (Exception ex)
            {
                LogToTerminal($"Initialization Error: {ex.Message}");
            }
        }

        private async void RefreshUI()
        {
            if (_repository == null) return;

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

                // Compute mock progress for local storage space allocation
                double progress = totalOriginalSize > 0 ? ((double)totalStoredSize / totalOriginalSize) * 100 : 0;
                StorageProgress.Value = Math.Clamp(progress, 0, 100);
                StorageStatusText.Text = $"{progress:F1}% Capacity Used (Normal)";
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
                // In WinUI 3, we use FileOpenPicker. For this PoC, we can present a quick simulation or
                // prompt the user to paste a path using a simple flyout, or use a default test file for easier demonstration.
                // Let's invoke a standard WinUI FileOpenPicker!
                var picker = new Windows.Storage.Pickers.FileOpenPicker();

                // Get Window handle
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

        private async void OnCompressSelectedClicked(object sender, RoutedEventArgs e)
        {
            if (_compressionEngine == null || ItemsListView.SelectedItem is not SmartBinItem selectedItem)
            {
                LogToTerminal("Please select an item from the list to compress.");
                return;
            }

            try
            {
                LogToTerminal($"Analyzing compressibility for {selectedItem.OriginalFileName}...");
                await _compressionEngine.CompressItemAsync(selectedItem.Id);
                LogToTerminal($"✓ Compression run complete for {selectedItem.OriginalFileName}.");
                RefreshUI();
            }
            catch (Exception ex)
            {
                LogToTerminal($"Compression Engine Error: {ex.Message}");
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
                // WinUI FolderPicker to select the restoration target folder
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
    }
}
