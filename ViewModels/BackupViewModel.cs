using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class BackupViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private bool _isBusy;
        private double _backupProgress;
        private string _statusText = "Đang chuẩn bị...";
        private List<BackupItem> _backupFolders;
        private bool _isDriverBusy;
        private string _driverStatusText = "Sẵn sàng";

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public double BackupProgress
        {
            get => _backupProgress;
            set { _backupProgress = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public List<BackupItem> BackupFolders
        {
            get => _backupFolders;
            set { _backupFolders = value; OnPropertyChanged(); }
        }

        public bool IsDriverBusy
        {
            get => _isDriverBusy;
            set { _isDriverBusy = value; OnPropertyChanged(); }
        }

        public string DriverStatusText
        {
            get => _driverStatusText;
            set { _driverStatusText = value; OnPropertyChanged(); }
        }

        public RelayCommand BackupDataCommand { get; }
        public RelayCommand BackupDriverCommand { get; }
        public RelayCommand RestoreDriverCommand { get; }

        public BackupViewModel(IInstallService installService)
        {
            _installService = installService;
            InitializeBackupFolders();
            BackupDataCommand = new RelayCommand(HandleBackup);
            BackupDriverCommand = new RelayCommand(HandleBackupDriver);
            RestoreDriverCommand = new RelayCommand(HandleRestoreDriver);
        }

        private void InitializeBackupFolders()
        {
            string userPath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            BackupFolders = new List<BackupItem>
            {
                new BackupItem { Name = "Desktop", Path = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) },
                new BackupItem { Name = "Documents", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments) },
                new BackupItem { Name = "Downloads", Path = Path.Combine(userPath, "Downloads") },
                new BackupItem { Name = "Pictures", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures) },
                new BackupItem { Name = "Videos", Path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos) },
                new BackupItem { Name = "Favorites", Path = Environment.GetFolderPath(Environment.SpecialFolder.Favorites) }
            };
        }

        private async void HandleBackup()
        {
            if (IsBusy) return;

            var selectedFolders = BackupFolders.Where(f => f.IsSelected).Select(f => f.Path).ToList();
            if (!selectedFolders.Any())
            {
                MessageBox.Show("Vui lòng chọn ít nhất một thư mục để sao lưu!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            IsBusy = true;
            BackupProgress = 0;
            StatusText = "Đang khởi tạo...";

            var progressReporter = new Progress<(double progress, string status)>(data =>
            {
                BackupProgress = data.progress;
                StatusText = data.status;
            });

            bool success = await _installService.BackupUserDataAsync(selectedFolders, progressReporter);
            
            IsBusy = false;
            
            if (success)
                MessageBox.Show("Sao lưu dữ liệu đã hoàn thành thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show("Sao lưu thất bại hoặc bị gián đoạn.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private async void HandleBackupDriver()
        {
            if (IsDriverBusy) return;

            string destPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                "Backup_Drivers");

            IsDriverBusy = true;
            DriverStatusText = "Đang xuất driver...";

            try
            {
                Directory.CreateDirectory(destPath);

                var psi = new ProcessStartInfo
                {
                    FileName = "dism",
                    Arguments = $"/online /export-driver /destination:\"{destPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Verb = "runas"
                };

                var proc = Process.Start(psi)!;
                await proc.WaitForExitAsync();

                if (proc.ExitCode == 0)
                {
                    DriverStatusText = $"Đã xuất driver vào Desktop/Backup_Drivers";
                    MessageBox.Show($"Backup driver thành công!\nĐường dẫn: {destPath}",
                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string err = await proc.StandardError.ReadToEndAsync();
                    DriverStatusText = "Lỗi xuất driver";
                    MessageBox.Show($"Backup driver thất bại.\n{err}",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DriverStatusText = $"Lỗi: {ex.Message}";
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDriverBusy = false;
            }
        }

        private async void HandleRestoreDriver()
        {
            if (IsDriverBusy) return;

            var dlg = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Chọn thư mục chứa driver đã backup"
            };

            if (dlg.ShowDialog() != true)
                return;

            string driverPath = dlg.FolderName;
            IsDriverBusy = true;
            DriverStatusText = "Đang cài đặt driver...";

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "pnputil",
                    Arguments = $"/add-driver \"{driverPath}\\*.inf\" /subdirs /install",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    Verb = "runas"
                };

                var proc = Process.Start(psi)!;
                await proc.WaitForExitAsync();

                string output = await proc.StandardOutput.ReadToEndAsync();

                if (proc.ExitCode == 0)
                {
                    DriverStatusText = "Đã cài đặt driver thành công";
                    MessageBox.Show("Restore driver thành công!\nKhởi động lại máy để hoàn tất.",
                        "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    string err = await proc.StandardError.ReadToEndAsync();
                    DriverStatusText = "Lỗi cài driver";
                    MessageBox.Show($"Restore driver thất bại.\n{err}",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            catch (Exception ex)
            {
                DriverStatusText = $"Lỗi: {ex.Message}";
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                IsDriverBusy = false;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
