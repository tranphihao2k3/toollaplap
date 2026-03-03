using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace LapLapAutoTool.Views
{
    public partial class KeyboardTestControl : UserControl
    {
        private readonly Dictionary<Key, Border> _keyBorders = new();
        private readonly HashSet<Key> _testedKeys = new();

        private const double KW = 28, KH = 26, G = 2;
        private const double U = KW + G;

        private static readonly (Key? key, string label, double w, double h, double x, double y)[] AllKeys = BuildKeys();

        private static (Key? key, string label, double w, double h, double x, double y)[] BuildKeys()
        {
            var keys = new List<(Key? key, string label, double w, double h, double x, double y)>();
            double rowH = KH + G;
            double navX = 15.5 * U;
            double numX = 19 * U;

            // ── Row 0: Function row ──────────────────────────────
            double y0 = 0;
            keys.Add((Key.Escape, "Esc", 1, 1, 0, y0));
            keys.Add((Key.F1, "F1", 1, 1, 2 * U, y0));
            keys.Add((Key.F2, "F2", 1, 1, 3 * U, y0));
            keys.Add((Key.F3, "F3", 1, 1, 4 * U, y0));
            keys.Add((Key.F4, "F4", 1, 1, 5 * U, y0));
            keys.Add((Key.F5, "F5", 1, 1, 6.5 * U, y0));
            keys.Add((Key.F6, "F6", 1, 1, 7.5 * U, y0));
            keys.Add((Key.F7, "F7", 1, 1, 8.5 * U, y0));
            keys.Add((Key.F8, "F8", 1, 1, 9.5 * U, y0));
            keys.Add((Key.F9, "F9", 1, 1, 11 * U, y0));
            keys.Add((Key.F10, "F10", 1, 1, 12 * U, y0));
            keys.Add((Key.F11, "F11", 1, 1, 13 * U, y0));
            keys.Add((Key.F12, "F12", 1, 1, 14 * U, y0));
            keys.Add((Key.Snapshot, "PrtSc", 1, 1, navX, y0));
            keys.Add((Key.Scroll, "ScrLk", 1, 1, navX + U, y0));
            keys.Add((Key.Pause, "Pause", 1, 1, navX + 2 * U, y0));

            // ── Row 1: Number row ────────────────────────────────
            double y1 = rowH + 4;
            string[] numL = { "`", "1", "2", "3", "4", "5", "6", "7", "8", "9", "0", "-", "=" };
            Key[] numK = {
                Key.OemTilde, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5,
                Key.D6, Key.D7, Key.D8, Key.D9, Key.D0, Key.OemMinus, Key.OemPlus
            };
            for (int i = 0; i < numK.Length; i++)
                keys.Add((numK[i], numL[i], 1, 1, i * U, y1));
            keys.Add((Key.Back, "⌫", 2, 1, 13 * U, y1));
            // Nav
            keys.Add((Key.Insert, "Ins", 1, 1, navX, y1));
            keys.Add((Key.Home, "Home", 1, 1, navX + U, y1));
            keys.Add((Key.PageUp, "PgUp", 1, 1, navX + 2 * U, y1));
            // Numpad
            keys.Add((Key.NumLock, "Num", 1, 1, numX, y1));
            keys.Add((Key.Divide, "/", 1, 1, numX + U, y1));
            keys.Add((Key.Multiply, "*", 1, 1, numX + 2 * U, y1));
            keys.Add((Key.Subtract, "-", 1, 1, numX + 3 * U, y1));

            // ── Row 2: QWERTY ────────────────────────────────────
            double y2 = y1 + rowH;
            keys.Add((Key.Tab, "Tab", 1.5, 1, 0, y2));
            string[] r2L = { "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "[", "]" };
            Key[] r2K = {
                Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y,
                Key.U, Key.I, Key.O, Key.P,
                Key.OemOpenBrackets, Key.OemCloseBrackets
            };
            for (int i = 0; i < r2K.Length; i++)
                keys.Add((r2K[i], r2L[i], 1, 1, 1.5 * U + i * U, y2));
            keys.Add((Key.OemPipe, "\\", 1.5, 1, 13.5 * U, y2));
            // Nav
            keys.Add((Key.Delete, "Del", 1, 1, navX, y2));
            keys.Add((Key.End, "End", 1, 1, navX + U, y2));
            keys.Add((Key.PageDown, "PgDn", 1, 1, navX + 2 * U, y2));
            // Numpad
            keys.Add((Key.NumPad7, "7", 1, 1, numX, y2));
            keys.Add((Key.NumPad8, "8", 1, 1, numX + U, y2));
            keys.Add((Key.NumPad9, "9", 1, 1, numX + 2 * U, y2));
            keys.Add((Key.Add, "+", 1, 2, numX + 3 * U, y2));

            // ── Row 3: ASDF ──────────────────────────────────────
            double y3 = y2 + rowH;
            keys.Add((Key.CapsLock, "Caps", 1.75, 1, 0, y3));
            string[] r3L = { "A", "S", "D", "F", "G", "H", "J", "K", "L", ";", "'" };
            Key[] r3K = {
                Key.A, Key.S, Key.D, Key.F, Key.G,
                Key.H, Key.J, Key.K, Key.L,
                Key.OemSemicolon, Key.OemQuotes
            };
            for (int i = 0; i < r3K.Length; i++)
                keys.Add((r3K[i], r3L[i], 1, 1, 1.75 * U + i * U, y3));
            keys.Add((Key.Return, "Enter", 2.25, 1, 12.75 * U, y3));
            // Numpad
            keys.Add((Key.NumPad4, "4", 1, 1, numX, y3));
            keys.Add((Key.NumPad5, "5", 1, 1, numX + U, y3));
            keys.Add((Key.NumPad6, "6", 1, 1, numX + 2 * U, y3));

            // ── Row 4: ZXCV ──────────────────────────────────────
            double y4 = y3 + rowH;
            keys.Add((Key.LeftShift, "Shift", 2.25, 1, 0, y4));
            string[] r4L = { "Z", "X", "C", "V", "B", "N", "M", ",", ".", "/" };
            Key[] r4K = {
                Key.Z, Key.X, Key.C, Key.V, Key.B,
                Key.N, Key.M,
                Key.OemComma, Key.OemPeriod, Key.OemQuestion
            };
            for (int i = 0; i < r4K.Length; i++)
                keys.Add((r4K[i], r4L[i], 1, 1, 2.25 * U + i * U, y4));
            keys.Add((Key.RightShift, "Shift", 2.75, 1, 12.25 * U, y4));
            // Arrow
            keys.Add((Key.Up, "▲", 1, 1, navX + U, y4));
            // Numpad
            keys.Add((Key.NumPad1, "1", 1, 1, numX, y4));
            keys.Add((Key.NumPad2, "2", 1, 1, numX + U, y4));
            keys.Add((Key.NumPad3, "3", 1, 1, numX + 2 * U, y4));
            keys.Add((null, "Ent", 1, 2, numX + 3 * U, y4));

            // ── Row 5: Bottom ────────────────────────────────────
            double y5 = y4 + rowH;
            keys.Add((Key.LeftCtrl, "Ctrl", 1.25, 1, 0, y5));
            keys.Add((Key.LWin, "Win", 1.25, 1, 1.25 * U, y5));
            keys.Add((Key.LeftAlt, "Alt", 1.25, 1, 2.5 * U, y5));
            keys.Add((Key.Space, "Space", 6.25, 1, 3.75 * U, y5));
            keys.Add((Key.RightAlt, "Alt", 1.25, 1, 10 * U, y5));
            keys.Add((null, "Fn", 1.25, 1, 11.25 * U, y5));
            keys.Add((Key.Apps, "Menu", 1.25, 1, 12.5 * U, y5));
            keys.Add((Key.RightCtrl, "Ctrl", 1.25, 1, 13.75 * U, y5));
            // Arrows
            keys.Add((Key.Left, "◄", 1, 1, navX, y5));
            keys.Add((Key.Down, "▼", 1, 1, navX + U, y5));
            keys.Add((Key.Right, "►", 1, 1, navX + 2 * U, y5));
            // Numpad
            keys.Add((Key.NumPad0, "0", 2, 1, numX, y5));
            keys.Add((Key.Decimal, ".", 1, 1, numX + 2 * U, y5));

            return keys.ToArray();
        }

        public KeyboardTestControl()
        {
            InitializeComponent();
        }

        private void Control_Loaded(object sender, RoutedEventArgs e)
        {
            BuildKeyboardUI();
            Focus();
        }

        private void BuildKeyboardUI()
        {
            var canvas = KeyboardCanvas;
            canvas.Children.Clear();
            _keyBorders.Clear();

            foreach (var (key, label, widthUnits, heightUnits, xPos, yPos) in AllKeys)
            {
                double w = widthUnits * KW + (widthUnits - 1) * G;
                double h = heightUnits * KH + (heightUnits - 1) * G;

                var tb = new TextBlock
                {
                    Text = label,
                    FontSize = label.Length > 4 ? 6.5 : label.Length > 2 ? 7.5 : 9,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = key == null
                        ? new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
                        : new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFC)),
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
                    CornerRadius = new CornerRadius(4),
                    Background = key == null
                        ? new SolidColorBrush(Color.FromRgb(0x14, 0x1E, 0x30))
                        : new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55)),
                    BorderThickness = new Thickness(1),
                    Child = tb,
                    Tag = key
                };

                Canvas.SetLeft(border, xPos);
                Canvas.SetTop(border, yPos);
                canvas.Children.Add(border);

                if (key.HasValue && !_keyBorders.ContainsKey(key.Value))
                    _keyBorders[key.Value] = border;
            }
        }

        private void Control_PreviewKeyDown(object sender, KeyEventArgs e)
        {
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

            PressedKeyText.Text = $"{actualKey}";
            StatusText.Text = $"Đã test: {_testedKeys.Count} / {_keyBorders.Count}";
        }

        private void Control_KeyDown(object sender, KeyEventArgs e)
        {
            var actualKey = e.Key == Key.System ? e.SystemKey : e.Key;
            HandleKeyPress(actualKey);
        }

        private void Control_KeyUp(object sender, KeyEventArgs e)
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
            StatusText.Text = $"Đã test: 0 / {_keyBorders.Count}";
            PressedKeyText.Text = "-";
            Focus();
        }

        public void Cleanup() { }
    }
}
