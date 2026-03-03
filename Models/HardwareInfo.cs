using System;
using System.Collections.Generic;

namespace LapLapAutoTool.Models
{
    public class HardwareInfo
    {
        // [ CPU ]
        public string CPU { get; set; } = "Unknown CPU";
        public string CPUCoresThreads { get; set; } = "N/A";
        public string CPUMaxSpeed { get; set; } = "N/A";
        public string CPUArchitecture { get; set; } = "N/A";

        // [ RAM ]
        public List<RamStickInfo> RamSticks { get; set; } = new List<RamStickInfo>();
        public string TotalRam { get; set; } = "N/A";
        public string MaxRamCapacity { get; set; } = "N/A";
        public string RamSlotsSummary { get; set; } = "N/A";

        // [ CARD DO HOA ]
        public List<GpuInfo> Gpus { get; set; } = new List<GpuInfo>();

        // [ O CUNG ]
        public List<StorageInfo> Storages { get; set; } = new List<StorageInfo>();

        // [ PIN (BATTERY) ]
        public string BatteryDesignCapacity { get; set; } = "N/A";
        public string BatteryFullChargeCapacity { get; set; } = "N/A";
        public string BatteryHealth { get; set; } = "N/A";
        public string BatteryCurrentPercent { get; set; } = "N/A";
        public string BatteryCycles { get; set; } = "N/A";
        public string BatteryWearPercent { get; set; } = "N/A";

        // [ MAINBOARD / BIOS ]
        public string Mainboard { get; set; } = "N/A";
        public string BiosVersion { get; set; } = "N/A";
        public string SerialNumber { get; set; } = "N/A";
        public string ModelName { get; set; } = "N/A";
        public string ComputerName { get; set; } = Environment.MachineName;

        // [ WIFI DA LUU ]
        public List<string> WiFiProfiles { get; set; } = new List<string>();

        // OS
        public string WindowsStatus { get; set; } = "Checking...";
    }

    public class RamStickInfo
    {
        public string Slot { get; set; } = "N/A";
        public string Capacity { get; set; } = "N/A";
        public string Speed { get; set; } = "N/A";
        public string Manufacturer { get; set; } = "N/A";
    }

    public class GpuInfo
    {
        public string Name { get; set; } = "N/A";
        public string Vram { get; set; } = "N/A";
        public string Tdp { get; set; } = "N/A";
    }

    public class StorageInfo
    {
        public string Model { get; set; } = "N/A";
        public string Type { get; set; } = "N/A";
        public string Capacity { get; set; } = "N/A";

        // SMART NVMe info
        public bool HasSmartInfo { get; set; } = false;
        public int HealthRemaining { get; set; }
        public int AvailableSpare { get; set; }
        public int Temperature { get; set; }
        public int PowerOnDays { get; set; }
        public string TotalHostWrites { get; set; } = "N/A";
        public string TotalHostReads { get; set; } = "N/A";
    }
}
