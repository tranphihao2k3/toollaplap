using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace LapLapAutoTool.Services
{
    public interface IDownloadService
    {
        Task<string?> DownloadFileAsync(string url, string fileName, IProgress<(double percent, string status)> progress, CancellationToken ct = default);
        Task<string?> ExtractArchiveAsync(string archivePath, IProgress<(double percent, string status)> progress, string password = "");
        void OpenFolderInExplorer(string folderPath);
    }

    public class DownloadService : IDownloadService
    {
        private readonly ILogService _logService;

        // Dùng CookieContainer để Google Drive confirm cookie hoạt động
        private static readonly HttpClientHandler _handler = new HttpClientHandler
        {
            AllowAutoRedirect = true,
            UseCookies = true,
            CookieContainer = new System.Net.CookieContainer()
        };
        private static readonly HttpClient _http = new HttpClient(_handler);

        private static readonly string _downloadRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads", "LapLapDownloads");

        public DownloadService(ILogService logService)
        {
            _logService = logService;
            _http.Timeout = TimeSpan.FromHours(2);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0.0.0 Safari/537.36");
            if (!Directory.Exists(_downloadRoot)) Directory.CreateDirectory(_downloadRoot);
        }

        public async Task<string?> DownloadFileAsync(string url, string fileName, IProgress<(double percent, string status)> progress, CancellationToken ct = default)
        {
            try
            {
                // Chuẩn hoá URL Google Drive
                url = ConvertGoogleDriveUrl(url);

                progress?.Report((0, "Đang kết nối..."));

                // Bước 1: HEAD request để lấy tên file thật từ Content-Disposition
                using var headReq = new HttpRequestMessage(HttpMethod.Get, url);
                using var response = await _http.SendAsync(headReq, HttpCompletionOption.ResponseHeadersRead, ct);
                response.EnsureSuccessStatusCode();

                // Lấy tên file từ Content-Disposition nếu có
                string? cdFileName = response.Content.Headers.ContentDisposition?.FileNameStar
                                  ?? response.Content.Headers.ContentDisposition?.FileName;
                if (!string.IsNullOrWhiteSpace(cdFileName))
                {
                    fileName = cdFileName.Trim('"').Trim();
                }

                // Nếu vẫn không có extension → detect từ Content-Type hoặc đọc magic bytes
                if (string.IsNullOrWhiteSpace(Path.GetExtension(fileName)))
                {
                    string? ct2 = response.Content.Headers.ContentType?.MediaType;
                    string guessedExt = ct2 switch
                    {
                        "application/zip"                    => ".zip",
                        "application/x-rar-compressed"
                            or "application/vnd.rar"         => ".rar",
                        "application/x-7z-compressed"        => ".7z",
                        // Google Drive trả về octet-stream → KHÔNG đoán mò
                        // Sẽ đọc magic bytes sau khi tải xong
                        _ => ""
                    };
                    if (!string.IsNullOrEmpty(guessedExt))
                        fileName += guessedExt;
                    // Nếu vẫn trống → để nguyên, sẽ detect bằng magic bytes sau
                }

                string destPath = Path.Combine(_downloadRoot, fileName);

                long totalBytes = response.Content.Headers.ContentLength ?? -1;
                long downloadedBytes = 0;
                var sw = Stopwatch.StartNew();

                using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, true);

                byte[] buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await stream.ReadAsync(buffer, ct)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
                    downloadedBytes += bytesRead;

                    if (totalBytes > 0)
                    {
                        double percent = (double)downloadedBytes / totalBytes * 100;
                        double speed = downloadedBytes / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                        double remaining = totalBytes - downloadedBytes;
                        double eta = speed > 0 ? remaining / 1024 / 1024 / speed : 0;
                        progress?.Report((percent, $"Đang tải... {FormatBytes(downloadedBytes)} / {FormatBytes(totalBytes)}  ({speed:0.0} MB/s)  còn {eta:0}s"));
                    }
                    else
                    {
                        progress?.Report((-1, $"Đang tải... {FormatBytes(downloadedBytes)}"));
                    }
                }

                // Detect extension bằng magic bytes nếu chưa có
                if (string.IsNullOrWhiteSpace(Path.GetExtension(destPath)))
                {
                    string detectedExt = DetectExtensionByMagicBytes(destPath);
                    if (!string.IsNullOrEmpty(detectedExt))
                    {
                        string newPath = destPath + detectedExt;
                        File.Move(destPath, newPath, true);
                        destPath = newPath;
                    }
                }

                _logService.LogInfo($"Downloaded: {Path.GetFileName(destPath)} → {destPath}");
                return destPath;
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, "Đã huỷ tải."));
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError($"Download failed: {url}", ex);
                progress?.Report((0, $"Lỗi tải: {ex.Message}"));
                return null;
            }
        }

        public async Task<string?> ExtractArchiveAsync(string archivePath, IProgress<(double percent, string status)> progress, string password = "")
        {
            try
            {
                string ext = Path.GetExtension(archivePath).ToLower();
                string extractDir = Path.Combine(Path.GetDirectoryName(archivePath)!, Path.GetFileNameWithoutExtension(archivePath));

                if (Directory.Exists(extractDir)) Directory.Delete(extractDir, true);
                Directory.CreateDirectory(extractDir);

                if (ext == ".zip")
                {
                    progress?.Report((0, "Đang giải nén..."));
                    await Task.Run(() =>
                    {
                        using var archive = ZipFile.OpenRead(archivePath);
                        int total = archive.Entries.Count;
                        int done = 0;
                        foreach (var entry in archive.Entries)
                        {
                            string destFile = Path.Combine(extractDir, entry.FullName);
                            string destFolder = Path.GetDirectoryName(destFile)!;
                            if (!Directory.Exists(destFolder)) Directory.CreateDirectory(destFolder);
                            if (!string.IsNullOrEmpty(entry.Name))
                                entry.ExtractToFile(destFile, true);
                            done++;
                            double pct = (double)done / total * 100;
                            progress?.Report((pct, $"Giải nén {done}/{total}: {entry.Name}"));
                        }
                    });

                    // Xoá file zip sau khi giải nén xong
                    File.Delete(archivePath);
                    _logService.LogInfo($"Extracted to: {extractDir}");
                    return extractDir;
                }
                else if (ext == ".rar" || ext == ".7z")
                {
                    progress?.Report((0, "Đang giải nén bằng 7-Zip..."));
                    bool success = await Task.Run(() => TryExtractWith7Zip(archivePath, extractDir, password));
                    if (success)
                    {
                        File.Delete(archivePath);
                        return extractDir;
                    }
                    else
                    {
                        // Không có 7-zip → mở thư mục chứa file để user tự giải nén
                        progress?.Report((100, "Không tìm thấy 7-Zip. Mở thư mục để bạn giải nén thủ công."));
                        return Path.GetDirectoryName(archivePath);
                    }
                }
                else
                {
                    // File .exe hoặc khác → trả về thư mục chứa nó
                    Directory.Delete(extractDir, true);
                    return Path.GetDirectoryName(archivePath);
                }
            }
            catch (Exception ex)
            {
                _logService.LogError($"Extract failed: {archivePath}", ex);
                progress?.Report((0, $"Lỗi giải nén: {ex.Message}"));
                return Path.GetDirectoryName(archivePath);
            }
        }

        public void OpenFolderInExplorer(string folderPath)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{folderPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.LogError($"Cannot open folder: {folderPath}", ex);
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private bool TryExtractWith7Zip(string archivePath, string destDir, string password = "")
        {
            string[] possiblePaths = {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe"
            };
            foreach (var path in possiblePaths)
            {
                if (!File.Exists(path)) continue;
                string pwArg = string.IsNullOrEmpty(password) ? "" : $" -p\"{password}\"";
                var p = Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = $"x \"{archivePath}\" -o\"{destDir}\" -y{pwArg}",
                    UseShellExecute = false,
                    CreateNoWindow = true
                });
                p?.WaitForExit();
                return p?.ExitCode == 0;
            }
            return false;
        }

        /// <summary>Chuẩn hoá mọi dạng link Google Drive → direct download với confirm</summary>
        private static string ConvertGoogleDriveUrl(string url)
        {
            string? fileId = null;

            // Dạng 1: https://drive.google.com/file/d/FILE_ID/view
            if (url.Contains("drive.google.com/file/d/"))
            {
                int start = url.IndexOf("/file/d/") + 8;
                int end = url.IndexOf("/", start);
                fileId = end > 0 ? url[start..end] : url[start..];
            }
            // Dạng 2: https://drive.google.com/uc?id=FILE_ID hoặc ?export=download&id=...
            else if (url.Contains("drive.google.com/uc") && url.Contains("id="))
            {
                int start = url.IndexOf("id=") + 3;
                int end = url.IndexOf("&", start);
                fileId = end > 0 ? url[start..end] : url[start..];
            }
            // Dạng 3: https://drive.usercontent.google.com/download?id=FILE_ID
            else if (url.Contains("drive.usercontent.google.com") && url.Contains("id="))
            {
                int start = url.IndexOf("id=") + 3;
                int end = url.IndexOf("&", start);
                fileId = end > 0 ? url[start..end] : url[start..];
            }

            if (!string.IsNullOrEmpty(fileId))
                return $"https://drive.usercontent.google.com/download?id={fileId}&export=download&authuser=0&confirm=t";

            // Link khác → giữ nguyên
            return url;
        }

        /// <summary>Đọc 8 bytes đầu để xác định loại file</summary>
        private static string DetectExtensionByMagicBytes(string filePath)
        {
            try
            {
                using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read);
                byte[] magic = new byte[8];
                int read = fs.Read(magic, 0, 8);
                if (read < 4) return "";

                // ZIP: PK\x03\x04
                if (magic[0] == 0x50 && magic[1] == 0x4B && magic[2] == 0x03 && magic[3] == 0x04)
                    return ".zip";
                // RAR4: Rar!\x1A\x07\x00
                if (magic[0] == 0x52 && magic[1] == 0x61 && magic[2] == 0x72 && magic[3] == 0x21
                    && magic[4] == 0x1A && magic[5] == 0x07 && magic[6] == 0x00)
                    return ".rar";
                // RAR5: Rar!\x1A\x07\x01\x00
                if (magic[0] == 0x52 && magic[1] == 0x61 && magic[2] == 0x72 && magic[3] == 0x21
                    && magic[4] == 0x1A && magic[5] == 0x07 && magic[6] == 0x01 && magic[7] == 0x00)
                    return ".rar";
                // 7Z: 7z\xBC\xAF\x27\x1C
                if (magic[0] == 0x37 && magic[1] == 0x7A && magic[2] == 0xBC && magic[3] == 0xAF)
                    return ".7z";
                // EXE/MSI: MZ
                if (magic[0] == 0x4D && magic[1] == 0x5A)
                    return ".exe";
            }
            catch { }
            return "";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
            if (bytes >= 1024 * 1024)         return $"{bytes / 1024.0 / 1024:0.0} MB";
            return $"{bytes / 1024.0:0.0} KB";
        }
    }
}
