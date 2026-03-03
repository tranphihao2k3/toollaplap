using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using System.IO;
using System.Management;
using LapLapAutoTool.Models;
using LapLapAutoTool.Services;
using System;
using System.Diagnostics;

namespace LapLapAutoTool.ViewModels
{
    public class HardwareViewModel : INotifyPropertyChanged
    {
        private readonly IHardwareService _hardwareService;
        private HardwareInfo _sysInfo = new HardwareInfo();
        private bool _isLoading;
        private DispatcherTimer? _realtimeTimer;

        // ── Real-time values ──────────────────────────────────
        private double _cpuLoad;
        private double _cpuTemp;
        private double _ramUsedGb;
        private double _ramUtil;

        private string _batteryStatus = "Tính toán...";
        private string _batteryRate = "0 W";

        // ── Battery rate calculation via delta ──────────────────
        private long _lastRemainingCapacity = -1;
        private Stopwatch _lastPollStopwatch = new Stopwatch();

        public double CpuLoad   { get => _cpuLoad;   set { _cpuLoad   = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuLoadText)); } }
        public double CpuTemp   { get => _cpuTemp;   set { _cpuTemp   = value; OnPropertyChanged(); OnPropertyChanged(nameof(CpuTempText)); } }
        public double RamUsedGb { get => _ramUsedGb; set { _ramUsedGb = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamUtilText)); } }
        public double RamUtil   { get => _ramUtil;   set { _ramUtil   = value; OnPropertyChanged(); OnPropertyChanged(nameof(RamUtilText)); } }

        public string BatteryStatus { get => _batteryStatus; set { _batteryStatus = value; OnPropertyChanged(); } }
        public string BatteryRate   { get => _batteryRate;   set { _batteryRate   = value; OnPropertyChanged(); } }

        public string CpuLoadText => $"{CpuLoad:0}%";
        public string CpuTempText => $"{CpuTemp:0}°C";
        public string RamUtilText => $"{RamUsedGb:0.0} / {SysInfo.TotalRam} ({RamUtil:0}%)";
        // ─────────────────────────────────────────────────────

        public HardwareInfo SysInfo
        {
            get => _sysInfo;
            set { _sysInfo = value; OnPropertyChanged(); }
        }

        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public HardwareViewModel(IHardwareService hardwareService)
        {
            _hardwareService = hardwareService;
            LoadInfoCommand    = new RelayCommand(async () => await LoadSystemInfo());
            CopyInfoCommand    = new RelayCommand(CopyInfoToClipboard);
            CopySerialCommand  = new RelayCommand(() =>
            {
                Clipboard.SetText(SysInfo.SerialNumber);
                MessageBox.Show("Số Serial đã được sao chép!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            });
            ExportReportCommand = new RelayCommand(ExportReportToFile);
            _ = LoadSystemInfo();

            // Real-time polling every 2 seconds
            _realtimeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            _realtimeTimer.Tick += async (s, e) => await PollRealtimeAsync();
            _realtimeTimer.Start();
        }

        public RelayCommand LoadInfoCommand    { get; }
        public RelayCommand CopyInfoCommand    { get; }
        public RelayCommand CopySerialCommand  { get; }
        public RelayCommand ExportReportCommand { get; }

        private async Task LoadSystemInfo()
        {
            IsLoading = true;
            SysInfo = await _hardwareService.GetSystemInfoAsync();
            IsLoading = false;
            OnPropertyChanged(nameof(RamUtilText));
        }

        private async Task PollRealtimeAsync()
        {
            try
            {
                await Task.Run(() =>
                {
                    // CPU Load via WMI
                    try
                    {
                        using var s = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
                        foreach (ManagementObject o in s.Get())
                            CpuLoad = Convert.ToDouble(o["LoadPercentage"]);
                    }
                    catch { }

                    // CPU Temperature via WMI ACPI (requires admin on some systems)
                    try
                    {
                        using var s = new ManagementObjectSearcher(@"root\WMI",
                            "SELECT CurrentTemperature FROM MSAcpi_ThermalZoneTemperature");
                        double maxC = 0;
                        foreach (ManagementObject o in s.Get())
                        {
                            double c = Convert.ToDouble(o["CurrentTemperature"]) / 10.0 - 273.15;
                            if (c > maxC) maxC = c;
                        }
                        if (maxC > 0) CpuTemp = Math.Round(maxC, 1);
                    }
                    catch { }

                    // RAM Usage
                    try
                    {
                        using var s = new ManagementObjectSearcher(
                            "SELECT FreePhysicalMemory, TotalVisibleMemorySize FROM Win32_OperatingSystem");
                        foreach (ManagementObject o in s.Get())
                        {
                            double total = Convert.ToDouble(o["TotalVisibleMemorySize"]);
                            double free  = Convert.ToDouble(o["FreePhysicalMemory"]);
                            double used  = total - free;
                            RamUsedGb = Math.Round(used / 1024.0 / 1024.0, 1);
                            RamUtil   = Math.Round(used / total * 100, 0);
                        }
                    }
                    catch { }

                    // Battery Real-time Rate
                    try
                    {
                        using var s = new ManagementObjectSearcher(@"root\WMI",
                            "SELECT ChargeRate, DischargeRate, Charging, Discharging, RemainingCapacity FROM BatteryStatus");
                        foreach (ManagementObject o in s.Get())
                        {
                            bool isCharging = Convert.ToBoolean(o["Charging"]);
                            bool isDischarging = Convert.ToBoolean(o["Discharging"]);
                            long chargeRate = Convert.ToInt64(o["ChargeRate"] ?? 0);
                            long dischargeRate = Convert.ToInt64(o["DischargeRate"] ?? 0);
                            long remainingCapacity = Convert.ToInt64(o["RemainingCapacity"] ?? 0);

                            // Primary: use ChargeRate/DischargeRate from WMI
                            double rateW = 0;
                            bool hasDirectRate = false;

                            if (isCharging && chargeRate > 0)
                            {
                                rateW = chargeRate / 1000.0;
                                hasDirectRate = true;
                            }
                            else if (isDischarging && dischargeRate > 0)
                            {
                                rateW = dischargeRate / 1000.0;
                                hasDirectRate = true;
                            }

                            // Fallback: calculate rate from delta RemainingCapacity over time
                            if (!hasDirectRate && (isCharging || isDischarging) && remainingCapacity > 0)
                            {
                                if (_lastRemainingCapacity > 0 && _lastPollStopwatch.IsRunning)
                                {
                                    double elapsedHours = _lastPollStopwatch.Elapsed.TotalHours;
                                    if (elapsedHours > 0.0001) // avoid divide by zero
                                    {
                                        long deltaMwh = remainingCapacity - _lastRemainingCapacity;
                                        // deltaMwh is in mWh, convert to W: mWh / hours = mW, then /1000 = W
                                        rateW = Math.Abs(deltaMwh) / elapsedHours / 1000.0;
                                    }
                                }
                                _lastRemainingCapacity = remainingCapacity;
                                _lastPollStopwatch.Restart();
                            }
                            else if (hasDirectRate)
                            {
                                // Still track for future fallback
                                _lastRemainingCapacity = remainingCapacity;
                                _lastPollStopwatch.Restart();
                            }

                            if (isCharging)
                            {
                                BatteryStatus = "Đang sạc";
                                BatteryRate = rateW > 0.1 ? $"+{rateW:0.0} W" : "Tính toán...";
                            }
                            else if (isDischarging)
                            {
                                BatteryStatus = "Đang xả pin";
                                BatteryRate = rateW > 0.1 ? $"-{rateW:0.0} W" : "Tính toán...";
                            }
                            else
                            {
                                BatteryStatus = "Đã đầy / Không sạc";
                                BatteryRate = "0 W";
                                _lastRemainingCapacity = -1;
                                _lastPollStopwatch.Reset();
                            }
                        }
                    }
                    catch { }
                });
            }
            catch { }
        }

        private void CopyInfoToClipboard()
        {
            Clipboard.SetText(GetFormattedReport());
            MessageBox.Show("Thông tin phần cứng đã được sao chép vào bộ nhớ tạm!", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ExportReportToFile()
        {
            try
            {
                string fileName   = $"HardwareReport_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports");
                if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
                string filePath   = Path.Combine(folderPath, fileName);
                File.WriteAllText(filePath, GetFormattedReport());
                MessageBox.Show($"Báo cáo đã được xuất thành công tại:\n{filePath}", "Thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi xuất báo cáo: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private string GetFormattedReport()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("THONG TIN HE THONG  |  SYSTEM INFO");
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════");
            sb.AppendLine("");
            sb.AppendLine("[ CPU ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"    Ten CPU                     {SysInfo.CPU}");
            sb.AppendLine($"    Nhan / Luong                {SysInfo.CPUCoresThreads}");
            sb.AppendLine($"    Xung nhip Max               {SysInfo.CPUMaxSpeed}");
            sb.AppendLine($"    Architecture                {SysInfo.CPUArchitecture}");
            sb.AppendLine("");
            sb.AppendLine("[ RAM ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            foreach (var stick in SysInfo.RamSticks)
                sb.AppendLine($"    {stick.Slot,-28} {stick.Capacity}   RAM   {stick.Speed}   {stick.Manufacturer}");
            sb.AppendLine("");
            sb.AppendLine($"    Tong cong RAM               {SysInfo.TotalRam}");
            sb.AppendLine($"    RAM toi da ho tro           {SysInfo.MaxRamCapacity} {SysInfo.RamSlotsSummary}");
            sb.AppendLine("");
            sb.AppendLine("[ CARD DO HOA ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            foreach (var gpu in SysInfo.Gpus)
            {
                sb.AppendLine($"    {gpu.Name}");
                sb.AppendLine($"      VRAM                      {gpu.Vram}");
                sb.AppendLine($"      TDP toi da                {gpu.Tdp}");
                sb.AppendLine("");
            }
            sb.AppendLine("[ O CUNG ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            foreach (var storage in SysInfo.Storages)
            {
                sb.AppendLine($"    Model                       {storage.Model}");
                sb.AppendLine($"    Loai                        {storage.Type}");
                sb.AppendLine($"    Dung luong goc              {storage.Capacity}");
                if (storage.HasSmartInfo)
                {
                    sb.AppendLine($"    Suc khoe (Remaining)        {storage.HealthRemaining}%");
                    sb.AppendLine($"    Nhiet do (Temperature)      {storage.Temperature} °C");
                    sb.AppendLine($"    Du phong (Spare)            {storage.AvailableSpare}%");
                    sb.AppendLine($"    Thoi gian hoat dong         {storage.PowerOnDays} ngay");
                }
                sb.AppendLine("");
            }
            sb.AppendLine("[ PIN (BATTERY) ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            sb.AppendLine($"    Dung luong thiet ke         {SysInfo.BatteryDesignCapacity}");
            sb.AppendLine($"    Dung luong hien tai         {SysInfo.BatteryFullChargeCapacity}");
            string h = SysInfo.BatteryHealth;
            if (double.TryParse(h?.Replace("%", ""), out double hv))
                sb.AppendLine($"    Suc khoe pin                {h}  (Da chai: {100 - hv:0.0}%)");
            else
                sb.AppendLine($"    Suc khoe pin                {h ?? "N/A"}");
            sb.AppendLine($"    Pin hien tai                {SysInfo.BatteryCurrentPercent}");
            sb.AppendLine($"    So lan sac                  {SysInfo.BatteryCycles}");
            sb.AppendLine("");
            sb.AppendLine("[ WIFI DA LUU (*) ]");
            sb.AppendLine("────────────────────────────────────────────────────────────────────────────────");
            for (int i = 0; i < SysInfo.WiFiProfiles.Count; i++)
                sb.AppendLine($"      [{i + 1}]                       {SysInfo.WiFiProfiles[i]}");
            sb.AppendLine("");
            sb.AppendLine("════════════════════════════════════════════════════════════════════════════════");
            return sb.ToString();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
