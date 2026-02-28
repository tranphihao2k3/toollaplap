using System;
using System.Windows;
using System.Windows.Input;
using LapLapAutoTool.Services;

namespace LapLapAutoTool.Views
{
    public partial class ActivationWindow : Window
    {
        private readonly LicensingService _licensingService;
        public bool IsAuthorized { get; private set; }

        public ActivationWindow()
        {
            InitializeComponent();
            _licensingService = new LicensingService();
            HwidText.Text = _licensingService.GetHWID();
            
            this.Loaded += ActivationWindow_Loaded;
        }

        private async void ActivationWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Tự động điền token cũ nếu có
            string? savedToken = _licensingService.LoadSavedToken();
            if (!string.IsNullOrEmpty(savedToken))
            {
                TokenInput.Text = savedToken;
                StatusMessage.Text = "Đang kiểm tra bảo mật...";
                ActivateBtn.IsEnabled = false;

                string hwid = _licensingService.GetHWID();
                var (success, message, _) = await _licensingService.CheckLicenseAsync(savedToken, hwid);
                
                if (success)
                {
                    IsAuthorized = true;
                    this.DialogResult = true;
                    this.Close();
                }
                else
                {
                    StatusMessage.Text = message;
                    StatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                    ActivateBtn.IsEnabled = true;
                }
            }
        }

        private async void Activate_Click(object sender, RoutedEventArgs e)
        {
            string token = TokenInput.Text.Trim();
            if (string.IsNullOrEmpty(token))
            {
                StatusMessage.Text = "Vui lòng nhập License Key!";
                StatusMessage.Foreground = System.Windows.Media.Brushes.Orange;
                return;
            }

            ActivateBtn.IsEnabled = false;
            StatusMessage.Text = "Đang xác thực bản quyền...";
            StatusMessage.Foreground = System.Windows.Media.Brushes.White;

            string hwid = _licensingService.GetHWID();
            var (success, message, _) = await _licensingService.CheckLicenseAsync(token, hwid);

            if (success)
            {
                _licensingService.SaveToken(token);
                IsAuthorized = true;
                this.DialogResult = true;
                this.Close();
            }
            else
            {
                StatusMessage.Text = message;
                StatusMessage.Foreground = System.Windows.Media.Brushes.Red;
                ActivateBtn.IsEnabled = true;
            }
        }

        private void CopyHwid_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(HwidText.Text);
            MessageBox.Show("Đã sao chép mã HWID vào bộ nhớ đệm!", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        }
    }
}
