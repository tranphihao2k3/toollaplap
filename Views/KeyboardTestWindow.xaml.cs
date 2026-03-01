using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LapLapAutoTool.Views
{
    public partial class KeyboardTestWindow : Window
    {
        private readonly Dictionary<Key, Border> _keyBorders = new();
        private readonly HashSet<Key> _testedKeys = new();

        // KW = key width unit, KH = height, G = gap
        private const double KW = 40, KH = 40, G = 5;
        private const double U = KW + G; // 1 unit step = 45px

        // Each entry: (Key? key, label, widthInUnits, xOffset, yRow)
        // Sections: MainBlock starts x=0, NavCluster starts after gap, Numpad after that
        private static readonly (Key? key, string label, double w, double x, double y)[] AllKeys = BuildKeys();

        private static (Key? key, string label, double w, double x, double y)[] BuildKeys()
        {
            var keys = new List<(Key? key, string label, double w, double x, double y)>();

            double rowH = KH + G;

            // ── Main block ────────────────────────────────────────────

            // Row 0 – Function keys (y=0)
            double y0 = 0;
            keys.Add((Key.Escape, "Esc", 1, 0, y0));
            // F1-F4 with gap
            keys.Add((Key.F1, "F1", 1, 2 * U, y0));
            keys.Add((Key.F2, "F2", 1, 3 * U, y0));
            keys.Add((Key.F3, "F3", 1, 4 * U, y0));
            keys.Add((Key.F4, "F4", 1, 5 * U, y0));
            // F5-F8
            keys.Add((Key.F5, "F5", 1, 6.5 * U, y0));
            keys.Add((Key.F6, "F6", 1, 7.5 * U, y0));
            keys.Add((Key.F7, "F7", 1, 8.5 * U, y0));
            keys.Add((Key.F8, "F8", 1, 9.5 * U, y0));
            // F9-F12
            keys.Add((Key.F9, "F9", 1, 11 * U, y0));
            keys.Add((Key.F10, "F10", 1, 12 * U, y0));
            keys.Add((Key.F11, "F11", 1, 13 * U, y0));
            keys.Add((Key.F12, "F12", 1, 14 * U, y0));

            // Row 1 – Number row (y=1 with extra gap)
            double y1 = rowH * 1.5;
            double[] numRowX = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 };
            string[] numLabels = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
            Key[] numKeys = {
                Key.OemTilde, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5,
                Key.D6, Key.D7, Key.D8, Key.D9, Key.D0, Key.OemMinus, Key.OemPlus
            };
            for (int i = 0; i < numKeys.Length; i++)
                keys.Add((numKeys[i], numLabels[i], 1, numRowX[i] * U, y1));
            keys.Add((Key.Back, "⌫ Back", 2, 13 * U, y1)); // 2u wide

            // Row 2 – QWERTY (y=2)
            double y2 = y1 + rowH;
            keys.Add((Key.Tab, "Tab", 1.5, 0, y2));
            string[] row2l = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]", "\\" };
            Key[] row2k = {
                Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y,
                Key.U, Key.I, Key.O, Key.P,
                Key.OemOpenBrackets, Key.OemCloseBrackets, Key.OemPipe
            };
            for (int i = 0; i < row2k.Length; i++)
                keys.Add((row2k[i], row2l[i], 1, 1.5 * U + i * U, y2));

            // Row 3 – ASDF
            double y3 = y2 + rowH;
            keys.Add((Key.CapsLock, "Caps", 1.75, 0, y3));
            string[] row3l = { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'" };
            Key[] row3k = {
                Key.A, Key.S, Key.D, Key.F, Key.G,
                Key.H, Key.J, Key.K, Key.L,
                Key.OemSemicolon, Key.OemQuotes
            };
            for (int i = 0; i < row3k.Length; i++)
                keys.Add((row3k[i], row3l[i], 1, 1.75 * U + i * U, y3));
            keys.Add((Key.Return, "Enter", 2.25, 12.75 * U, y3));

            // Row 4 – ZXCV
            double y4 = y3 + rowH;
            keys.Add((Key.LeftShift, "Shift", 2.25, 0, y4));
            string[] row4l = { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" };
            Key[] row4k = {
                Key.Z, Key.X, Key.C, Key.V, Key.B,
                Key.N, Key.M,
                Key.OemComma, Key.OemPeriod, Key.OemQuestion
            };
            for (int i = 0; i < row4k.Length; i++)
                keys.Add((row4k[i], row4l[i], 1, 2.25 * U + i * U, y4));
            keys.Add((Key.RightShift, "Shift", 2.75, 12.25 * U, y4));

            // Row 5 – Bottom
            double y5 = y4 + rowH;
            keys.Add((Key.LeftCtrl, "Ctrl", 1.25, 0, y5));
            keys.Add((Key.LWin, "Win", 1.25, 1.25 * U, y5));
            keys.Add((Key.LeftAlt, "Alt", 1.25, 2.5 * U, y5));
            keys.Add((Key.Space, "Space", 6.25, 3.75 * U, y5));
            keys.Add((Key.RightAlt, "Alt", 1.25, 10 * U, y5));
            keys.Add((Key.RightCtrl, "Ctrl", 1.25, 11.25 * U, y5));
            // Win + Menu can be optional 
            keys.Add((Key.Apps, "Menu", 1.25, 12.5 * U, y5));

            // ── Navigation cluster (offset: 15.5u) ─────────────────────
            double navX = 15.5 * U;

            // Row 0 – PrtSc ScrLk Pause
            keys.Add((Key.PrintScreen, "PrtSc", 1, navX, y0));
            keys.Add((Key.Scroll, "ScrLk", 1, navX + U, y0));
            keys.Add((Key.Pause, "Pause", 1, navX + 2 * U, y0));

            // Row 1 – Insert Home PgUp
            keys.Add((Key.Insert, "Ins", 1, navX, y1));
            keys.Add((Key.Home, "Home", 1, navX + U, y1));
            keys.Add((Key.PageUp, "PgUp", 1, navX + 2 * U, y1));

            // Row 2 – Delete End PgDn
            keys.Add((Key.Delete, "Del", 1, navX, y2));
            keys.Add((Key.End, "End", 1, navX + U, y2));
            keys.Add((Key.PageDown, "PgDn", 1, navX + 2 * U, y2));

            // Arrow keys
            keys.Add((Key.Up, "▲", 1, navX + U, y4));
            keys.Add((Key.Left, "◄", 1, navX, y5));
            keys.Add((Key.Down, "▼", 1, navX + U, y5));
            keys.Add((Key.Right, "►", 1, navX + 2 * U, y5));

            // ── Numpad (offset: 19.25u) ─────────────────────────────────
            double npX = 19.25 * U;

            keys.Add((Key.NumLock, "NumLk", 1, npX, y1));
            keys.Add((Key.Divide, "/ Num", 1, npX + U, y1));
            keys.Add((Key.Multiply, "*", 1, npX + 2 * U, y1));
            keys.Add((Key.Subtract, "−", 1, npX + 3 * U, y1));

            keys.Add((Key.NumPad7, "7 Home", 1, npX, y2));
            keys.Add((Key.NumPad8, "8 ▲", 1, npX + U, y2));
            keys.Add((Key.NumPad9, "9 PgUp", 1, npX + 2 * U, y2));
            keys.Add((Key.Add, "+", 1, npX + 3 * U, y2)); // tall in real kbd – simplified

            keys.Add((Key.NumPad4, "4 ◄", 1, npX, y3));
            keys.Add((Key.NumPad5, "5", 1, npX + U, y3));
            keys.Add((Key.NumPad6, "6 ►", 1, npX + 2 * U, y3));

            keys.Add((Key.NumPad1, "1 End", 1, npX, y4));
            keys.Add((Key.NumPad2, "2 ▼", 1, npX + U, y4));
            keys.Add((Key.NumPad3, "3 PgDn", 1, npX + 2 * U, y4));
            keys.Add((Key.Return, "Enter", 1, npX + 3 * U, y4)); // numpad enter

            keys.Add((Key.NumPad0, "0 Ins", 2, npX, y5));
            keys.Add((Key.Decimal, ". Del", 1, npX + 2 * U, y5));

            return keys.ToArray();
        }

        public KeyboardTestWindow()
        {
            InitializeComponent();
            Loaded += (s, e) => BuildKeyboardUI();
        }

        private void BuildKeyboardUI()
        {
            var canvas = KeyboardCanvas;
            canvas.Children.Clear();
            _keyBorders.Clear();

            foreach (var (key, label, widthUnits, xPos, yPos) in AllKeys)
            {
                double w = widthUnits * KW + (widthUnits - 1) * G;
                double h = KH;

                var tb = new TextBlock
                {
                    Text = label,
                    FontSize = label.Length > 4 ? 8 : label.Length > 3 ? 9 : 11,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    TextWrapping = TextWrapping.Wrap,
                    IsHitTestVisible = false
                };

                var border = new Border
                {
                    Width = w,
                    Height = h,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                    BorderThickness = new Thickness(1),
                    Child = tb,
                    Tag = key
                };

                Canvas.SetLeft(border, xPos);
                Canvas.SetTop(border, yPos);
                canvas.Children.Add(border);

                // Map key to border (don't overwrite if key already mapped – e.g. dual Enter/numpad)
                if (key.HasValue && !_keyBorders.ContainsKey(key.Value))
                    _keyBorders[key.Value] = border;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // Chặn WPF tự đóng window khi nhấn Esc, xử lý Esc như phím bình thường
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                HandleKeyPress(Key.Escape);
            }
        }

        private void HandleKeyPress(Key actualKey)
        {
            if (_keyBorders.TryGetValue(actualKey, out var border))
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                _testedKeys.Add(actualKey);
                border.Opacity = 1.0;
            }

            PressedKeyText.Text = $"Phím: {actualKey}";
            StatusText.Text = $"Đã test: {_testedKeys.Count} / {_keyBorders.Count} phím";
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            HandleKeyPress(actualKey);
        }

        private void Window_KeyUp(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;

            if (_keyBorders.TryGetValue(actualKey, out var border))
            {
                if (_testedKeys.Contains(actualKey))
                {
                    border.Background = new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46));
                    border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81));
                }
            }
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _testedKeys.Clear();
            foreach (var (_, border) in _keyBorders)
            {
                border.Background = new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B));
                border.BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
            }
            StatusText.Text = $"Đã test: 0 / {_keyBorders.Count} phím";
            PressedKeyText.Text = "-";
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();
    }
}
