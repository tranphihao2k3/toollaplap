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

    public class SoftwareItem : INotifyPropertyChanged
    {
        private InstallStatus _status = InstallStatus.Pending;
        private bool _isSelected = true;

        public string Name { get; set; } = string.Empty;
        public string FileName { get; set; } = string.Empty;
        public string SilentArgs { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public bool IsAsync { get; set; } = false;

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

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
