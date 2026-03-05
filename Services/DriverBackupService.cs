using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Threading;
using System.Threading.Tasks;

namespace LapLapAutoTool.Services
{
    public interface IDriverBackupService
    {
        Task<string?> ExportDriversAsync(IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default);
        Task<string?> ZipDriverBackupAsync(string backupFolder, IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default);
    }

    public class DriverBackupService : IDriverBackupService
    {
        private readonly ILogService _logService;

        private static readonly string _backupRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "LapLapDownloads", "DriverBackup");

        public DriverBackupService(ILogService logService)
        {
            _logService = logService;
        }

        public async Task<string?> ExportDriversAsync(IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Get model name for folder naming
                    string modelName = GetModelName();
                    string safeName = SanitizeFileName(modelName);
                    string exportDir = Path.Combine(_backupRoot, safeName);

                    if (Directory.Exists(exportDir))
                        Directory.Delete(exportDir, true);
                    Directory.CreateDirectory(exportDir);

                    progress?.Report((10, $"Dang export driver ({modelName})..."));
                    _logService.LogInfo($"Exporting drivers to: {exportDir}");

                    var psi = new ProcessStartInfo
                    {
                        FileName = "DISM.exe",
                        Arguments = $"/Online /Export-Driver /Destination:\"{exportDir}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null)
                    {
                        _logService.LogError("Failed to start DISM export");
                        return null;
                    }

                    // Read output for progress
                    string output = "";
                    while (!process.StandardOutput.EndOfStream)
                    {
                        ct.ThrowIfCancellationRequested();
                        string? line = process.StandardOutput.ReadLine();
                        if (line != null)
                        {
                            output += line + "\n";
                            // Parse progress from DISM output lines like "Exporting 1 of 25"
                            if (line.Contains("Exporting") && line.Contains(" of "))
                            {
                                try
                                {
                                    var parts = line.Trim().Split(' ');
                                    for (int i = 0; i < parts.Length - 2; i++)
                                    {
                                        if (parts[i + 1] == "of" &&
                                            int.TryParse(parts[i], out int current) &&
                                            int.TryParse(parts[i + 2].TrimEnd('.'), out int total) &&
                                            total > 0)
                                        {
                                            double pct = 10 + (double)current / total * 80;
                                            progress?.Report((pct, $"Export driver {current}/{total}..."));
                                            break;
                                        }
                                    }
                                }
                                catch { }
                            }
                        }
                    }

                    process.WaitForExit();
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode != 0)
                    {
                        _logService.LogError($"DISM export failed (exit {process.ExitCode}): {error}");
                        progress?.Report((0, $"Export that bai (exit code: {process.ExitCode})"));
                        return null;
                    }

                    // Count exported drivers
                    int driverCount = Directory.GetDirectories(exportDir).Length;
                    progress?.Report((90, $"Da export {driverCount} driver"));
                    _logService.LogInfo($"Exported {driverCount} drivers to: {exportDir}");

                    return exportDir;
                }
                catch (OperationCanceledException)
                {
                    progress?.Report((0, "Da huy export"));
                    return null;
                }
                catch (Exception ex)
                {
                    _logService.LogError("Driver export failed", ex);
                    progress?.Report((0, $"Loi export: {ex.Message}"));
                    return null;
                }
            }, ct);
        }

        public async Task<string?> ZipDriverBackupAsync(string backupFolder, IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default)
        {
            return await Task.Run(() =>
            {
                try
                {
                    string modelName = GetModelName();
                    string safeName = SanitizeFileName(modelName);
                    string zipPath = Path.Combine(_backupRoot, $"{safeName}_Drivers.zip");

                    if (File.Exists(zipPath)) File.Delete(zipPath);

                    progress?.Report((0, "Dang nen file ZIP..."));

                    // Count total files for progress
                    var allFiles = Directory.GetFiles(backupFolder, "*.*", SearchOption.AllDirectories);
                    int total = allFiles.Length;
                    int done = 0;

                    using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
                    {
                        foreach (var file in allFiles)
                        {
                            ct.ThrowIfCancellationRequested();
                            string entryName = Path.GetRelativePath(backupFolder, file);
                            zip.CreateEntryFromFile(file, entryName, CompressionLevel.Optimal);
                            done++;
                            if (done % 10 == 0 || done == total)
                            {
                                double pct = (double)done / total * 100;
                                progress?.Report((pct, $"Nen {done}/{total} files..."));
                            }
                        }
                    }

                    long zipSize = new FileInfo(zipPath).Length;
                    progress?.Report((100, $"ZIP hoan tat: {FormatBytes(zipSize)}"));
                    _logService.LogInfo($"Created ZIP: {zipPath} ({FormatBytes(zipSize)})");

                    return zipPath;
                }
                catch (OperationCanceledException)
                {
                    progress?.Report((0, "Da huy nen"));
                    return null;
                }
                catch (Exception ex)
                {
                    _logService.LogError("ZIP creation failed", ex);
                    progress?.Report((0, $"Loi nen: {ex.Message}"));
                    return null;
                }
            }, ct);
        }

        public static string GetModelName()
        {
            // Try PowerShell Get-CimInstance first (works on Windows 11)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "powershell",
                    Arguments = "-NoProfile -Command \"(Get-CimInstance Win32_ComputerSystem).Model\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadToEnd()?.Trim() ?? "";
                p?.WaitForExit();

                if (!string.IsNullOrEmpty(output) && p?.ExitCode == 0)
                    return output;
            }
            catch { }

            // Fallback: wmic (older Windows)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "computersystem get model",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = p?.StandardOutput.ReadToEnd() ?? "";
                p?.WaitForExit();

                var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                foreach (var line in lines)
                {
                    string trimmed = line.Trim();
                    if (!string.IsNullOrEmpty(trimmed) && !trimmed.StartsWith("Model", StringComparison.OrdinalIgnoreCase))
                        return trimmed;
                }
            }
            catch { }
            return "Unknown_Model";
        }

        private static string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name.Replace(' ', '_').Trim('_');
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
            if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024:0.0} MB";
            return $"{bytes / 1024.0:0.0} KB";
        }
    }
}
