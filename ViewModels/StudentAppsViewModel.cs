using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class StudentAppsViewModel : INotifyPropertyChanged
    {
        private readonly IDownloadService _downloadService;
        private string _statusText = "Chọn phiên bản và nhấn Tải về";

        public ObservableCollection<StudentAppGroup> AppGroups { get; set; } = new();

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }

        public StudentAppsViewModel(IDownloadService downloadService)
        {
            _downloadService = downloadService;
            LoadApps();
        }

        private void LoadApps()
        {
            try
            {
                string json = null;
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "student_config.json");
                if (File.Exists(configPath))
                {
                    json = File.ReadAllText(configPath);
                }
                else
                {
                    var resourceUri = new Uri("pack://application:,,,/Resources/student_config.json");
                    var info = Application.GetResourceStream(resourceUri);
                    if (info != null)
                    {
                        using (var reader = new StreamReader(info.Stream))
                        {
                            json = reader.ReadToEnd();
                        }
                    }
                }

                if (!string.IsNullOrEmpty(json))
                {
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var groups = JsonSerializer.Deserialize<System.Collections.Generic.List<StudentAppGroup>>(json, options);
                    if (groups != null)
                    {
                        foreach (var g in groups)
                            AppGroups.Add(g);
                        return;
                    }
                }
            }
            catch { }

            // Fallback
            AppGroups.Add(new StudentAppGroup { Group = "Adobe Photoshop", Icon = "🎨", Versions = new() {
                new StudentAppVersion { Name = "Adobe Photoshop 2024", Year = "2024" },
                new StudentAppVersion { Name = "Adobe Photoshop 2023", Year = "2023" }
            }});
        }

        public RelayCommand CreateDownloadCommand(StudentAppVersion item)
            => new RelayCommand(async () => await DownloadApp(item));

        private async Task DownloadApp(StudentAppVersion item)
        {
            if (item.IsDownloading) return;

            if (string.IsNullOrWhiteSpace(item.DownloadUrl))
            {
                MessageBox.Show(
                    $"Chưa có link tải cho '{item.Name}'.",
                    "Chưa có link", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Dùng tên app làm tên file tạm — KHÔNG gán extension cứng
            // DownloadService sẽ tự detect extension từ Content-Disposition hoặc Content-Type
            string fileName = GetFileNameFromUrl(item.DownloadUrl);
            if (string.IsNullOrEmpty(fileName))
                fileName = item.Name.Replace(" ", "_");

            item.DownloadStatus = DownloadStatus.Downloading;
            item.DownloadProgress = 0;
            item.DownloadStatusText = "Đang kết nối...";

            var progress = new Progress<(double percent, string status)>(report =>
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    item.DownloadProgress = report.percent >= 0 ? report.percent : 0;
                    item.DownloadStatusText = report.status;
                });
            });

            string? filePath = await _downloadService.DownloadFileAsync(item.DownloadUrl, fileName, progress);

            if (filePath == null)
            {
                item.DownloadStatus = DownloadStatus.Failed;
                item.DownloadStatusText = "Tải thất bại!";
                return;
            }

            string ext = Path.GetExtension(filePath).ToLower();
            string? finalFolder;

            if (ext == ".zip" || ext == ".rar" || ext == ".7z")
            {
                item.DownloadStatus = DownloadStatus.Extracting;
                item.DownloadStatusText = "Đang giải nén...";

                var extractProgress = new Progress<(double percent, string status)>(report =>
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        item.DownloadProgress = report.percent;
                        item.DownloadStatusText = report.status;
                    });
                });

                finalFolder = await _downloadService.ExtractArchiveAsync(filePath, extractProgress, item.Password);
            }
            else
            {
                finalFolder = Path.GetDirectoryName(filePath);
            }

            item.DownloadStatus = DownloadStatus.Done;
            item.DownloadProgress = 100;
            item.DownloadStatusText = "Hoàn tất! Mở thư mục...";

            if (!string.IsNullOrEmpty(finalFolder))
                _downloadService.OpenFolderInExplorer(finalFolder);
        }

        private static string GetFileNameFromUrl(string url)
        {
            try
            {
                if (url.Contains("drive.google.com")) return "";
                return Path.GetFileName(new Uri(url).AbsolutePath);
            }
            catch { return ""; }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? p = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
    }
}
