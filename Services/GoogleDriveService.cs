using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LapLapAutoTool.Services
{
    public interface IGoogleDriveService
    {
        bool IsAuthenticated { get; }
        string? AuthenticatedEmail { get; }
        Task<bool> AuthenticateAsync();
        Task<(string fileId, string webViewLink)?> UploadFileAsync(string filePath, IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default);
        void Logout();
    }

    public class GoogleDriveService : IGoogleDriveService
    {
        private readonly ILogService _logService;
        private static readonly HttpClient _http = new HttpClient { Timeout = TimeSpan.FromHours(4) };

        private string _clientId = "";
        private string _clientSecret = "";
        private string? _accessToken;
        private string? _refreshToken;
        private DateTime _tokenExpiry = DateTime.MinValue;

        private static readonly string _tokenPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "gdrive_token.json");

        private static readonly string _configPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Resources", "google_drive_config.json");

        public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpiry;
        public string? AuthenticatedEmail { get; private set; }

        public GoogleDriveService(ILogService logService)
        {
            _logService = logService;
            LoadConfig();
            LoadSavedToken();
        }

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    var json = File.ReadAllText(_configPath);
                    using var doc = JsonDocument.Parse(json);
                    _clientId = doc.RootElement.GetProperty("ClientId").GetString() ?? "";
                    _clientSecret = doc.RootElement.GetProperty("ClientSecret").GetString() ?? "";
                }
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to load Google Drive config", ex);
            }
        }

        public void SaveConfig(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;

            var dir = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            var config = JsonSerializer.Serialize(new { ClientId = clientId, ClientSecret = clientSecret },
                new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_configPath, config);
        }

        private void LoadSavedToken()
        {
            try
            {
                if (!File.Exists(_tokenPath)) return;
                var json = File.ReadAllText(_tokenPath);
                using var doc = JsonDocument.Parse(json);
                _refreshToken = doc.RootElement.GetProperty("refresh_token").GetString();
                if (doc.RootElement.TryGetProperty("email", out var emailProp))
                    AuthenticatedEmail = emailProp.GetString();
            }
            catch { }
        }

        private void SaveToken()
        {
            try
            {
                var data = new Dictionary<string, string?>
                {
                    ["refresh_token"] = _refreshToken,
                    ["email"] = AuthenticatedEmail
                };
                File.WriteAllText(_tokenPath, JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception ex)
            {
                _logService.LogError("Failed to save token", ex);
            }
        }

        public async Task<bool> AuthenticateAsync()
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            {
                _logService.LogError("Google Drive Client ID/Secret chua duoc cau hinh");
                return false;
            }

            // Try refresh first
            if (!string.IsNullOrEmpty(_refreshToken))
            {
                if (await RefreshAccessTokenAsync())
                    return true;
            }

            // Full OAuth2 flow
            return await FullOAuth2FlowAsync();
        }

        private async Task<bool> FullOAuth2FlowAsync()
        {
            try
            {
                // PKCE
                string codeVerifier = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');
                byte[] challengeBytes = SHA256.HashData(Encoding.ASCII.GetBytes(codeVerifier));
                string codeChallenge = Convert.ToBase64String(challengeBytes)
                    .TrimEnd('=').Replace('+', '-').Replace('/', '_');

                // Start local listener
                int port = FindFreePort();
                string redirectUri = $"http://127.0.0.1:{port}/";
                var listener = new HttpListener();
                listener.Prefixes.Add(redirectUri);
                listener.Start();

                string authUrl = "https://accounts.google.com/o/oauth2/v2/auth"
                    + $"?client_id={Uri.EscapeDataString(_clientId)}"
                    + $"&redirect_uri={Uri.EscapeDataString(redirectUri)}"
                    + "&response_type=code"
                    + $"&scope={Uri.EscapeDataString("https://www.googleapis.com/auth/drive.file email")}"
                    + $"&code_challenge={Uri.EscapeDataString(codeChallenge)}"
                    + "&code_challenge_method=S256"
                    + "&access_type=offline"
                    + "&prompt=consent";

                Process.Start(new ProcessStartInfo(authUrl) { UseShellExecute = true });

                var context = await listener.GetContextAsync();
                string? code = context.Request.QueryString["code"];
                string? error = context.Request.QueryString["error"];

                // Send response to browser
                string html = error != null
                    ? "<html><body style='background:#1a1a2e;color:#ff6b6b;font-family:Consolas;text-align:center;padding-top:100px'><h2>Xac thuc that bai</h2><p>Ban co the dong tab nay.</p></body></html>"
                    : "<html><body style='background:#1a1a2e;color:#1ae65d;font-family:Consolas;text-align:center;padding-top:100px'><h2>Xac thuc thanh cong!</h2><p>Ban co the dong tab nay va quay lai app.</p></body></html>";
                byte[] responseBytes = Encoding.UTF8.GetBytes(html);
                context.Response.ContentType = "text/html; charset=utf-8";
                context.Response.ContentLength64 = responseBytes.Length;
                await context.Response.OutputStream.WriteAsync(responseBytes);
                context.Response.Close();
                listener.Stop();

                if (string.IsNullOrEmpty(code))
                {
                    _logService.LogError($"OAuth error: {error}");
                    return false;
                }

                // Exchange code for tokens
                var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["code"] = code,
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code",
                    ["code_verifier"] = codeVerifier
                });

                var tokenResponse = await _http.PostAsync("https://oauth2.googleapis.com/token", tokenRequest);
                string tokenJson = await tokenResponse.Content.ReadAsStringAsync();

                if (!tokenResponse.IsSuccessStatusCode)
                {
                    _logService.LogError($"Token exchange failed: {tokenJson}");
                    return false;
                }

                using var doc = JsonDocument.Parse(tokenJson);
                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                _refreshToken = doc.RootElement.GetProperty("refresh_token").GetString();
                int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                // Get user email
                await FetchUserEmailAsync();
                SaveToken();

                _logService.LogInfo($"Google Drive authenticated: {AuthenticatedEmail}");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("OAuth2 flow failed", ex);
                return false;
            }
        }

        private async Task<bool> RefreshAccessTokenAsync()
        {
            try
            {
                var request = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = _clientId,
                    ["client_secret"] = _clientSecret,
                    ["refresh_token"] = _refreshToken!,
                    ["grant_type"] = "refresh_token"
                });

                var response = await _http.PostAsync("https://oauth2.googleapis.com/token", request);
                if (!response.IsSuccessStatusCode) return false;

                string json = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                _accessToken = doc.RootElement.GetProperty("access_token").GetString();
                int expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
                _tokenExpiry = DateTime.UtcNow.AddSeconds(expiresIn - 60);

                _logService.LogInfo("Google Drive token refreshed");
                return true;
            }
            catch (Exception ex)
            {
                _logService.LogError("Token refresh failed", ex);
                return false;
            }
        }

        private async Task FetchUserEmailAsync()
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, "https://www.googleapis.com/oauth2/v2/userinfo");
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
                var resp = await _http.SendAsync(req);
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    AuthenticatedEmail = doc.RootElement.GetProperty("email").GetString();
                }
            }
            catch { }
        }

        public async Task<(string fileId, string webViewLink)?> UploadFileAsync(
            string filePath, IProgress<(double percent, string status)>? progress = null, CancellationToken ct = default)
        {
            // Ensure authenticated
            if (!IsAuthenticated)
            {
                if (!string.IsNullOrEmpty(_refreshToken))
                    await RefreshAccessTokenAsync();

                if (!IsAuthenticated)
                {
                    progress?.Report((0, "Chua xac thuc Google Drive"));
                    return null;
                }
            }

            try
            {
                string fileName = Path.GetFileName(filePath);
                long fileSize = new FileInfo(filePath).Length;

                progress?.Report((0, $"Dang khoi tao upload: {fileName}"));

                // Initiate resumable upload
                var initReq = new HttpRequestMessage(HttpMethod.Post,
                    "https://www.googleapis.com/upload/drive/v3/files?uploadType=resumable");
                initReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

                string metadata = JsonSerializer.Serialize(new { name = fileName });
                initReq.Content = new StringContent(metadata, Encoding.UTF8, "application/json");
                initReq.Headers.Add("X-Upload-Content-Type", "application/zip");
                initReq.Headers.Add("X-Upload-Content-Length", fileSize.ToString());

                var initResp = await _http.SendAsync(initReq, ct);
                initResp.EnsureSuccessStatusCode();

                Uri sessionUri = initResp.Headers.Location!;

                // Upload in chunks (4MB)
                const int chunkSize = 4 * 1024 * 1024;
                using var fileStream = File.OpenRead(filePath);
                byte[] buffer = new byte[chunkSize];
                long bytesSent = 0;
                var sw = Stopwatch.StartNew();

                string? fileId = null;
                while (bytesSent < fileSize)
                {
                    ct.ThrowIfCancellationRequested();

                    int bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(chunkSize, fileSize - bytesSent)), ct);

                    var chunkReq = new HttpRequestMessage(HttpMethod.Put, sessionUri);
                    chunkReq.Content = new ByteArrayContent(buffer, 0, bytesRead);
                    chunkReq.Content.Headers.ContentLength = bytesRead;
                    chunkReq.Content.Headers.ContentRange =
                        new ContentRangeHeaderValue(bytesSent, bytesSent + bytesRead - 1, fileSize);

                    var chunkResp = await _http.SendAsync(chunkReq, ct);

                    if (chunkResp.StatusCode == (HttpStatusCode)308)
                    {
                        bytesSent += bytesRead;
                        double pct = (double)bytesSent / fileSize * 100;
                        double speed = bytesSent / 1024.0 / 1024.0 / sw.Elapsed.TotalSeconds;
                        progress?.Report((pct, $"Dang upload... {FormatBytes(bytesSent)} / {FormatBytes(fileSize)} ({speed:0.0} MB/s)"));
                        continue;
                    }

                    chunkResp.EnsureSuccessStatusCode();
                    bytesSent += bytesRead;

                    string respJson = await chunkResp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(respJson);
                    fileId = doc.RootElement.GetProperty("id").GetString();
                    break;
                }

                if (string.IsNullOrEmpty(fileId))
                {
                    progress?.Report((0, "Upload that bai: khong nhan duoc file ID"));
                    return null;
                }

                // Set permission: anyone with link
                progress?.Report((95, "Dang chia se file..."));
                await SetAnyonePermissionAsync(fileId, ct);

                // Get shareable link
                string webViewLink = await GetShareableLinkAsync(fileId, ct);

                progress?.Report((100, $"Upload thanh cong!"));
                _logService.LogInfo($"Uploaded to Drive: {fileName} → {webViewLink}");

                return (fileId, webViewLink);
            }
            catch (OperationCanceledException)
            {
                progress?.Report((0, "Da huy upload"));
                return null;
            }
            catch (Exception ex)
            {
                _logService.LogError("Upload failed", ex);
                progress?.Report((0, $"Loi upload: {ex.Message}"));
                return null;
            }
        }

        private async Task SetAnyonePermissionAsync(string fileId, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Post,
                $"https://www.googleapis.com/drive/v3/files/{fileId}/permissions");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);
            string body = JsonSerializer.Serialize(new { role = "reader", type = "anyone" });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();
        }

        private async Task<string> GetShareableLinkAsync(string fileId, CancellationToken ct)
        {
            var req = new HttpRequestMessage(HttpMethod.Get,
                $"https://www.googleapis.com/drive/v3/files/{fileId}?fields=webViewLink");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _accessToken);

            var resp = await _http.SendAsync(req, ct);
            resp.EnsureSuccessStatusCode();

            string json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.GetProperty("webViewLink").GetString()
                ?? $"https://drive.google.com/file/d/{fileId}/view";
        }

        public void Logout()
        {
            _accessToken = null;
            _refreshToken = null;
            _tokenExpiry = DateTime.MinValue;
            AuthenticatedEmail = null;
            if (File.Exists(_tokenPath)) File.Delete(_tokenPath);
        }

        private static int FindFreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:0.0} GB";
            if (bytes >= 1024 * 1024) return $"{bytes / 1024.0 / 1024:0.0} MB";
            return $"{bytes / 1024.0:0.0} KB";
        }
    }
}
