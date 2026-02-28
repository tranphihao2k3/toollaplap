using System.Windows;
using System.Windows.Controls;
using LapLapAutoTool.Models;
using LapLapAutoTool.ViewModels;

namespace LapLapAutoTool.Views
{
    public partial class StudentAppsView : UserControl
    {
        public StudentAppsView()
        {
            InitializeComponent();
        }

        private void DownloadButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is StudentAppVersion item && DataContext is StudentAppsViewModel vm)
            {
                vm.CreateDownloadCommand(item).Execute(null);
            }
        }
    }
}
