using System.IO;
using System.Net.Http;
using System.Text.Json;
using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class GrokQuotaService
{
    private readonly MonitorConfig _config;
    private readonly string _grokDir;
    private readonly string _authPath;
    private readonly string _sessionsPath;
    private readonly string _versionPath;
    private readonly string _unifiedLogPath;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private int _lastPercent = 100;
    private string _lastDetail = "Grok: 측정 대기 중...";

    public GrokQuotaService(MonitorConfig config)
    {
        _config = config;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _grokDir = Path.Combine(userProfile, ".grok");
        _authPath = Path.Combine(_grokDir, "auth.json");
        _sessionsPath = Path.Combine(_grokDir, "active_sessions.json");
        _versionPath = Path.Combine(_grokDir, "version.json");
        _unifiedLogPath = Path.Combine(_grokDir, "logs", "unified.jsonl");
    }

    public async Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        string version = "1.0";
        if (File.Exists(_versionPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_versionPath));
                if (doc.RootElement.TryGetProperty("version", out var v))
                {
                    version = v.GetString() ?? version;
                }
            }
            catch { }
        }

        int activeCount = 0;
        if (File.Exists(_sessionsPath))
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(_sessionsPath));
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    activeCount = doc.RootElement.GetArrayLength();
                }
            }
            catch { }
        }

        string? token = null;
        string? userId = null;
        string? email = null;

        if (File.Exists(_authPath))
        {
            try
            {
                var authText = File.ReadAllText(_authPath);
                using var doc = JsonDocument.Parse(authText);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("key", out var k)) token = k.GetString();
                    if (prop.Value.TryGetProperty("user_id", out var u)) userId = u.GetString();
                    if (prop.Value.TryGetProperty("email", out var em)) email = em.GetString();
                    if (!string.IsNullOrEmpty(token) && !string.IsNullOrEmpty(userId)) break;
                }
            }
            catch { }
        }

        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(userId))
        {
            return (0, $"xAI Grok CLI (v{version})\n인증 정보 없음 (grok login 필요)");
        }

        // 1. 실시간 xAI Grok 공식 API 호출
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://cli-chat-proxy.grok.com/v1/billing?format=credits");
            req.Headers.Add("Authorization", $"Bearer {token}");
            req.Headers.Add("x-userid", userId);
            req.Headers.Add("User-Agent", "grok-cli/1.0.50");

            using var resp = await _httpClient.SendAsync(req);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("config", out var cfg))
                {
                    double usedPercent = cfg.TryGetProperty("creditUsagePercent", out var cup) ? cup.GetDouble() : 0;
                    int remaining = Math.Clamp((int)Math.Round(100.0 - usedPercent), 0, 100);

                    string resetStr = "N/A";
                    if (cfg.TryGetProperty("currentPeriod", out var period) &&
                        period.TryGetProperty("end", out var endElem) &&
                        DateTime.TryParse(endElem.GetString(), out var dt))
                    {
                        var kst = dt.ToUniversalTime().AddHours(9);
                        var diff = dt.ToUniversalTime() - DateTime.UtcNow;
                        string diffText = diff.TotalDays >= 1
                            ? $"{(int)diff.TotalDays}일 {diff.Hours}시간 뒤"
                            : $"{Math.Max(0, (int)diff.TotalHours)}시간 {diff.Minutes}분 뒤";

                        string datePart = kst.Date == DateTime.Today ? "오늘 " : $"{kst:M월 d일} ";
                        resetStr = $"{datePart}{kst:HH:mm} ({diffText})";
                    }

                    _lastPercent = remaining;
                    _lastDetail = $"xAI Grok CLI (SuperGrok Plus - 실시간 API)\n" +
                                  $"주간 잔여량: {remaining}% (사용률: {Math.Round(usedPercent, 1)}%)\n" +
                                  $"초기화 예정: {resetStr}\n" +
                                  $"계정: {email}\n" +
                                  $"활성 세션: {activeCount}개";

                    return (_lastPercent, _lastDetail);
                }
            }
        }
        catch { }

        // 2. 로컬 로그 캐시 (~/.grok/logs/unified.jsonl) Fallback
        try
        {
            if (File.Exists(_unifiedLogPath))
            {
                using var fs = new FileStream(_unifiedLogPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using var reader = new StreamReader(fs);
                string? line;
                string? latestBillingJson = null;
                while ((line = reader.ReadLine()) != null)
                {
                    if (line.Contains("\"billing: fetched credits config\""))
                    {
                        latestBillingJson = line;
                    }
                }

                if (!string.IsNullOrEmpty(latestBillingJson))
                {
                    using var doc = JsonDocument.Parse(latestBillingJson);
                    if (doc.RootElement.TryGetProperty("ctx", out var ctx) &&
                        ctx.TryGetProperty("config", out var cfg))
                    {
                        double usedPercent = cfg.TryGetProperty("creditUsagePercent", out var cup) ? cup.GetDouble() : 0;
                        int remaining = Math.Clamp((int)Math.Round(100.0 - usedPercent), 0, 100);

                        string resetStr = "N/A";
                        if (cfg.TryGetProperty("currentPeriod", out var period) &&
                            period.TryGetProperty("end", out var endElem) &&
                            DateTime.TryParse(endElem.GetString(), out var dt))
                        {
                            var kst = dt.ToUniversalTime().AddHours(9);
                            var diff = dt.ToUniversalTime() - DateTime.UtcNow;
                            string diffText = diff.TotalDays >= 1
                                ? $"{(int)diff.TotalDays}일 {diff.Hours}시간 뒤"
                                : $"{Math.Max(0, (int)diff.TotalHours)}시간 {diff.Minutes}분 뒤";

                            string datePart = kst.Date == DateTime.Today ? "오늘 " : $"{kst:M월 d일} ";
                            resetStr = $"{datePart}{kst:HH:mm} ({diffText})";
                        }

                        _lastPercent = remaining;
                        _lastDetail = $"xAI Grok CLI (SuperGrok Plus - 로그 캐시)\n" +
                                      $"주간 잔여량: {remaining}% (사용률: {Math.Round(usedPercent, 1)}%)\n" +
                                      $"초기화 예정: {resetStr}\n" +
                                      $"계정: {email}\n" +
                                      $"활성 세션: {activeCount}개";

                        return (_lastPercent, _lastDetail);
                    }
                }
            }
        }
        catch { }

        return (_lastPercent, _lastDetail);
    }
}
