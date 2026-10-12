using System.IO;
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

    public GrokQuotaService(MonitorConfig config)
    {
        _config = config;
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _grokDir = Path.Combine(userProfile, ".grok");
        _authPath = Path.Combine(_grokDir, "auth.json");
        _sessionsPath = Path.Combine(_grokDir, "active_sessions.json");
        _versionPath = Path.Combine(_grokDir, "version.json");
    }

    public Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        try
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

            string? email = null;
            if (File.Exists(_authPath))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(_authPath));
                    foreach (var prop in doc.RootElement.EnumerateObject())
                    {
                        if (prop.Value.TryGetProperty("email", out var em))
                        {
                            email = em.GetString();
                            break;
                        }
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

            if (!string.IsNullOrEmpty(email))
            {
                int percent = 100;
                string detail = $"xAI Grok CLI (v{version})\n" +
                               $"계정: {email}\n" +
                               $"활성 세션: {activeCount}개\n" +
                               $"상태: 인증됨 (정상)";
                return Task.FromResult((percent, detail));
            }
            else
            {
                int percent = 0;
                string detail = $"xAI Grok CLI (v{version})\n" +
                               $"인증 정보 없음 (grok login 필요)";
                return Task.FromResult((percent, detail));
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult((0, $"Grok: 측정 오류 ({ex.Message})"));
        }
    }
}
