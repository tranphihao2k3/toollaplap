using System;
using System.Management;
using System.Collections.Generic;
using System.Threading.Tasks;
using LapLapAutoTool.Models;
using System.Linq;
using System.Diagnostics;

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
                using var diskSearcher = new ManagementObjectSearcher("SELECT Model, Size, InterfaceType, SerialNumber FROM Win32_DiskDrive");
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

                foreach (var disk in diskCollection)
                {
                    double sizeBytes = Convert.ToDouble(disk["Size"] ?? 0);
                    string interfaceType = disk["InterfaceType"]?.ToString() ?? "N/A";
                    string type = interfaceType.Contains("SCSI") || interfaceType.Contains("NVMe") ? "NVMe (PCIe M.2) [SSD]" : "SATA [SSD/HDD]";

                    info.Storages.Add(new StorageInfo
                    {
                        Model = disk["Model"]?.ToString() ?? "Unknown",
                        Type = type,
                        Capacity = $"{Math.Round(sizeBytes / (1024.0 * 1024.0 * 1024.0), 1)} GB",
                        UsagePercent = $"{Math.Round(usagePercent, 1)}%",
                        UsedSpace = $"{Math.Round(totalUsed / (1024.0 * 1024.0 * 1024.0), 1)} GB",
                        FreeSpace = $"{Math.Round(totalFree / (1024.0 * 1024.0 * 1024.0), 1)} GB",
                        Serial = disk["SerialNumber"]?.ToString()?.Trim() ?? "N/A"
                    });
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Lỗi khi quét thông tin ổ cứng", ex);
            }
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

                    // Tùy chọn: Lấy chu kỳ sạc (nếu có trong BatteryStatus)
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT CycleCount FROM BatteryStatus"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            info.BatteryCycles = obj["CycleCount"]?.ToString() ?? "N/A";
                        }
                    }
                }
                catch
                {
                    if (string.IsNullOrEmpty(info.BatteryHealth))
                        info.BatteryHealth = "Yêu cầu quyền Admin";
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
