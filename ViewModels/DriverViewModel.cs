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
        private readonly GoogleDriveService _driveService;
        private readonly DriverBackupService _backupService;

        private string _detectedModel = "Dang phat hien...";
        private string _matchStatus = "";
        private string _matchedConfigModel = "";
        private double _progress;
        private string _statusText = "San sang";
        private bool _isBusy;
        private bool _isAllSelected;
        private CancellationTokenSource? _cts;

        // Backup/Upload state
        private double _backupProgress;
        private string _backupStatusText = "";
        private bool _isBackupBusy;
        private string _driveAuthStatus = "Chua ket noi";
        private string _clientId = "";
        private string _clientSecret = "";

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

        // === Backup/Upload Properties ===

        public double BackupProgress
        {
            get => _backupProgress;
            set { _backupProgress = value; OnPropertyChanged(); }
        }

        public string BackupStatusText
        {
            get => _backupStatusText;
            set { _backupStatusText = value; OnPropertyChanged(); }
        }

        public bool IsBackupBusy
        {
            get => _isBackupBusy;
            set { _isBackupBusy = value; OnPropertyChanged(); }
        }

        public string DriveAuthStatus
        {
            get => _driveAuthStatus;
            set { _driveAuthStatus = value; OnPropertyChanged(); }
        }

        public string ClientId
        {
            get => _clientId;
            set { _clientId = value; OnPropertyChanged(); }
        }

        public string ClientSecret
        {
            get => _clientSecret;
            set { _clientSecret = value; OnPropertyChanged(); }
        }

        // === Commands ===

        public RelayCommand InstallSelectedCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand RefreshCommand { get; }
        public RelayCommand ConnectDriveCommand { get; }
        public RelayCommand DisconnectDriveCommand { get; }
        public RelayCommand BackupAndUploadCommand { get; }
        public RelayCommand SaveDriveConfigCommand { get; }
        public RelayCommand CancelBackupCommand { get; }

        public DriverViewModel(IInstallService installService, IDownloadService downloadService,
            ILogService logService, HardwareViewModel hardwareVM,
            GoogleDriveService driveService, DriverBackupService backupService)
        {
            _installService = installService;
            _downloadService = downloadService;
            _logService = logService;
            _hardwareVM = hardwareVM;
            _driveService = driveService;
            _backupService = backupService;

            // Install commands
            InstallSelectedCommand = new RelayCommand(async () => await InstallSelectedDriversAsync(), () => CanInstall);
            SelectAllCommand = new RelayCommand(() => IsAllSelected = true);
            SelectNoneCommand = new RelayCommand(() => IsAllSelected = false);
            CancelCommand = new RelayCommand(() => _cts?.Cancel());
            RefreshCommand = new RelayCommand(() => LoadDriverConfig());

            // Backup/Drive commands
            ConnectDriveCommand = new RelayCommand(async () => await ConnectDriveAsync());
            DisconnectDriveCommand = new RelayCommand(() => DisconnectDrive());
            BackupAndUploadCommand = new RelayCommand(async () => await BackupAndUploadAsync());
            SaveDriveConfigCommand = new RelayCommand(() => SaveDriveConfig());
            CancelBackupCommand = new RelayCommand(() => _cts?.Cancel());

            // Load saved config
            LoadDriveConfig();
            UpdateDriveAuthStatus();

            // Subscribe to HardwareVM changes
            _hardwareVM.PropertyChanged += OnHardwareVMPropertyChanged;
            LoadDriverConfig();
        }

        private void LoadDriveConfig()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "google_drive_config.json");
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    using var doc = JsonDocument.Parse(json);
                    ClientId = doc.RootElement.GetProperty("ClientId").GetString() ?? "";
                    ClientSecret = doc.RootElement.GetProperty("ClientSecret").GetString() ?? "";
                }
            }
            catch { }
        }

        private void SaveDriveConfig()
        {
            _driveService.SaveConfig(ClientId, ClientSecret);
            BackupStatusText = "Da luu cau hinh Google Drive";
        }

        private void UpdateDriveAuthStatus()
        {
            if (_driveService.IsAuthenticated)
                DriveAuthStatus = $"Da ket noi: {_driveService.AuthenticatedEmail ?? "Google Drive"}";
            else if (!string.IsNullOrEmpty(_driveService.AuthenticatedEmail))
                DriveAuthStatus = $"Token het han: {_driveService.AuthenticatedEmail}";
            else
                DriveAuthStatus = "Chua ket noi";
        }

        private async Task ConnectDriveAsync()
        {
            if (string.IsNullOrWhiteSpace(ClientId) || string.IsNullOrWhiteSpace(ClientSecret))
            {
                BackupStatusText = "Vui long nhap Client ID va Client Secret truoc";
                return;
            }

            _driveService.SaveConfig(ClientId, ClientSecret);
            BackupStatusText = "Dang mo trinh duyet de xac thuc...";

            bool success = await _driveService.AuthenticateAsync();
            if (success)
            {
                BackupStatusText = $"Ket noi thanh cong: {_driveService.AuthenticatedEmail}";
            }
            else
            {
                BackupStatusText = "Xac thuc that bai. Kiem tra Client ID/Secret.";
            }
            UpdateDriveAuthStatus();
        }

        private void DisconnectDrive()
        {
            _driveService.Logout();
            DriveAuthStatus = "Chua ket noi";
            BackupStatusText = "Da ngat ket noi Google Drive";
        }

        private async Task BackupAndUploadAsync()
        {
            if (!_driveService.IsAuthenticated)
            {
                // Try refresh
                bool refreshed = await _driveService.AuthenticateAsync();
                if (!refreshed)
                {
                    BackupStatusText = "Chua ket noi Google Drive. Vui long ket noi truoc.";
                    return;
                }
            }

            IsBackupBusy = true;
            _cts = new CancellationTokenSource();
            BackupProgress = 0;
            BackupStatusText = "Bat dau backup driver...";

            try
            {
                // Step 1: DISM Export (0-40%)
                var exportProgress = new Progress<(double percent, string status)>(p =>
                {
                    BackupProgress = p.percent * 0.4;
                    BackupStatusText = p.status;
                });

                string? exportedPath = await _backupService.ExportDriversAsync(exportProgress, _cts.Token);
                if (string.IsNullOrEmpty(exportedPath))
                {
                    BackupStatusText = "Export driver that bai";
                    return;
                }

                // Step 2: ZIP (40-60%)
                var zipProgress = new Progress<(double percent, string status)>(p =>
                {
                    BackupProgress = 40 + p.percent * 0.2;
                    BackupStatusText = p.status;
                });

                string? zipPath = await _backupService.ZipDriverBackupAsync(exportedPath, zipProgress, _cts.Token);
                if (string.IsNullOrEmpty(zipPath))
                {
                    BackupStatusText = "Nen ZIP that bai";
                    return;
                }

                // Step 3: Upload to Drive (60-95%)
                // Re-authenticate before upload in case token expired during DISM export + ZIP
                if (!_driveService.IsAuthenticated)
                {
                    BackupStatusText = "Dang lam moi token Google Drive...";
                    bool refreshed = await _driveService.AuthenticateAsync();
                    if (!refreshed)
                    {
                        BackupStatusText = "Token het han va khong the lam moi. Vui long ket noi lai.";
                        UpdateDriveAuthStatus();
                        return;
                    }
                    UpdateDriveAuthStatus();
                }

                var uploadProgress = new Progress<(double percent, string status)>(p =>
                {
                    BackupProgress = 60 + p.percent * 0.35;
                    BackupStatusText = p.status;
                });

                var result = await _driveService.UploadFileAsync(zipPath, uploadProgress, _cts.Token);
                if (result == null)
                {
                    BackupStatusText = "Upload len Drive that bai";
                    return;
                }

                // Step 4: Auto-update driver_config.json (95-100%)
                BackupProgress = 96;
                BackupStatusText = "Dang cap nhat driver_config.json...";

                string modelName = _hardwareVM.SysInfo?.ModelName ?? DriverBackupService.GetModelName();
                AutoUpdateDriverConfig(modelName, Path.GetFileName(zipPath), result.Value.webViewLink);

                BackupProgress = 100;
                BackupStatusText = $"Hoan tat! Link: {result.Value.webViewLink}";

                // Copy link to clipboard
                try { Clipboard.SetText(result.Value.webViewLink); } catch { }

                _logService.LogInfo($"Driver backup completed: {result.Value.webViewLink}");

                // Reload config to show updated list
                LoadDriverConfig();
            }
            catch (OperationCanceledException)
            {
                BackupStatusText = "Da huy backup";
            }
            catch (Exception ex)
            {
                _logService.LogError("Backup and upload failed", ex);
                BackupStatusText = $"Loi: {ex.Message}";
            }
            finally
            {
                IsBackupBusy = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void AutoUpdateDriverConfig(string modelName, string zipFileName, string driveLink)
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "driver_config.json");

                List<DriverModelConfig> configs;
                if (File.Exists(configPath))
                {
                    var json = File.ReadAllText(configPath);
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    configs = JsonSerializer.Deserialize<List<DriverModelConfig>>(json, options) ?? new();
                }
                else
                {
                    configs = new();
                }

                // Find or create config for this model
                var existing = configs.FirstOrDefault(c =>
                    c.ModelMatch.Any(m => modelName.Contains(m, StringComparison.OrdinalIgnoreCase)));

                if (existing != null)
                {
                    // Update: check if Full Driver Pack already exists, update link
                    var fullPack = existing.Drivers.FirstOrDefault(d =>
                        d.Category.Equals("All", StringComparison.OrdinalIgnoreCase) ||
                        d.Name.Contains("Full Driver", StringComparison.OrdinalIgnoreCase));

                    if (fullPack != null)
                    {
                        fullPack.DownloadUrl = driveLink;
                        fullPack.FileName = zipFileName;
                    }
                    else
                    {
                        existing.Drivers.Insert(0, new DriverItem
                        {
                            Name = "Full Driver Pack (Backup)",
                            Category = "All",
                            FileName = zipFileName,
                            DownloadUrl = driveLink,
                            Description = $"Backup tu may chuan - {DateTime.Now:dd/MM/yyyy}"
                        });
                    }
                }
                else
                {
                    // Create new model entry
                    // Generate smart match strings
                    var matchStrings = new List<string>();
                    var words = modelName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (words.Length >= 2)
                        matchStrings.Add(string.Join(" ", words.Take(Math.Min(3, words.Length))));
                    matchStrings.Add(modelName);

                    configs.Add(new DriverModelConfig
                    {
                        Model = modelName,
                        ModelMatch = matchStrings,
                        Drivers = new List<DriverItem>
                        {
                            new DriverItem
                            {
                                Name = "Full Driver Pack (Backup)",
                                Category = "All",
                                FileName = zipFileName,
                                DownloadUrl = driveLink,
                                Description = $"Backup tu may chuan - {DateTime.Now:dd/MM/yyyy}"
                            }
                        }
                    });
                }

                // Save back
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var writeOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
                File.WriteAllText(configPath, JsonSerializer.Serialize(configs, writeOptions));

                _logService.LogInfo($"Auto-updated driver_config.json for model: {modelName}");
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to auto-update driver_config.json", ex);
            }
        }

        // === Existing methods ===

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
