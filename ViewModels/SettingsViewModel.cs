using LapLapAutoTool.Models;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.ViewModels
{
    public class SettingsViewModel
    {
        public LicenseInfo? CurrentLicense => LicensingService.CurrentLicense;

        public string Token => CurrentLicense?.Token ?? "N/A";
        public string CustomerName => string.IsNullOrEmpty(CurrentLicense?.CustomerName) ? "Chưa cập nhật" : CurrentLicense.CustomerName;
        public string CustomerPhone => string.IsNullOrEmpty(CurrentLicense?.CustomerPhone) ? "Chưa cập nhật" : CurrentLicense.CustomerPhone;
        public string Status => CurrentLicense?.Status?.ToUpper() == "ACTIVE" ? "Đang hoạt động" : (CurrentLicense?.Status ?? "N/A");
        public string Hwid => CurrentLicense?.Hwid ?? "N/A";
        
        public string ExpiryDateString 
        {
            get
            {
                if (CurrentLicense?.ExpiryDate == null) return "Vĩnh viễn";
                return CurrentLicense.ExpiryDate.Value.ToString("dd/MM/yyyy");
            }
        }

        public string DaysRemainingString
        {
            get
            {
                if (CurrentLicense?.ExpiryDate == null) return "Không giới hạn";
                int days = CurrentLicense.DaysRemaining;
                if (days <= 0) return "Đã hết hạn";
                return $"Còn {days} ngày";
            }
        }

        public RelayCommand LogoutCommand => new RelayCommand(() =>
        {
            var result = System.Windows.MessageBox.Show("Bạn có chắc chắn muốn đăng xuất bản quyền trên máy này không?", "Xác nhận", 
                System.Windows.MessageBoxButton.YesNo, System.Windows.MessageBoxImage.Question);
            
            if (result == System.Windows.MessageBoxResult.Yes)
            {
                var service = new LicensingService();
                service.Logout();
                
                System.Windows.MessageBox.Show("Đã đăng xuất thành công. Ứng dụng sẽ khởi động lại để áp dụng thay đổi.", "Thông báo", 
                    System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                
                System.Windows.Application.Current.Shutdown();
            }
        });
    }
}
