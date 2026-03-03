using System.ComponentModel;
using System.Runtime.CompilerServices;
using LapLapAutoTool.Services;
using System.Windows;

namespace LapLapAutoTool.ViewModels
{
    public class MainWindowViewModel : INotifyPropertyChanged
    {
        private object? _currentView;
        private readonly IHardwareService _hardwareService;
        private readonly IInstallService _installService;
        private readonly ILicenseService _licenseService;
        private readonly ILogService _logService;

        public object? CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public string LicenseStatus
        {
            get
            {
                var license = LicensingService.CurrentLicense;
                if (license != null)
                {
                    if (license.Token == "ADMIN-PIN-292003") return "Đăng nhập Quyền Admin";
                    return "Đã kích hoạt (Vĩnh viễn)";
                }
                return "Chưa kích hoạt bản quyền";
            }
        }

        public bool IsLicensed => LicensingService.CurrentLicense != null;
        public string VersionText => "v1.0.0";

        public string MachineCode => LicensingService.CurrentLicense?.Hwid ?? (new LicensingService()).GetHWID();

        // ViewModels
        public HardwareViewModel HardwareVM { get; set; }
        public QuickSetupViewModel QuickSetupVM { get; set; }

        public UtilitiesViewModel UtilitiesVM { get; set; }
        public BackupViewModel BackupVM { get; set; }
        public SettingsViewModel SettingsVM { get; set; }
        public LaptopTestViewModel LaptopTestVM { get; set; }
        public UninstallViewModel UninstallVM { get; set; }
        public AssessmentViewModel AssessmentVM { get; set; }
        public DriverViewModel DriverVM { get; set; }

        public string CustomerGreeting
        {
            get
            {
                var license = LicensingService.CurrentLicense;
                if (license != null && !string.IsNullOrEmpty(license.CustomerName))
                {
                    return $"Xin chào, {license.CustomerName}";
                }
                return "";
            }
        }

        public Visibility GreetingVisibility => string.IsNullOrEmpty(CustomerGreeting) ? Visibility.Collapsed : Visibility.Visible;

        public MainWindowViewModel()
        {
            _logService = new LogService();
            _licenseService = new LicenseService();
            _hardwareService = new HardwareService(_logService);
            _installService = new InstallService(_logService);
            var downloadService = new DownloadService(_logService);

            HardwareVM = new HardwareViewModel(_hardwareService);
            QuickSetupVM = new QuickSetupViewModel(_installService, downloadService, _logService);

            UtilitiesVM = new UtilitiesViewModel(_installService);
            BackupVM = new BackupViewModel(_installService);
            SettingsVM = new SettingsViewModel();
            LaptopTestVM = new LaptopTestViewModel();
            UninstallVM = new UninstallViewModel();
            AssessmentVM = new AssessmentViewModel();
            DriverVM = new DriverViewModel(_installService, downloadService, _logService, HardwareVM);

            // Notify UI about status
            OnPropertyChanged(nameof(LicenseStatus));
            OnPropertyChanged(nameof(IsLicensed));
            OnPropertyChanged(nameof(CustomerGreeting));
            OnPropertyChanged(nameof(GreetingVisibility));
            OnPropertyChanged(nameof(MachineCode));

            // Default Tab
            CurrentView = HardwareVM;
            
            _logService.LogInfo("Ứng dụng đã khởi động với phiên bản " + VersionText);
        }

        public RelayCommand ShowHardwareCommand => new RelayCommand(() => CurrentView = HardwareVM);
        public RelayCommand ShowQuickSetupCommand => new RelayCommand(() => CurrentView = QuickSetupVM);

        public RelayCommand ShowUtilitiesCommand => new RelayCommand(() => CurrentView = UtilitiesVM);
        public RelayCommand ShowBackupCommand => new RelayCommand(() => CurrentView = BackupVM);
        public RelayCommand ShowSettingsCommand => new RelayCommand(() => CurrentView = SettingsVM);
        public RelayCommand ShowLaptopTestCommand  => new RelayCommand(() => CurrentView = LaptopTestVM);
        public RelayCommand ShowUninstallCommand   => new RelayCommand(() => CurrentView = UninstallVM);
        public RelayCommand ShowDriverCommand      => new RelayCommand(() => CurrentView = DriverVM);
        public RelayCommand ShowAssessmentCommand  => new RelayCommand(() =>
        {
            AssessmentVM.LoadFromSysInfo(HardwareVM.SysInfo);
            CurrentView = AssessmentVM;
        });
        public RelayCommand OpenLogCommand => new RelayCommand(() =>
        {
            try
            {
                string logPath = _logService.GetLogPath();
                if (System.IO.File.Exists(logPath))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = logPath,
                        UseShellExecute = true
                    });
                }
                else
                {
                    MessageBox.Show("Chưa có thông tin log nào được ghi lại.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể mở file log: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        });

        public RelayCommand ActivateLicenseCommand => new RelayCommand(() =>
        {
            // Simple approach for activation (Microsoft.VisualBasic.Interaction.InputBox is not available by default in .NET 8)
            // In a real app, you would use a custom WPF Window for input.
            // For now, let's simulate activation with a fixed key for demo purposes.
            string key = $"LAP-{MachineCode}"; 
            
            if (_licenseService.Activate(key))
            {
                OnPropertyChanged(nameof(LicenseStatus));
                OnPropertyChanged(nameof(IsLicensed));
                MessageBox.Show("Kích hoạt thành công! (Tự động cho bản demo)", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        });

        public RelayCommand CopyHWIDCommand => new RelayCommand(() =>
        {
            Clipboard.SetText(MachineCode);
            MessageBox.Show($"Mã máy HWID đã được sao chép vào bộ nhớ tạm: {MachineCode}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        });

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
