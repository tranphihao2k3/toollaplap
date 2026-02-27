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

                    // [ MAINBOARD / BIOS ]
                    info.Mainboard = GetWmiValue("Win32_BaseBoard", "Manufacturer") + " " + GetWmiValue("Win32_BaseBoard", "Product");
                    info.BiosVersion = GetWmiValue("Win32_BIOS", "Version");
                    info.SerialNumber = GetWmiValue("Win32_BIOS", "SerialNumber");
                    info.ModelName = GetWmiValue("Win32_ComputerSystem", "Model");

                    // [ WIFI ]
                    info.WiFiProfiles = GetWiFiList();

                    // Windows
                    info.WindowsStatus = GetWindowsActivationStatus();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Hardware Scan Error: {ex.Message}");
                }
                return info;
            });
        }

        private string GetWmiValue(string className, string propertyName)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {className}");
                foreach (var obj in searcher.Get())
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
                using var searcher = new ManagementObjectSearcher("SELECT Capacity, Speed, Manufacturer, DeviceLocator FROM Win32_PhysicalMemory");
                foreach (var obj in searcher.Get())
                {
                    double cap = Convert.ToDouble(obj["Capacity"] ?? 0);
                    totalBytes += cap;
                    slotsUsed++;
                    info.RamSticks.Add(new RamStickInfo
                    {
                        Slot = obj["DeviceLocator"]?.ToString() ?? $"Slot {slotsUsed}",
                        Capacity = $"{Math.Round(cap / (1024 * 1024 * 1024), 0)} GB",
                        Speed = $"{obj["Speed"]} MHz",
                        Manufacturer = obj["Manufacturer"]?.ToString() ?? "Generic"
                    });
                }
                info.TotalRam = $"{Math.Round(totalBytes / (1024 * 1024 * 1024), 2)} GB";

                using var maxSearcher = new ManagementObjectSearcher("SELECT MaxCapacity, MemoryDevices FROM Win32_PhysicalMemoryArray");
                foreach (var obj in maxSearcher.Get())
                {
                    double maxKb = Convert.ToDouble(obj["MaxCapacity"] ?? 0);
                    info.MaxRamCapacity = $"{Math.Round(maxKb / (1024 * 1024), 0)} GB";
                    int totalSlots = Convert.ToInt32(obj["MemoryDevices"] ?? 0);
                    info.RamSlotsSummary = $"({totalSlots} slots)";
                }
            }
            catch { }
        }

        private void PopulateGpuInfo(HardwareInfo info)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController");
                foreach (var obj in searcher.Get())
                {
                    long vramBytes = 0;
                    if (obj["AdapterRAM"] != null) long.TryParse(obj["AdapterRAM"].ToString(), out vramBytes);
                    // Handle buggy WMI negative VRAM
                    double vramGb = Math.Abs(vramBytes) / (1024.0 * 1024.0 * 1024.0);
                    if (vramGb > 256) vramGb = 0; // Likely error value

                    info.Gpus.Add(new GpuInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "Unknown GPU",
                        Vram = vramGb > 0 ? $"{Math.Round(vramGb, 1)} GB" : "Shared"
                    });
                }
            }
            catch { }
        }

        private void PopulateStorageInfo(HardwareInfo info)
        {
            try
            {
                // Link DiskDrive to LogicalDisk is complex in WMI, we'll simplify for the tool's needs
                using var diskSearcher = new ManagementObjectSearcher("SELECT Model, InterfaceType, Size, SerialNumber FROM Win32_DiskDrive");
                var diskList = diskSearcher.Get();

                using var driveSearcher = new ManagementObjectSearcher("SELECT DeviceID, Size, FreeSpace FROM Win32_LogicalDisk WHERE DriveType = 3");
                var driveList = driveSearcher.Get();

                // For simple display, we'll use first physical disk and sum of logical drives if multiple
                foreach (var disk in diskList)
                {
                    double sizeBytes = Convert.ToDouble(disk["Size"] ?? 0);
                    string interfaceType = disk["InterfaceType"]?.ToString() ?? "N/A";
                    string type = interfaceType.Contains("SCSI") || interfaceType.Contains("NVMe") ? "NVMe (PCIe M.2) [SSD]" : "SATA [SSD/HDD]";

                    double totalUsed = 0, totalFree = 0;
                    foreach (var drive in driveList)
                    {
                        totalFree += Convert.ToDouble(drive["FreeSpace"] ?? 0);
                        totalUsed += (Convert.ToDouble(drive["Size"] ?? 0) - Convert.ToDouble(drive["FreeSpace"] ?? 0));
                    }

                    double totalSize = totalUsed + totalFree;
                    double usagePercent = totalSize > 0 ? (totalUsed / totalSize) * 100 : 0;

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
            catch { }
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
                    {
                        foreach (var obj in searcher.Get())
                        {
                            designCap = Convert.ToDouble(obj["DesignedCapacity"] ?? 0);
                        }
                    }

                    // 3. Lấy dung lượng sạc đầy hiện tại (BatteryFullChargedCapacity trong root\WMI)
                    using (var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT FullChargedCapacity FROM BatteryFullChargedCapacity"))
                    {
                        foreach (var obj in searcher.Get())
                        {
                            fullChargeCap = Convert.ToDouble(obj["FullChargedCapacity"] ?? 0);
                        }
                    }

                    if (designCap > 0)
                    {
                        double health = (fullChargeCap / designCap) * 100;
                        info.BatteryDesignCapacity = $"{designCap} mWh";
                        info.BatteryFullChargeCapacity = $"{fullChargeCap} mWh";
                        info.BatteryHealth = $"{Math.Round(health, 1)}%";
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
                System.Diagnostics.Debug.WriteLine($"Battery Error: {ex.Message}");
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
            catch { }
            return ssids;
        }

        private string GetWindowsActivationStatus()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ef3110505a'");
                foreach (var obj in searcher.Get())
                {
                    int status = Convert.ToInt32(obj["LicenseStatus"]);
                    return status == 1 ? "Đã kích hoạt ✅" : "Chưa kích hoạt ❌";
                }
            }
            catch { }
            return "Chưa xác định ⚠️";
        }
    }
}
