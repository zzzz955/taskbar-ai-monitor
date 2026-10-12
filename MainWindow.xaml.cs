using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Input;
using TaskbarAiMonitor.Models;
using TaskbarAiMonitor.Services;
using TaskbarAiMonitor.ViewModels;

namespace TaskbarAiMonitor;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private System.Windows.Forms.NotifyIcon? _trayIcon;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;

        Topmost = _vm.IsTopmost;
        InitTrayIcon();

        // 툴팁 팝업이 작업표시줄 뒤로 가려지지 않도록 Topmost 및 Taskbar 소유권 강제
        EventManager.RegisterClassHandler(
            typeof(System.Windows.Controls.ToolTip), 
            System.Windows.Controls.ToolTip.OpenedEvent, 
            new RoutedEventHandler(OnToolTipOpened)
        );
    }

    private void OnToolTipOpened(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.ToolTip tt)
        {
            var source = (System.Windows.Interop.HwndSource?)PresentationSource.FromVisual(tt);
            if (source?.Handle != null && source.Handle != IntPtr.Zero)
            {
                Win32Helper.AttachToTaskbar(source.Handle);
                Win32Helper.EnsureTopmost(source.Handle);
            }
        }
    }

    private void InitTrayIcon()
    {
        try
        {
            _trayIcon = new System.Windows.Forms.NotifyIcon
            {
                Icon = SystemIcons.Application,
                Text = $"AI & System Monitor ({_vm.VersionText})",
                Visible = true
            };

            var trayMenu = new System.Windows.Forms.ContextMenuStrip();
            trayMenu.Items.Add("위젯 보이기/숨기기", null, (s, e) => ToggleVisibility());
            trayMenu.Items.Add("시작버튼 좌측에 스냅", null, (s, e) => SnapToStartButtonLeft());
            var autoStartItem = new System.Windows.Forms.ToolStripMenuItem("윈도우 시작 시 자동 실행")
            {
                CheckOnClick = true,
                Checked = _vm.IsAutoStart
            };
            autoStartItem.Click += (s, e) => _vm.IsAutoStart = autoStartItem.Checked;
            trayMenu.Items.Add(autoStartItem);
            trayMenu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            trayMenu.Items.Add("설정 파일 열기", null, (s, e) => OpenConfigFile());
            trayMenu.Items.Add("업데이트 / 설치 적용", null, (s, e) => RunApplyScript());
            trayMenu.Items.Add("종료", null, (s, e) => Close());

            _trayIcon.ContextMenuStrip = trayMenu;
            _trayIcon.DoubleClick += (s, e) => ToggleVisibility();
        }
        catch { }
    }

    private void ToggleVisibility()
    {
        if (Visibility == Visibility.Visible)
        {
            Hide();
        }
        else
        {
            Show();
            Activate();
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        Win32Helper.AttachToTaskbar(hwnd);
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        Win32Helper.EnsureTopmost(hwnd);
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        Win32Helper.AttachToTaskbar(hwnd);

        // 마지막 저장된 위치가 있고, 시작버튼 강제 스냅 모드가 아닌 경우 저장된 위치 복원
        if (!_vm.Config.SnapToStartButton && _vm.Config.WindowLeft >= 0 && _vm.Config.WindowTop >= 0)
        {
            double left = _vm.Config.WindowLeft;
            double top = _vm.Config.WindowTop;
            double vsLeft = SystemParameters.VirtualScreenLeft;
            double vsTop = SystemParameters.VirtualScreenTop;
            double vsWidth = SystemParameters.VirtualScreenWidth;
            double vsHeight = SystemParameters.VirtualScreenHeight;

            // 저장된 위치가 가상 화면 영역 내에 유효한지 검증
            if (left >= vsLeft - 40 && left <= (vsLeft + vsWidth - 40) &&
                top >= vsTop - 40 && top <= (vsTop + vsHeight - 30))
            {
                Left = left;
                Top = top;
                return;
            }
        }

        // 저장된 좌표가 없거나 스냅 모드인 경우 시작버튼 좌측에 배치
        Dispatcher.BeginInvoke(new Action(SnapToStartButtonLeft), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void SnapToStartButtonLeft()
    {
        double dpiScaleX = 1.0;
        double dpiScaleY = 1.0;
        var source = PresentationSource.FromVisual(this);
        if (source?.CompositionTarget != null)
        {
            dpiScaleX = source.CompositionTarget.TransformToDevice.M11;
            dpiScaleY = source.CompositionTarget.TransformToDevice.M22;
        }

        double taskbarTop = 0;
        double taskbarHeight = 48;

        if (Win32Helper.TryGetTaskbarRect(out var tbRect))
        {
            taskbarTop = tbRect.Top / dpiScaleY;
            taskbarHeight = (tbRect.Bottom - tbRect.Top) / dpiScaleY;
        }
        else
        {
            taskbarHeight = SystemParameters.PrimaryScreenHeight - SystemParameters.WorkArea.Bottom;
            taskbarTop = SystemParameters.WorkArea.Bottom;
        }

        // 작업표시줄 높이와 동기화
        Height = Math.Max(32, taskbarHeight);
        Top = taskbarTop;

        double currentWidth = ActualWidth > 0 ? ActualWidth : 360;

        // 시작버튼 위치 탐색
        if (Win32Helper.TryGetStartButtonRect(out var startRect))
        {
            double startLeftDip = startRect.Left / dpiScaleX;
            // 시작버튼이 화면 맨 왼쪽에 붙어있는 경우(좌측 정렬) vs 중앙 정렬 구분
            if (startLeftDip > currentWidth + 20)
            {
                // 중앙 정렬: 시작버튼 바로 좌측에 8px 여백 두고 배치
                Left = startLeftDip - currentWidth - 8;
            }
            else
            {
                // 좌측 정렬인 경우: 시작버튼 우측 아이콘들과 겹치지 않게 시작버튼 우측이나 트레이 좌측 배치
                Left = Math.Max(8, startLeftDip - currentWidth - 4);
            }
        }
        else
        {
            // 시작버튼 못 찾은 경우: Windows 11 기본 중앙 시작버튼 좌측 추정 배치
            double screenWidth = SystemParameters.PrimaryScreenWidth;
            Left = Math.Max(10, (screenWidth / 2.0) - currentWidth - 120);
        }

        _vm.Config.SnapToStartButton = true;
        SavePosition();
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!_vm.IsLocked && e.ButtonState == MouseButtonState.Pressed)
        {
            DragMove();
            _vm.Config.SnapToStartButton = false;
            SavePosition();
        }
    }

    private void Window_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        SnapToStartButtonLeft();
    }

    private void SavePosition()
    {
        _vm.Config.WindowLeft = Left;
        _vm.Config.WindowTop = Top;
        _vm.Config.Save();
    }

    private void MenuRefresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.UpdateSystemMetrics();
        _vm.UpdateAiMetrics();
    }

    private void MenuToggleMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.ToggleQuotaMode();
    }

    private void MenuWeeklyMode_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsWeeklyMode = !_vm.IsWeeklyMode;
    }

    private void MenuRedetect_Click(object sender, RoutedEventArgs e)
    {
        _vm.DetectAndInitAiMetrics();
        _vm.UpdateAiMetrics();
    }

    private void MenuToggleCodexExhausted_Click(object sender, RoutedEventArgs e)
    {
        _vm.CodexExhausted = !_vm.CodexExhausted;
    }

    private void MenuToggleClaudeExhausted_Click(object sender, RoutedEventArgs e)
    {
        _vm.ClaudeExhausted = !_vm.ClaudeExhausted;
    }

    private void MenuToggleGeminiExhausted_Click(object sender, RoutedEventArgs e)
    {
        _vm.GeminiExhausted = !_vm.GeminiExhausted;
    }

    private void MenuSnapStartButton_Click(object sender, RoutedEventArgs e)
    {
        SnapToStartButtonLeft();
    }

    private void MenuTopmost_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsTopmost = !_vm.IsTopmost;
        Topmost = _vm.IsTopmost;
    }

    private void MenuLock_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsLocked = !_vm.IsLocked;
    }

    private void MenuAutoStart_Click(object sender, RoutedEventArgs e)
    {
        _vm.IsAutoStart = !_vm.IsAutoStart;
    }

    private void MenuOpenConfig_Click(object sender, RoutedEventArgs e)
    {
        OpenConfigFile();
    }

    private void MenuApplyUpdate_Click(object sender, RoutedEventArgs e)
    {
        RunApplyScript();
    }

    private void RunApplyScript()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var scriptPath = Path.Combine(baseDir, "apply.bat");
            if (!File.Exists(scriptPath))
            {
                var dir = new DirectoryInfo(baseDir);
                while (dir != null)
                {
                    var cand = Path.Combine(dir.FullName, "apply.bat");
                    if (File.Exists(cand))
                    {
                        scriptPath = cand;
                        break;
                    }
                    dir = dir.Parent;
                }
            }

            if (File.Exists(scriptPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(scriptPath)
                });
            }
            else
            {
                System.Windows.MessageBox.Show("apply.bat 파일을 찾을 수 없습니다.", "Taskbar AI Monitor");
            }
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"업데이트 스크립트 실행 오류: {ex.Message}", "Taskbar AI Monitor");
        }
    }

    private void OpenConfigFile()
    {
        try
        {
            var path = MonitorConfig.GetConfigFilePath();
            if (!File.Exists(path))
            {
                _vm.Config.Save();
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = "notepad.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        SavePosition();
        if (_trayIcon != null)
        {
            _trayIcon.Visible = false;
            _trayIcon.Dispose();
            _trayIcon = null;
        }
    }
}