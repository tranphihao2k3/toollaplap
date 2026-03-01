using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using LapLapAutoTool.Models;
using QRCoder;
using System.Net;

namespace LapLapAutoTool.ViewModels
{
    public class AssessmentViewModel : INotifyPropertyChanged
    {
        // ── Thông tin máy (tự điền từ HW) ────────────────────────────
        private string _cpuName = "";
        private string _ramSize = "";
        private string _ramBus = "";
        private string _gpuName = "";
        private string _gpuVram = "";
        private string _ssdSize = "";
        private string _batteryHealth = "";
        private string _serialNumber = "";
        private string _modelName = "";

        public string CpuName    { get => _cpuName;    set { _cpuName = value;    OnPropertyChanged(); } }
        public string RamSize    { get => _ramSize;    set { _ramSize = value;    OnPropertyChanged(); } }
        public string RamBus     { get => _ramBus;     set { _ramBus = value;     OnPropertyChanged(); } }
        public string GpuName    { get => _gpuName;    set { _gpuName = value;    OnPropertyChanged(); } }
        public string GpuVram    { get => _gpuVram;    set { _gpuVram = value;    OnPropertyChanged(); } }
        public string SsdSize    { get => _ssdSize;    set { _ssdSize = value;    OnPropertyChanged(); } }
        public string BatteryHealth { get => _batteryHealth; set { _batteryHealth = value; OnPropertyChanged(); } }
        public string SerialNumber  { get => _serialNumber;  set { _serialNumber = value;  OnPropertyChanged(); } }
        public string ModelName     { get => _modelName;     set { _modelName = value;     OnPropertyChanged(); } }

        // ── Đánh giá ngoại hình ──────────────────────────────────────
        public string AppearanceGrade { get; set; } = "B";  // S, A, B, C, D
        private string _appearanceNote = "";
        public string AppearanceNote { get => _appearanceNote; set { _appearanceNote = value; OnPropertyChanged(); } }

        // ── Danh mục kiểm tra ────────────────────────────────────────
        public LaptopCheckItem Screen     { get; } = new("Màn hình");
        public LaptopCheckItem Keyboard   { get; } = new("Bàn phím");
        public LaptopCheckItem Touchpad   { get; } = new("Touchpad");
        public LaptopCheckItem Camera     { get; } = new("Camera");
        public LaptopCheckItem Microphone { get; } = new("Microphone");
        public LaptopCheckItem Speakers   { get; } = new("Loa");
        public LaptopCheckItem Battery    { get; } = new("Pin");
        public LaptopCheckItem UsbPorts   { get; } = new("Cổng USB");
        public LaptopCheckItem HdmiPort   { get; } = new("HDMI");
        public LaptopCheckItem Wifi       { get; } = new("Wi-Fi");
        public LaptopCheckItem Bluetooth  { get; } = new("Bluetooth");
        public LaptopCheckItem Charger    { get; } = new("Sạc / Nguồn");

        public List<LaptopCheckItem> CheckItems { get; }

        // ── Ghi chú tổng ─────────────────────────────────────────────
        private string _generalNote = "";
        public string GeneralNote { get => _generalNote; set { _generalNote = value; OnPropertyChanged(); } }

        // ── Thông tin khách ───────────────────────────────────────────
        private string _customerName = "";
        private string _customerPhone = "";
        private string _technicianName = "";
        public string CustomerName   { get => _customerName;   set { _customerName = value;   OnPropertyChanged(); } }
        public string CustomerPhone  { get => _customerPhone;  set { _customerPhone = value;  OnPropertyChanged(); } }
        public string TechnicianName { get => _technicianName; set { _technicianName = value; OnPropertyChanged(); } }

        // ── QR ────────────────────────────────────────────────────────
        private BitmapImage? _qrImage;
        public BitmapImage? QrImage { get => _qrImage; private set { _qrImage = value; OnPropertyChanged(); } }

        // ── Commands ──────────────────────────────────────────────────
        public RelayCommand GenerateQrCommand   { get; }
        public RelayCommand ExportTextCommand   { get; }
        public RelayCommand CopyReportCommand   { get; }

