using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class UtilitiesViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private bool _isBusy;

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public UtilitiesViewModel(IInstallService installService)
        {
            _installService = installService;
            
            DisableUpdateCommand = new RelayCommand(async () => await RunTask("Tắt Windows Update", () => _installService.DisableWindowsUpdate()));
            DisableDefenderCommand = new RelayCommand(async () => await RunTask("Tắt Defender Realtime", () => _installService.DisableDefender()));
            DisableFastBootCommand = new RelayCommand(async () => await RunTask("Tắt Fast Boot", () => _installService.DisableFastBoot()));
            CleanTempCommand = new RelayCommand(async () => await RunTask("Dọn dẹp file tạm", () => _installService.CleanTempFiles()));
        }

        public RelayCommand DisableUpdateCommand { get; }
        public RelayCommand DisableDefenderCommand { get; }
        public RelayCommand DisableFastBootCommand { get; }
        public RelayCommand CleanTempCommand { get; }

        private async Task RunTask(string description, Func<bool> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            
            bool success = await Task.Run(() => action());
            
            IsBusy = false;
            
            if (success)
                MessageBox.Show($"{description} đã hoàn thành thành công!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            else
                MessageBox.Show($"{description} thất bại hoặc yêu cầu quyền Quản trị viên.", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
