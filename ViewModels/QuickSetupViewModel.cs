using System;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Windows;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class QuickSetupViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private readonly IDownloadService _downloadService;
        private readonly ILogService _logService;
        private double _progress;
        private string _statusText = "Sẵn sàng để bắt đầu";
        private bool _isBusy;

        public ObservableCollection<SoftwareItem> SoftwareList { get; set; }

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
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public QuickSetupViewModel(IInstallService installService, IDownloadService downloadService, ILogService logService)
        {
            _installService = installService;
            _downloadService = downloadService;
            _logService = logService;
            SoftwareList = new ObservableCollection<SoftwareItem>();
            LoadSoftwareList();

            StartInstallCommand = new RelayCommand(async () => await RunInstallationSequence());
            SelectAllCommand = new RelayCommand(SelectAll);
            SelectNoneCommand = new RelayCommand(SelectNone);
            RefreshCommand = new RelayCommand(RefreshSoftwareStatus);
        }

        private void RefreshSoftwareStatus()
        {
            foreach (var item in SoftwareList)
            {
                item.IsAlreadyInstalled = _installService.IsSoftwareInstalled(item.Name);
                if (item.IsAlreadyInstalled)
                    item.Status = InstallStatus.Completed;
                else if (item.Status == InstallStatus.Completed || item.Status == InstallStatus.Failed)
                    item.Status = InstallStatus.Pending;
            }
            StatusText = "Đã cập nhật trạng thái phần mềm!";
        }

        private void LoadSoftwareList()
        {
            try
            {
                string json = null;
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "software_config.json");
                
                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                }
                else
                {
                    // Fallback to embedded resource
                    var resourceUri = new Uri("pack://application:,,,/Resources/software_config.json");
                    var info = Application.GetResourceStream(resourceUri);
                    if (info != null)
                    {
                        using (var reader = new StreamReader(info.Stream))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<SoftwareItem>>(json);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            item.IsAlreadyInstalled = _installService.IsSoftwareInstalled(item.Name);
                            SoftwareList.Add(item);
                        }
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi tải danh sách phần mềm", ex);
            }

            var fallback = new System.Collections.Generic.List<SoftwareItem>
            {
                new SoftwareItem { Name = "EVKey", FileName = "EVKey64.exe", SilentArgs = "COPY_TO_DESKTOP", Description = "Bộ gõ tiếng Việt" },
                new SoftwareItem { Name = "UltraViewer", FileName = "UltraViewer_setup.exe", SilentArgs = "/S", Description = "Điều khiển máy tính từ xa" },
                new SoftwareItem { Name = "VLC Media Player", FileName = "vlc_setup.exe", SilentArgs = "/S", Description = "Trình phát đa phương tiện" }
            };
            foreach (var item in fallback)
            {
                item.IsAlreadyInstalled = _installService.IsSoftwareInstalled(item.Name);
                SoftwareList.Add(item);
            }
        }

        public RelayCommand StartInstallCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand SelectNoneCommand { get; }
        public RelayCommand RefreshCommand { get; }

        private void SelectAll() { foreach (var item in SoftwareList) item.IsSelected = true; }
        private void SelectNone() { foreach (var item in SoftwareList) item.IsSelected = false; }

        private async Task RunInstallationSequence()
        {
            if (IsBusy) return;
            IsBusy = true;
            Progress = 0;

            var selectedItems = SoftwareList.Where(s => s.IsSelected && !s.IsAlreadyInstalled).ToList();
            var alreadyDone = SoftwareList.Where(s => s.IsSelected && s.IsAlreadyInstalled).ToList();

            foreach (var item in alreadyDone)
                item.Status = InstallStatus.Completed;

            if (selectedItems.Count == 0)
            {
                StatusText = alreadyDone.Count > 0 ? "Tất cả phần mềm đã được cài đặt!" : "Chưa chọn phần mềm nào!";
                IsBusy = false;
                return;
            }

            int total = selectedItems.Count;
            int installedCount = 0;
            string setupDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SetupFiles");

            // Tạo Channel để truyền Item từ Downloader sang Installer
            var channel = Channel.CreateUnbounded<SoftwareItem>();

            // --- TASK 1: DOWNLOADER (PRODUCER) ---
            var downloaderTask = Task.Run(async () =>
            {
                try
                {
                    foreach (var item in selectedItems)
                    {
                        string localPath = Path.Combine(setupDir, item.FileName);
                        item.LocalInstallerPath = localPath;

                        // Nếu file chưa có sẵn -> Tải
                        if (!File.Exists(localPath) && !string.IsNullOrWhiteSpace(item.DownloadUrl))
                        {
                            item.Status = InstallStatus.Pending;
                            item.DownloadStatus = DownloadStatus.Downloading;
                            item.DownloadProgress = 0;
                            
                            // Cập nhật text trạng thái nếu chưa có gì đang cài
                            if (installedCount == SoftwareList.Count(s => s.IsSelected && s.Status == InstallStatus.Completed))
                                StatusText = $"Đang tải {item.Name}...";

                            var dlProgress = new Progress<(double percent, string status)>(report =>
                            {
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    item.DownloadProgress = report.percent >= 0 ? report.percent : 0;
                                    item.DownloadStatusText = report.status;
                                });
                            });

                            string? downloadedPath = await _downloadService.DownloadFileAsync(item.DownloadUrl, item.FileName, dlProgress);

                            if (downloadedPath != null)
                            {
                                try
                                {
                                    if (!Directory.Exists(setupDir)) Directory.CreateDirectory(setupDir);
                                    string finalPath = Path.Combine(setupDir, item.FileName);
                                    if (File.Exists(finalPath)) File.Delete(finalPath);
                                    File.Move(downloadedPath, finalPath);
                                    item.LocalInstallerPath = finalPath;
                                    item.DownloadStatus = DownloadStatus.Done;
                                }
                                catch (Exception ex) { _logService.LogError($"Lỗi di chuyển file: {ex.Message}"); }
                            }
                            else
                            {
                                item.Status = InstallStatus.Failed;
                                item.DownloadStatus = DownloadStatus.Failed;
                                item.DownloadStatusText = "Tải thất bại!";
                            }
                        }
                        else if (File.Exists(localPath))
                        {
                            item.DownloadStatus = DownloadStatus.Done;
                            item.DownloadStatusText = "Sẵn sàng (có sẵn)";
                        }
                        else
                        {
                            item.Status = InstallStatus.Failed;
                            item.DownloadStatusText = "Thiếu bộ cài!";
                        }

                        // Đẩy vào kênh cài đặt (kể cả FAILED để installer bỏ qua và tăng tiến trình)
                        await channel.Writer.WriteAsync(item);
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError("Downloader task error", ex);
                }
                finally
                {
                    channel.Writer.Complete();
                }
            });

            // --- TASK 2: INSTALLER (CONSUMER) ---
            var installerTask = Task.Run(async () =>
            {
                try
                {
                    await foreach (var item in channel.Reader.ReadAllAsync())
                    {
                        if (item.Status == InstallStatus.Failed)
                        {
                            Application.Current.Dispatcher.Invoke(() => {
                                installedCount++;
                                Progress = (double)installedCount / total * 100;
                            });
                            continue;
                        }

                        // Kiểm tra file cuối cùng
                        if (!File.Exists(item.LocalInstallerPath))
                        {
                            item.Status = InstallStatus.Failed;
                            item.DownloadStatusText = "Lỗi file!";
                            Application.Current.Dispatcher.Invoke(() => {
                                installedCount++;
                                Progress = (double)installedCount / total * 100;
                            });
                            continue;
                        }

                        // Bắt đầu cài
                        StatusText = $"Đang cài đặt {item.Name}...";
                        item.DownloadStatusText = "Đang cài đặt...";
                        item.Status = InstallStatus.Installing;

                        bool success = await _installService.InstallAsync(item.LocalInstallerPath, item.SilentArgs, !item.IsAsync);
                        await Task.Delay(500);

                        item.Status = success ? InstallStatus.Completed : InstallStatus.Failed;
                        item.DownloadStatusText = success ? "Hoàn tất!" : "Cài đặt thất bại!";

                        Application.Current.Dispatcher.Invoke(() => {
                            installedCount++;
                            Progress = (double)installedCount / total * 100;
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogError("Installer task error", ex);
                }
            });

            await Task.WhenAll(downloaderTask, installerTask);

            StatusText = "Tất cả tác vụ đã hoàn tất!";
            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