        public AssessmentViewModel()
        {
            CheckItems = new List<LaptopCheckItem>
            {
                Screen, Keyboard, Touchpad, Camera, Microphone, Speakers,
                Battery, UsbPorts, HdmiPort, Wifi, Bluetooth, Charger
            };
            GenerateQrCommand = new RelayCommand(GenerateQr);
            ExportTextCommand = new RelayCommand(ExportText);
            CopyReportCommand = new RelayCommand(() => Clipboard.SetText(BuildReport()));
        }

        public void LoadFromSysInfo(HardwareInfo info)
        {
            ModelName     = info.ModelName ?? "";
            SerialNumber  = info.SerialNumber ?? "";
            CpuName       = info.CPU ?? "";
            RamSize       = info.TotalRam ?? "";
            RamBus        = info.RamSticks?.Count > 0 ? info.RamSticks[0].Speed : "";
            // Ghép tất cả GPU
            if (info.Gpus != null && info.Gpus.Count > 0)
            {
                var names = new List<string>();
                var vrams = new List<string>();
                foreach (var gpu in info.Gpus)
                {
                    names.Add(gpu.Name ?? "");
                    vrams.Add(gpu.Vram ?? "");
                }
                GpuName = string.Join("  |  ", names);
                GpuVram = string.Join("  |  ", vrams);
            }
            SsdSize       = info.Storages?.Count > 0 ? info.Storages[0].Capacity : "";
            BatteryHealth = info.BatteryHealth ?? "";
        }

        private string BuildReport()
        {
            const string line = "════════════════════════════════════════════════════";
            const string thin = "────────────────────────────────────────────────────";
            var sb = new StringBuilder();

            sb.AppendLine(line);
            sb.AppendLine("           PHIẾU THẨM ĐỊNH LAPTOP");
            sb.AppendLine($"           Ngày: {DateTime.Now:dd/MM/yyyy}   Giờ: {DateTime.Now:HH:mm}");
            sb.AppendLine(line);
            sb.AppendLine();

            // Thông tin máy
            sb.AppendLine($"  Mẫu máy     :  {ModelName}");
            sb.AppendLine($"  Serial       :  {SerialNumber}");
            sb.AppendLine();

            // Phần cứng
            sb.AppendLine("  [ THÔNG SỐ PHẦN CỨNG ]");
            sb.AppendLine($"  {thin}");
            sb.AppendLine($"  CPU          :  {CpuName}");
            sb.AppendLine();
            sb.AppendLine($"  RAM          :  {RamSize}   Bus: {RamBus}");
            sb.AppendLine();
            sb.AppendLine($"  VGA          :  {GpuName}");
            sb.AppendLine($"                  VRAM: {GpuVram}");
            sb.AppendLine();
            sb.AppendLine($"  Ổ cứng       :  {SsdSize}");
            sb.AppendLine();
            sb.AppendLine($"  Sức khỏe pin :  {BatteryHealth}");
            sb.AppendLine();
            sb.AppendLine();

            // Ngoại hình
            sb.AppendLine("  [ ĐÁNH GIÁ NGOẠI HÌNH ]");
            sb.AppendLine($"  {thin}");
            sb.AppendLine();
            sb.AppendLine($"  Xếp loại     :  {AppearanceGrade}");
            if (!string.IsNullOrWhiteSpace(AppearanceNote))
            {
                sb.AppendLine();
                sb.AppendLine($"  Mô tả        :  {AppearanceNote}");
            }
            sb.AppendLine();
            sb.AppendLine();

            // Kiểm tra chức năng
            sb.AppendLine("  [ KIỂM TRA CHỨC NĂNG ]");
            sb.AppendLine($"  {thin}");
            sb.AppendLine();
            foreach (var item in GetCheckItems())
            {
                var status = item.IsOk ? "[OK]  " : "[LỖI] ";
                var note = string.IsNullOrWhiteSpace(item.Note) ? "" : $"  -- {item.Note}";
                sb.AppendLine($"  {status} {item.Name,-14}{note}");
            }
            sb.AppendLine();

            // Tổng kết lỗi
            var errorItems = new List<string>();
            foreach (var item in GetCheckItems())
            {
                if (!item.IsOk)
                    errorItems.Add(string.IsNullOrWhiteSpace(item.Note) ? item.Name : $"{item.Name} ({item.Note})");
            }
            if (errorItems.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("  ⚠ CÁC MỤC LỖI:");
                sb.AppendLine();
                foreach (var err in errorItems)
                    sb.AppendLine($"    • {err}");
                sb.AppendLine();
            }

            // Ghi chú
            if (!string.IsNullOrWhiteSpace(GeneralNote))
            {
                sb.AppendLine("  [ GHI CHÚ TỔNG ]");
                sb.AppendLine($"  {thin}");
                sb.AppendLine();
                sb.AppendLine($"  {GeneralNote}");
                sb.AppendLine();
            }

            sb.AppendLine($"  KTV kiểm tra :  {TechnicianName}");
            sb.AppendLine();
            sb.AppendLine(line);
            sb.AppendLine("  Xuất bởi LapLap Auto Tool");
            sb.AppendLine(line);

            return sb.ToString();
        }

