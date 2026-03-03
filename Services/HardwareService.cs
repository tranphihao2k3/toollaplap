using System;
using System.Management;
using System.Collections.Generic;
using System.Threading.Tasks;
using LapLapAutoTool.Models;
using System.Linq;
using System.Diagnostics;
using System.Text.Json;
using System.IO;

namespace LapLapAutoTool.Services
{
    public interface IHardwareService
    {
        Task<HardwareInfo> GetSystemInfoAsync();
    }

    public class HardwareService : IHardwareService
    {
        private readonly ILogService _logService;

        public HardwareService(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<HardwareInfo> GetSystemInfoAsync()
        {
            return await Task.Run(() =>
            {
                var info = new HardwareInfo();
                try
                {
                    // [ CPU ]
                    info.CPU = GetWmiValue("Win32_Processor", "Name");
                    int cores = Convert.ToInt32(GetWmiValue("Win32_Processor", "NumberOfCores"));
                    int threads = Convert.ToInt32(GetWmiValue("Win32_Processor", "NumberOfLogicalProcessors"));
                    info.CPUCoresThreads = $"{cores} nhân  /  {threads} luồng";
                    info.CPUMaxSpeed = GetWmiValue("Win32_Processor", "MaxClockSpeed") + " MHz";
                    info.CPUArchitecture = GetWmiValue("Win32_Processor", "AddressWidth") + "-bit";

                    // [ RAM ]
                    PopulateRamInfo(info);

                    // [ CARD DO HOA ]
                    PopulateGpuInfo(info);

                    // [ O CUNG ]
                    PopulateStorageInfo(info);

                    // [ PIN ]
                    PopulateBatteryInfo(info);

                    // [ MAINBOARD / BIOS / MODEL ]
                    string vendor = GetWmiValue("Win32_ComputerSystem", "Manufacturer");
                    string productModel = GetWmiValue("Win32_ComputerSystem", "Model");
                    string productProduct = GetWmiValue("Win32_ComputerSystemProduct", "Name");
                    string family = GetWmiValue("Win32_ComputerSystem", "SystemFamily");

                    // Priority logic for marketing name (especially for Lenovo/HP/Dell)
                    string bestModel = productProduct; // Usually "IdeaPad Gaming 3..."
                    if (string.IsNullOrEmpty(bestModel) || bestModel == "N/A" || bestModel.Length < 4) bestModel = productModel;
                    if (!string.IsNullOrEmpty(family) && family != "N/A" && !bestModel.Contains(family)) bestModel = $"{family} {bestModel}";
                    
                    info.ModelName = bestModel;
                    info.Mainboard = GetWmiValue("Win32_BaseBoard", "Manufacturer") + " " + GetWmiValue("Win32_BaseBoard", "Product");
                    info.BiosVersion = GetWmiValue("Win32_BIOS", "Version");
                    info.SerialNumber = GetWmiValue("Win32_BIOS", "SerialNumber");
                    
                    // [ WIFI ]
                    info.WiFiProfiles = GetWiFiList();

                    // Windows
                    info.WindowsStatus = GetWindowsActivationStatus();
                }
                catch (Exception ex)
                {
                    _logService.LogError("Lỗi khi quét phần cứng hệ thống", ex);
                }
                return info;
            });
        }

        private string GetWmiValue(string className, string propertyName)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    return obj[propertyName]?.ToString()?.Trim() ?? "N/A";
                }
            }
            catch { }
            return "N/A";
        }

        private void PopulateRamInfo(HardwareInfo info)
        {
            try
            {
                double totalBytes = 0;
                int slotsUsed = 0;
                using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer, DeviceLocator, BankLabel FROM Win32_PhysicalMemory");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    double cap = Convert.ToDouble(obj["Capacity"] ?? 0);
                    totalBytes += cap;
                    slotsUsed++;
                    
                    string locator = obj["DeviceLocator"]?.ToString()?.Trim() ?? "";
                    string bank = obj["BankLabel"]?.ToString()?.Trim() ?? "";
                    string slotDisplay = locator;
                    if (string.IsNullOrEmpty(slotDisplay) || slotDisplay == "0") slotDisplay = bank;
                    if (string.IsNullOrEmpty(slotDisplay)) slotDisplay = $"Slot {slotsUsed}";

                    info.RamSticks.Add(new RamStickInfo
                    {
                        Slot = slotDisplay,
                        Capacity = $"{Math.Round(cap / (1024 * 1024 * 1024), 0)} GB",
                        Speed = $"{obj["Speed"]} MHz",
                        Manufacturer = obj["Manufacturer"]?.ToString() ?? "Generic"
                    });
                }
                info.TotalRam = $"{Math.Round(totalBytes / (1024 * 1024 * 1024), 2)} GB";

                using var maxSearcher = new ManagementObjectSearcher("SELECT MemoryDevices FROM Win32_PhysicalMemoryArray");
                using var maxCollection = maxSearcher.Get();
                foreach (var obj in maxCollection)
                {
                    int totalSlots = Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                    info.RamSlotsSummary = $"({slotsUsed}/{totalSlots} slots used)";
                }
                
                // Max Capacity handle separately to avoid overwriting if first query fails
                using var capSearcher = new ManagementObjectSearcher("SELECT MaxCapacity FROM Win32_PhysicalMemoryArray");
                foreach (var obj in capSearcher.Get())
                {
                    double maxKb = Convert.ToDouble(obj["MaxCapacity"] ?? 0);
                    if (maxKb > 0) info.MaxRamCapacity = $"{Math.Round(maxKb / (1024 * 1024), 0)} GB";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét thông tin RAM", ex);
            }
        }

        private void PopulateGpuInfo(HardwareInfo info)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    string name = obj["Name"]?.ToString() ?? "Unknown GPU";
                    long vramBytes = 0;
                    if (obj["AdapterRAM"] != null) long.TryParse(obj["AdapterRAM"].ToString(), out vramBytes);
                    // Handle buggy WMI negative VRAM
                    double vramGb = Math.Abs(vramBytes) / (1024.0 * 1024.0 * 1024.0);
                    if (vramGb > 256) vramGb = 0; // Likely error value

                    string tdp = "N/A";
                    if (name.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
                    {
                        tdp = GetNvidiaTdp();
                    }

                    info.Gpus.Add(new GpuInfo
                    {
                        Name = name,
                        Vram = vramGb > 0 ? $"{Math.Round(vramGb, 1)} GB" : "Shared",
                        Tdp = tdp
                    });
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét thông tin GPU", ex);
            }
        }

        private string GetNvidiaTdp()
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "nvidia-smi",
                        Arguments = "--query-gpu=power.max_limit --format=csv,noheader",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrWhiteSpace(output))
                {
                    return output.Trim();
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét thông tin TDP NVIDIA", ex);
            }
            return "N/A";
        }

        private void PopulateStorageInfo(HardwareInfo info)
        {
            try
            {
                using var diskSearcher = new ManagementObjectSearcher("SELECT Model, Size, InterfaceType, SerialNumber, DeviceID FROM Win32_DiskDrive");
                using var driveSearcher = new ManagementObjectSearcher("SELECT Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType=3");
                using var diskCollection = diskSearcher.Get();
                using var driveCollection = driveSearcher.Get();

                double totalUsed = 0, totalFree = 0;
                foreach (var drive in driveCollection)
                {
                    totalFree += Convert.ToDouble(drive["FreeSpace"] ?? 0);
                    totalUsed += (Convert.ToDouble(drive["Size"] ?? 0) - Convert.ToDouble(drive["FreeSpace"] ?? 0));
                }
                double totalSize = totalUsed + totalFree;
                double usagePercent = totalSize > 0 ? (totalUsed / totalSize) * 100 : 0;

                string smartctlPath = FindSmartctlPath();

                foreach (var disk in diskCollection)
                {
                    double sizeBytes = Convert.ToDouble(disk["Size"] ?? 0);
                    string interfaceType = disk["InterfaceType"]?.ToString() ?? "N/A";
                    string deviceId = disk["DeviceID"]?.ToString() ?? "";
                    string type = interfaceType.Contains("SCSI") || interfaceType.Contains("NVMe") || deviceId.Contains("NVMe") ? "NVMe (PCIe M.2) [SSD]" : "SATA [SSD/HDD]";

                    var storage = new StorageInfo
                    {
                        Model = disk["Model"]?.ToString() ?? "Unknown",
                        Type = type,
                        Capacity = $"{Math.Round(sizeBytes / (1024.0 * 1024.0 * 1024.0), 1)} GB"
                    };

                    // Try SMART info if smartctl is available
                    if (!string.IsNullOrEmpty(smartctlPath))
                    {
                        UpdateSmartInfo(storage, smartctlPath, deviceId);
                    }

                    info.Storages.Add(storage);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét thông tin ổ cứng", ex);
            }
        }

        private string FindSmartctlPath()
        {
            // 0. Extract from embedded resource to Temp
            string tempPath = Path.Combine(Path.GetTempPath(), "LapLap_smartctl.exe");
            try
            {
                using (var stream = System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("LapLapAutoTool.smartctl.exe"))
                {
                    if (stream != null)
                    {
                        // Luôn ghi đè để đảm bảo bản mới nhất được dùng nếu có cập nhật
                        using (var fileStream = new FileStream(tempPath, FileMode.Create))
                        {
                            stream.CopyTo(fileStream);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi trích xuất smartctl.exe: " + ex.Message, ex);
            }

            if (File.Exists(tempPath)) return tempPath;

            // 1. Check local directory
            string localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smartctl.exe");
            if (File.Exists(localPath)) return localPath;

            // 2. Default path
            string defaultPath = @"C:\Program Files\smartmontools\bin\smartctl.exe";
            if (File.Exists(defaultPath)) return defaultPath;

            // 3. Search in PATH
            try
            {
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = "where.exe",
                    Arguments = "smartctl",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                string output = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();
                if (proc.ExitCode == 0)
                {
                    return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                }
            }
            catch { }

            return null;
        }

        private void UpdateSmartInfo(StorageInfo storage, string smartctlPath, string deviceId)
        {
            try
            {
                // We need to map DeviceID (e.g. \\.\PHYSICALDRIVE0) to smartctl format (/dev/pd0)
                string smartDevice = deviceId.Replace(@"\\.\PHYSICALDRIVE", "/dev/pd");
                using var doc = ExecuteSmartctlJson(smartctlPath, smartDevice);
                if (doc == null) return;
                var root = doc.RootElement;

                // 1. Dành cho NVMe
                if (root.TryGetProperty("nvme_smart_health_information_log", out var nvme))
                {
                    storage.HasSmartInfo = true;
                    if (nvme.TryGetProperty("percentage_used", out var wear))
                        storage.HealthRemaining = 100 - GetIntValue(wear);
                    if (nvme.TryGetProperty("available_spare", out var spare))
                        storage.AvailableSpare = GetIntValue(spare);
                    if (nvme.TryGetProperty("temperature", out var temp))
                        storage.Temperature = GetIntValue(temp);
                    // data_units_written * 512 * 1000 bytes per unit
                    if (nvme.TryGetProperty("data_units_written", out var duw))
                    {
                        double tb = GetLongValue(duw) * 512.0 * 1000.0 / (1024.0 * 1024.0 * 1024.0 * 1024.0);
                        storage.TotalHostWrites = tb >= 1 ? $"{tb:0.1} TB" : $"{tb * 1024:0} GB";
                    }
                    if (nvme.TryGetProperty("data_units_read", out var dur))
                    {
                        double tb = GetLongValue(dur) * 512.0 * 1000.0 / (1024.0 * 1024.0 * 1024.0 * 1024.0);
                        storage.TotalHostReads = tb >= 1 ? $"{tb:0.1} TB" : $"{tb * 1024:0} GB";
                    }
                }
                // 2. Dành cho SATA SSD
                else if (root.TryGetProperty("ata_smart_attributes", out var ataParams) && 
                         ataParams.TryGetProperty("table", out var table) && 
                         table.ValueKind == JsonValueKind.Array)
                {
                    storage.HasSmartInfo = true;
                    
                    // Một số ID thường dùng cho % Health của SATA SSD: 231 (SSD/Life Left), 202 (Percent Lifetime Remaining), hoặc 169 (Remaining Life)
                    foreach (var element in table.EnumerateArray())
                    {
                        if (element.TryGetProperty("id", out var attrId) && element.TryGetProperty("value", out var attrValue))
                        {
                            int id = GetIntValue(attrId);
                            // ID 231 hoặc 202 là SSD Life Left (còn lại), nếu có lấy cái này làm Health
                            if (id == 231 || id == 202 || id == 169)
                            {
                                storage.HealthRemaining = GetIntValue(attrValue);
                            }
                            // Nhiệt độ thường nằm ở ID 194
                            if (id == 194 && element.TryGetProperty("raw", out var raw) && raw.TryGetProperty("value", out var rawValue))
                            {
                                storage.Temperature = GetIntValue(rawValue);
                            }
                        }
                    }
                    
                    // Dự phòng: nếu tìm Smart Status trong `smart_status`
                    if (storage.HealthRemaining == 0 && root.TryGetProperty("smart_status", out var smartStatus))
                    {
                        if (smartStatus.TryGetProperty("passed", out var passed) && passed.GetBoolean())
                        {
                            storage.HealthRemaining = 100; // Passed thì tạm xem như Good
                        }
                    }
                }

                // Lấy giờ chạy (Power on hours) - Hỗ trợ cả NVMe & SATA
                if (root.TryGetProperty("power_on_time", out var pot) && pot.TryGetProperty("hours", out var hours))
                {
                    storage.PowerOnDays = GetIntValue(hours) / 24;
                    storage.HasSmartInfo = true;
                }
            }
            catch (Exception ex)
            {
                _logService.LogWarning($"Lỗi khi đọc SMART cho {deviceId}: {ex.Message}");
            }
        }

        private JsonDocument ExecuteSmartctlJson(string smartctlPath, string smartDevice)
        {
            // Một số máy cần khai báo type (-d) khác nhau thì smartctl mới trả đúng SMART.
            var argumentCandidates = new[]
            {
                $"-a {smartDevice} -j",
                $"-a {smartDevice} -d auto -j",
                $"-a {smartDevice} -d nvme -j",
                $"-a {smartDevice} -d sat -j",
                $"-a {smartDevice} -d scsi -j"
            };

            foreach (var args in argumentCandidates)
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = smartctlPath,
                        Arguments = args,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) continue;

                    string output = process.StandardOutput.ReadToEnd();
                    string err = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    if (string.IsNullOrWhiteSpace(output))
                    {
                        _logService.LogWarning($"smartctl không có output với args '{args}' ({smartDevice}). stderr: {err}");
                        continue;
                    }

                    using var parsed = JsonDocument.Parse(output);
                    // smartctl có thể trả JSON nhưng exit code != 0, nên ưu tiên check phần JSON này.
                    if (parsed.RootElement.TryGetProperty("smartctl", out var smartctl) &&
                        smartctl.TryGetProperty("exit_status", out var exitStatus) &&
                        (GetIntValue(exitStatus) & 0x03) != 0)
                    {
                        _logService.LogWarning($"smartctl trả lỗi thiết bị với args '{args}' ({smartDevice}), thử profile khác.");
                        continue;
                    }

                    return JsonDocument.Parse(output);
                }
                catch (Exception ex)
                {
                    _logService.LogWarning($"smartctl lỗi với args '{args}' ({smartDevice}): {ex.Message}");
                }
            }

            return null;
        }

        private int GetIntValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number)
            {
                if (element.TryGetInt32(out int intValue)) return intValue;
                if (element.TryGetInt64(out long longValue)) return (int)longValue;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string text = element.GetString() ?? "0";
                if (int.TryParse(text, out int parsedInt)) return parsedInt;
                if (long.TryParse(text, out long parsedLong)) return (int)parsedLong;
            }

            return 0;
        }

        private long GetLongValue(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out long value))
            {
                return value;
            }

            if (element.ValueKind == JsonValueKind.String)
            {
                string text = element.GetString() ?? "0";
                if (long.TryParse(text, out long parsed)) return parsed;
            }

            return 0;
        }



        private void PopulateBatteryInfo(HardwareInfo info)
        {
            try
            {
                // 1. Lấy % pin hiện tại (Win32_Battery)
                using (var searcher = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining FROM Win32_Battery"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        info.BatteryCurrentPercent = $"{obj["EstimatedChargeRemaining"]}%";
                    }
                }

                try
                {
                    double designCap = 0;
                    double fullChargeCap = 0;

                    // 2. Lấy dung lượng thiết kế (BatteryStaticData trong root\WMI)
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT DesignedCapacity FROM BatteryStaticData"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var obj in collection)
                        {
                            designCap = Convert.ToDouble(obj["DesignedCapacity"] ?? 0);
                        }
                    }

                    // 3. Lấy dung lượng sạc đầy hiện tại (BatteryFullChargedCapacity trong root\WMI)
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"))
                    using (var collection = searcher.Get())
                    {
                        foreach (var obj in collection)
                        {
                            fullChargeCap = Convert.ToDouble(obj["FullChargedCapacity"] ?? 0);
                        }
                    }

                    if (designCap > 0)
                    {
                        double health = (fullChargeCap / designCap) * 100;
                        double wear = 100 - health;
                        if (wear < 0) wear = 0;

                        info.BatteryDesignCapacity = $"{designCap} mWh";
                        info.BatteryFullChargeCapacity = $"{fullChargeCap} mWh";
                        info.BatteryHealth = $"{Math.Round(health, 1)}%";
                        info.BatteryWearPercent = $"{Math.Round(wear, 1)}%";
                    }

                    // Tùy chọn: Lấy chu kỳ sạc và trạng thái sạc/xả
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CycleCount, ChargeRate, DischargeRate, Charging, Discharging FROM BatteryStatus"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.BatteryCycles = obj["CycleCount"]?.ToString() ?? "N/A";

                            bool isCharging = Convert.ToBoolean(obj["Charging"]);
                            bool isDischarging = Convert.ToBoolean(obj["Discharging"]);
                            long chargeRate = Convert.ToInt64(obj["ChargeRate"] ?? 0);
                            long dischargeRate = Convert.ToInt64(obj["DischargeRate"] ?? 0);

                            if (isCharging)
                            {
                                info.BatteryStatus = "Đang sạc";
                                info.BatteryChargeRate = chargeRate > 0 ? $"+{chargeRate / 1000.0:0.0} W" : "Đang tính...";
                            }
                            else if (isDischarging)
                            {
                                info.BatteryStatus = "Đang xả pin";
                                info.BatteryChargeRate = dischargeRate > 0 ? $"-{dischargeRate / 1000.0:0.0} W" : "Đang tính...";
                            }
                            else
                            {
                                info.BatteryStatus = "Đã đầy / Không sạc";
                                info.BatteryChargeRate = "0 W";
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logService.LogWarning("Lỗi khi đọc chi tiết pin WMI: " + ex.Message);
                }
            }
            catch (Exception ex)
            {
                info.BatteryCurrentPercent = "N/A (Máy bàn)";
                _logService.LogError("Lỗi khi quét thông tin Pin", ex);
            }
        }

        private List<string> GetWiFiList()
        {
            var ssids = new List<string>();
            try
            {
               var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "netsh",
                        Arguments = "wlan show profiles",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                string output = process.StandardOutput.ReadToEnd();
                process.WaitForExit();

                var lines = output.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    if (line.Contains(":"))
                    {
                        string ssid = line.Split(':')[1].Trim();
                        if (!string.IsNullOrEmpty(ssid)) ssids.Add(ssid);
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét danh sách WiFi", ex);
            }
            return ssids;
        }

        private string GetWindowsActivationStatus()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ef3110505a'");
                using var collection = searcher.Get();
                foreach (var obj in collection)
                {
                    int status = Convert.ToInt32(obj["LicenseStatus"]);
                    return status == 1 ? "Đã kích hoạt ✅" : "Chưa kích hoạt ❌";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét trạng thái Windows", ex);
            }
            return "Chưa xác định ⚠️";
        }
    }
}
