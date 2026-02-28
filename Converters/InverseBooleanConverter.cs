using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using LapLapAutoTool.Models;

namespace LapLapAutoTool.Converters
{
    public class InverseBooleanConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool b) return !b;
            return value;
        }
    }

    /// Hiện nút "Tải về" khi trạng thái là Idle hoặc Failed VÀ có URL
    public class DownloadIdleVisibilityConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length < 1) return Visibility.Visible;
            var status = values[0] is DownloadStatus s ? s : DownloadStatus.Idle;
            bool isIdle = status == DownloadStatus.Idle || status == DownloadStatus.Failed;
            return isIdle ? Visibility.Visible : Visibility.Collapsed;
        }
        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// Hiện khi DownloadStatus == Done
    public class DownloadDoneVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is DownloadStatus s && s == DownloadStatus.Done ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// Hiện khi DownloadStatus == Failed
    public class DownloadFailedVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is DownloadStatus s && s == DownloadStatus.Failed ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }

    /// Ẩn TextBlock nếu string rỗng
    public class StringToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
