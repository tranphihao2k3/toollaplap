using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LapLapAutoTool.Views
{
    public partial class TouchpadTestControl : UserControl
    {
        private int _leftClicks = 0;
        private int _rightClicks = 0;
        private bool _isDrawing = false;
        private readonly List<Polyline> _trails = new();

        public TouchpadTestControl()
        {
            InitializeComponent();
        }

        private void TouchArea_MouseMove(object sender, MouseEventArgs e)
        {
            var pos = e.GetPosition((FrameworkElement)sender);
            PosText.Text = $"X:{(int)pos.X}  Y:{(int)pos.Y}";

            CursorTransform.X = pos.X - 9;
            CursorTransform.Y = pos.Y - 9;
            CursorDot.Visibility = Visibility.Visible;
            HintPanel.Visibility = Visibility.Collapsed;

            if (_isDrawing && e.LeftButton == MouseButtonState.Pressed)
            {
                if (TrailCanvas.Children.Count > 0 && TrailCanvas.Children[^1] is Polyline active)
                {
                    var canvasPos = e.GetPosition(TrailCanvas);
                    active.Points.Add(canvasPos);
                }
            }
        }

        private void TouchArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _leftClicks++;
            LClickText.Text = $"{_leftClicks} lần";
            _isDrawing = true;

            var trail = new Polyline
            {
                Stroke = new SolidColorBrush(Color.FromArgb(180, 0x22, 0xC5, 0x5E)),
                StrokeThickness = 3,
                StrokeLineJoin = PenLineJoin.Round,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            var pos = e.GetPosition(TrailCanvas);
            trail.Points.Add(pos);
            TrailCanvas.Children.Add(trail);
        }

        private void TouchArea_MouseUp(object sender, MouseButtonEventArgs e)
        {
            _isDrawing = false;
        }

        private void TouchArea_RightDown(object sender, MouseButtonEventArgs e)
        {
            _rightClicks++;
            RClickText.Text = $"{_rightClicks} lần";

            var pos = e.GetPosition(TrailCanvas);
            var dot = new Ellipse
            {
                Width = 14, Height = 14,
                Fill = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B))
            };
            Canvas.SetLeft(dot, pos.X - 7);
            Canvas.SetTop(dot, pos.Y - 7);
            TrailCanvas.Children.Add(dot);
        }

        private void TouchArea_Leave(object sender, MouseEventArgs e)
        {
            _isDrawing = false;
            CursorDot.Visibility = Visibility.Collapsed;
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            TrailCanvas.Children.Clear();
            _leftClicks = 0;
            _rightClicks = 0;
            LClickText.Text = "0 lần";
            RClickText.Text = "0 lần";
            HintPanel.Visibility = Visibility.Visible;
        }

        public void Cleanup()
        {
            _isDrawing = false;
            TrailCanvas.Children.Clear();
        }
    }
}
