using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using NAudio.Wave;

namespace LapLapAutoTool.Views
{
    public partial class MicTestControl : UserControl
    {
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private bool _isRecording;
        private string? _tempRecordPath;
        private TimeSpan _recordDuration;

        private WaveOutEvent? _waveOut;
        private AudioFileReader? _reader;

        private readonly DispatcherTimer _uiTimer;
        private float _currentVolume;
        private DateTime _recordStart;
        private readonly List<float> _waveData = new();
        private readonly object _waveLock = new();

        public MicTestControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
            _uiTimer.Tick += UpdateUI;
            _uiTimer.Start();
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => LoadDevices();

        private void LoadDevices()
        {
            int count = WaveInEvent.DeviceCount;
            DeviceCombo.Items.Clear();

            if (count == 0)
            {
                DeviceCombo.Items.Add("Không tìm thấy microphone");
                DeviceCombo.SelectedIndex = 0;
                StatusLabel.Text = "Không tìm thấy thiết bị âm thanh vào";
                return;
            }

            for (int i = 0; i < count; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                DeviceCombo.Items.Add($"{i}: {caps.ProductName}");
            }
            DeviceCombo.SelectedIndex = 0;
        }

        private void DeviceCombo_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (_isRecording) StopRecording();
            StopCapture();

            int index = DeviceCombo.SelectedIndex;
            if (index < 0 || index >= WaveInEvent.DeviceCount) return;

            StartCapture(index);
        }

        private void StartCapture(int deviceIndex)
        {
            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(44100, 1),
                    BufferMilliseconds = 40
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                SetStatus("Đang lắng nghe…", Color.FromRgb(0x10, 0xB9, 0x81));
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }

        private void StopCapture()
        {
            try { _waveIn?.StopRecording(); } catch { }
            _waveIn?.Dispose();
            _waveIn = null;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            float max = 0;
            var samples = new List<float>();
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short s = BitConverter.ToInt16(e.Buffer, i);
                float f = s / 32768f;
                if (Math.Abs(f) > max) max = Math.Abs(f);
                if (i % 6 == 0) samples.Add(f);
            }
            _currentVolume = max;

            lock (_waveLock)
            {
                _waveData.AddRange(samples);
                if (_waveData.Count > 600)
                    _waveData.RemoveRange(0, _waveData.Count - 600);
            }

            _writer?.Write(e.Buffer, 0, e.BytesRecorded);
        }

        private void ToggleRecord(object sender, RoutedEventArgs e)
        {
            if (_isRecording) StopRecording();
            else StartRecording();
        }

        private void StartRecording()
        {
            if (_waveIn == null) return;

            _tempRecordPath = Path.Combine(Path.GetTempPath(), $"mic_test_{Guid.NewGuid()}.wav");
            _writer = new WaveFileWriter(_tempRecordPath, _waveIn.WaveFormat);
            _isRecording = true;
            _recordStart = DateTime.Now;

            BtnRecord.Content = "⏹  Dừng Ghi";
            SetStatus("⏺ Đang ghi âm…", Color.FromRgb(0xEF, 0x44, 0x44));
        }

        private void StopRecording()
        {
            _isRecording = false;
            _recordDuration = DateTime.Now - _recordStart;

            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;

            BtnRecord.Content = "⏺  Ghi Âm";
            SetStatus($"Đã ghi {_recordDuration.TotalSeconds:F1}s — Nhấn Phát lại để nghe", Color.FromRgb(0x10, 0xB9, 0x81));

            if (!string.IsNullOrEmpty(_tempRecordPath) && File.Exists(_tempRecordPath))
            {
                RecordedInfo.Text = $"Đã ghi: {_recordDuration:mm\\:ss}";
                PlaybackPanel.Visibility = Visibility.Visible;
            }
        }

        private void PlayRecording(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_tempRecordPath) || !File.Exists(_tempRecordPath)) return;

            _waveOut?.Stop();
            _waveOut?.Dispose();
            _reader?.Dispose();

            try
            {
                _reader = new AudioFileReader(_tempRecordPath);
                _waveOut = new WaveOutEvent();
                _waveOut.Init(_reader);
                _waveOut.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
                {
                    BtnPlay.Content = "▶ Phát lại";
                    SetStatus("⏹ Phát xong", Color.FromRgb(0x94, 0xA3, 0xB8));
                });
                _waveOut.Play();
                BtnPlay.Content = "⏸ Đang phát";
                SetStatus("▶ Đang phát lại bản ghi…", Color.FromRgb(0xF5, 0x9E, 0x0B));
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi phát: {ex.Message}", Color.FromRgb(0xEF, 0x44, 0x44));
            }
        }

        private void DeleteRecording(object sender, RoutedEventArgs e)
        {
            _waveOut?.Stop();
            if (!string.IsNullOrEmpty(_tempRecordPath) && File.Exists(_tempRecordPath))
                File.Delete(_tempRecordPath);
            _tempRecordPath = null;
            PlaybackPanel.Visibility = Visibility.Collapsed;
            SetStatus("Đang lắng nghe…", Color.FromRgb(0x10, 0xB9, 0x81));
        }

        private void UpdateUI(object? sender, EventArgs e)
        {
            double w = WaveCanvas.ActualWidth;
            double h = WaveCanvas.ActualHeight;
            if (w < 10 || h < 10) return;

            double barW = _currentVolume * w;
            VolumeBar.Width = Math.Max(0, Math.Min(barW, w - 2));
            int db = _currentVolume > 0 ? (int)(20 * Math.Log10(_currentVolume + 0.0001)) : -60;
            VolumeText.Text = $"{db} dB";

            var barColor = _currentVolume > 0.7 ? Color.FromRgb(0xEF, 0x44, 0x44)
                         : _currentVolume > 0.3 ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                         : Color.FromRgb(0x22, 0xC5, 0x5E);
            VolumeBar.Background = new SolidColorBrush(barColor);

            if (_isRecording)
                RecordTimer.Text = (DateTime.Now - _recordStart).ToString(@"mm\:ss");

            double cy = h / 2;
            var pts = new System.Windows.Media.PointCollection();
            List<float> data;
            lock (_waveLock) { data = new List<float>(_waveData); }

            if (data.Count > 2)
            {
                double step = w / data.Count;
                for (int i = 0; i < data.Count; i++)
                    pts.Add(new System.Windows.Point(i * step, cy - data[i] * cy * 0.88));
            }
            WaveLine.Points = pts;

            WaveLine.Stroke = _isRecording
                ? new SolidColorBrush(Color.FromRgb(0xEF, 0x44, 0x44))
                : new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB));
        }

        private void SetStatus(string msg, Color dotColor)
        {
            StatusLabel.Text = msg;
            StatusDot.Fill = new SolidColorBrush(dotColor);
        }

        public void Cleanup()
        {
            _uiTimer.Stop();
            if (_isRecording) StopRecording();
            StopCapture();
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _reader?.Dispose();
        }
    }
}
