using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Runtime.InteropServices;

namespace LapLapAutoTool.Services
{
    public interface IInstallService
    {
        Task<bool> InstallAsync(string filePath, string args, bool waitExit = true);
        bool IsSoftwareInstalled(string appName);
        bool ResetWindowsUpdate();
        bool IsWindowsUpdateEnabled();
        bool DisableWindowsUpdate();
        bool EnableWindowsUpdate();
        bool IsDefenderEnabled();
        bool DisableDefender();
        bool EnableDefender();
        bool IsFastBootEnabled();
        bool DisableFastBoot();
        bool EnableFastBoot();
        bool CleanTempFiles();
        bool SetupTimezoneAndRegion();
        bool ShowModernDesktopIcons();
        Task<bool> BackupUserDataAsync(System.Collections.Generic.List<string> folderPaths, IProgress<(double progress, string status)> progress);

        // New features
        bool ActivateWindows();
        bool ActivateOffice();
        bool DisableTelemetry();
        bool FlushDnsAndResetNetwork();
        string GetCurrentPowerPlan();
        bool SetHighPerformancePower();
        bool SetBalancedPower();
        bool DisableSleepAndHibernate();
        bool EnableSleepAndHibernate();
        Task<string> RunSfcAndDismAsync(IProgress<string> progress);
        void OpenSystemTool(string tool);
        bool DisableBitLocker();
        Task<bool> InstallDriverWithDismAsync(string driverFolderPath, IProgress<string>? progress = null);
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
            string fullPath = Path.IsPathRooted(fileName) ? fileName : Path.Combine(_basePath, fileName);
            
            if (!File.Exists(fullPath)) 
            {
                _logService.LogError($"File not found: {fullPath}");
                return false; 
            }

