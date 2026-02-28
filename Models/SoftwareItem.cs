using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LapLapAutoTool.Models
{
    public enum InstallStatus
    {
        Pending,
        Installing,
        Completed,
        Failed,
        Skipped
    }

    public enum DownloadStatus
    {
        Idle,
        Downloading,
        Extracting,
        Done,
        Failed
    }

    public class SoftwareItem : INotifyPropertyChanged
    {
        private InstallStatus _status = InstallStatus.Pending;
        private bool _isSelected = true;
        private bool _isAlreadyInstalled;
        private DownloadStatus _downloadStatus = DownloadStatus.Idle;
        private double _downloadProgress;
        private string _downloadStatusText = "";

        // === Thông tin cài đặt local (cũ) ===
        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string SilentArgs { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAsync { get; set; } = false;

        // === Thông tin download (mới) ===
        public string DownloadUrl { get; set; } = string.Empty;
        public string Category { get; set; } = "general"; // "general" | "student"
        public string FileSize { get; set; } = "";        // VD: "2.4 GB" — chỉ để hiển thị
        public string LocalInstallerPath { get; set; } = string.Empty; // Đường dẫn file sau khi tải/có sẵn

        // === Trạng thái UI ===
        public bool IsAlreadyInstalled
        {
            get => _isAlreadyInstalled;
            set { _isAlreadyInstalled = value; OnPropertyChanged(); }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public InstallStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }

        public DownloadStatus DownloadStatus
        {
            get => _downloadStatus;
            set { _downloadStatus = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsDownloading)); OnPropertyChanged(nameof(CanDownload)); }
        }

        public double DownloadProgress
        {
            get => _downloadProgress;
            set { _downloadProgress = value; OnPropertyChanged(); }
        }

        public string DownloadStatusText
        {
            get => _downloadStatusText;
            set { _downloadStatusText = value; OnPropertyChanged(); }
        }

        public bool IsDownloading => DownloadStatus == DownloadStatus.Downloading || DownloadStatus == DownloadStatus.Extracting;
        public bool CanDownload => !string.IsNullOrEmpty(DownloadUrl) && !IsDownloading;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
