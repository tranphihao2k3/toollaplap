using System.Windows.Controls;
using System.Windows.Input;
using LapLapAutoTool.Models;

namespace LapLapAutoTool.Views
{
    public partial class DriverView : UserControl
    {
        public DriverView()
        {
            InitializeComponent();
        }

        private void DriverRow_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is System.Windows.FrameworkElement fe && fe.DataContext is DriverItem item)
            {
                item.IsSelected = !item.IsSelected;
            }
        }
    }
}
