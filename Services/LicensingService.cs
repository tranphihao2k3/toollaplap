using System;
using System.Management;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Threading.Tasks;
using System.Linq;

using LapLapAutoTool.Constants;

namespace LapLapAutoTool.Services
{
    public class LicensingService
    {
        private readonly string LicenseApiUrl = AppConfig.LicenseApiUrl; 
        private readonly string _tokenFilePath;

        public LicensingService()
        {
            // Lưu token vào thư mục AppData/LapLap
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string lapLapDir = System.IO.Path.Combine(appData, "LapLap");
            if (!System.IO.Directory.Exists(lapLapDir)) System.IO.Directory.CreateDirectory(lapLapDir);
            _tokenFilePath = System.IO.Path.Combine(lapLapDir, "license.txt");
        }

        public string GetHWID()
        {
            try
            {
                string cpuId = GetWmiValue("Win32_Processor", "ProcessorId");
                string mbId = GetWmiValue("Win32_BaseBoard", "SerialNumber");
                string rawId = $"LAPLAP-{cpuId}-{mbId}";

                using (SHA256 sha256Hash = SHA256.Create())
                {
                    byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawId));
                    StringBuilder builder = new StringBuilder();
                    for (int i = 0; i < 8; i++) 
                    {
                        builder.Append(bytes[i].ToString("X2"));
                    }
                    return builder.ToString();
                }
            }
            catch
            {
                return "UNKNOWN-HWID";
            }
        }

        private string GetWmiValue(string table, string property)
        {
            try
            {
                using var searcher = new ManagementObjectSearcher($"SELECT {property} FROM {table}");
                foreach (var obj in searcher.Get())
                {
                    return obj[property]?.ToString()?.Trim() ?? "N/A";
                }
            }
            catch { }
            return "N/A";
        }

        public string? LoadSavedToken()
        {
            if (System.IO.File.Exists(_tokenFilePath))
            {
                return System.IO.File.ReadAllText(_tokenFilePath).Trim();
            }
            return null;
        }

        public void SaveToken(string token)
        {
            System.IO.File.WriteAllText(_tokenFilePath, token.Trim());
        }

        public void Logout()
        {
            if (System.IO.File.Exists(_tokenFilePath))
            {
                System.IO.File.Delete(_tokenFilePath);
            }
            CurrentLicense = null;
        }

        public static LapLapAutoTool.Models.LicenseInfo? CurrentLicense { get; private set; }

        public bool IsLicensed => LicensingService.CurrentLicense != null;
        public string VersionText => Constants.AppConfig.AppVersion;

        public string MachineCode => LicensingService.CurrentLicense?.Hwid ?? (new LicensingService()).GetHWID();

        public async Task<(bool success, string message, LapLapAutoTool.Models.LicenseInfo? license)> CheckLicenseAsync(string token, string hwid)
        {
            if (token == AppConfig.AdminPin || token == $"ADMIN-PIN-{AppConfig.AdminPin}")
            {
                var adminLicense = new LapLapAutoTool.Models.LicenseInfo
                {
                    Token = $"ADMIN-PIN-{AppConfig.AdminPin}",
                    CustomerName = "ADMIN VIP",
                    CustomerPhone = "Host Systems",
                    Status = "active",
                    Hwid = hwid,
                    ExpiryDate = DateTime.Now.AddYears(10)
                };
                CurrentLicense = adminLicense;
                return (true, "Đăng nhập quyền Admin", adminLicense);
            }

            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(15);

                var requestData = new
                {
                    token = token,
                    hwid = CleanString(hwid)
                };

                string json = System.Text.Json.JsonSerializer.Serialize(requestData);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                var response = await client.PostAsync(LicenseApiUrl, content);
                var resultJson = await response.Content.ReadAsStringAsync();
                
                using var doc = System.Text.Json.JsonDocument.Parse(resultJson);
                bool success = false;
                if (doc.RootElement.TryGetProperty("success", out var successElement))
                {
                    success = successElement.GetBoolean();
                }

                string message = "Unknown error";
                if (doc.RootElement.TryGetProperty("message", out var msgElement))
                {
                    message = msgElement.GetString() ?? "Unknown error";
                }

                LapLapAutoTool.Models.LicenseInfo? licenseInfo = null;

                if (success && doc.RootElement.TryGetProperty("data", out var dataElement))
                {
                    licenseInfo = new LapLapAutoTool.Models.LicenseInfo();

                    if (dataElement.TryGetProperty("token", out var tokenEl)) licenseInfo.Token = tokenEl.GetString() ?? "";
                    if (dataElement.TryGetProperty("customerName", out var nameEl)) licenseInfo.CustomerName = nameEl.GetString() ?? "";
                    if (dataElement.TryGetProperty("customerPhone", out var phoneEl)) licenseInfo.CustomerPhone = phoneEl.GetString() ?? "";
                    if (dataElement.TryGetProperty("status", out var statusEl)) licenseInfo.Status = statusEl.GetString() ?? "";
                    if (dataElement.TryGetProperty("hwid", out var hwidEl)) licenseInfo.Hwid = hwidEl.GetString() ?? "";
                    
                    if (dataElement.TryGetProperty("expiryDate", out var expEl) && expEl.ValueKind != System.Text.Json.JsonValueKind.Null)
                    {
                        if (DateTime.TryParse(expEl.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out DateTime dt))
                        {
                            licenseInfo.ExpiryDate = dt;
                        }
                    }

                    CurrentLicense = licenseInfo;
                }

                return (success, message, licenseInfo);
            }
            catch (Exception ex)
            {
                return (false, $"Lỗi kết nối máy chủ: {ex.Message}", null);
            }
        }

        private string CleanString(string input)
        {
            if (string.IsNullOrEmpty(input)) return "";
            return new string(input.Where(c => char.IsLetterOrDigit(c)).ToArray()).ToUpper();
        }
    }
}
