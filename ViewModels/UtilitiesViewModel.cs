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
    public class UtilitiesViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private bool _isBusy;
        private string _statusText = "Đang chuẩn bị...";

        private string _windowsUpdateText = "Kiểm tra...";
        private string _defenderText = "Kiểm tra...";
        private string _fastBootText = "Kiểm tra...";
        private string _sleepText = "Kiểm tra...";
        private string _powerPlanText = "Kiểm tra...";

        private bool _isWUEnabled;
        private bool _isDefEnabled;
        private bool _isFBEnabled;

        public bool IsWUEnabled
        {
            get => _isWUEnabled;
            set { _isWUEnabled = value; OnPropertyChanged(); }
        }

        public bool IsDefEnabled
        {
            get => _isDefEnabled;
            set { _isDefEnabled = value; OnPropertyChanged(); }
        }

        public bool IsFBEnabled
        {
            get => _isFBEnabled;
            set { _isFBEnabled = value; OnPropertyChanged(); }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public string WindowsUpdateText
        {
            get => _windowsUpdateText;
            set { _windowsUpdateText = value; OnPropertyChanged(); }
        }

        public string DefenderText
        {
            get => _defenderText;
            set { _defenderText = value; OnPropertyChanged(); }
        }

        public string FastBootText
        {
            get => _fastBootText;
            set { _fastBootText = value; OnPropertyChanged(); }
        }

        public string SleepText
        {
            get => _sleepText;
            set { _sleepText = value; OnPropertyChanged(); }
        }

        public string PowerPlanText
        {
            get => _powerPlanText;
            set { _powerPlanText = value; OnPropertyChanged(); }
        }

        private bool _isSleepDisabled;
        public bool IsSleepDisabled
        {
            get => _isSleepDisabled;
            set { _isSleepDisabled = value; OnPropertyChanged(); }
        }

        private bool _isHighPerf;
        public bool IsHighPerf
        {
            get => _isHighPerf;
            set { _isHighPerf = value; OnPropertyChanged(); }
        }

        public UtilitiesViewModel(IInstallService installService)
        {
            _installService = installService;

            RefreshStates();

            // === Card trái: Tối ưu ===
            DisableUpdateCommand = new RelayCommand(async () => {
                bool isEnabled = _installService.IsWindowsUpdateEnabled();
                await RunTask(isEnabled ? "Tắt Windows Update" : "Bật Windows Update",
                    () => isEnabled ? _installService.DisableWindowsUpdate() : _installService.EnableWindowsUpdate());
                RefreshStates();
            });

            DisableDefenderCommand = new RelayCommand(async () => {
                bool isEnabled = _installService.IsDefenderEnabled();
                await RunTask(isEnabled ? "Tắt Windows Defender" : "Bật Windows Defender",
                    () => isEnabled ? _installService.DisableDefender() : _installService.EnableDefender());
                RefreshStates();
            });

            DisableFastBootCommand = new RelayCommand(async () => {
                bool isEnabled = _installService.IsFastBootEnabled();
                await RunTask(isEnabled ? "Tắt Fast Boot" : "Bật Fast Boot",
                    () => isEnabled ? _installService.DisableFastBoot() : _installService.EnableFastBoot());
                RefreshStates();
            });

            CleanTempCommand = new RelayCommand(async () => await RunTask("Dọn dẹp file tạm", () => _installService.CleanTempFiles()));
            SetupTimeAndRegionCommand = new RelayCommand(async () => await RunTask("Cài đặt Timezone (UTC+7) & Region (UK)", () => _installService.SetupTimezoneAndRegion()));
            ShowDesktopIconsCommand = new RelayCommand(async () => await RunTask("Hiện icons (This PC, Recycle, Control)", () => _installService.ShowModernDesktopIcons()));

            // === Card phải: Cài đặt & Bảo trì ===
            ActivateWindowsCommand = new RelayCommand(async () => await RunTask("Kích hoạt Windows (KMS)", () => _installService.ActivateWindows()));
            ActivateOfficeCommand = new RelayCommand(async () => await RunTask("Kích hoạt Office (MAS)", () => _installService.ActivateOffice()));

            DisableTelemetryCommand = new RelayCommand(async () => await RunTask("Tắt Telemetry & Tracking", () => _installService.DisableTelemetry()));

            FlushDnsCommand = new RelayCommand(async () => await RunTask("Flush DNS & Reset Network", () => _installService.FlushDnsAndResetNetwork()));

            TogglePowerPlanCommand = new RelayCommand(async () => {
                bool isHighPerf = _installService.GetCurrentPowerPlan() == "High Performance";
                await RunTask(isHighPerf ? "Chuyển sang Balanced" : "Chuyển sang High Performance",
                    () => isHighPerf ? _installService.SetBalancedPower() : _installService.SetHighPerformancePower());
                RefreshStates();
            });

            ToggleSleepCommand = new RelayCommand(async () => {
                await RunTask(_isSleepDisabled ? "Bật Sleep & Hibernate" : "Tắt Sleep & Hibernate",
                    () => _isSleepDisabled ? _installService.EnableSleepAndHibernate() : _installService.DisableSleepAndHibernate());
                RefreshStates();
            });

            RunSfcDismCommand = new RelayCommand(async () => {
                if (IsBusy) return;
                IsBusy = true;
                StatusText = "Đang chạy SFC & DISM (có thể mất vài phút)...";
                var progress = new Progress<string>(msg => {
                    Application.Current.Dispatcher.Invoke(() => StatusText = msg);
                });
                string result = await _installService.RunSfcAndDismAsync(progress);
                IsBusy = false;

                // Save log to Reports folder
                try {
                    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                    if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
                    string file = Path.Combine(folder, $"SFC_DISM_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                    File.WriteAllText(file, result);
                    MessageBox.Show($"Hoàn tất! Kết quả đã lưu tại:\n{file}", "SFC & DISM", MessageBoxButton.OK, MessageBoxImage.Information);
                } catch {
                    MessageBox.Show(result.Length > 500 ? result[..500] + "..." : result, "SFC & DISM - Kết quả", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            });

            // Quick-open system tools
            OpenDeviceManagerCommand  = new RelayCommand(() => _installService.OpenSystemTool("devmgmt.msc"));
            OpenDiskMgmtCommand       = new RelayCommand(() => _installService.OpenSystemTool("diskmgmt.msc"));
            OpenMsconfigCommand       = new RelayCommand(() => _installService.OpenSystemTool("msconfig"));
            OpenEventViewerCommand    = new RelayCommand(() => _installService.OpenSystemTool("eventvwr.msc"));
            OpenTaskManagerCommand    = new RelayCommand(() => _installService.OpenSystemTool("taskmgr"));
            OpenSystemPropsCommand    = new RelayCommand(() => _installService.OpenSystemTool("sysdm.cpl"));

            // === BitLocker ===
            DisableBitLockerCommand = new RelayCommand(async () => await RunTask("Tắt BitLocker trên tất cả ổ đĩa", () => _installService.DisableBitLocker()));
        }

        private void RefreshStates()
        {
            Task.Run(() => {
                bool wu  = _installService.IsWindowsUpdateEnabled();
                bool def = _installService.IsDefenderEnabled();
                bool fb  = _installService.IsFastBootEnabled();
                string plan = _installService.GetCurrentPowerPlan();

                Application.Current.Dispatcher.Invoke(() => {
                    IsWUEnabled = wu;
                    IsDefEnabled = def;
                    IsFBEnabled = fb;

                    WindowsUpdateText = wu  ? "Tắt Windows Update"   : "Bật Windows Update";
                    DefenderText      = def ? "Tắt Windows Defender"  : "Bật Windows Defender";
                    FastBootText      = fb  ? "Tắt Fast Boot"         : "Bật Fast Boot";

                    IsHighPerf    = plan == "High Performance";
                    PowerPlanText = IsHighPerf ? "Power Plan: High Performance  →  Chuyển Balanced" : "Power Plan: Balanced  →  Chuyển High Performance";

                    // Sleep: we track via flag (no simple registry read — assume disabled if user toggled)
                    SleepText = _isSleepDisabled ? "Bật Sleep & Hibernate" : "Tắt Sleep & Hibernate (Setup)";
                });
            });
        }

        // Card trái
        public RelayCommand DisableUpdateCommand   { get; }
        public RelayCommand DisableDefenderCommand { get; }
        public RelayCommand DisableFastBootCommand { get; }
        public RelayCommand CleanTempCommand       { get; }
        public RelayCommand SetupTimeAndRegionCommand { get; }
        public RelayCommand ShowDesktopIconsCommand   { get; }

        // Card phải
        public RelayCommand ActivateWindowsCommand   { get; }
        public RelayCommand ActivateOfficeCommand    { get; }
        public RelayCommand DisableTelemetryCommand  { get; }
        public RelayCommand FlushDnsCommand          { get; }
        public RelayCommand TogglePowerPlanCommand   { get; }
        public RelayCommand ToggleSleepCommand       { get; }
        public RelayCommand RunSfcDismCommand        { get; }

        // Quick open tools
        public RelayCommand OpenDeviceManagerCommand { get; }
        public RelayCommand OpenDiskMgmtCommand      { get; }
        public RelayCommand OpenMsconfigCommand      { get; }
        public RelayCommand OpenEventViewerCommand   { get; }
        public RelayCommand OpenTaskManagerCommand   { get; }
        public RelayCommand OpenSystemPropsCommand   { get; }

        // BitLocker
        public RelayCommand DisableBitLockerCommand { get; }

        private async Task RunTask(string description, Func<bool> action)
        {
            if (IsBusy) return;
            IsBusy = true;
            StatusText = $"Đang thực hiện: {description}";

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
