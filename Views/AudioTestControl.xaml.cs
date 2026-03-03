using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LapLapAutoTool.Views
{
    public partial class AudioTestControl : UserControl
    {
        // ── Speaker ─────────────────────────────────────────
        private WaveOutEvent? _waveOut;
        private AudioFileReader? _audioReader;
        private string? _currentAudioPath;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
        private const string DEFAULT_AUDIO_URL = "https://drive.google.com/file/d/1OmSqfp63pZZ4X8-JH5Qn5ieJrEd4pfid/view?usp=sharing";

        // ── Mic ─────────────────────────────────────────────
        private WaveInEvent? _waveIn;
        private WaveFileWriter? _writer;
        private bool _isRecording;
        private string? _tempRecordPath;
        private DateTime _recordStart;
        private float _currentVolume;
        private readonly DispatcherTimer _uiTimer;

        // ── Playback ─────────────────────────────────────────
        private WaveOutEvent? _playbackOut;
        private AudioFileReader? _playbackReader;

        public AudioTestControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;

            _uiTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            _uiTimer.Tick += UpdateMicUI;
            _uiTimer.Start();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            LoadDevices();
            UrlBox.Text = DEFAULT_AUDIO_URL;
        }

        // ══════ SPEAKER ══════

        private void PlayToneClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double freq))
            {
                StopSpeaker();
                _waveOut = new WaveOutEvent();
                var gen = new SignalGenerator(44100, 1)
                {
                    Frequency = freq, Gain = 0.5, Type = SignalGeneratorType.Sin
                };
                _waveOut.Init(gen);
                _waveOut.Volume = 0.7f;
                _waveOut.Play();
                SetSpeakerStatus($"▶ Tone {freq}Hz", true);
            }
        }

        private void PlayLeft(object sender, RoutedEventArgs e) => PlayStereoTone(true, false, "Left speaker");
        private void PlayBoth(object sender, RoutedEventArgs e) => PlayStereoTone(true, true, "Both speakers");
        private void PlayRight(object sender, RoutedEventArgs e) => PlayStereoTone(false, true, "Right speaker");

        private void PlayStereoTone(bool left, bool right, string label)
        {
            // If we have audio file, use it for stereo test
            if (!string.IsNullOrEmpty(_currentAudioPath) && File.Exists(_currentAudioPath))
            {
                PlayAudioSide(left, right, label);
                return;
            }

            // Otherwise use tone
            StopSpeaker();
            var gen = new SignalGenerator(44100, 1)
            {
                Frequency = 440, Gain = 0.5, Type = SignalGeneratorType.Sin
            };
            var stereo = new MonoToStereoSampleProvider(gen)
            {
                LeftVolume = left ? 1.0f : 0f,
                RightVolume = right ? 1.0f : 0f
            };
            _waveOut = new WaveOutEvent { Volume = 0.7f };
            _waveOut.Init(stereo);
            _waveOut.Play();
            SetSpeakerStatus($"▶ {label} 440Hz", true);
        }

        private void PlayAudioSide(bool left, bool right, string label)
        {
            try
            {
                StopSpeaker();
                var reader = new AudioFileReader(_currentAudioPath!);
                ISampleProvider provider = reader;
                if (provider.WaveFormat.Channels == 1)
                    provider = new MonoToStereoSampleProvider(provider);

                provider = new ChannelMuterProvider(provider, !left, !right);

                _waveOut = new WaveOutEvent { Volume = 0.7f };
                _waveOut.Init(provider);
                _waveOut.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
                {
                    SetSpeakerStatus("⏹ Phát xong", false);
                    reader.Dispose();
                });
                _waveOut.Play();
                SetSpeakerStatus($"▶ {label}", true);
            }
            catch (Exception ex)
            {
                SetSpeakerStatus($"Lỗi: {ex.Message}", false);
            }
        }

        private class ChannelMuterProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly bool _muteLeft, _muteRight;
            public ChannelMuterProvider(ISampleProvider source, bool muteLeft, bool muteRight)
            { _source = source; _muteLeft = muteLeft; _muteRight = muteRight; }
            public WaveFormat WaveFormat => _source.WaveFormat;
            public int Read(float[] buffer, int offset, int count)
            {
                int read = _source.Read(buffer, offset, count);
                if (_source.WaveFormat.Channels == 2)
                    for (int n = 0; n < read; n += 2)
                    {
                        if (_muteLeft) buffer[offset + n] = 0;
                        if (_muteRight) buffer[offset + n + 1] = 0;
                    }
                return read;
            }
        }

        // ── Download & Play ──────────────────────────────────

        private async void DownloadAndPlay(object sender, RoutedEventArgs e)
        {
            string url = UrlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url) || url == "https://...")
                url = DEFAULT_AUDIO_URL;
            else
            {
                var driveMatch = System.Text.RegularExpressions.Regex.Match(url, @"/d/([a-zA-Z0-9_-]+)|id=([a-zA-Z0-9_-]+)");
                if (url.Contains("drive.google.com") && driveMatch.Success)
                {
                    string fileId = !string.IsNullOrEmpty(driveMatch.Groups[1].Value)
                        ? driveMatch.Groups[1].Value : driveMatch.Groups[2].Value;
                    url = $"https://docs.google.com/uc?export=download&id={fileId}";
                }
            }

            BtnDownload.IsEnabled = false;
            SetSpeakerStatus("⬇ Đang tải nhạc…", false);

            try
            {
                string ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".mp3";
                string tempFile = Path.Combine(Path.GetTempPath(), $"laplap_audio_{Guid.NewGuid()}{ext}");

                await Task.Run(async () =>
                {
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    using var fileStream = File.Create(tempFile);
                    using var httpStream = await response.Content.ReadAsStreamAsync();
                    byte[] buffer = new byte[8192];
                    int read;
                    while ((read = await httpStream.ReadAsync(buffer)) > 0)
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                });

                PlayLocalFile(tempFile);
            }
            catch (Exception ex)
            {
                SetSpeakerStatus($"Lỗi tải: {ex.Message}", false);
            }
            finally
            {
                BtnDownload.IsEnabled = true;
            }
        }

        private void OpenLocalFile(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "Chọn file nhạc",
                Filter = "Audio files|*.mp3;*.wav;*.flac;*.m4a;*.aac;*.ogg|All files|*.*"
            };
            if (dlg.ShowDialog() == true)
                PlayLocalFile(dlg.FileName);
        }

        private void PlayLocalFile(string path)
        {
            StopSpeaker();
            try
            {
                _audioReader = new AudioFileReader(path);
                _waveOut = new WaveOutEvent { Volume = 0.7f };
                _waveOut.Init(_audioReader);
                _waveOut.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
                {
                    SetSpeakerStatus("⏹ Phát xong", false);
                });
                _waveOut.Play();
                _currentAudioPath = path;
                SetSpeakerStatus($"▶ {Path.GetFileName(path)}", true);
            }
            catch (Exception ex)
            {
                SetSpeakerStatus($"Lỗi: {ex.Message}", false);
            }
        }

        private void StopSpeaker_Click(object sender, RoutedEventArgs e)
        {
            StopSpeaker();
            SetSpeakerStatus("Đã dừng", false);
        }

        private void StopSpeaker()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _audioReader?.Dispose();
            _audioReader = null;
        }

        private void SetSpeakerStatus(string msg, bool playing)
        {
            SpeakerStatus.Text = msg;
            PlayDot.Fill = playing
                ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                : new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
        }

        private void UrlBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UrlBox.Text == "https://...") UrlBox.Text = "";
        }

        // ══════ MIC ══════

        private void LoadDevices()
        {
            int count = WaveInEvent.DeviceCount;
            DeviceCombo.Items.Clear();
            if (count == 0)
            {
                DeviceCombo.Items.Add("Không tìm thấy mic");
                DeviceCombo.SelectedIndex = 0;
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
                    BufferMilliseconds = 50
                };
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.StartRecording();
                RecordStatus.Text = "Đang lắng nghe…";
            }
            catch { }
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
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short s = BitConverter.ToInt16(e.Buffer, i);
                float f = Math.Abs(s / 32768f);
                if (f > max) max = f;
            }
            _currentVolume = max;
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
            StopPlayback();

            _tempRecordPath = Path.Combine(Path.GetTempPath(), $"mic_test_{Guid.NewGuid()}.wav");
            _writer = new WaveFileWriter(_tempRecordPath, _waveIn.WaveFormat);
            _isRecording = true;
            _recordStart = DateTime.Now;
            BtnRecord.Content = "⏹ Dừng";
            BtnPlayback.IsEnabled = false;
            RecordStatus.Text = "⏺ Đang ghi âm…";
        }

        private void StopRecording()
        {
            _isRecording = false;
            var duration = DateTime.Now - _recordStart;
            _writer?.Flush();
            _writer?.Dispose();
            _writer = null;
            BtnRecord.Content = "⏺ Ghi";

            if (!string.IsNullOrEmpty(_tempRecordPath) && File.Exists(_tempRecordPath))
            {
                BtnPlayback.IsEnabled = true;
                RecordStatus.Text = $"Đã ghi {duration.TotalSeconds:F1}s — nhấn ▶ Phát";
            }
            else
            {
                RecordStatus.Text = "Đang lắng nghe…";
            }
        }

        private void PlayRecording(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_tempRecordPath) || !File.Exists(_tempRecordPath)) return;
            StopPlayback();

            try
            {
                _playbackReader = new AudioFileReader(_tempRecordPath);
                _playbackOut = new WaveOutEvent();
                _playbackOut.Init(_playbackReader);
                _playbackOut.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
                {
                    BtnPlayback.Content = "▶ Phát";
                    RecordStatus.Text = "⏹ Phát xong — ghi lại hoặc phát lại";
                });
                _playbackOut.Play();
                BtnPlayback.Content = "⏸ Dừng";
                RecordStatus.Text = "▶ Đang phát lại bản ghi…";
            }
            catch (Exception ex)
            {
                RecordStatus.Text = $"Lỗi: {ex.Message}";
            }
        }

        private void StopPlayback()
        {
            _playbackOut?.Stop();
            _playbackOut?.Dispose();
            _playbackOut = null;
            _playbackReader?.Dispose();
            _playbackReader = null;
            BtnPlayback.Content = "▶ Phát";
        }

        // ── UI update (level meter + pulse rings) ────────────

        private void UpdateMicUI(object? sender, EventArgs e)
        {
            double w = MicBar.Parent is FrameworkElement parent ? parent.ActualWidth : 200;
            if (w < 10) return;

            double barW = _currentVolume * w;
            MicBar.Width = Math.Max(0, Math.Min(barW, w - 2));

            int db = _currentVolume > 0 ? (int)(20 * Math.Log10(_currentVolume + 0.0001)) : -60;
            DbText.Text = $"{db} dB";

            var barColor = _currentVolume > 0.7 ? Color.FromRgb(0xEF, 0x44, 0x44)
                         : _currentVolume > 0.3 ? Color.FromRgb(0xF5, 0x9E, 0x0B)
                         : Color.FromRgb(0x22, 0xC5, 0x5E);
            MicBar.Background = new SolidColorBrush(barColor);

            // Pulse rings - opacity based on volume
            double vol = _currentVolume;
            PulseRing1.Opacity = Math.Min(vol * 2.0, 0.6);
            PulseRing2.Opacity = Math.Min(vol * 1.4, 0.35);
            PulseRing3.Opacity = Math.Min(vol * 0.9, 0.2);

            // Recording timer
            if (_isRecording)
            {
                var elapsed = DateTime.Now - _recordStart;
                RecordStatus.Text = $"⏺ Ghi âm… {elapsed:mm\\:ss}";
            }
        }

        public void Cleanup()
        {
            _uiTimer.Stop();
            if (_isRecording) StopRecording();
            StopCapture();
            StopSpeaker();
            StopPlayback();
            try
            {
                if (!string.IsNullOrEmpty(_currentAudioPath) && File.Exists(_currentAudioPath)
                    && Path.GetDirectoryName(_currentAudioPath) == Path.GetTempPath().TrimEnd('\\'))
                    File.Delete(_currentAudioPath);
            }
            catch { }
        }
    }
}
