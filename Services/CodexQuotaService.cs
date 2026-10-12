using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class CodexQuotaService
{
    private readonly MonitorConfig _config;
    private readonly string _authPath;
    private readonly string _cookiePath;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private int _lastPercent = 0;
    private string _lastDetail = "Codex: 측정 대기 중...";
    private bool _hasSuccessData = false;

    public CodexQuotaService(MonitorConfig config)
    {
        _config = config;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _authPath = Path.Combine(userProfile, ".codex", "auth.json");
        _cookiePath = Path.Combine(userProfile, ".codex", "curl_cookies.txt");
    }

    public async Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        try
        {
            if (!File.Exists(_authPath))
            {
                if (!_hasSuccessData) _lastDetail = "Codex: auth.json 없음";
                return (_lastPercent, _lastDetail);
            }

            var authText = await File.ReadAllTextAsync(_authPath);
            using var authDoc = JsonDocument.Parse(authText);

            if (!authDoc.RootElement.TryGetProperty("tokens", out var tokens) ||
                !tokens.TryGetProperty("access_token", out var tokenElem))
            {
                if (!_hasSuccessData) _lastDetail = "Codex: 토큰 파싱 실패";
                return (_lastPercent, _lastDetail);
            }

            string? token = tokenElem.GetString();
            if (string.IsNullOrEmpty(token))
            {
                if (!_hasSuccessData) _lastDetail = "Codex: 토큰 없음";
                return (_lastPercent, _lastDetail);
            }

            // Cloudflare WAF 세션 쿠키(__cf_bm)를 유지하면서 curl.exe로 호출
            var (json, curlErr) = await FetchViaCurlAsync(token);
            if (string.IsNullOrEmpty(json))
            {
                json = await FetchViaHttpClientAsync(token);
            }

            if (string.IsNullOrEmpty(json))
            {
                if (!_hasSuccessData)
                {
                    _lastDetail = $"Codex: 사용량 API 응답 없음 ({curlErr})";
                }
                return (_lastPercent, _lastDetail);
            }

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string rawPlan = root.TryGetProperty("plan_type", out var p) ? p.GetString() ?? "Pro" : "Pro";
            string plan = rawPlan.ToLower() switch
            {
                "prolite" => "Pro Lite",
                "pro" => "Pro",
                _ => rawPlan
            };

            // rate_limit.primary_window
            if (root.TryGetProperty("rate_limit", out var rateLimit) &&
                rateLimit.TryGetProperty("primary_window", out var primaryWindow) &&
                primaryWindow.ValueKind == JsonValueKind.Object)
            {
                int usedPercent = primaryWindow.TryGetProperty("used_percent", out var up) ? up.GetInt32() : 0;
                int remaining = Math.Clamp(100 - usedPercent, 0, 100);

                string resetStr = "N/A";
                if (primaryWindow.TryGetProperty("reset_at", out var resetAtElem))
                {
                    long resetAtEpoch = resetAtElem.GetInt64();
                    var resetTime = DateTimeOffset.FromUnixTimeSeconds(resetAtEpoch).LocalDateTime;
                    var diff = resetTime - DateTime.Now;

                    string diffText = diff.TotalDays >= 1 
                        ? $"{(int)diff.TotalDays}일 {diff.Hours}시간 뒤" 
                        : $"{Math.Max(0, (int)diff.TotalHours)}시간 {diff.Minutes}분 뒤";

                    string datePart = resetTime.Date == DateTime.Today ? "오늘 " : $"{resetTime:M월 d일} ";
                    resetStr = $"{datePart}{resetTime:HH:mm} ({diffText})";
                }

                string credits = "";
                if (root.TryGetProperty("credits", out var cr) &&
                    cr.TryGetProperty("balance", out var bal))
                {
                    credits = $"\n크레딧 잔액: {bal.GetString()}";
                }

                _lastPercent = remaining;
                _lastDetail = $"Codex ({plan})\n" +
                              $"주간 잔여량: {remaining}% (사용률: {usedPercent}%)\n" +
                              $"초기화 예정: {resetStr}" + credits;
                _hasSuccessData = true;

                return (_lastPercent, _lastDetail);
            }
        }
        catch (Exception ex)
        {
            if (!_hasSuccessData) _lastDetail = $"Codex 조회 오류: {ex.Message}";
        }

        return (_lastPercent, _lastDetail);
    }

    private async Task<(string? Json, string? Error)> FetchViaCurlAsync(string token)
    {
        try
        {
            var curlPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "curl.exe");
            if (!File.Exists(curlPath))
            {
                curlPath = "curl.exe";
            }

            var psi = new ProcessStartInfo
            {
                FileName = curlPath,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            // Cloudflare Bot Management(__cf_bm) 쿠키를 파일로 저장하고 매 요청 재사용
            psi.ArgumentList.Add("-s");
            psi.ArgumentList.Add("--max-time");
            psi.ArgumentList.Add("6");
            psi.ArgumentList.Add("-b");
            psi.ArgumentList.Add(_cookiePath);
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(_cookiePath);
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add($"Authorization: Bearer {token}");
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add("User-Agent: codex-tui/0.154.0");
            psi.ArgumentList.Add("-H");
            psi.ArgumentList.Add("Accept: application/json");
            psi.ArgumentList.Add("https://chatgpt.com/backend-api/codex/usage");

            using var proc = Process.Start(psi);
            if (proc == null) return (null, "curl 실행 불가");

            var stdoutTask = proc.StandardOutput.ReadToEndAsync();
            var stderrTask = proc.StandardError.ReadToEndAsync();
            await proc.WaitForExitAsync();

            string output = await stdoutTask;
            string err = await stderrTask;

            string trimmed = output.TrimStart();
            if (trimmed.StartsWith('{'))
            {
                return (output, null);
            }

            if (trimmed.StartsWith('<'))
            {
                return (null, "Cloudflare WAF 응답");
            }

            return (null, string.IsNullOrWhiteSpace(err) ? "JSON 아님" : err);
        }
        catch (Exception ex)
        {
            return (null, ex.Message);
        }
    }

    private static async Task<string?> FetchViaHttpClientAsync(string token)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://chatgpt.com/backend-api/codex/usage");
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Headers.Add("User-Agent", "codex-tui/0.154.0");

            using var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                return await resp.Content.ReadAsStringAsync();
            }
        }
        catch { }

        return null;
    }
}
