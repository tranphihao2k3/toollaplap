using System;
using System.Collections.Generic;
using System.ComponentModel;
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

        public RelayCommand BackupDataCommand { get; }

        public BackupViewModel(IInstallService installService)
        {
            _installService = installService;
            InitializeBackupFolders();
            BackupDataCommand = new RelayCommand(HandleBackup);
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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
