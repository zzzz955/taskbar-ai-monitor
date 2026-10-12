using System.IO;
using System.Net.Http;
using System.Text.Json;
using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class ClaudeQuotaService
{
    private readonly MonitorConfig _config;
    private readonly string _credentialsPath;
    private readonly string _claudeJsonPath;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(5) };

    private int _lastPercent = 0;
    private string _lastDetail = "Claude: 측정 대기 중...";

    public ClaudeQuotaService(MonitorConfig config)
    {
        _config = config;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _credentialsPath = Path.Combine(userProfile, ".claude", ".credentials.json");
        _claudeJsonPath = Path.Combine(userProfile, ".claude.json");
    }

    public async Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        // 1. 실시간 Anthropic 공식 API 직접 호출 (중간 조기 리셋 및 실시간 쿼터 즉시 반영)
        try
        {
            if (File.Exists(_credentialsPath))
            {
                var credJson = await File.ReadAllTextAsync(_credentialsPath);
                using var credDoc = JsonDocument.Parse(credJson);
                if (credDoc.RootElement.TryGetProperty("claudeAiOauth", out var oauth) &&
                    oauth.TryGetProperty("accessToken", out var tokenElem))
                {
                    string? token = tokenElem.GetString();
                    if (!string.IsNullOrEmpty(token))
                    {
                        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
                        req.Headers.Add("Authorization", $"Bearer {token}");
                        req.Headers.Add("User-Agent", "claude-code/0.2.29");

                        using var resp = await _httpClient.SendAsync(req);
                        if (resp.IsSuccessStatusCode)
                        {
                            var respJson = await resp.Content.ReadAsStringAsync();
                            using var doc = JsonDocument.Parse(respJson);
                            var root = doc.RootElement;

                            if (root.TryGetProperty("seven_day", out var sevenDay) && sevenDay.ValueKind == JsonValueKind.Object)
                            {
                                double sevenDayUtil = sevenDay.TryGetProperty("utilization", out var u) ? u.GetDouble() : 0;
                                int usedPercent = (int)Math.Round(sevenDayUtil);
                                int remaining = Math.Clamp(100 - usedPercent, 0, 100);

                                string resetStr = "N/A";
                                if (sevenDay.TryGetProperty("resets_at", out var r) && DateTime.TryParse(r.GetString(), out var dt))
                                {
                                    var kst = dt.ToUniversalTime().AddHours(9);
                                    var diff = dt.ToUniversalTime() - DateTime.UtcNow;
                                    string diffText = diff.TotalDays >= 1
                                        ? $"{(int)diff.TotalDays}일 {diff.Hours}시간 뒤"
                                        : $"{Math.Max(0, (int)diff.TotalHours)}시간 {diff.Minutes}분 뒤";

                                    string datePart = kst.Date == DateTime.Today ? "오늘 " : $"{kst:M월 d일} ";
                                    resetStr = $"{datePart}{kst:HH:mm} ({diffText})";
                                }

                                int fiveHourUtil = 0;
                                if (root.TryGetProperty("five_hour", out var fiveHour) && fiveHour.ValueKind == JsonValueKind.Object)
                                {
                                    if (fiveHour.TryGetProperty("utilization", out var fu)) fiveHourUtil = (int)Math.Round(fu.GetDouble());
                                }

                                _lastPercent = remaining;
                                _lastDetail = $"Claude Code (Max 플랜 - 실시간 API)\n" +
                                              $"주간 잔여량: {remaining}% (사용률: {usedPercent}%)\n" +
                                              $"초기화 예정: {resetStr}\n" +
                                              $"현재 5시간 세션: {fiveHourUtil}% 사용 중";

                                return (_lastPercent, _lastDetail);
                            }
                        }
                    }
                }
            }
        }
        catch { }

        // 2. Fallback: 로컬 ~/.claude.json 파일 캐시 읽기
        return GetQuotaFromLocalFile();
    }

    private (int Percent, string Detail) GetQuotaFromLocalFile()
    {
        try
        {
            if (File.Exists(_claudeJsonPath))
            {
                var json = File.ReadAllText(_claudeJsonPath);
                using var doc = JsonDocument.Parse(json);

                if (doc.RootElement.TryGetProperty("cachedUsageUtilization", out var cached) &&
                    cached.TryGetProperty("utilization", out var util))
                {
                    int sevenDayUtil = 0;
                    DateTime? sevenDayReset = null;

                    if (util.TryGetProperty("seven_day", out var sevenDay) && sevenDay.ValueKind == JsonValueKind.Object)
                    {
                        if (sevenDay.TryGetProperty("utilization", out var u)) sevenDayUtil = u.GetInt32();
                        if (sevenDay.TryGetProperty("resets_at", out var r) && DateTime.TryParse(r.GetString(), out var dt))
                        {
                            sevenDayReset = dt.ToUniversalTime();
                        }
                    }

                    int fiveHourUtil = 0;
                    if (util.TryGetProperty("five_hour", out var fiveHour) && fiveHour.ValueKind == JsonValueKind.Object)
                    {
                        if (fiveHour.TryGetProperty("utilization", out var fu)) fiveHourUtil = fu.GetInt32();
                    }

                    // 초기화 시점이 지났는지 검사
                    if (sevenDayReset.HasValue && DateTime.UtcNow >= sevenDayReset.Value)
                    {
                        _lastPercent = 100;
                        _lastDetail = "Claude Code (Max)\n주간 사용량 초기화 완료 (100% 잔여)";
                        return (_lastPercent, _lastDetail);
                    }

                    int remaining = Math.Clamp(100 - sevenDayUtil, 0, 100);

                    string resetStr = "N/A";
                    if (sevenDayReset.HasValue)
                    {
                        var kst = sevenDayReset.Value.AddHours(9);
                        var diff = sevenDayReset.Value - DateTime.UtcNow;

                        string diffText = diff.TotalHours >= 1
                            ? $"{(int)diff.TotalHours}시간 {diff.Minutes}분 뒤"
                            : $"{Math.Max(0, diff.Minutes)}분 뒤";

                        string datePart = kst.Date == DateTime.Today.AddDays(1) ? "오늘 자정 00:00" : $"{kst:M월 d일 HH:mm}";
                        resetStr = $"{datePart} ({diffText})";
                    }

                    _lastPercent = remaining;
                    _lastDetail = $"Claude Code (Max 플랜 - 로컬 캐시)\n" +
                                  $"주간 잔여량: {remaining}% (사용률: {sevenDayUtil}%)\n" +
                                  $"초기화 예정: {resetStr}\n" +
                                  $"현재 5시간 세션: {fiveHourUtil}% 사용 중";

                    return (_lastPercent, _lastDetail);
                }
            }
        }
        catch { }

        return (_lastPercent, _lastDetail);
    }
}
