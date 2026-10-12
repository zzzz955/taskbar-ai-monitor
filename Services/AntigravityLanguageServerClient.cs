using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TaskbarAiMonitor.Services;

public class AntigravityLanguageServerClient
{
    private static readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(2) };

    public record QuotaResult(double RemainingFraction, DateTime? ResetTimeUtc, string ModelLabel);

    public static async Task<QuotaResult?> TryFetchLiveQuotaAsync()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT ProcessId, CommandLine FROM Win32_Process WHERE Name = 'language_server_windows_x64.exe'"
            );

            foreach (ManagementObject obj in searcher.Get())
            {
                string cmd = obj["CommandLine"]?.ToString() ?? "";
                if (string.IsNullOrEmpty(cmd)) continue;

                var csrfMatch = Regex.Match(cmd, @"--csrf_token\s+([a-zA-Z0-9\-_.]+)");
                var portMatch = Regex.Match(cmd, @"--extension_server_port\s+(\d+)");

                if (!csrfMatch.Success || !portMatch.Success) continue;

                string csrf = csrfMatch.Groups[1].Value;
                int extPort = int.Parse(portMatch.Groups[1].Value);
                int pid = Convert.ToInt32(obj["ProcessId"]);

                // 프로세스가 실제 LISTEN 중인 포트 직접 추출
                var activePorts = Win32Helper.GetListeningPortsForPid(pid);
                var candidatePorts = new HashSet<int>(activePorts)
                {
                    extPort + 4, extPort + 2, extPort + 1, extPort + 3, extPort, extPort + 5
                };

                foreach (int p in candidatePorts)
                {
                    try
                    {
                        var res = await QueryPortAsync(p, csrf);
                        if (res != null) return res;
                    }
                    catch { }
                }
            }
        }
        catch { }

        return null;
    }

    private static async Task<QuotaResult?> QueryPortAsync(int port, string csrf)
    {
        string url = $"http://127.0.0.1:{port}/exa.language_server_pb.LanguageServerService/GetUserStatus";
        string jsonBody = "{\"metadata\":{\"ideName\":\"antigravity\",\"extensionName\":\"antigravity\",\"locale\":\"en\"}}";

        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
        request.Headers.Add("Connect-Protocol-Version", "1");
        request.Headers.Add("X-Codeium-Csrf-Token", csrf);

        using var response = await _httpClient.SendAsync(request);
        if (!response.IsSuccessStatusCode) return null;

        string respString = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(respString);

        if (!doc.RootElement.TryGetProperty("userStatus", out var userStatus)) return null;
        if (!userStatus.TryGetProperty("cascadeModelConfigData", out var cascadeData)) return null;
        if (!cascadeData.TryGetProperty("clientModelConfigs", out var configs) || configs.ValueKind != JsonValueKind.Array) return null;

        foreach (var c in configs.EnumerateArray())
        {
            string label = c.TryGetProperty("label", out var l) ? (l.GetString() ?? "") : "";
            if (label.Contains("Gemini", StringComparison.OrdinalIgnoreCase) &&
                c.TryGetProperty("quotaInfo", out var q))
            {
                double fraction = q.TryGetProperty("remainingFraction", out var rf) ? rf.GetDouble() : 1.0;
                DateTime? resetTime = null;
                if (q.TryGetProperty("resetTime", out var rt) && DateTime.TryParse(rt.GetString(), out var parsedRt))
                {
                    resetTime = parsedRt.ToUniversalTime();
                }

                return new QuotaResult(fraction, resetTime, label);
            }
        }

        return null;
    }
}
