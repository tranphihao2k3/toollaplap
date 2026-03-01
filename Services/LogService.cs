using System;
using System.IO;

namespace LapLapAutoTool.Services
{
    public interface ILogService
    {
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message, Exception? ex = null);
        string GetLogPath();
    }

    public class LogService : ILogService
    {
        private readonly string _logFolder;
        private readonly string _logFile;

        public LogService()
        {
            _logFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            _logFile = Path.Combine(_logFolder, "install_log.txt");

            if (!Directory.Exists(_logFolder))
                Directory.CreateDirectory(_logFolder);
        }

        public void LogInfo(string message)
        {
            WriteToFile($"[INFO] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        public void LogWarning(string message)
        {
            WriteToFile($"[WARNING] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
        }

        public void LogError(string message, Exception? ex = null)
        {
            string logMessage = $"[ERROR] [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}";
            if (ex != null)
            {
                logMessage += $"\nException: {ex.Message}\nStack Trace: {ex.StackTrace}";
            }
            WriteToFile(logMessage);
        }

        public string GetLogPath() => _logFile;

        private void WriteToFile(string content)
        {
            try
            {
                File.AppendAllText(_logFile, content + Environment.NewLine);
            }
            catch
            {
                // Should not fail if possible, maybe write to Console/Debug as fallback
            }
        }
    }
}
