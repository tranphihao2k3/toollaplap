using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace LapLapAutoTool.ViewModels
{
    public class InstalledAppItem : INotifyPropertyChanged
    {
        public string Name { get; set; } = "";
        public string Publisher { get; set; } = "";
        public string Version { get; set; } = "";
        public string InstallDate { get; set; } = "";
        public string UninstallString { get; set; } = "";
        public string EstimatedSize { get; set; } = "";
        public string DisplayIconPath { get; set; } = "";

        private ImageSource? _iconSource;
        public ImageSource? IconSource
        {
            get
            {
                if (_iconSource == null && !string.IsNullOrEmpty(DisplayIconPath))
                    _iconSource = ExtractIcon(DisplayIconPath);
                return _iconSource;
            }
        }

        private static ImageSource? ExtractIcon(string iconPath)
        {
            try
            {
                // Tách path và index (vd: "C:\app.exe,0" hoặc "C:\app.ico")
                var path = iconPath.Trim('"');
                int commaIdx = path.LastIndexOf(',');
                if (commaIdx > 0)
                    path = path.Substring(0, commaIdx).Trim('"');

                if (!File.Exists(path)) return null;

                using var icon = Icon.ExtractAssociatedIcon(path);
                if (icon == null) return null;

                var bmpSource = Imaging.CreateBitmapSourceFromHIcon(
                    icon.Handle,
                    Int32Rect.Empty,
                    BitmapSizeOptions.FromEmptyOptions());
                bmpSource.Freeze();
                return bmpSource;
            }
            catch
            {
                return null;
            }
        }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class UninstallViewModel : INotifyPropertyChanged
    {
        private ObservableCollection<InstalledAppItem> _allApps = new();
        private ObservableCollection<InstalledAppItem> _filteredApps = new();
        private string _searchText = "";
        private bool _isLoading;
        private int _selectedCount;

        public ObservableCollection<InstalledAppItem> FilteredApps
        {
            get => _filteredApps;
            private set { _filteredApps = value; OnPropertyChanged(); }
        }

        public string SearchText
        {
            get => _searchText;
            set { _searchText = value; OnPropertyChanged(); ApplyFilter(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public int SelectedCount
        {
            get => _selectedCount;
            set { _selectedCount = value; OnPropertyChanged(); }
        }

        public string AppCountText => $"{FilteredApps.Count} phần mềm";

        public RelayCommand LoadAppsCommand { get; }
        public RelayCommand UninstallSelectedCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }

        public UninstallViewModel()
        {
            LoadAppsCommand = new RelayCommand(() => LoadInstalledApps());
            UninstallSelectedCommand = new RelayCommand(() => UninstallSelected());
            SelectAllCommand = new RelayCommand(() =>
            {
                foreach (var app in FilteredApps) app.IsSelected = true;
                UpdateSelectedCount();
            });
            ClearSelectionCommand = new RelayCommand(() =>
            {
                foreach (var app in FilteredApps) app.IsSelected = false;
                UpdateSelectedCount();
            });

            LoadInstalledApps();
        }

        public void ToggleSelection(InstalledAppItem item)
        {
            item.IsSelected = !item.IsSelected;
            UpdateSelectedCount();
        }

        private void UpdateSelectedCount()
        {
            SelectedCount = _allApps.Count(a => a.IsSelected);
        }

        private async void LoadInstalledApps()
        {
            IsLoading = true;
            _allApps.Clear();
            FilteredApps.Clear();

            var apps = await System.Threading.Tasks.Task.Run(() =>
            {
                var resultList = new List<InstalledAppItem>();
                var regKeys = new[]
                {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var keyPath in regKeys)
                {
                    foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
                    {
                        try
                        {
                            using var key = hive.OpenSubKey(keyPath);
                            if (key == null) continue;

                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using var sub = key.OpenSubKey(subKeyName);
                                if (sub == null) continue;

                                var name = sub.GetValue("DisplayName")?.ToString();
                                var uninstall = sub.GetValue("UninstallString")?.ToString();

                                if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(uninstall)) continue;
                                if (name.StartsWith("KB") || name.Contains("Update for") || name.Contains("Hotfix")) continue;

                                var sizeKb = sub.GetValue("EstimatedSize");
                                string sizeStr = "";
                                if (sizeKb != null && long.TryParse(sizeKb.ToString(), out long kb))
                                    sizeStr = kb > 1024 ? $"{kb / 1024} MB" : $"{kb} KB";

                                var dateStr = sub.GetValue("InstallDate")?.ToString() ?? "";
                                if (dateStr.Length == 8 && DateTime.TryParseExact(dateStr, "yyyyMMdd",
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    System.Globalization.DateTimeStyles.None, out var dt))
                                    dateStr = dt.ToString("dd/MM/yyyy");

                                var displayIcon = sub.GetValue("DisplayIcon")?.ToString() ?? "";
                                if (string.IsNullOrEmpty(displayIcon))
                                {
                                    var installLoc = sub.GetValue("InstallLocation")?.ToString();
                                    if (!string.IsNullOrEmpty(installLoc))
                                    {
                                        try
                                        {
                                            var exes = Directory.GetFiles(installLoc, "*.exe", SearchOption.TopDirectoryOnly);
                                            if (exes.Length > 0) displayIcon = exes[0];
                                        }
                                        catch { }
                                    }
                                }

                                resultList.Add(new InstalledAppItem
                                {
                                    Name = name,
                                    Publisher = sub.GetValue("Publisher")?.ToString() ?? "—",
                                    Version = sub.GetValue("DisplayVersion")?.ToString() ?? "—",
                                    InstallDate = dateStr,
                                    UninstallString = uninstall,
                                    EstimatedSize = sizeStr,
                                    DisplayIconPath = displayIcon
                                });
                            }
                        }
                        catch { /* ignore registry errors */ }
                    }
                }

                return resultList.OrderBy(a => a.Name).DistinctBy(a => a.Name).ToList();
            });

            _allApps = new ObservableCollection<InstalledAppItem>(apps);
            ApplyFilter();
            IsLoading = false;
        }

        private void ApplyFilter()
        {
            var filtered = string.IsNullOrWhiteSpace(_searchText)
                ? _allApps.ToList()
                : _allApps.Where(a =>
                    a.Name.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ||
                    a.Publisher.Contains(_searchText, StringComparison.OrdinalIgnoreCase)).ToList();

            FilteredApps = new ObservableCollection<InstalledAppItem>(filtered);
            OnPropertyChanged(nameof(AppCountText));
        }

        private void UninstallSelected()
        {
            var selected = _allApps.Where(a => a.IsSelected).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Chưa chọn phần mềm nào để gỡ cài đặt.",
                    "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var names = string.Join("\n• ", selected.Select(a => a.Name));
            var result = MessageBox.Show(
                $"Xác nhận gỡ cài đặt {selected.Count} phần mềm sau?\n\n• {names}",
                "Xác nhận gỡ cài đặt", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            foreach (var app in selected)
            {
                try
                {
                    var cmd = app.UninstallString;
                    if (cmd.StartsWith("MsiExec", StringComparison.OrdinalIgnoreCase))
                    {
                        // MSI uninstall
                        var productCode = System.Text.RegularExpressions.Regex
                            .Match(cmd, @"\{[^}]+\}").Value;
                        if (!string.IsNullOrEmpty(productCode))
                            Process.Start("msiexec.exe", $"/x {productCode} /qb");
                    }
                    else
                    {
                        // EXE uninstall – run via cmd
                        Process.Start(new ProcessStartInfo("cmd.exe", $"/c \"{cmd}\"")
                        {
                            UseShellExecute = true,
                            CreateNoWindow = false
                        });
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Lỗi khi gỡ '{app.Name}': {ex.Message}",
                        "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            // Reload after short delay
            System.Threading.Tasks.Task.Delay(3000).ContinueWith(_ =>
                Application.Current.Dispatcher.Invoke(LoadInstalledApps));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
