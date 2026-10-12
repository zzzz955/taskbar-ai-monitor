using System.IO;
using System.Text.Json;

namespace TaskbarAiMonitor.Models;

public class MonitorConfig
{
    public int SystemPollIntervalMs { get; set; } = 1500;
    public int AiPollIntervalSeconds { get; set; } = 300; // 5분 폴링 (서버 과부하 0 + 조기 리셋 감지)
    public bool ShowRemainingQuota { get; set; } = true;
    public bool IsWeeklyMode { get; set; } = true;

    // 주간 및 세션 리셋 시점 (UTC)
    public DateTime? ClaudeResetTimeUtc { get; set; } = new DateTime(2026, 9, 14, 15, 0, 0, DateTimeKind.Utc);
    public DateTime? CodexResetTimeUtc { get; set; } = new DateTime(2026, 9, 20, 14, 41, 0, DateTimeKind.Utc);
    public DateTime? GeminiResetTimeUtc { get; set; } = null;

    // 수동 소진 플래그 (리셋 시점 이전이면 자동으로 0% 처리)
    public bool ClaudeExhausted { get; set; } = false;
    public bool CodexExhausted { get; set; } = false;
    public bool GeminiExhausted { get; set; } = false;

    // 활성화할 에이전트 목록 (null이거나 비어있으면 자동 탐색된 모든 에이전트 노출)
    public List<string>? EnabledAgents { get; set; } = null;

    // 창 위치 및 상태
    public double WindowLeft { get; set; } = -1;
    public double WindowTop { get; set; } = -1;
    public bool IsTopmost { get; set; } = true;
    public bool IsLocked { get; set; } = false;
    public bool SnapToStartButton { get; set; } = false;

    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "TaskbarAiMonitor",
        "config.json"
    );

    public static MonitorConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<MonitorConfig>(json);
                if (config != null)
                {
                    // 리셋 일자가 지났으면 소진 플래그 자동 해제
                    if (config.ClaudeResetTimeUtc.HasValue && DateTime.UtcNow >= config.ClaudeResetTimeUtc.Value)
                    {
                        config.ClaudeExhausted = false;
                    }
                    if (config.CodexResetTimeUtc.HasValue && DateTime.UtcNow >= config.CodexResetTimeUtc.Value)
                    {
                        config.CodexExhausted = false;
                    }
                    if (config.GeminiResetTimeUtc.HasValue && DateTime.UtcNow >= config.GeminiResetTimeUtc.Value)
                    {
                        config.GeminiExhausted = false;
                    }
                    return config;
                }
            }
        }
        catch { }

        var def = new MonitorConfig();
        def.Save();
        return def;
    }

    public void Save()
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var options = new JsonSerializerOptions { WriteIndented = true };
            var json = JsonSerializer.Serialize(this, options);
            File.WriteAllText(ConfigPath, json);
        }
        catch { }
    }

    public static string GetConfigFilePath() => ConfigPath;
}
