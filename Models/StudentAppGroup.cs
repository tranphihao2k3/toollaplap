using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LapLapAutoTool.Models
{
    public class StudentAppVersion : INotifyPropertyChanged
    {
        private DownloadStatus _downloadStatus = DownloadStatus.Idle;
        private double _downloadProgress;
        private string _downloadStatusText = "";

        public string Name { get; set; } = "";
        public string Year { get; set; } = "";
        public string DownloadUrl { get; set; } = "";
        public string FileSize { get; set; } = "";
        public string Password { get; set; } = "";

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
        protected void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }

    public class StudentAppGroup
    {
        public string Group { get; set; } = "";
        public string Icon { get; set; } = "";
        public List<StudentAppVersion> Versions { get; set; } = new();
    }
}
