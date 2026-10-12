using System.Net.Http;
using System.Text.Json;
using TaskbarAiMonitor.Models;

namespace TaskbarAiMonitor.Services;

public class OllamaQuotaService
{
    private readonly MonitorConfig _config;
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    public OllamaQuotaService(MonitorConfig config)
    {
        _config = config;
    }

    public async Task<(int Percent, string Detail)> GetQuotaAsync()
    {
        try
        {
            var resp = await _httpClient.GetAsync("http://localhost:11434/api/tags");
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                int modelCount = 0;
                var modelNames = new List<string>();

                if (doc.RootElement.TryGetProperty("models", out var models) && models.ValueKind == JsonValueKind.Array)
                {
                    modelCount = models.GetArrayLength();
                    foreach (var m in models.EnumerateArray())
                    {
                        if (m.TryGetProperty("name", out var n))
                        {
                            modelNames.Add(n.GetString() ?? "");
                        }
                    }
                }

                string detail = $"Ollama 로컬 LLM (실행 중)\n" +
                               $"로컬 모델: {modelCount}개\n" +
                               $"목록: {(modelNames.Count > 0 ? string.Join(", ", modelNames.Take(3)) : "없음")}\n" +
                               $"엔드포인트: localhost:11434";

                return (100, detail);
            }
        }
        catch { }

        return (0, "Ollama 로컬 LLM\n서비스 미실행 (ollama serve 필요)");
    }
}
