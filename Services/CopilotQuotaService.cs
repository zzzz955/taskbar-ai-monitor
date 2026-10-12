using System.IO;
using System.Text.Json;
using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class CopilotQuotaService
{
    private readonly MonitorConfig _config;
    private readonly string _copilotDir;
    private readonly string _configPath;
    private readonly string _historyPath;

    public CopilotQuotaService(MonitorConfig config)
    {
        _config = config;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _copilotDir = Path.Combine(userProfile, ".copilot");
        _configPath = Path.Combine(_copilotDir, "config.json");
        _historyPath = Path.Combine(_copilotDir, "command-history-state.json");
    }

    public Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        try
        {
            string? loginUser = null;
            if (File.Exists(_configPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(_configPath));
                    if (doc.RootElement.TryGetProperty("lastLoggedInUser", out var u) &&
                        u.TryGetProperty("login", out var logElem))
                    {
                        loginUser = logElem.GetString();
                    }
                }
                catch { }
            }

            int historyCount = 0;
            if (File.Exists(_historyPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(_historyPath));
                    if (doc.RootElement.TryGetProperty("commandHistory", out var hist) &&
                        hist.ValueKind == JsonValueKind.Array)
                    {
                        historyCount = hist.GetArrayLength();
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(loginUser))
            {
                int percent = 100;
                string detail = $"GitHub Copilot CLI\n" +
                               $"계정: @{loginUser} (GitHub)\n" +
                               $"명령 기록: {historyCount}회\n" +
                               $"상태: 로그인됨 (정상)";
                return Task.FromResult((percent, detail));
            }
            else
            {
                int percent = 0;
                string detail = $"GitHub Copilot CLI\n" +
                               $"로그인 정보 없음 (copilot login 필요)";
                return Task.FromResult((percent, detail));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult((0, $"Copilot: 측정 오류 ({ex.Message})"));
        }
    }
}
