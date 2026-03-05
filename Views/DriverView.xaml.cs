using System.Windows.Controls;
using System.Windows.Input;
using LapLapAutoTool.Models;
using LapLapAutoTool.ViewModels;

namespace LapLapAutoTool.Views
{
    public partial class DriverView : UserControl
    {
        public DriverView()
        {
            InitializeComponent();
            Loaded += DriverView_Loaded;
        }

        private void DriverView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Sync PasswordBox with ViewModel on load
            if (DataContext is DriverViewModel vm && !string.IsNullOrEmpty(vm.ClientSecret))
            {
                ClientSecretBox.Password = vm.ClientSecret;
            }
        }

        private void DriverRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext is DriverItem item)
            {
                item.IsSelected = !item.IsSelected;
            }
        }

        private void ClientSecretBox_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is DriverViewModel vm)
            {
                vm.ClientSecret = ClientSecretBox.Password;
            }
        }
    }
}
