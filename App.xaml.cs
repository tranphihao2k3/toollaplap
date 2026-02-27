using System.Configuration;
using System.Data;
using System.Windows;

namespace LapLapAutoTool;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        if (!IsRunningAsAdministrator())
        {
            MessageBox.Show("Công cụ LapLap yêu cầu quyền Quản trị viên để thực hiện tối ưu hệ thống và cài đặt phần mềm.\n\nVui lòng khởi động lại ứng dụng bằng quyền Administrator.", 
                            "Quyền truy cập bị từ chối", MessageBoxButton.OK, MessageBoxImage.Warning);
            System.Environment.Exit(0);
        }
        base.OnStartup(e);
    }

    private bool IsRunningAsAdministrator()
    {
        var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
        var principal = new System.Security.Principal.WindowsPrincipal(identity);
        return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
    }
}

