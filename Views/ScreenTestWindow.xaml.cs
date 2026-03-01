using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LapLapAutoTool.Views
{
    public partial class ScreenTestWindow : Window
    {
        private readonly (Color color, string name)[] _colors = new[]
        {
            (Colors.Black,    "ĐEN - Tìm điểm sáng bất thường"),
            (Colors.White,    "TRẮNG - Tìm điểm tối bất thường"),
            (Colors.Red,      "ĐỎ - Dead pixel màu đỏ"),
            (Colors.Green,    "XANH LÁ - Dead pixel màu xanh"),
            (Colors.Blue,     "XANH DƯƠNG - Dead pixel màu xanh"),
            (Colors.Gray,     "XÁM - Gradient kiểm tra"),
        };
        private int _currentIndex = 0;

        public ScreenTestWindow()
        {
            InitializeComponent();
            UpdateColor();
        }

        private void UpdateColor()
        {
            var (color, name) = _colors[_currentIndex];
            ColorPanel.Background = new SolidColorBrush(color);
            
            bool isDark = (color.R + color.G + color.B) < 384;
            var textColor = isDark ? Colors.White : Colors.Black;
            var textBrush = new SolidColorBrush(textColor);

            InfoText.Foreground = textBrush;
            ColorNameText.Foreground = textBrush;
            ColorNameText.Text = name;
            HintText.Foreground = textBrush;
            StepText.Foreground = textBrush;
            StepText.Text = $"{_currentIndex + 1} / {_colors.Length}";
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _currentIndex = (_currentIndex + 1) % _colors.Length;
            UpdateColor();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
                Close();
            else if (e.Key == Key.Right || e.Key == Key.Space)
            {
                _currentIndex = (_currentIndex + 1) % _colors.Length;
                UpdateColor();
            }
            else if (e.Key == Key.Left)
            {
                _currentIndex = (_currentIndex - 1 + _colors.Length) % _colors.Length;
                UpdateColor();
            }
        }
    }
}
