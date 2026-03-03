using System;
using System.IO;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading.Channels;
using System.Windows;
using System.Windows.Data;
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
        private bool _suppressCheckAllUpdate;

        public ObservableCollection<SoftwareItem> SoftwareList { get; set; }
        public ObservableCollection<SoftwareItem> UtilityList { get; set; }
        public ObservableCollection<SoftwareItem> GameList { get; set; }
        public ObservableCollection<SoftwareItem> StudentList { get; set; }

        public ICollectionView SoftwareView { get; private set; }
        public ICollectionView UtilityView { get; private set; }
        public ICollectionView GameView { get; private set; }
        public ICollectionView StudentView { get; private set; }

        private string _searchText = "";
        public string SearchText
        {
            get => _searchText;
            set
            {
                _searchText = value;
                OnPropertyChanged();
                SoftwareView?.Refresh();
                UtilityView?.Refresh();
                GameView?.Refresh();
                StudentView?.Refresh();
            }
        }

        private bool SearchFilter(object obj)
        {
            if (string.IsNullOrWhiteSpace(_searchText)) return true;
            if (obj is SoftwareItem item)
            {
                var search = _searchText.Trim();
                return item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Description.Contains(search, StringComparison.OrdinalIgnoreCase);
            }
            return true;
        }

        public System.Collections.Generic.IEnumerable<SoftwareItem> AllItems => SoftwareList.Concat(UtilityList).Concat(GameList).Concat(StudentList);

        private bool _isAllSoftwareSelected;
        public bool IsAllSoftwareSelected
        {
            get => _isAllSoftwareSelected;
            set
            {
                if (_isAllSoftwareSelected == value) return;
                _isAllSoftwareSelected = value;
                OnPropertyChanged();
                if (!_suppressCheckAllUpdate)
                {
                    _suppressCheckAllUpdate = true;
                    foreach (var item in SoftwareList) item.IsSelected = value;
                    _suppressCheckAllUpdate = false;
                }
            }
        }

        private bool _isAllUtilitySelected;
        public bool IsAllUtilitySelected
        {
            get => _isAllUtilitySelected;
            set
            {
                if (_isAllUtilitySelected == value) return;
                _isAllUtilitySelected = value;
                OnPropertyChanged();
                if (!_suppressCheckAllUpdate)
                {
                    _suppressCheckAllUpdate = true;
                    foreach (var item in UtilityList) item.IsSelected = value;
                    _suppressCheckAllUpdate = false;
                }
            }
        }

        private bool _isAllGameSelected;
        public bool IsAllGameSelected
        {
            get => _isAllGameSelected;
            set
            {
                if (_isAllGameSelected == value) return;
                _isAllGameSelected = value;
                OnPropertyChanged();
                if (!_suppressCheckAllUpdate)
                {
                    _suppressCheckAllUpdate = true;
                    foreach (var item in GameList) item.IsSelected = value;
                    _suppressCheckAllUpdate = false;
                }
            }
        }

        private bool _isAllStudentSelected;
        public bool IsAllStudentSelected
        {
            get => _isAllStudentSelected;
            set
            {
                if (_isAllStudentSelected == value) return;
                _isAllStudentSelected = value;
                OnPropertyChanged();
                if (!_suppressCheckAllUpdate)
                {
                    _suppressCheckAllUpdate = true;
                    foreach (var item in StudentList) item.IsSelected = value;
                    _suppressCheckAllUpdate = false;
                }
            }
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
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public QuickSetupViewModel(IInstallService installService, IDownloadService downloadService, ILogService logService)
        {
            _installService = installService;
            _downloadService = downloadService;
            _logService = logService;
            SoftwareList = new ObservableCollection<SoftwareItem>();
            UtilityList = new ObservableCollection<SoftwareItem>();
            GameList = new ObservableCollection<SoftwareItem>();
            StudentList = new ObservableCollection<SoftwareItem>();
            LoadSoftwareList();
            SubscribeItemChanges();

            SoftwareView = CollectionViewSource.GetDefaultView(SoftwareList);
            SoftwareView.Filter = SearchFilter;
            UtilityView = CollectionViewSource.GetDefaultView(UtilityList);
            UtilityView.Filter = SearchFilter;
            GameView = CollectionViewSource.GetDefaultView(GameList);
            GameView.Filter = SearchFilter;
            StudentView = CollectionViewSource.GetDefaultView(StudentList);
            StudentView.Filter = SearchFilter;

            StartInstallCommand = new RelayCommand(async () => await RunInstallationSequence());
            SelectAllCommand = new RelayCommand<string>(SelectAll);
            SelectNoneCommand = new RelayCommand<string>(SelectNone);
            RefreshCommand = new RelayCommand(RefreshSoftwareStatus);
        }

        private void SubscribeItemChanges()
        {
            foreach (var item in SoftwareList) item.PropertyChanged += OnSoftwareItemChanged;
            foreach (var item in UtilityList) item.PropertyChanged += OnUtilityItemChanged;
            foreach (var item in GameList) item.PropertyChanged += OnGameItemChanged;
            foreach (var item in StudentList) item.PropertyChanged += OnStudentItemChanged;
        }

        private void OnSoftwareItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareItem.IsSelected) && !_suppressCheckAllUpdate)
            {
                _suppressCheckAllUpdate = true;
                _isAllSoftwareSelected = SoftwareList.All(i => i.IsSelected);
                OnPropertyChanged(nameof(IsAllSoftwareSelected));
                _suppressCheckAllUpdate = false;
            }
        }

        private void OnUtilityItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareItem.IsSelected) && !_suppressCheckAllUpdate)
            {
                _suppressCheckAllUpdate = true;
                _isAllUtilitySelected = UtilityList.All(i => i.IsSelected);
                OnPropertyChanged(nameof(IsAllUtilitySelected));
                _suppressCheckAllUpdate = false;
            }
        }

        private void OnGameItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareItem.IsSelected) && !_suppressCheckAllUpdate)
            {
                _suppressCheckAllUpdate = true;
                _isAllGameSelected = GameList.All(i => i.IsSelected);
                OnPropertyChanged(nameof(IsAllGameSelected));
                _suppressCheckAllUpdate = false;
            }
        }

        private void OnStudentItemChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareItem.IsSelected) && !_suppressCheckAllUpdate)
            {
                _suppressCheckAllUpdate = true;
                _isAllStudentSelected = StudentList.All(i => i.IsSelected);
                OnPropertyChanged(nameof(IsAllStudentSelected));
                _suppressCheckAllUpdate = false;
            }
        }

        private void RefreshSoftwareStatus()
        {
            foreach (var item in AllItems)
            {
                // Bỏ logic kiểm tra phần mềm đã cài
                item.IsAlreadyInstalled = false; 
                
                if (item.Status == InstallStatus.Completed || item.Status == InstallStatus.Failed)
                    item.Status = InstallStatus.Pending;
            }
            StatusText = "Đã cập nhật trạng thái (Bỏ qua phần mềm có sẵn)";
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
                            item.IsAlreadyInstalled = false; // _installService.IsSoftwareInstalled(item.Name);
                            if (item.Category == "game")
                            {
                                GameList.Add(item);
                            }
                            else if (item.Category == "utility")
                            {
                                UtilityList.Add(item);
                            }
                            else if (item.Category == "student")
                            {
                                StudentList.Add(item);
                            }
                            else
                            {
                                SoftwareList.Add(item);
                            }
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
                item.IsAlreadyInstalled = false; // _installService.IsSoftwareInstalled(item.Name);
                SoftwareList.Add(item);
            }
        }

        public RelayCommand StartInstallCommand { get; }
        public RelayCommand<string> SelectAllCommand { get; }
        public RelayCommand<string> SelectNoneCommand { get; }
        public RelayCommand RefreshCommand { get; }

        private void SelectAll(string category)
        {
            var targets = category switch
            {
                "general" => SoftwareList,
                "utility" => UtilityList,
                "game" => GameList,
                "student" => StudentList,
                _ => AllItems
            };
            foreach (var item in targets) item.IsSelected = true;
        }

        private void SelectNone(string category)
        {
            var targets = category switch
            {
                "general" => SoftwareList,
                "utility" => UtilityList,
                "game" => GameList,
                "student" => StudentList,
                _ => AllItems
            };
            foreach (var item in targets) item.IsSelected = false;
        }

        private async Task RunInstallationSequence()
        {
            if (IsBusy) return;
            IsBusy = true;
            Progress = 0;

            var selectedItems = AllItems.Where(s => s.IsSelected).ToList();

            if (selectedItems.Count == 0)
            {
                StatusText = "Chưa chọn phần mềm nào!";
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
                            if (installedCount == AllItems.Count(s => s.IsSelected && s.Status == InstallStatus.Completed))
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

                        // DOWNLOAD_ONLY: chỉ tải + giải nén + mở thư mục
                        if (item.SilentArgs == "DOWNLOAD_ONLY")
                        {
                            StatusText = $"Đang xử lý {item.Name}...";
                            item.Status = InstallStatus.Installing;

                            string ext = Path.GetExtension(item.LocalInstallerPath).ToLower();
                            string? finalFolder = null;

                            if (ext == ".zip" || ext == ".rar" || ext == ".7z")
                            {
                                item.DownloadStatusText = "Đang giải nén...";
                                item.DownloadStatus = DownloadStatus.Extracting;
                                var extractProgress = new Progress<(double percent, string status)>(report =>
                                {
                                    Application.Current.Dispatcher.Invoke(() =>
                                    {
                                        item.DownloadProgress = report.percent;
                                        item.DownloadStatusText = report.status;
                                    });
                                });
                                finalFolder = await _downloadService.ExtractArchiveAsync(item.LocalInstallerPath, extractProgress, item.Password);
                            }
                            else
                            {
                                finalFolder = Path.GetDirectoryName(item.LocalInstallerPath);
                            }

                            item.Status = InstallStatus.Completed;
                            item.DownloadStatusText = "Hoàn tất! Đã mở thư mục.";

                            if (!string.IsNullOrEmpty(finalFolder))
                                _downloadService.OpenFolderInExplorer(finalFolder);
                        }
                        else
                        {
                            // Bắt đầu cài bình thường
                            StatusText = $"Đang cài đặt {item.Name}...";
                            item.DownloadStatusText = "Đang cài đặt...";
                            item.Status = InstallStatus.Installing;

                            bool success = await _installService.InstallAsync(item.LocalInstallerPath, item.SilentArgs, !item.IsAsync);
                            await Task.Delay(500);

                            item.Status = success ? InstallStatus.Completed : InstallStatus.Failed;
                            item.DownloadStatusText = success ? "Hoàn tất!" : "Cài đặt thất bại!";
                        }

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
