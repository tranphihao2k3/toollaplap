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
        private string _licenseStatus = string.Empty;

        public object? CurrentView
        {
            get => _currentView;
            set { _currentView = value; OnPropertyChanged(); }
        }

        public string LicenseStatus
        {
            get => _licenseStatus;
            set { _licenseStatus = value; OnPropertyChanged(); }
        }

        public string MachineCode => _licenseService.GetMachineCode();

        // ViewModels
        public HardwareViewModel HardwareVM { get; set; }
        public QuickSetupViewModel QuickSetupVM { get; set; }
        public StudentAppsViewModel StudentAppsVM { get; set; }
        public UtilitiesViewModel UtilitiesVM { get; set; }

        public MainWindowViewModel()
        {
            _logService = new LogService();
            _licenseService = new LicenseService();
            _hardwareService = new HardwareService();
            _installService = new InstallService(_logService);

            HardwareVM = new HardwareViewModel(_hardwareService);
            QuickSetupVM = new QuickSetupViewModel(_installService);
            StudentAppsVM = new StudentAppsViewModel(_installService);
            UtilitiesVM = new UtilitiesViewModel(_installService);

            LicenseStatus = _licenseService.GetLicenseStatus();

            // Default Tab
            CurrentView = HardwareVM;
            
            _logService.LogInfo("Ứng dụng đã khởi động.");
        }

        public RelayCommand ShowHardwareCommand => new RelayCommand(() => CurrentView = HardwareVM);
        public RelayCommand ShowQuickSetupCommand => new RelayCommand(() => CurrentView = QuickSetupVM);
        public RelayCommand ShowStudentAppsCommand => new RelayCommand(() => CurrentView = StudentAppsVM);
        public RelayCommand ShowUtilitiesCommand => new RelayCommand(() => CurrentView = UtilitiesVM);

        public RelayCommand ActivateLicenseCommand => new RelayCommand(() =>
        {
            // Simple approach for activation (Microsoft.VisualBasic.Interaction.InputBox is not available by default in .NET 8)
            // In a real app, you would use a custom WPF Window for input.
            // For now, let's simulate activation with a fixed key for demo purposes.
            string key = $"LAP-{MachineCode}"; 
            
            if (_licenseService.Activate(key))
            {
                LicenseStatus = _licenseService.GetLicenseStatus();
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
