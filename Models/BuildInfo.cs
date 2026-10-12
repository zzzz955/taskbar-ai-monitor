using System.IO;
using System.Reflection;
using System.Text.Json;

namespace TaskbarAiMonitor.Models;

public class BuildInfo
{
    public string Version { get; set; } = "1.0.0";
    public string SourceCommit { get; set; } = "local";
    public string BuiltAt { get; set; } = "";
    public string Runtime { get; set; } = "win-x64";

    private static BuildInfo? _current;
    public static BuildInfo Current => _current ??= Load();

    public string ShortCommit => SourceCommit.Length > 7 ? SourceCommit[..7] : SourceCommit;
    public string DisplayVersion => $"v{Version} ({ShortCommit})";

    private static BuildInfo Load()
    {
        try
        {
            var exeDir = AppContext.BaseDirectory;
            var buildJsonPath = Path.Combine(exeDir, "build.json");
            if (File.Exists(buildJsonPath))
            {
                var json = File.ReadAllText(buildJsonPath);
                var info = JsonSerializer.Deserialize<BuildInfo>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (info != null) return info;
            }
        }
        catch { }

        // Fallback to AssemblyInformationalVersion if available
        var attr = typeof(BuildInfo).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
        var commit = attr?.InformationalVersion ?? "dev";
        if (commit.Contains('+'))
        {
            var parts = commit.Split('+');
            return new BuildInfo
            {
                Version = parts[0],
                SourceCommit = parts.Length > 1 ? parts[1] : "local"
            };
        }

        return new BuildInfo();
    }
}
