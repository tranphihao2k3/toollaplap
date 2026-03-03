using System.Configuration;
using System.Data;
using System.Windows;

namespace LapLapAutoTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            System.IO.File.WriteAllText(@"c:\Users\HAO\.gemini\antigravity\scratch\LapLapAutoTool\crash.log", args.Exception.ToString());
            System.Environment.Exit(1);
        };

        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show("Công cụ LapLap yêu cầu quyền Quản trị viên để thực hiện tối ưu hệ thống và cài đặt phần mềm.\n\nVui lòng khởi động lại ứng dụng bằng quyền Administrator.", 
                            "Quyền truy cập bị từ chối", MessageBoxButton.OK, MessageBoxImage.Warning);
            System.Environment.Exit(0);
        }

        if (!CheckInternetConnection())
        {
            MessageBox.Show("Công cụ LapLap yêu cầu kết nối Internet để xác thực và tải cấu hình mới nhất.\n\nVui lòng kết nối mạng và mở lại ứng dụng.", 
                            "Yêu cầu Internet", MessageBoxButton.OK, MessageBoxImage.Error);
            System.Environment.Exit(0);
        }

        // Kiểm tra bản quyền ngầm trước
        var licensingService = new Services.LicensingService();
        string? savedToken = licensingService.LoadSavedToken();
        bool isAlreadyLicensed = false;

        if (!string.IsNullOrEmpty(savedToken))
        {
            string hwid = licensingService.GetHWID();
            var (success, _, _) = await licensingService.CheckLicenseAsync(savedToken, hwid);
            isAlreadyLicensed = success;
        }

        // Chỉnh ShutdownMode để app không tự tắt khi popup kích hoạt đóng lại
        Application.Current.ShutdownMode = ShutdownMode.OnExplicitShutdown;

        if (!isAlreadyLicensed)
        {
            // Bypass activation for testing
            // var activation = new Views.ActivationWindow();
            // if (activation.ShowDialog() != true)
            // {
            //     Application.Current.Shutdown();
            //     return;
            // }
        }

        // Kích hoạt thành công (hoặc đã kích hoạt từ trước), đưa ShutdownMode về bình thường
        Application.Current.ShutdownMode = ShutdownMode.OnLastWindowClose;

        base.OnStartup(e);

        try
        {
            // Mở màn hình chính của ứng dụng
            var mainWindow = new MainWindow();
            this.MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (System.Exception ex)
        {
            System.IO.File.WriteAllText(@"c:\Users\HAO\.gemini\antigravity\scratch\LapLapAutoTool\crash2.log", ex.ToString());
            System.Environment.Exit(2);
        }
    }

    private bool CheckInternetConnection()
    {
        try
        {
            using (var client = new System.Net.Http.HttpClient())
            {
                client.Timeout = System.TimeSpan.FromSeconds(5);
                using (var response = client.GetAsync("http://www.google.com").Result)
                {
                    return response.IsSuccessStatusCode;
                }
            }
        }
        catch
        {
            return false;
        }
    }

    private bool IsRunningAsAdministrator()
    {
        return true;
    }
}

