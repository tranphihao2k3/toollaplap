using System;
using System.IO;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class QuickSetupViewModel : INotifyPropertyChanged
    {
        private readonly IInstallService _installService;
        private double _progress;
        private string _statusText = "Sẵn sàng để bắt đầu";
        private bool _isBusy;

        public ObservableCollection<SoftwareItem> SoftwareList { get; set; }

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

        public QuickSetupViewModel(IInstallService installService)
        {
            _installService = installService;
            SoftwareList = new ObservableCollection<SoftwareItem>();
            LoadSoftwareList();

            StartInstallCommand = new RelayCommand(async () => await RunInstallationSequence());
        }

        private void LoadSoftwareList()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "software_config.json");
                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);
                    var items = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<SoftwareItem>>(json);
                    if (items != null)
                    {
                        foreach (var item in items) SoftwareList.Add(item);
                        return;
                    }
                }
            }
            catch { /* Fallback to defaults */ }

            // Default fallback if JSON fails
            SoftwareList.Add(new SoftwareItem { Name = "UnikeyNT", FileName = "unikey.exe", SilentArgs = "/S", Description = "Bộ gõ tiếng Việt phổ biến" });
            SoftwareList.Add(new SoftwareItem { Name = "UltraViewer", FileName = "UltraViewer_setup.exe", SilentArgs = "/S", Description = "Công cụ điều khiển máy tính từ xa" });
            SoftwareList.Add(new SoftwareItem { Name = "VLC Media Player", FileName = "vlc_setup.exe", SilentArgs = "/S", Description = "Trình phát đa phương tiện miễn phí" });
        }

        public RelayCommand StartInstallCommand { get; }

        private async Task RunInstallationSequence()
        {
            if (IsBusy) return;
            IsBusy = true;
            Progress = 0;
            var selectedItems = SoftwareList.Where(s => s.IsSelected).ToList();
            
            if (selectedItems.Count == 0)
            {
                StatusText = "Chưa chọn phần mềm nào!";
                IsBusy = false;
                return;
            }

            int completed = 0;
            foreach (var item in selectedItems)
            {
                StatusText = $"Đang cài đặt {item.Name}...";
                item.Status = InstallStatus.Installing;
                
                // Gọi service cài đặt, nếu IsAsync = true thì sẽ không đợi kết quả mà sang app tiếp theo luôn
                bool success = await _installService.InstallAsync(item.FileName, item.SilentArgs, !item.IsAsync);
                
                // Giảm độ trễ demo xuống 1s
                await Task.Delay(1000); 

                item.Status = success ? InstallStatus.Completed : InstallStatus.Failed;
                completed++;
                Progress = (double)completed / selectedItems.Count * 100;
            }

            StatusText = "Tất cả tác vụ đã hoàn tất! ✅";
            IsBusy = false;
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
