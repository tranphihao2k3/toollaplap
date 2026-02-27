using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
using System.IO;

namespace LapLapAutoTool.Services
{
    public interface ILicenseService
    {
        string GetMachineCode();
        bool IsLicensed();
        bool Activate(string key);
        string GetLicenseStatus();
    }

    public class LicenseService : ILicenseService
    {
        private const string RegistryPath = @"SOFTWARE\LapLapAutoTool";
        private const string LicenseKeyName = "LicenseKey";

        public string GetMachineCode()
        {
            try
            {
                string cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
                string diskId = GetWmiValue("Win32_DiskDrive", "SerialNumber");
                string uuid = GetWmiValue("Win32_ComputerSystemProduct", "UUID");

                string rawId = $"{cpuId}-{diskId}-{uuid}";
                return HashString(rawId).Substring(0, 16).ToUpper();
            }
            catch
            {
                return "LAPLAP-HWID-ERROR";
            }
        }

        public bool IsLicensed()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
                if (key == null) return false;

                string? encryptedKey = key.GetValue(LicenseKeyName) as string;
                if (string.IsNullOrEmpty(encryptedKey)) return false;

                // Simple validation: In a real app, you'd decrypt and check against a server or local logic
                // For this demo, we'll assume a key starting with "LAP-" and ending with HWID hash is valid
                return ValidateKey(encryptedKey);
            }
            catch { return false; }
        }

        public bool Activate(string licenseKey)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
                key.SetValue(LicenseKeyName, licenseKey);
                return ValidateKey(licenseKey);
            }
            catch { return false; }
        }

        public string GetLicenseStatus()
        {
            if (IsLicensed()) return "Đã kích hoạt (Vĩnh viễn) ✅";
            return "Bản dùng thử (Chưa kích hoạt) ❌";
        }

        private bool ValidateKey(string key)
        {
            // Demo logic: Key should be "LAP-" + MachineCode
            return key == $"LAP-{GetMachineCode()}";
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
            return "Unknown";
        }

        private string HashString(string input)
        {
            using (SHA256 sha256Hash = SHA256.Create())
            {
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(input));
                StringBuilder builder = new StringBuilder();
                foreach (var b in bytes) builder.Append(b.ToString("x2"));
                return builder.ToString();
            }
        }
    }
}
