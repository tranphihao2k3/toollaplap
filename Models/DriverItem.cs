using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace LapLapAutoTool.Models
{
    public enum DriverInstallStatus
    {
        Pending,
        Downloading,
        Extracting,
        Installing,
        Completed,
        Failed
    }

    public class DriverItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private DriverInstallStatus _status = DriverInstallStatus.Pending;
        private double _progress;
        private string _statusText = "";

        public string Name { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string DownloadUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;

        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public DriverInstallStatus Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusDisplay)); }
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

        public string StatusDisplay => Status switch
        {
            DriverInstallStatus.Pending => "",
            DriverInstallStatus.Downloading => "Dang tai...",
            DriverInstallStatus.Extracting => "Giai nen...",
            DriverInstallStatus.Installing => "Dang cai...",
            DriverInstallStatus.Completed => "Hoan tat",
            DriverInstallStatus.Failed => "That bai",
            _ => ""
        };

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