        private IEnumerable<LaptopCheckItem> GetCheckItems() =>
            new[] { Screen, Keyboard, Touchpad, Camera, Microphone, Speakers,
                    Battery, UsbPorts, HdmiPort, Wifi, Bluetooth, Charger };

        private string BuildQrText()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"PHIẾU THẨM ĐỊNH - {DateTime.Now:dd/MM/yyyy}");
            sb.AppendLine($"Mẫu máy: {ModelName}");
            sb.AppendLine($"Serial: {SerialNumber}");
            sb.AppendLine($"CPU: {CpuName}");
            sb.AppendLine($"RAM: {RamSize} {RamBus}");
            sb.AppendLine($"VGA: {GpuName}");
            sb.AppendLine($"Ổ cứng: {SsdSize}");
            sb.AppendLine($"Pin: {BatteryHealth}");
            sb.AppendLine($"Ngoại hình: {AppearanceGrade}");
            var errors = new List<string>();
            foreach (var item in GetCheckItems())
            {
                if (!item.IsOk)
                    errors.Add(string.IsNullOrEmpty(item.Note) ? item.Name : $"{item.Name}: {item.Note}");
            }
            if (errors.Count > 0)
                sb.AppendLine($"Lỗi: {string.Join(", ", errors)}");
            else
                sb.AppendLine("Tất cả OK");
            if (!string.IsNullOrEmpty(GeneralNote))
                sb.AppendLine($"Ghi chú: {GeneralNote}");
            sb.AppendLine($"KTV: {TechnicianName}");
            return sb.ToString().TrimEnd();
        }

        private void GenerateQr()
        {
            try
            {
                var content = BuildQrText();

                using var qrGen = new QRCodeGenerator();
                var data = qrGen.CreateQrCode(content, QRCodeGenerator.ECCLevel.L);
                using var qrCode = new BitmapByteQRCode(data);
                byte[] bmpBytes = qrCode.GetGraphic(8);

                using var ms = new MemoryStream(bmpBytes);
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
                QrImage = bmp;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi tạo QR: {ex.Message}", "QR Code", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportText()
        {
            var report = BuildReport();
            var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Phieu_Tham_Dinh_{SerialNumber}_{DateTime.Now:yyyyMMdd_HHmm}.txt");
            File.WriteAllText(path, report, Encoding.UTF8);
            MessageBox.Show($"Đã xuất phiếu ra Desktop:\n{Path.GetFileName(path)}",
                "Xuất thành công", MessageBoxButton.OK, MessageBoxImage.Information);
            System.Diagnostics.Process.Start("notepad.exe", path);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }

    public class LaptopCheckItem : INotifyPropertyChanged
    {
        public string Name { get; }
        private bool _isOk = true;
        private string _note = "";
        public bool IsOk  { get => _isOk;  set { _isOk = value;  OnPropertyChanged(); } }
        public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }
        public RelayCommand ToggleCommand { get; }
        public LaptopCheckItem(string name)
        {
            Name = name;
            ToggleCommand = new RelayCommand(() => IsOk = !IsOk);
        }
        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? n = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    }
}
