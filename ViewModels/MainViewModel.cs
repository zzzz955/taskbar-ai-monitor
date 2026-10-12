using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using TaskbarAiMonitor.Models;
using TaskbarAiMonitor.Services;

namespace TaskbarAiMonitor.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly MonitorConfig _config;
    private readonly SystemMetricService _systemService;
    private readonly LlmDetectionService _detectionService;

    private readonly ClaudeQuotaService _claudeService;
    private readonly CodexQuotaService _codexService;
    private readonly GeminiQuotaService _geminiService;
    private readonly GrokQuotaService _grokService;
    private readonly CopilotQuotaService _copilotService;
    private readonly OllamaQuotaService _ollamaService;

    private readonly DispatcherTimer _systemTimer;
    private readonly DispatcherTimer _aiTimer;

    public MonitorConfig Config => _config;
    public string VersionText => BuildInfo.Current.DisplayVersion;

    public MetricItem Cpu { get; }
    public MetricItem Ram { get; }

    public ObservableCollection<MetricItem> AiMetrics { get; } = new();
    public List<DetectedAgentInfo> DetectedAgents { get; private set; } = new();

    private bool _isLocked;
    public bool IsLocked
    {
        get => _isLocked;
        set
        {
            if (SetField(ref _isLocked, value))
            {
                _config.IsLocked = value;
                _config.Save();
            }
        }
    }

    private bool _isTopmost;
    public bool IsTopmost
    {
        get => _isTopmost;
        set
        {
            if (SetField(ref _isTopmost, value))
            {
                _config.IsTopmost = value;
                _config.Save();
            }
        }
    }

    public bool IsAutoStart
    {
        get => AutoStartHelper.IsAutoStartEnabled();
        set
        {
            AutoStartHelper.SetAutoStart(value);
            OnPropertyChanged();
        }
    }

    public bool IsWeeklyMode
    {
        get => _config.IsWeeklyMode;
        set
        {
            if (_config.IsWeeklyMode != value)
            {
                _config.IsWeeklyMode = value;
                _config.Save();
                OnPropertyChanged();
                UpdateAiMetrics();
            }
        }
    }

    public bool CodexExhausted
    {
        get => _config.CodexExhausted;
        set
        {
            if (_config.CodexExhausted != value)
            {
                _config.CodexExhausted = value;
                _config.Save();
                OnPropertyChanged();
                UpdateAiMetrics();
            }
        }
    }

    public bool ClaudeExhausted
    {
        get => _config.ClaudeExhausted;
        set
        {
            if (_config.ClaudeExhausted != value)
            {
                _config.ClaudeExhausted = value;
                _config.Save();
                OnPropertyChanged();
                UpdateAiMetrics();
            }
        }
    }

    public bool GeminiExhausted
    {
        get => _config.GeminiExhausted;
        set
        {
            if (_config.GeminiExhausted != value)
            {
                _config.GeminiExhausted = value;
                _config.Save();
                OnPropertyChanged();
                UpdateAiMetrics();
            }
        }
    }

    public MainViewModel()
    {
        _config = MonitorConfig.Load();
        _isLocked = _config.IsLocked;
        _isTopmost = _config.IsTopmost;

        _systemService = new SystemMetricService();
        _detectionService = new LlmDetectionService();

        _claudeService = new ClaudeQuotaService(_config);
        _codexService = new CodexQuotaService(_config);
        _geminiService = new GeminiQuotaService(_config);
        _grokService = new GrokQuotaService(_config);
        _copilotService = new CopilotQuotaService(_config);
        _ollamaService = new OllamaQuotaService(_config);

        Cpu = new MetricItem
        {
            Id = "cpu",
            Name = "CPU",
            IconGlyph = "⚡",
            IconColorHex = "#00E5FF",
            Percentage = 0,
            Detail = "CPU: 측정 중..."
        };

        Ram = new MetricItem
        {
            Id = "ram",
            Name = "RAM",
            IconGlyph = "💾",
            IconColorHex = "#C084FC",
            Percentage = 0,
            Detail = "RAM: 측정 중..."
        };

        DetectAndInitAiMetrics();

        _systemTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(_config.SystemPollIntervalMs)
        };
        _systemTimer.Tick += (s, e) => UpdateSystemMetrics();

        _aiTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(_config.AiPollIntervalSeconds)
        };
        _aiTimer.Tick += (s, e) => UpdateAiMetrics();

        // 초기 1회 실행
        UpdateSystemMetrics();
        UpdateAiMetrics();

        _systemTimer.Start();
        _aiTimer.Start();
    }

    public void DetectAndInitAiMetrics()
    {
        DetectedAgents = _detectionService.DetectAll();

        // 사용자 필터 적용 (Config.EnabledAgents가 있는 경우)
        var targetAgents = DetectedAgents;
        if (_config.EnabledAgents != null && _config.EnabledAgents.Count > 0)
        {
            targetAgents = DetectedAgents.Where(a => _config.EnabledAgents.Contains(a.Id, StringComparer.OrdinalIgnoreCase)).ToList();
        }

        AiMetrics.Clear();
        foreach (var agent in targetAgents)
        {
            var item = new MetricItem
            {
                Id = agent.Id,
                Name = agent.DisplayName,
                IconGlyph = agent.IconGlyph,
                IconColorHex = agent.DefaultColorHex,
                Percentage = 0,
                Detail = $"{agent.DisplayName}: 탐색됨 (측정 중...)"
            };
            AiMetrics.Add(item);
        }

        OnPropertyChanged(nameof(DetectedAgents));
    }

    public void UpdateSystemMetrics()
    {
        var (cpuPercent, cpuDetail) = _systemService.GetCpuUsage();
        Cpu.Percentage = cpuPercent;
        Cpu.Detail = cpuDetail;
        Cpu.IsWarning = cpuPercent >= 90;

        var (ramPercent, ramDetail) = _systemService.GetRamUsage();
        Ram.Percentage = ramPercent;
        Ram.Detail = ramDetail;
        Ram.IsWarning = ramPercent >= 90;

        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            var win = System.Windows.Application.Current.MainWindow;
            if (win != null)
            {
                var hwnd = new System.Windows.Interop.WindowInteropHelper(win).Handle;
                Win32Helper.EnsureTopmost(hwnd);
            }
        });
    }

    public void UpdateAiMetrics()
    {
        Task.Run(async () =>
        {
            var updateTasks = new List<(string Id, Task<(int Percent, string Detail)> Task)>();

            foreach (var metric in AiMetrics)
            {
                switch (metric.Id.ToLowerInvariant())
                {
                    case "codex":
                        updateTasks.Add(("codex", _codexService.GetQuotaAsync()));
                        break;
                    case "claude":
                        updateTasks.Add(("claude", _claudeService.GetQuotaAsync()));
                        break;
                    case "gemini":
                        updateTasks.Add(("gemini", _geminiService.GetQuotaAsync()));
                        break;
                    case "grok":
                        updateTasks.Add(("grok", _grokService.GetQuotaAsync()));
                        break;
                    case "copilot":
                        updateTasks.Add(("copilot", _copilotService.GetQuotaAsync()));
                        break;
                    case "ollama":
                        updateTasks.Add(("ollama", _ollamaService.GetQuotaAsync()));
                        break;
                }
            }

            var results = new Dictionary<string, (int Percent, string Detail)>();
            foreach (var (id, task) in updateTasks)
            {
                try
                {
                    results[id] = await task;
                }
                catch (Exception ex)
                {
                    results[id] = (0, $"{id}: 오류 ({ex.Message})");
                }
            }

            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
            {
                foreach (var metric in AiMetrics)
                {
                    if (results.TryGetValue(metric.Id.ToLowerInvariant(), out var data))
                    {
                        metric.Percentage = data.Percent;
                        metric.Detail = data.Detail;

                        // 특수 상태 및 소진 처리
                        bool isExhausted = false;
                        if (metric.Id == "codex" && _config.CodexExhausted) isExhausted = true;
                        if (metric.Id == "claude" && _config.ClaudeExhausted) isExhausted = true;
                        if (metric.Id == "gemini" && _config.GeminiExhausted) isExhausted = true;

                        if (isExhausted)
                        {
                            metric.Percentage = 0;
                            metric.IconColorHex = "#EF4444";
                            metric.IsWarning = true;
                        }
                        else
                        {
                            metric.IconColorHex = data.Percent == 0 ? "#EF4444" : GetDefaultColor(metric.Id);
                            metric.IsWarning = _config.ShowRemainingQuota ? (data.Percent <= 10) : (data.Percent >= 90);
                        }
                    }
                }
            });
        });
    }

    private static string GetDefaultColor(string id) => id.ToLowerInvariant() switch
    {
        "codex" => "#10A37F",
        "claude" => "#F97316",
        "gemini" => "#3B82F6",
        "grok" => "#A855F7",
        "copilot" => "#06B6D4",
        "ollama" => "#EC4899",
        _ => "#EEEEF2"
    };

    public void ToggleQuotaMode()
    {
        _config.ShowRemainingQuota = !_config.ShowRemainingQuota;
        _config.Save();
        UpdateAiMetrics();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }
}
