using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using LapLapAutoTool.Models;
using LapLapAutoTool.Views;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace LapLapAutoTool.ViewModels
{
    public class LaptopTestViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TestItem> TestItems { get; set; }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public LaptopTestViewModel()
        {
            TestItems = new ObservableCollection<TestItem>
            {
                new TestItem
                {
                    Title = "Camera",
                    Description = "Mở webcam và hiển thị hình ảnh trực tiếp từ camera",
                    IconData = "M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z",
                    AccentColor = "#EC4899",
                    Command = new RelayCommand(() => TestCamera())
                },
                new TestItem
                {
                    Title = "Microphone",
                    Description = "Hiển thị waveform âm thanh thời gian thực từ micro",
                    IconData = "M12 2a3 3 0 0 0-3 3v7a3 3 0 0 0 6 0V5a3 3 0 0 0-3-3zm-1 16.93V21h-2v-2h2v-.07A7.001 7.001 0 0 1 5 12H7a5 5 0 0 0 10 0h2a7.001 7.001 0 0 1-6 6.93z",
                    AccentColor = "#8B5CF6",
                    Command = new RelayCommand(() => TestMicrophone())
                },
                new TestItem
                {
                    Title = "Loa & Âm Thanh",
                    Description = "Phát âm thanh thử nghiệm: Tone 440Hz, sweep và stereo check",
                    IconData = "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z",
                    AccentColor = "#F59E0B",
                    Command = new RelayCommand(() => TestSpeakers())
                },
                new TestItem
                {
                    Title = "Màn Hình",
                    Description = "Test dead pixel bằng các màu toàn màn hình: Đen, Trắng, Đỏ, Xanh",
                    IconData = "M21 2H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h7l-2 3v1h8v-1l-2-3h7c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H3V4h18v12z",
                    AccentColor = "#10B981",
                    Command = new RelayCommand(() => TestScreen())
                },
                new TestItem
                {
                    Title = "Bàn Phím",
                    Description = "Bàn phím ảo tương tác — nhấn từng phím để xác nhận hoạt động",
                    IconData = "M20 5H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z",
                    AccentColor = "#6366F1",
                    Command = new RelayCommand(() => TestKeyboard())
                },
                new TestItem
                {
                    Title = "Touchpad",
                    Description = "Test touchpad — di chuyển chuột và kiểm tra các click/gesture",
                    IconData = "M9 0C9 0 4 3 4 8v4l2 2V8c0-3.14 3-5.43 3-5.43L9 0zM15 0l-.57 2.57S18 4.86 18 8v6l2-2V8c0-5-5-8-5-8zm-3 7c-1.66 0-3 1.34-3 3v7l3 3 3-3v-7c0-1.66-1.34-3-3-3z",
                    AccentColor = "#14B8A6",
                    Command = new RelayCommand(() => TestTouchpad())
                },
                new TestItem
                {
                    Title = "Tốc Độ Ổ Cứng",
                    Description = "Đo tốc độ đọc/ghi thực tế — cập nhật realtime, biểu đồ lịch sử",
                    IconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z",
                    AccentColor = "#F97316",
                    Command = new RelayCommand(() => TestDiskSpeed())
                },
            };
        }

        private void TestCamera()
        {
            // Open Windows Camera app (built into Windows)
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "microsoft.windows.camera:",
                    UseShellExecute = true
                });
            }
            catch
            {
                MessageBox.Show(
                    "Không thể mở ứng dụng Camera.\nHãy kiểm tra Device Manager để xem camera có được nhận diện không.",
                    "Camera Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TestMicrophone()
        {
            var win = new MicTestWindow();
            win.Show();
        }

        private void TestSpeakers()
        {
            var win = new SpeakerTestWindow();
            win.Show();
        }

        private void TestScreen()
        {
            var win = new ScreenTestWindow();
            win.Show();
        }

        private void TestKeyboard()
        {
            var win = new KeyboardTestWindow();
            win.Show();
        }

        private void TestTouchpad()
        {
            var win = new TouchpadTestWindow();
            win.Show();
        }

        private void TestDiskSpeed()
        {
            var win = new DiskSpeedTestWindow();
            win.Show();
        }
    }
}
