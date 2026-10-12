using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class GeminiQuotaService
{
    private readonly MonitorConfig _config;
    private int _lastKnownPercent = 88;
    private string _lastKnownDetail = "Antigravity Gemini (Google AI Pro)\n실시간 측정 대기 중...";

    public GeminiQuotaService(MonitorConfig config)
    {
        _config = config;
    }

    public async Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        try
        {
            var live = await AntigravityLanguageServerClient.TryFetchLiveQuotaAsync();
            if (live != null)
            {
                int percent = Math.Clamp((int)Math.Round(live.RemainingFraction * 100.0), 0, 100);
                _lastKnownPercent = percent;

                string resetText = "N/A";
                if (live.ResetTimeUtc.HasValue)
                {
                    var kst = live.ResetTimeUtc.Value.AddHours(9);
                    var diff = live.ResetTimeUtc.Value - DateTime.UtcNow;
                    string diffStr = diff.TotalHours >= 1 
                        ? $"{(int)diff.TotalHours}시간 {diff.Minutes}분 뒤" 
                        : $"{Math.Max(0, diff.Minutes)}분 뒤";

                    resetText = $"{kst:HH:mm} ({diffStr})";
                }

                _lastKnownDetail = $"Antigravity Gemini (Google AI Pro)\n" +
                                   $"실시간 잔여량: {percent}% (5시간 롤링 윈도우)\n" +
                                   $"다음 리셋: {resetText}\n" +
                                   $"모델: {live.ModelLabel}";

                return (percent, _lastKnownDetail);
            }
        }
        catch { }

        return (_lastKnownPercent, _lastKnownDetail);
    }
}
