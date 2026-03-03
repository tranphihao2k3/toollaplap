using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class DriverViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private readonly IDownloadService _downloadService;
        private readonly ILogService _logService;
        private readonly HardwareViewModel _hardwareVM;

        private string _detectedModel = "Dang phat hien...";
        private string _matchStatus = "";
        private string _matchedConfigModel = "";
        private double _progress;
        private string _statusText = "San sang";
        private bool _isBusy;
        private bool _isAllSelected;
        private CancellationTokenSource? _cts;

        public ObservableCollection<DriverItem> DriverList { get; set; } = new();

        public string DetectedModel
        {
            get => _detectedModel;
            set { _detectedModel = value; OnPropertyChanged(); }
        }

        public string MatchStatus
        {
            get => _matchStatus;
            set { _matchStatus = value; OnPropertyChanged(); }
        }

        public string MatchedConfigModel
        {
            get => _matchedConfigModel;
            set { _matchedConfigModel = value; OnPropertyChanged(); }
        }

        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanInstall)); }
        }

        public bool CanInstall => !IsBusy && DriverList.Any(d => d.IsSelected);

        public bool IsAllSelected
        {
            get => _isAllSelected;
            set
            {
                if (_isAllSelected == value) return;
                _isAllSelected = value;
                OnPropertyChanged();
                foreach (var item in DriverList)
                    item.IsSelected = value;
            }
        }

        public RelayCommand InstallSelectedCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RefreshCommand { get; }

        public DriverViewModel(IInstallService installService, IDownloadService downloadService, ILogService logService, HardwareViewModel hardwareVM)
        {
            _installService = installService;
            _downloadService = downloadService;
            _logService = logService;
            _hardwareVM = hardwareVM;

            InstallSelectedCommand = new RelayCommand(async () => await InstallSelectedDriversAsync(), () => CanInstall);
            SelectAllCommand = new RelayCommand(() => IsAllSelected = true);
            SelectNoneCommand = new RelayCommand(() => IsAllSelected = false);
            CancelCommand = new RelayCommand(() => _cts?.Cancel());
            RefreshCommand = new RelayCommand(() => LoadDriverConfig());

            // Subscribe to HardwareVM changes to detect when model info is ready
            _hardwareVM.PropertyChanged += OnHardwareVMPropertyChanged;

            // Try to load immediately if SysInfo already has data
            LoadDriverConfig();
        }

        private void OnHardwareVMPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(HardwareViewModel.SysInfo))
            {
                Application.Current?.Dispatcher.Invoke(() => LoadDriverConfig());
            }
        }

        private void LoadDriverConfig()
        {
            try
            {
                string modelName = _hardwareVM.SysInfo?.ModelName ?? "N/A";
                DetectedModel = modelName;

                // Load config JSON
                string? json = null;
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "driver_config.json");

                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                }
                else
                {
                    var resourceUri = new Uri("pack://application:,,,/Resources/driver_config.json");
                    var info = Application.GetResourceStream(resourceUri);
                    if (info != null)
                    {
                        using var reader = new StreamReader(info.Stream);
                        json = reader.ReadToEnd();
                    }
                }

                if (string.IsNullOrWhiteSpace(json))
                {
                    MatchStatus = "Khong tim thay file driver_config.json";
                    return;
                }

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var configs = JsonSerializer.Deserialize<List<DriverModelConfig>>(json, options);

                if (configs == null || configs.Count == 0)
                {
                    MatchStatus = "driver_config.json trong hoac khong hop le";
                    return;
                }

                // Match model
                var matched = configs.FirstOrDefault(c =>
                    c.ModelMatch.Any(m => modelName.Contains(m, StringComparison.OrdinalIgnoreCase)));

                DriverList.Clear();

                if (matched != null)
                {
                    MatchedConfigModel = matched.Model;
                    MatchStatus = $"Da tim thay driver cho: {matched.Model}";

                    foreach (var driver in matched.Drivers)
                    {
                        DriverList.Add(new DriverItem
                        {
                            Name = driver.Name,
                            Category = driver.Category,
                            FileName = driver.FileName,
                            DownloadUrl = driver.DownloadUrl,
                            Description = driver.Description
                        });
                    }
                }
                else
                {
                    MatchStatus = "Khong tim thay driver cho model nay";
                    MatchedConfigModel = "";
                }

                OnPropertyChanged(nameof(CanInstall));
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load driver config", ex);
                MatchStatus = $"Loi doc config: {ex.Message}";
            }
        }

        private async Task InstallSelectedDriversAsync()
        {
            var selected = DriverList.Where(d => d.IsSelected).ToList();
            if (selected.Count == 0) return;

            IsBusy = true;
            _cts = new CancellationTokenSource();
            Progress = 0;
            StatusText = "Bat dau cai driver...";

            int completed = 0;
            int total = selected.Count;

            try
            {
                foreach (var driver in selected)
                {
                    if (_cts.Token.IsCancellationRequested) break;

                    StatusText = $"[{completed + 1}/{total}] {driver.Name}";

                    // Step 1: Download
                    driver.Status = DriverInstallStatus.Downloading;
                    driver.StatusText = "Dang tai...";

                    var downloadProgress = new Progress<(double percent, string status)>(p =>
                    {
                        driver.Progress = p.percent;
                        driver.StatusText = p.status;
                    });

                    string? downloadedPath = await _downloadService.DownloadFileAsync(
                        driver.DownloadUrl, driver.FileName, downloadProgress, _cts.Token);

                    if (string.IsNullOrEmpty(downloadedPath))
                    {
                        driver.Status = DriverInstallStatus.Failed;
                        driver.StatusText = "Tai that bai";
                        completed++;
                        Progress = (double)completed / total * 100;
                        continue;
                    }

                    // Step 2: Extract
                    driver.Status = DriverInstallStatus.Extracting;
                    driver.StatusText = "Giai nen...";

                    var extractProgress = new Progress<(double percent, string status)>(p =>
                    {
                        driver.StatusText = p.status;
                    });

                    string? extractedPath = await _downloadService.ExtractArchiveAsync(downloadedPath, extractProgress);

                    if (string.IsNullOrEmpty(extractedPath))
                    {
                        driver.Status = DriverInstallStatus.Failed;
                        driver.StatusText = "Giai nen that bai";
                        completed++;
                        Progress = (double)completed / total * 100;
                        continue;
                    }

                    // Step 3: Install with DISM
                    driver.Status = DriverInstallStatus.Installing;
                    driver.StatusText = "Dang cai driver (DISM)...";

                    var dismProgress = new Progress<string>(msg =>
                    {
                        driver.StatusText = msg;
                    });

                    bool success = await _installService.InstallDriverWithDismAsync(extractedPath, dismProgress);

                    if (success)
                    {
                        driver.Status = DriverInstallStatus.Completed;
                        driver.StatusText = "Hoan tat!";
                    }
                    else
                    {
                        driver.Status = DriverInstallStatus.Failed;
                        driver.StatusText = "DISM that bai";
                    }

                    completed++;
                    Progress = (double)completed / total * 100;
                }

                int successCount = selected.Count(d => d.Status == DriverInstallStatus.Completed);
                int failCount = selected.Count(d => d.Status == DriverInstallStatus.Failed);
                StatusText = $"Hoan tat: {successCount} thanh cong, {failCount} that bai";
            }
            catch (OperationCanceledException)
            {
                StatusText = "Da huy cai dat driver";
            }
            catch (Exception ex)
            {
                _logService.LogError("Driver installation failed", ex);
                StatusText = $"Loi: {ex.Message}";
            }
            finally
            {
                IsBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
