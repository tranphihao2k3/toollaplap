using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LapLapAutoTool.Views
{
    public partial class SpeakerTestControl : UserControl
    {
        private WaveOutEvent? _waveOut;
        private WaveFileReader? _fileReader;
        private AudioFileReader? _audioReader;
        private string? _currentAudioPath;
        private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(5) };
        private const string DEFAULT_AUDIO_URL = "https://drive.google.com/file/d/1OmSqfp63pZZ4X8-JH5Qn5ieJrEd4pfid/view?usp=sharing";

        public SpeakerTestControl()
        {
            InitializeComponent();
            VolumeSlider.ValueChanged += VolumeChanged;
            UrlBox.Text = DEFAULT_AUDIO_URL;
        }

        private void PlayToneClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && double.TryParse(btn.Tag?.ToString(), out double freq))
            {
                StopCurrent();
                _waveOut = new WaveOutEvent();
                var gen = new SignalGenerator(44100, 1)
                {
                    Frequency = freq,
                    Gain = 0.5,
                    Type = SignalGeneratorType.Sin
                };
                _waveOut.Init(gen);
                _waveOut.Volume = (float)VolumeSlider.Value / 100f;
                _waveOut.Play();
                SetStatus($"▶ Tone {freq}Hz đang phát…", true);
            }
        }

        private void PlayLeft(object sender, RoutedEventArgs e) => PlayAudioSide(true, false, "Loa TRÁI (Nhạc)");
        private void PlayRight(object sender, RoutedEventArgs e) => PlayAudioSide(false, true, "Loa PHẢI (Nhạc)");
        private void PlayBoth(object sender, RoutedEventArgs e) => PlayAudioSide(true, true, "Cả hai loa (Nhạc)");

        private void PlayAudioSide(bool left, bool right, string label)
        {
            if (string.IsNullOrEmpty(_currentAudioPath) || !File.Exists(_currentAudioPath))
            {
                MessageBox.Show("Chưa có file âm thanh nào được tải. Vui lòng tải hoặc mở file nhạc trước.", "Thông báo", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                StopCurrent();
                var reader = new AudioFileReader(_currentAudioPath);
                ISampleProvider provider = reader;

                if (provider.WaveFormat.Channels == 1)
                    provider = new MonoToStereoSampleProvider(provider);

                provider = new SpeakerChannelMuterProvider(provider, !left, !right);

                _waveOut = new WaveOutEvent();
                _waveOut.Volume = (float)VolumeSlider.Value / 100f;
                _waveOut.Init(provider);
                _waveOut.PlaybackStopped += (_, __) => Dispatcher.Invoke(() =>
                {
                    SetStatus("⏹ Phát xong", false);
                    ProgressText.Text = "";
                    reader.Dispose();
                });
                _waveOut.Play();
                SetStatus($"▶ {label} đang phát…", true);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi phát âm thanh: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private class SpeakerChannelMuterProvider : ISampleProvider
        {
            private readonly ISampleProvider _source;
            private readonly bool _muteLeft;
            private readonly bool _muteRight;

            public SpeakerChannelMuterProvider(ISampleProvider source, bool muteLeft, bool muteRight)
            {
                _source = source;
                _muteLeft = muteLeft;
                _muteRight = muteRight;
            }

            public WaveFormat WaveFormat => _source.WaveFormat;

            public int Read(float[] buffer, int offset, int count)
            {
                int samplesRead = _source.Read(buffer, offset, count);
                if (_source.WaveFormat.Channels == 2)
                {
                    for (int n = 0; n < samplesRead; n += 2)
                    {
                        if (_muteLeft) buffer[offset + n] = 0;
                        if (_muteRight) buffer[offset + n + 1] = 0;
                    }
                }
                return samplesRead;
            }
        }

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
                    string fileId = !string.IsNullOrEmpty(driveMatch.Groups[1].Value) ? driveMatch.Groups[1].Value : driveMatch.Groups[2].Value;
                    url = $"https://docs.google.com/uc?export=download&id={fileId}";
                }
            }

            BtnDownload.IsEnabled = false;
            SetStatus("⬇ Đang tải nhạc…", false);
            ProgressText.Text = "Đang kết nối…";

            try
            {
                string ext = Path.GetExtension(new Uri(url).AbsolutePath);
                if (string.IsNullOrEmpty(ext)) ext = ".mp3";
                string tempFile = Path.Combine(Path.GetTempPath(), $"laplap_audio_{Guid.NewGuid()}{ext}");

                var progress = new Progress<long>(bytesReceived =>
                {
                    ProgressText.Text = $"Đã tải: {bytesReceived / 1024.0:F0} KB";
                });

                await Task.Run(async () =>
                {
                    using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                    response.EnsureSuccessStatusCode();
                    using var fileStream = File.Create(tempFile);
                    using var httpStream = await response.Content.ReadAsStreamAsync();
                    byte[] buffer = new byte[8192];
                    long total = 0;
                    int read;
                    while ((read = await httpStream.ReadAsync(buffer)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, read));
                        total += read;
                        ((IProgress<long>)progress).Report(total);
                    }
                });

                ProgressText.Text = "Tải xong! Đang phát…";
                PlayLocalFile(tempFile);
            }
            catch (Exception ex)
            {
                SetStatus($"Lỗi tải: {ex.Message}", false);
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
            StopCurrent();
            try
            {
                _audioReader = new AudioFileReader(path);
                _waveOut = new WaveOutEvent();
                _waveOut.Volume = (float)VolumeSlider.Value / 100f;
                _waveOut.Init(_audioReader);
                _waveOut.PlaybackStopped += (_, _) => Dispatcher.Invoke(() =>
                {
                    SetStatus("⏹ Phát xong", false);
                    ProgressText.Text = "";
                    _waveOut?.Dispose();
                    _audioReader?.Dispose();
                    _waveOut = null;
                    _audioReader = null;
                });
                _waveOut.Play();
                SetStatus($"▶ {Path.GetFileName(path)}", true);
                ProgressText.Text = $"Thời lượng: {_audioReader.TotalTime:mm\\:ss}";
                _currentAudioPath = path;
            }
            catch (Exception ex)
            {
                SetStatus($"Không thể phát: {ex.Message}", false);
            }
        }

        private void VolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (VolLabel == null) return;
            VolLabel.Text = $"{(int)VolumeSlider.Value}%";
            if (_waveOut != null)
                _waveOut.Volume = (float)VolumeSlider.Value / 100f;
        }

        private void Stop_Click(object sender, RoutedEventArgs e) => StopCurrent();

        private void StopCurrent()
        {
            _waveOut?.Stop();
            _waveOut?.Dispose();
            _waveOut = null;
            _audioReader?.Dispose();
            _audioReader = null;
            _fileReader?.Dispose();
            _fileReader = null;
        }

        private void SetStatus(string msg, bool playing)
        {
            StatusText.Text = msg;
            PlayDot.Fill = playing
                ? new SolidColorBrush(Color.FromRgb(0x10, 0xB9, 0x81))
                : new SolidColorBrush(Color.FromRgb(0x33, 0x41, 0x55));
        }

        private void UrlBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (UrlBox.Text == "https://...")
                UrlBox.Text = "";
        }

        public void Cleanup()
        {
            StopCurrent();
            try
            {
                if (!string.IsNullOrEmpty(_currentAudioPath) && File.Exists(_currentAudioPath) && Path.GetDirectoryName(_currentAudioPath) == Path.GetTempPath().TrimEnd('\\'))
                    File.Delete(_currentAudioPath);
            }
            catch { }
        }
    }
}