            // Logic COPY TO DESKTOP
            if (args == "COPY_TO_DESKTOP")
            {
                try
                {
                    string justFileName = Path.GetFileName(fullPath);
                    string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), justFileName);
                    File.Copy(fullPath, desktopPath, true);
                    _logService.LogInfo($"Copied {fileName} to Desktop.");
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"Error copying {fileName} to Desktop", ex);
                    return false;
                }
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
                        _logService.LogInfo($"{fileName} exited with code {process.ExitCode}");
                        // 0 = Success, 3010 = Success (Reboot Required)
                        bool success = (process.ExitCode == 0 || process.ExitCode == 3010);
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

        public bool IsSoftwareInstalled(string appName)
        {
            try
            {
                // Special case for UniKey - ONLY check Desktop as per user request (avoid stale registry)
                if (appName.Contains("unikey", StringComparison.OrdinalIgnoreCase) || 
                    appName.Contains("evkey", StringComparison.OrdinalIgnoreCase))
                {
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string publicDesktopPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory);
                    
                    bool isEvKey = appName.Contains("evkey", StringComparison.OrdinalIgnoreCase);
                    string targetFile = isEvKey ? "EVKey64.exe" : "unikey.exe";
                    
                    return CheckFileInDirectory(desktopPath, targetFile) || 
                           CheckFileInDirectory(publicDesktopPath, targetFile);
                }

                // Special case for Zalo - Check for exe existence
                if (appName.Contains("zalo", StringComparison.OrdinalIgnoreCase))
                {
                    string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    string zaloExePath = Path.Combine(localAppData, "Zalo-PC", "Zalo.exe");
                    if (File.Exists(zaloExePath)) return true;
                }

                // Special case for Microsoft Office
                if (appName.Contains("Microsoft Office", StringComparison.OrdinalIgnoreCase))
                {
                    string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                    string commonStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs");

                    bool hasWord = SearchShortcutRecursive(userStartMenu, "Word") || SearchShortcutRecursive(commonStartMenu, "Word");
                    bool hasExcel = SearchShortcutRecursive(userStartMenu, "Excel") || SearchShortcutRecursive(commonStartMenu, "Excel");
                    bool hasPpt = SearchShortcutRecursive(userStartMenu, "PowerPoint") || SearchShortcutRecursive(commonStartMenu, "PowerPoint");

                    return hasWord && hasExcel && hasPpt;
                }

                // General registry check
                string[] registryPaths = {
                    @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                    @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
                };

                foreach (var path in registryPaths)
                {
                    using (var key = Registry.LocalMachine.OpenSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (var subKeyName in key.GetSubKeyNames())
                            {
                                using (var subKey = key.OpenSubKey(subKeyName))
                                {
                                    var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                    if (displayName != null)
                                    {
                                        if (displayName.Contains(appName, StringComparison.OrdinalIgnoreCase) || 
                                            appName.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                                            return true;
                                    }
                                }
                            }
                        }
                    }
                }

                // Check Current User registry
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                {
                    if (key != null)
                    {
                        foreach (var subKeyName in key.GetSubKeyNames())
                        {
                            using (var subKey = key.OpenSubKey(subKeyName))
                            {
                                var displayName = subKey?.GetValue("DisplayName")?.ToString();
                                if (displayName != null)
                                {
                                    if (displayName.Contains(appName, StringComparison.OrdinalIgnoreCase) || 
                                        appName.Contains(displayName, StringComparison.OrdinalIgnoreCase))
                                        return true;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Error checking if {appName} is installed", ex);
            }

            // Fallback: Check Start Menu recursively for ANY app
            try
            {
                string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs");
                string commonStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), @"Microsoft\Windows\Start Menu\Programs");

                if (SearchShortcutRecursive(userStartMenu, appName) || SearchShortcutRecursive(commonStartMenu, appName))
                    return true;
            }
            catch { }

            return false;
        }

        private bool SearchShortcutRecursive(string directory, string appName)
        {
            if (!Directory.Exists(directory)) return false;
            try
            {
                var files = Directory.GetFiles(directory, "*.lnk", SearchOption.AllDirectories);
                foreach (var file in files)
                {
                    string fileName = Path.GetFileNameWithoutExtension(file);
                    if (IsMatch(fileName, appName))
                    {
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool IsMatch(string s1, string s2)
        {
            if (string.IsNullOrWhiteSpace(s1) || string.IsNullOrWhiteSpace(s2)) return false;
            
            s1 = s1.ToLower().Trim();
            s2 = s2.ToLower().Trim();

            // Direct containment
            if (s1.Contains(s2) || s2.Contains(s1)) return true;

            // Split into significant words (ignore short words like 'and', 'the', version numbers)
            var words1 = s1.Split(new[] { ' ', '-', '_', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length > 2 && !double.TryParse(w, out _)).ToList();
            var words2 = s2.Split(new[] { ' ', '-', '_', '.', '(', ')' }, StringSplitOptions.RemoveEmptyEntries)
                           .Where(w => w.Length > 2 && !double.TryParse(w, out _)).ToList();

            if (!words1.Any() || !words2.Any()) return false;

            // Count overlapping significant words
            int commonWords = words1.Count(w => words2.Contains(w));
            
            // If we have "Heaven Benchmark" and find "Heaven Benchmark 4.0", it's a match
            return commonWords >= 2 || (words1.Count <= 2 && commonWords >= 1 && words1.Any(w => words2.Contains(w)));
        }

        private bool CheckFileInDirectory(string directory, string fileName)
        {
            if (!Directory.Exists(directory)) return false;
            
            // Literal match
            if (File.Exists(Path.Combine(directory, fileName))) return true;

            // Search in root level
            try
            {
                var files = Directory.GetFiles(directory, $"*{Path.GetFileNameWithoutExtension(fileName)}*", SearchOption.TopDirectoryOnly);
                return files.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase));
            }
            catch { return false; }
        }

        public bool ResetWindowsUpdate() => RunCommand("net stop wuauserv & net start wuauserv");

        public bool IsWindowsUpdateEnabled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\wuauserv"))
                {
                    if (key != null)
                    {
                        var startType = (int)key.GetValue("Start", 4);
                        return startType != 4; // Not Disabled
                    }
                }
            }
            catch { }
            return true;
        }

        public bool DisableWindowsUpdate()
        {
            _logService.LogInfo("Attempting to disable Windows Update...");
            return RunCommand("sc config wuauserv start= disabled & net stop wuauserv");
        }

        public bool EnableWindowsUpdate()
        {
            _logService.LogInfo("Attempting to enable Windows Update...");
            return RunCommand("sc config wuauserv start= auto & net start wuauserv");
        }

        public bool IsDefenderEnabled()
        {
            try
            {
                // Check Registry Policy first - this is the most definitive for "Disabled by tool"
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection"))
                {
                    if (key != null)
                    {
                        var val = key.GetValue("DisableRealtimeMonitoring");
                        if (val != null && Convert.ToInt32(val) == 1) return false;
                    }
                }

                // Fallback to PowerShell check for real-time status
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"(Get-MpPreference).DisableRealtimeMonitoring\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using (var process = Process.Start(startInfo))
                {
                    string output = process?.StandardOutput.ReadToEnd() ?? "False";
                    process?.WaitForExit();
                    // If DisableRealtimeMonitoring is True, Defender is Disabled (return false)
                    return !output.Trim().Equals("True", StringComparison.OrdinalIgnoreCase);
                }
            }
            catch { return true; }
        }

        public bool DisableDefender() 
        {
            _logService.LogInfo("Attempting to disable Windows Defender...");
            // Try PowerShell first
            RunCommand("powershell -NoProfile -ExecutionPolicy Bypass -Command \"Set-MpPreference -DisableRealtimeMonitoring $true\"");
            // Then force Registry Policy as fallback/reinforcement
            return RunCommand("reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\Real-Time Protection\" /v \"DisableRealtimeMonitoring\" /t REG_DWORD /d 1 /f");
        }

        public bool EnableDefender()
        {
            _logService.LogInfo("Attempting to enable Windows Defender...");
            RunCommand("powershell -NoProfile -ExecutionPolicy Bypass -Command \"Set-MpPreference -DisableRealtimeMonitoring $false\"");
            return RunCommand("reg add \"HKEY_LOCAL_MACHINE\\SOFTWARE\\Policies\\Microsoft\\Windows Defender\\Real-Time Protection\" /v \"DisableRealtimeMonitoring\" /t REG_DWORD /d 0 /f");
        }

        public bool IsFastBootEnabled()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager\Power"))
                {
                    if (key != null)
                    {
                        var value = key.GetValue("HiberbootEnabled");
                        if (value != null && Convert.ToInt32(value) == 1) return true;
                    }
                }
            }
            catch { }
            return false;
        }

        public bool DisableFastBoot()
        {
            _logService.LogInfo("Attempting to disable Fast Boot...");
            return RunCommand("powercfg /h off");
        }

        public bool EnableFastBoot()
        {
            _logService.LogInfo("Attempting to enable Fast Boot...");
            return RunCommand("powercfg /h on");
        }

        public bool CleanTempFiles()
        {
            _logService.LogInfo("Starting junk file cleanup...");
            bool success = true;
            success &= RunCommand("del /q /s /f %temp%\\*");
            success &= RunCommand("del /q /s /f C:\\Windows\\Temp\\*");
            return success;
        }

        public async Task<bool> BackupUserDataAsync(List<string> folderPaths, IProgress<(double progress, string status)> progress)
        {
            return await Task.Run(() =>
            {
                try
                {
                    if (folderPaths == null || !folderPaths.Any())
                    {
                        progress?.Report((0, "Không có thư mục nào được chọn!"));
                        return false;
                    }

                    progress?.Report((0, "Đang chuẩn bị dữ liệu..."));
                    
                    string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string backupRoot = Path.Combine(desktopPath, $"Backup_CaiWin_{DateTime.Now:ddMMyy_HHmm}");
                    
                    // Step 1: Calculate total size for progress bar
                    progress?.Report((0, "Đang tính toán dung lượng..."));
                    long totalSize = 0;
                    foreach (var path in folderPaths)
                    {
                        if (Directory.Exists(path))
                            totalSize += GetDirectorySize(path, backupRoot);
                    }

                    if (totalSize == 0) totalSize = 1; // Avoid division by zero

                    if (!Directory.Exists(backupRoot)) Directory.CreateDirectory(backupRoot);

                    // Step 2: Copy with progress
                    long copiedSize = 0;
                    foreach (var path in folderPaths)
                    {
                        if (!Directory.Exists(path)) continue;
                        
                        string folderName = Path.GetFileName(path);
                        if (string.IsNullOrEmpty(folderName)) folderName = "UserFolder";
                        
                        string destination = Path.Combine(backupRoot, folderName);
                        
                        // We use a wrapper for copiedSize to pass it by reference effectively in recursion
                        CopyDirectoryWithProgress(path, destination, backupRoot, totalSize, ref copiedSize, progress);
                    }

                    progress?.Report((100, "Hoàn tất sao lưu!"));
                    return true;
                }
                catch (Exception ex)
                {
                    _logService.LogError("Backup failed", ex);
                    progress?.Report((0, $"Lỗi: {ex.Message}"));
                    return false;
                }
            });
        }

        private long GetDirectorySize(string directory, string excludeDir)
        {
            if (directory.Equals(excludeDir, StringComparison.OrdinalIgnoreCase)) return 0;
            long size = 0;
            try
            {
                var files = Directory.GetFiles(directory);
                foreach (var file in files) size += new FileInfo(file).Length;
                
                var subDirs = Directory.GetDirectories(directory);
                foreach (var subDir in subDirs) size += GetDirectorySize(subDir, excludeDir);
            }
            catch { }
            return size;
        }

        private void CopyDirectoryWithProgress(string sourceDir, string destDir, string excludeDir, long totalSize, ref long copiedSize, IProgress<(double, string)> progress)
        {
            if (sourceDir.Equals(excludeDir, StringComparison.OrdinalIgnoreCase)) return;

            Directory.CreateDirectory(destDir);

            foreach (string file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string fileName = Path.GetFileName(file);
                    string destFile = Path.Combine(destDir, fileName);
                    
                    progress?.Report(((double)copiedSize / totalSize * 100, $"Đang copy: {fileName}"));
                    
                    File.Copy(file, destFile, true);
                    copiedSize += new FileInfo(file).Length;
                }
                catch { }
            }

            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                if (subDir.Equals(excludeDir, StringComparison.OrdinalIgnoreCase)) continue;
                string destSubDir = Path.Combine(destDir, Path.GetFileName(subDir));
                CopyDirectoryWithProgress(subDir, destSubDir, excludeDir, totalSize, ref copiedSize, progress);
            }
        }

        public bool SetupTimezoneAndRegion()
        {
            try
            {
                // 1. Set Timezone to SE Asia Standard Time (UTC+07:00 Bangkok, Hanoi, Jakarta)
                RunCommand("tzutil /s \"SE Asia Standard Time\"");

                // 2. Disable Auto Timezone update (matches image "Set time zone automatically: Off")
                using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\tzautoupdate", true))
                {
                    if (key != null) key.SetValue("Start", 4, RegistryValueKind.DWord);
                }

                // Sync thời gian online
                RunCommand("net start w32time & w32tm /resync /force");

                // 3. Set Region Format to English (United Kingdom)
                // We use PowerShell for this as it's the most reliable across Win 10/11
                string psCommand = "Set-Culture en-GB; Set-WinSystemLocale en-GB; Set-WinHomeLocation -GeoId 242";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                })?.WaitForExit();

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Setup Timezone/Region failed", ex);
                return false;
            }
        }

        public bool ShowModernDesktopIcons()
        {
            try
            {
                _logService.LogInfo("Setting desktop icons visibility (Advanced)...");
                
                // 1. Ensure global "Show Desktop Icons" is ON
                using (var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    key?.SetValue("HideIcons", 0, RegistryValueKind.DWord);
                }

                // 2. Set icons IDs for both Start Menu styles
                string[] paths = {
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel",
                    @"Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\ClassicStartMenu"
                };

                string[] iconGuids = {
                    "{20D04FE0-3AEA-1069-A2D8-08002B30309D}", // This PC
                    "{645FF040-5081-101B-9F08-00AA002F954E}", // Recycle Bin
                    "{5399E806-8260-4AD6-9744-245050A0E000}", // Control Panel
                    "{59031a47-3f72-44a7-89c5-5595fe6b30ee}", // User Files
                    "{F02C103A-2240-4C57-96A7-D36F60A0C000}"  // Network
                };

                foreach (var path in paths)
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(path))
                    {
                        if (key != null)
                        {
                            foreach (var guid in iconGuids)
                                key.SetValue(guid, 0, RegistryValueKind.DWord);
                        }
                    }
                }

                // 3. Force system-wide refresh using direct Win32 API
                SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero); // SHCNE_ASSOCCHANGED
                
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Show Desktop Icons failed", ex);
                return false;
            }
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        public static extern void SHChangeNotify(uint wEventId, uint uFlags, System.IntPtr dwItem1, System.IntPtr dwItem2);

        public bool ActivateWindows()
        {
            _logService.LogInfo("Attempting Windows activation via KMS...");
            try
            {
                // Generic KMS activation script
                string script = "slmgr /skms kms8.msguides.com & slmgr /ato";
                return RunCommand(script);
            }
            catch (Exception ex)
            {
                _logService.LogError("Windows activation failed", ex);
                return false;
            }
        }

        public bool ActivateOffice()
        {
            _logService.LogInfo("Opening MAS (Microsoft Activation Scripts) interface...");
            try
            {
                // Chuyển lại cho hiện cửa sổ để người dùng tự chọn menu (IRM MAS)
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -ExecutionPolicy Bypass -Command \"irm https://get.activated.win | iex\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };
                
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to open MAS interface", ex);
                return false;
            }
        }

        public bool DisableTelemetry()
        {
            _logService.LogInfo("Disabling Windows Telemetry & Tracking...");
            try
            {
                // Disable DiagTrack service
                RunCommand("sc config DiagTrack start= disabled & net stop DiagTrack");
                RunCommand("sc config dmwappushservice start= disabled & net stop dmwappushservice");

                // Registry: set telemetry to 0 (Security level — lowest)
                RunCommand("reg add \"HKLM\\SOFTWARE\\Policies\\Microsoft\\Windows\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f");
                RunCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\DataCollection\" /v AllowTelemetry /t REG_DWORD /d 0 /f");

                // Disable Windows Error Reporting
                RunCommand("sc config WerSvc start= disabled & net stop WerSvc");

                // Disable Customer Experience Improvement
                RunCommand("reg add \"HKLM\\SOFTWARE\\Microsoft\\SQMClient\\Windows\" /v CEIPEnable /t REG_DWORD /d 0 /f");

                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Disable Telemetry failed", ex);
                return false;
            }
        }

        public bool FlushDnsAndResetNetwork()
        {
            _logService.LogInfo("Flushing DNS and resetting network stack...");
            try
            {
                RunCommand("ipconfig /flushdns");
                RunCommand("ipconfig /release");
                RunCommand("ipconfig /renew");
                RunCommand("netsh winsock reset");
                RunCommand("netsh int ip reset");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Flush DNS / Reset Network failed", ex);
                return false;
            }
        }

        public string GetCurrentPowerPlan()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powercfg",
                    Arguments = "/getactivescheme",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();
                if (output.Contains("High performance", StringComparison.OrdinalIgnoreCase)) return "High Performance";
                if (output.Contains("Balanced", StringComparison.OrdinalIgnoreCase)) return "Balanced";
                if (output.Contains("Power saver", StringComparison.OrdinalIgnoreCase)) return "Power Saver";
                return "Unknown";
            }
            catch { return "Unknown"; }
        }

        public bool SetHighPerformancePower()
        {
            _logService.LogInfo("Setting power plan to High Performance...");
            return RunCommand("powercfg /setactive 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        }

        public bool SetBalancedPower()
        {
            _logService.LogInfo("Setting power plan to Balanced...");
            return RunCommand("powercfg /setactive 381b4222-f694-41f0-9685-ff5bb260df2e");
        }

        public bool DisableSleepAndHibernate()
        {
            _logService.LogInfo("Disabling Sleep and Hibernate...");
            RunCommand("powercfg /change standby-timeout-ac 0");
            RunCommand("powercfg /change standby-timeout-dc 0");
            RunCommand("powercfg /change hibernate-timeout-ac 0");
            RunCommand("powercfg /change hibernate-timeout-dc 0");
            return RunCommand("powercfg /h off");
        }

        public bool EnableSleepAndHibernate()
        {
            _logService.LogInfo("Enabling Sleep and Hibernate...");
            RunCommand("powercfg /change standby-timeout-ac 15");
            RunCommand("powercfg /change standby-timeout-dc 10");
            RunCommand("powercfg /h on");
            return true;
        }

        public async Task<string> RunSfcAndDismAsync(IProgress<string> progress)
        {
            return await Task.Run(() =>
            {
                var sb = new System.Text.StringBuilder();
                try
                {
                    progress?.Report("Đang chạy SFC /scannow...");
                    var sfcResult = RunCommandWithOutput("sfc /scannow");
                    sb.AppendLine("=== SFC /scannow ===");
                    sb.AppendLine(sfcResult);
                    sb.AppendLine();

                    progress?.Report("Đang chạy DISM CheckHealth...");
                    var dismCheck = RunCommandWithOutput("DISM /Online /Cleanup-Image /CheckHealth");
                    sb.AppendLine("=== DISM CheckHealth ===");
                    sb.AppendLine(dismCheck);
                    sb.AppendLine();

                    progress?.Report("Đang chạy DISM RestoreHealth...");
                    var dismRestore = RunCommandWithOutput("DISM /Online /Cleanup-Image /RestoreHealth");
                    sb.AppendLine("=== DISM RestoreHealth ===");
                    sb.AppendLine(dismRestore);

                    progress?.Report("Hoàn tất!");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"Lỗi: {ex.Message}");
                    _logService.LogError("SFC/DISM failed", ex);
                }
                return sb.ToString();
            });
        }

        private string RunCommandWithOutput(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadToEnd() ?? "";
                string err = p?.StandardError.ReadToEnd() ?? "";
                p?.WaitForExit();
                return string.IsNullOrWhiteSpace(output) ? err : output;
            }
            catch (Exception ex) { return ex.Message; }
        }

        public void OpenSystemTool(string tool)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = tool,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Cannot open tool: {tool}", ex);
            }
        }

        public bool DisableBitLocker()
        {
            _logService.LogInfo("Attempting to disable BitLocker on all drives...");
            try
            {
                // Tắt BitLocker trên tất cả các ổ đĩa
                string psCommand = @"
                    $volumes = Get-BitLockerVolume | Where-Object { $_.ProtectionStatus -eq 'On' }
                    foreach ($vol in $volumes) {
                        Disable-BitLocker -MountPoint $vol.MountPoint
                    }
                ";
                
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psCommand}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    CreateNoWindow = false
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
                
                _logService.LogInfo("BitLocker disable command completed.");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Disable BitLocker failed", ex);
                return false;
            }
        }

        public async Task<bool> InstallDriverWithDismAsync(string driverFolderPath, IProgress<string>? progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logService.LogInfo($"Installing driver from: {driverFolderPath}");
                    progress?.Report($"DISM /Add-Driver: {driverFolderPath}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "DISM.exe",
                        Arguments = $"/Online /Add-Driver /Driver:\"{driverFolderPath}\" /Recurse /ForceUnsigned",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        Verb = "runas"
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        _logService.LogError("Failed to start DISM process");
                        return false;
                    }

                    string output = process.StandardOutput.ReadToEnd();
                    string error = process.StandardError.ReadToEnd();
                    process.WaitForExit();

                    _logService.LogInfo($"DISM output: {output}");
                    if (!string.IsNullOrWhiteSpace(error))
                        _logService.LogError($"DISM error: {error}");

                    bool success = process.ExitCode == 0;
                    if (success)
                    {
                        _logService.LogInfo($"Driver installed successfully from: {driverFolderPath}");
                        progress?.Report("Driver da cai thanh cong!");
                    }
                    else
                    {
                        _logService.LogError($"DISM failed with exit code {process.ExitCode}");
                        progress?.Report($"DISM that bai (exit code: {process.ExitCode})");
                    }

                    return success;
                }
                catch (Exception ex)
                {
                    _logService.LogError($"InstallDriverWithDism failed: {driverFolderPath}", ex);
                    progress?.Report($"Loi: {ex.Message}");
                    return false;
                }
            });
        }

        private bool RunCommand(string command)
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c {command}",
                    CreateNoWindow = true,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    Verb = "runas"
                };
                var process = Process.Start(psi);
                process?.WaitForExit();
                return process?.ExitCode == 0;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Command failed: {command}", ex);
                return false;
            }
        }
    }
}
