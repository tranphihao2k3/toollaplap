using System;

namespace LapLapAutoTool.Models
{
    public class LicenseInfo
    {
        public string Token { get; set; } = string.Empty;
        public DateTime? ExpiryDate { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string Hwid { get; set; } = string.Empty;

        public bool IsExpired => ExpiryDate.HasValue && ExpiryDate.Value < DateTime.Now;

        public int DaysRemaining 
        {
            get
            {
                if (!ExpiryDate.HasValue) return 0;
                var span = ExpiryDate.Value - DateTime.Now;
                return span.Days > 0 ? span.Days : 0;
            }
        }
    }
}
