using System.IO;

namespace TaskbarAiMonitor.Services;

public class DetectedAgentInfo
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string IconGlyph { get; set; } = string.Empty;
    public string DefaultColorHex { get; set; } = "#FFFFFF";
    public bool IsInstalled { get; set; }
    public string? CliPath { get; set; }
    public string? ConfigDir { get; set; }
}

public class LlmDetectionService
{
    private static readonly string UserProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private static readonly string AppData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    public List<DetectedAgentInfo> DetectAll()
    {
        var agents = new List<DetectedAgentInfo>
        {
            DetectCodex(),
            DetectClaude(),
            DetectGemini(),
            DetectGrok(),
            DetectCopilot(),
            DetectOllama()
        };

        return agents.Where(a => a.IsInstalled).ToList();
    }

    public DetectedAgentInfo DetectCodex()
    {
        var dir = Path.Combine(UserProfile, ".codex");
        var cli = FindExecutable("codex");
        bool installed = Directory.Exists(dir) || cli != null;

        return new DetectedAgentInfo
        {
            Id = "codex",
            DisplayName = "Codex",
            IconGlyph = "🟢",
            DefaultColorHex = "#10A37F",
            IsInstalled = installed,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    public DetectedAgentInfo DetectClaude()
    {
        var dir = Path.Combine(UserProfile, ".claude");
        var cfgJson = Path.Combine(UserProfile, ".claude.json");
        var cli = FindExecutable("claude");
        bool installed = Directory.Exists(dir) || File.Exists(cfgJson) || cli != null;

        return new DetectedAgentInfo
        {
            Id = "claude",
            DisplayName = "Claude",
            IconGlyph = "🟠",
            DefaultColorHex = "#F97316",
            IsInstalled = installed,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    public DetectedAgentInfo DetectGemini()
    {
        var dir = Path.Combine(UserProfile, ".gemini");
        var cli = FindExecutable("gemini");
        bool installed = Directory.Exists(dir) || cli != null;

        return new DetectedAgentInfo
        {
            Id = "gemini",
            DisplayName = "Gemini",
            IconGlyph = "🔵",
            DefaultColorHex = "#3B82F6",
            IsInstalled = installed,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    public DetectedAgentInfo DetectGrok()
    {
        var dir = Path.Combine(UserProfile, ".grok");
        var bundledExe = Path.Combine(dir, "bin", "grok.exe");
        var cli = File.Exists(bundledExe) ? bundledExe : FindExecutable("grok");
        bool installed = Directory.Exists(dir) || cli != null;

        return new DetectedAgentInfo
        {
            Id = "grok",
            DisplayName = "Grok",
            IconGlyph = "🟣",
            DefaultColorHex = "#A855F7",
            IsInstalled = installed,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    public DetectedAgentInfo DetectCopilot()
    {
        var dir = Path.Combine(UserProfile, ".copilot");
        var cli = FindExecutable("copilot");
        bool installed = Directory.Exists(dir) || cli != null;

        return new DetectedAgentInfo
        {
            Id = "copilot",
            DisplayName = "Copilot",
            IconGlyph = "💠",
            DefaultColorHex = "#06B6D4",
            IsInstalled = installed,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    public DetectedAgentInfo DetectOllama()
    {
        var dir = Path.Combine(LocalAppData, "Programs", "Ollama");
        var cli = FindExecutable("ollama");
        bool hasExe = cli != null || Directory.Exists(dir);

        return new DetectedAgentInfo
        {
            Id = "ollama",
            DisplayName = "Ollama",
            IconGlyph = "🦙",
            DefaultColorHex = "#EC4899",
            IsInstalled = hasExe,
            CliPath = cli,
            ConfigDir = Directory.Exists(dir) ? dir : null
        };
    }

    private static string? FindExecutable(string name)
    {
        // 1. Direct check in npm global prefix
        var npmPath = Path.Combine(AppData, "npm");
        var cmdCandidates = new[]
        {
            Path.Combine(npmPath, $"{name}.cmd"),
            Path.Combine(npmPath, $"{name}.ps1"),
            Path.Combine(npmPath, name)
        };
        foreach (var c in cmdCandidates)
        {
            if (File.Exists(c)) return c;
        }

        // 2. Search PATH
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var extensions = new[] { ".exe", ".cmd", ".bat", "" };
        var dirs = pathEnv.Split(';', StringSplitOptions.RemoveEmptyEntries);

        foreach (var d in dirs)
        {
            try
            {
                if (!Directory.Exists(d)) continue;
                foreach (var ext in extensions)
                {
                    var file = Path.Combine(d, name + ext);
                    if (File.Exists(file)) return file;
                }
            }
            catch { }
        }

        return null;
    }
}
