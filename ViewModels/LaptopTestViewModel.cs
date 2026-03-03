using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using LapLapAutoTool.Models;
using LapLapAutoTool.Views;

namespace LapLapAutoTool.ViewModels
{
    public class LaptopTestViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<TestItem> TestItems { get; set; }

        private int _selectedIndex = -1;
        public int SelectedIndex
        {
            get => _selectedIndex;
            set { _selectedIndex = value; OnPropertyChanged(); }
        }

        private UserControl? _currentTestControl;
        public UserControl? CurrentTestControl
        {
            get => _currentTestControl;
            set { _currentTestControl = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

        public LaptopTestViewModel()
        {
            TestItems = new ObservableCollection<TestItem>
            {
                new TestItem // 0
                {
                    Title = "Audio",
                    Description = "Loa & Mic",
                    IconData = "M3 9v6h4l5 5V4L7 9H3zm13.5 3c0-1.77-1.02-3.29-2.5-4.03v8.05c1.48-.73 2.5-2.25 2.5-4.02z",
                    AccentColor = "#00D2FF",
                    Command = new RelayCommand(() => SelectTest(0))
                },
                new TestItem // 1
                {
                    Title = "Bàn Phím",
                    Description = "Keyboard",
                    IconData = "M20 5H4c-1.1 0-2 .9-2 2v10c0 1.1.9 2 2 2h16c1.1 0 2-.9 2-2V7c0-1.1-.9-2-2-2zm-9 3h2v2h-2V8zm0 3h2v2h-2v-2zM8 8h2v2H8V8zm0 3h2v2H8v-2zm-1 2H5v-2h2v2zm0-3H5V8h2v2zm9 7H8v-2h8v2zm0-4h-2v-2h2v2zm0-3h-2V8h2v2zm3 3h-2v-2h2v2zm0-3h-2V8h2v2z",
                    AccentColor = "#6366F1",
                    Command = new RelayCommand(() => SelectTest(1))
                },
                new TestItem // 2
                {
                    Title = "Touchpad",
                    Description = "Chuột cảm ứng",
                    IconData = "M9 0C9 0 4 3 4 8v4l2 2V8c0-3.14 3-5.43 3-5.43L9 0zM15 0l-.57 2.57S18 4.86 18 8v6l2-2V8c0-5-5-8-5-8zm-3 7c-1.66 0-3 1.34-3 3v7l3 3 3-3v-7c0-1.66-1.34-3-3-3z",
                    AccentColor = "#14B8A6",
                    Command = new RelayCommand(() => SelectTest(2))
                },
                new TestItem // 3
                {
                    Title = "Màn Hình",
                    Description = "Dead pixel",
                    IconData = "M21 2H3c-1.1 0-2 .9-2 2v12c0 1.1.9 2 2 2h7l-2 3v1h8v-1l-2-3h7c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2zm0 14H3V4h18v12z",
                    AccentColor = "#10B981",
                    Command = new RelayCommand(() => TestScreen())
                },
                new TestItem // 4
                {
                    Title = "Camera",
                    Description = "Webcam",
                    IconData = "M17 10.5V7c0-.55-.45-1-1-1H4c-.55 0-1 .45-1 1v10c0 .55.45 1 1 1h12c.55 0 1-.45 1-1v-3.5l4 4v-11l-4 4z",
                    AccentColor = "#EC4899",
                    Command = new RelayCommand(() => TestCamera())
                },
                new TestItem // 5
                {
                    Title = "Ổ Cứng",
                    Description = "Disk Speed",
                    IconData = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm1 15h-2v-6h2v6zm0-8h-2V7h2v2z",
                    AccentColor = "#F97316",
                    Command = new RelayCommand(() => SelectTest(5))
                },
            };
        }

        private void CleanupCurrentControl()
        {
            if (_currentTestControl is AudioTestControl audio) audio.Cleanup();
            else if (_currentTestControl is KeyboardTestControl kb) kb.Cleanup();
            else if (_currentTestControl is TouchpadTestControl tp) tp.Cleanup();
            else if (_currentTestControl is DiskSpeedTestControl disk) disk.Cleanup();
            CurrentTestControl = null;
        }

        public void SelectTest(int index)
        {
            CleanupCurrentControl();
            SelectedIndex = index;

            switch (index)
            {
                case 0: // Audio (Loa + Mic)
                    CurrentTestControl = new AudioTestControl();
                    break;
                case 1: // Bàn phím
                    CurrentTestControl = new KeyboardTestControl();
                    break;
                case 2: // Touchpad
                    CurrentTestControl = new TouchpadTestControl();
                    break;
                case 5: // Ổ cứng
                    CurrentTestControl = new DiskSpeedTestControl();
                    break;
            }
        }

        private void TestCamera()
        {
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
                    "Không thể mở ứng dụng Camera.\nHãy kiểm tra Device Manager.",
                    "Camera Test", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void TestScreen()
        {
            var win = new ScreenTestWindow();
            win.Show();
        }
    }
}
