using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class StudentAppsViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private double _progress;
        private string _statusText = "Sẵn sàng cài đặt phần mềm sinh viên";
        private bool _isBusy;
        private long _totalSizeMb;
        private int _estimatedTimeMin;

        public ObservableCollection<SoftwareItem> ProfessionalApps { get; set; }
        public ObservableCollection<SoftwareItem> AcademicApps { get; set; }

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

        public bool IsBusy
        {
            get => _isBusy;
            set { _isBusy = value; OnPropertyChanged(); }
        }

        public long TotalSizeMb
        {
            get => _totalSizeMb;
            set { _totalSizeMb = value; OnPropertyChanged(); }
        }

        public int EstimatedTimeMin
        {
            get => _estimatedTimeMin;
            set { _estimatedTimeMin = value; OnPropertyChanged(); }
        }

        public StudentAppsViewModel(IInstallService installService)
        {
            _installService = installService;
            
            ProfessionalApps = new ObservableCollection<SoftwareItem>
            {
                new SoftwareItem { Name = "Adobe Photoshop 2024", Description = "Thiết kế đồ họa & chỉnh sửa ảnh", IsSelected=false },
                new SoftwareItem { Name = "AutoCAD 2024", Description = "Thiết kế CAD 2D và 3D", IsSelected=false },
                new SoftwareItem { Name = "Adobe Premiere Pro", Description = "Phần mềm chỉnh sửa video", IsSelected=false }
            };

            AcademicApps = new ObservableCollection<SoftwareItem>
            {
                new SoftwareItem { Name = "IBM SPSS Statistics", Description = "Phần mềm phân tích thống kê", IsSelected=false },
                new SoftwareItem { Name = "Visual Studio Code", Description = "Trình soạn thảo mã nguồn tối ưu", IsSelected=false },
                new SoftwareItem { Name = "Stata 17", Description = "Phần mềm khoa học dữ liệu", IsSelected=false }
            };

            foreach (var item in ProfessionalApps) item.PropertyChanged += Item_PropertyChanged;
            foreach (var item in AcademicApps) item.PropertyChanged += Item_PropertyChanged;

            StartInstallCommand = new RelayCommand(async () => await RunInstallation());
            UpdateEstimates();
        }

        private void Item_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SoftwareItem.IsSelected))
            {
                UpdateEstimates();
            }
        }

        private void UpdateEstimates()
        {
            int selectedCount = ProfessionalApps.Count(x => x.IsSelected) + AcademicApps.Count(x => x.IsSelected);
            TotalSizeMb = selectedCount * 512; // Giả định trung bình 512MB/app
            EstimatedTimeMin = selectedCount * 5; // Giả định 5 phút/app
        }

        public RelayCommand StartInstallCommand { get; }

        private async Task RunInstallation()
        {
            if (IsBusy) return;
            IsBusy = true;
            Progress = 0;

            var allApps = ProfessionalApps.Concat(AcademicApps).Where(x => x.IsSelected).ToList();
            if (!allApps.Any())
            {
                StatusText = "Chưa chọn ứng dụng nào!";
                IsBusy = false;
                return;
            }

            int count = 0;
            foreach (var app in allApps)
            {
                StatusText = $"Đang cài đặt {app.Name}...";
                app.Status = InstallStatus.Installing;
                
                await Task.Delay(2000); // Demo delay
                
                app.Status = InstallStatus.Completed;
                count++;
                Progress = (double)count / allApps.Count * 100;
            }

            StatusText = "Cài đặt đã hoàn tất! ✅";
            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
