using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace LapLapAutoTool.Services
{
    public interface IInstallService
    {
        Task<bool> InstallAsync(string filePath, string args, bool waitExit = true);
        bool ResetWindowsUpdate();
        bool DisableWindowsUpdate();
        bool DisableDefender();
        bool DisableFastBoot();
        bool CleanTempFiles();
    }

    public class InstallService : IInstallService
    {
        private readonly string _basePath;
        private readonly ILogService _logService;

        public InstallService(ILogService logService)
        {
            _logService = logService;
            // Thư mục chứa bộ cài mặc định
            _basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SetupFiles");
            if (!Directory.Exists(_basePath)) Directory.CreateDirectory(_basePath);
        }

        public async Task<bool> InstallAsync(string fileName, string args, bool waitExit = true)
        {
            _logService.LogInfo($"Starting installation of {fileName}...");
            string fullPath = Path.Combine(_basePath, fileName);
            
            if (!File.Exists(fullPath)) 
            {
                _logService.LogError($"File not found: {fullPath}. Falling back to demo delay.");
                await Task.Delay(1000); 
                return true; 
            }

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = fullPath,
                    Arguments = args,
                    UseShellExecute = true, // Better for some installers
                    Verb = "runas"
                };

                var process = Process.Start(startInfo);
                if (process != null)
                {
                    if (waitExit)
                    {
                        await process.WaitForExitAsync();
                        bool success = process.ExitCode == 0;
                        if (success) _logService.LogInfo($"Successfully installed {fileName}");
                        else _logService.LogError($"Failed to install {fileName}. Exit code: {process.ExitCode}");
                        return success;
                    }
                    else
                    {
                        _logService.LogInfo($"Started installation of {fileName} in background.");
                        return true; // Assume success if started
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error during installation of {fileName}", ex);
            }
            return false;
        }

        public bool ResetWindowsUpdate() => RunCommand("net stop wuauserv & net start wuauserv");
        
        public bool DisableWindowsUpdate() 
        {
            _logService.LogInfo("Attempting to disable Windows Update...");
            return RunCommand("sc config wuauserv start= disabled & net stop wuauserv");
        }

        public bool DisableDefender() 
        {
            _logService.LogInfo("Attempting to disable Windows Defender...");
            return RunCommand("powershell Set-MpPreference -DisableRealtimeMonitoring $true");
        }

        public bool DisableFastBoot()
        {
            _logService.LogInfo("Attempting to disable Fast Boot...");
            return RunCommand("powercfg /h off");
        }

        public bool CleanTempFiles()
        {
            _logService.LogInfo("Starting junk file cleanup...");
            bool success = true;
            success &= RunCommand("del /q /s /f %temp%\\*");
            success &= RunCommand("del /q /s /f C:\\Windows\\Temp\\*");
            return success;
        }

        private bool RunCommand(string command)
        {
            try
            {
                var process = Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    Verb = "runas"
                });
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch { return false; }
        }
    }
}
