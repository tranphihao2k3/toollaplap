using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace LapLapAutoTool.Views
{
    public partial class DiskSpeedTestControl : UserControl
    {
        private CancellationTokenSource? _cts;
        private readonly List<double> _writeHistory = new();
        private readonly List<double> _readHistory = new();
        private double _maxWrite, _maxRead;
        private const double MAX_EXPECTED = 3500.0;
        private string? _tempFile;

        public DiskSpeedTestControl()
        {
            InitializeComponent();
        }

        private int GetTestSizeMb()
        {
            return SizeCombo.SelectedIndex switch
            {
                0 => 64, 1 => 128, 2 => 256, _ => 512
            };
        }

        private async void StartTest(object sender, RoutedEventArgs e)
        {
            _cts = new CancellationTokenSource();
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            _writeHistory.Clear();
            _readHistory.Clear();
            _maxWrite = _maxRead = 0;

            SetStatus("⏺ Đang test...", Color.FromRgb(0x10, 0xB9, 0x81));

            bool loop = LoopCheck.IsChecked == true;
            _tempFile = Path.Combine(Path.GetTempPath(), "laplap_disktest.bin");

            try
            {
                do
                {
                    int testSize = GetTestSizeMb();
                    SetStatus("Ghi dữ liệu test...", Color.FromRgb(0xF5, 0x9E, 0x0B));
                    double writeSpeed = await Task.Run(() => MeasureWrite(_cts.Token, testSize), _cts.Token);
                    if (_cts.IsCancellationRequested) break;

                    SetStatus("Đọc dữ liệu test...", Color.FromRgb(0x25, 0x63, 0xEB));
                    double readSpeed = await Task.Run(() => MeasureRead(_cts.Token), _cts.Token);
                    if (_cts.IsCancellationRequested) break;

                    Dispatcher.Invoke(() => UpdateSpeedUI(writeSpeed, readSpeed));

                } while (loop && !_cts.IsCancellationRequested);
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Disk Speed Test", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                CleanupTempFile();
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                SetStatus("⏹ Hoàn thành", Color.FromRgb(0x94, 0xA3, 0xB8));
            }
        }

        private double MeasureWrite(CancellationToken ct, int sizeMb)
        {
            byte[] data = new byte[4 * 1024 * 1024];
            new Random(42).NextBytes(data);
            int chunks = sizeMb / 4;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var fs = new FileStream(_tempFile!, FileMode.Create, FileAccess.Write,
                FileShare.None, 4096, FileOptions.WriteThrough);
            for (int i = 0; i < chunks; i++)
            {
                ct.ThrowIfCancellationRequested();
                fs.Write(data, 0, data.Length);
            }
            fs.Flush();
            sw.Stop();

            double totalMb = (double)(chunks * data.Length) / (1024 * 1024);
            return totalMb / sw.Elapsed.TotalSeconds;
        }

        private double MeasureRead(CancellationToken ct)
        {
            if (!File.Exists(_tempFile!)) return 0;
            byte[] buf = new byte[4 * 1024 * 1024];

            var sw = System.Diagnostics.Stopwatch.StartNew();
            long totalRead = 0;
            using var fs = new FileStream(_tempFile!, FileMode.Open, FileAccess.Read,
                FileShare.None, 4096, FileOptions.SequentialScan);
            int read;
            while ((read = fs.Read(buf, 0, buf.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                totalRead += read;
            }
            sw.Stop();

            return (totalRead / (1024.0 * 1024.0)) / sw.Elapsed.TotalSeconds;
        }

        private void UpdateSpeedUI(double writeSpeed, double readSpeed)
        {
            WriteSpeedText.Text = $"{writeSpeed:F0}";
            ReadSpeedText.Text = $"{readSpeed:F0}";

            if (writeSpeed > _maxWrite) { _maxWrite = writeSpeed; WriteMaxText.Text = $"Max: {_maxWrite:F0} MB/s"; }
            if (readSpeed > _maxRead) { _maxRead = readSpeed; ReadMaxText.Text = $"Max: {_maxRead:F0} MB/s"; }

            double writeBarW = Math.Min(writeSpeed / MAX_EXPECTED, 1.0) * (WriteBar.Parent as FrameworkElement)!.ActualWidth;
            double readBarW = Math.Min(readSpeed / MAX_EXPECTED, 1.0) * (ReadBar.Parent as FrameworkElement)!.ActualWidth;
            WriteBar.Width = Math.Max(0, writeBarW);
            ReadBar.Width = Math.Max(0, readBarW);

            _writeHistory.Add(writeSpeed);
            _readHistory.Add(readSpeed);
            if (_writeHistory.Count > 40) { _writeHistory.RemoveAt(0); _readHistory.RemoveAt(0); }

            DrawChart();

            SetStatus($"Write: {writeSpeed:F1} MB/s  |  Read: {readSpeed:F1} MB/s",
                Color.FromRgb(0x10, 0xB9, 0x81));
        }

        private void DrawChart()
        {
            double w = ChartCanvas.ActualWidth;
            double h = ChartCanvas.ActualHeight;
            if (w < 10 || h < 10 || _writeHistory.Count < 2) return;

            double maxVal = Math.Max(Math.Max(_maxWrite, _maxRead), 10);
            double step = w / (_writeHistory.Count - 1);

            var wp = new PointCollection();
            var rp = new PointCollection();

            for (int i = 0; i < _writeHistory.Count; i++)
            {
                double x = i * step;
                wp.Add(new Point(x, h - (_writeHistory[i] / maxVal * h * 0.90)));
                rp.Add(new Point(x, h - (_readHistory[i] / maxVal * h * 0.90)));
            }

            WriteChart.Points = wp;
            ReadChart.Points = rp;
        }

        private void SetStatus(string msg, Color dot)
        {
            Dispatcher.Invoke(() =>
            {
                StatusText.Text = msg;
                StatusDot.Fill = new SolidColorBrush(dot);
            });
        }

        private void StopTest(object sender, RoutedEventArgs e) => _cts?.Cancel();

        private void CleanupTempFile()
        {
            try { if (_tempFile != null && File.Exists(_tempFile)) File.Delete(_tempFile); }
            catch { }
        }

        public void Cleanup()
        {
            _cts?.Cancel();
            CleanupTempFile();
        }
    }
}
