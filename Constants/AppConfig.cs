using System;

namespace LapLapAutoTool.Constants
{
    public static class AppConfig
    {
        // 1. Thông tin ứng dụng
        public const string AppName = "tool v1";
        public const string AppVersion = "v1.0.0";
        public const string Developer = "Phi Hào";
        public const string Website = "laplapcantho.store";

        // 2. Bảo mật & Bản quyền
        public const string AdminPin = "292003"; // Mã số 6 số của bạn
        public const string LicenseApiUrl = "https://laplapcantho.store/api/check-license";
        
        // 3. Cấu hình hệ thống
        public const int NetworkTimeoutSeconds = 10;
        public const string SetupFolderName = "SetupFiles";
        
        // 4. Lời chào khách hàng (Nếu không có tên)
        public const string DefaultCustomerName = "Khách hàng LapLap";
    }
}
