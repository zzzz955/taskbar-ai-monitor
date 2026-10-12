using System.Runtime.InteropServices;

namespace TaskbarAiMonitor.Services;

public static class Win32Helper
{
    [StructLayout(LayoutKind.Sequential)]
    public struct FILETIME
    {
        public uint dwLowDateTime;
        public uint dwHighDateTime;

        public ulong ToUInt64() => ((ulong)dwHighDateTime << 32) | dwLowDateTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetSystemTimes(out FILETIME lpIdleTime, out FILETIME lpKernelTime, out FILETIME lpUserTime);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;

        public MEMORYSTATUSEX()
        {
            dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct APPBARDATA
    {
        public uint cbSize;
        public IntPtr hWnd;
        public uint uCallbackMessage;
        public uint uEdge;
        public RECT rc;
        public int lParam;
    }

    public const int GWL_EXSTYLE = -20;
    public const int GWLP_HWNDPARENT = -8;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;

    public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr FindWindow(string lpClassName, string? lpWindowName);

    public static IntPtr GetTaskbarHwnd()
    {
        var h = FindWindow("Shell_TrayWnd", null);
        if (h != IntPtr.Zero) return h;

        try
        {
            var tray = System.Windows.Automation.AutomationElement.RootElement.FindFirst(
                System.Windows.Automation.TreeScope.Children,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ClassNameProperty, "Shell_TrayWnd")
            );
            if (tray != null)
            {
                return new IntPtr(tray.Current.NativeWindowHandle);
            }
        }
        catch { }

        return IntPtr.Zero;
    }

    public static void AttachToTaskbar(IntPtr windowHwnd)
    {
        if (windowHwnd == IntPtr.Zero) return;

        // 1. WS_EX_TOOLWINDOW (Alt+Tab 제외) & WS_EX_NOACTIVATE (포커스 탈취 방지)
        int exStyle = GetWindowLong(windowHwnd, GWL_EXSTYLE);
        SetWindowLong(windowHwnd, GWL_EXSTYLE, exStyle | (int)WS_EX_TOOLWINDOW | (int)WS_EX_NOACTIVATE);

        // 2. 작업표시줄(Shell_TrayWnd)을 Owner로 지정
        // Win32 규칙: 소유된 창(Owned Window)은 Z-order에서 항상 소유자 창보다 위에 유지됨
        IntPtr taskbarHwnd = GetTaskbarHwnd();
        if (taskbarHwnd != IntPtr.Zero)
        {
            SetWindowLongPtr(windowHwnd, GWLP_HWNDPARENT, taskbarHwnd);
        }

        // 3. 최상위 Z-order 설정
        SetWindowPos(windowHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW);
    }

    public static void EnsureTopmost(IntPtr windowHwnd)
    {
        if (windowHwnd != IntPtr.Zero)
        {
            SetWindowPos(windowHwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    public const uint ABM_GETTASKBARPOS = 0x00000005;

    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    public static extern uint SHAppBarMessage(uint dwMessage, ref APPBARDATA pData);

    public static bool TryGetTaskbarRect(out RECT rect)
    {
        var abd = new APPBARDATA
        {
            cbSize = (uint)Marshal.SizeOf(typeof(APPBARDATA))
        };
        uint result = SHAppBarMessage(ABM_GETTASKBARPOS, ref abd);
        if (result != 0)
        {
            rect = abd.rc;
            return true;
        }
        rect = default;
        return false;
    }

    public static bool TryGetStartButtonRect(out System.Windows.Rect rect)
    {
        rect = System.Windows.Rect.Empty;
        try
        {
            var tray = System.Windows.Automation.AutomationElement.RootElement.FindFirst(
                System.Windows.Automation.TreeScope.Children,
                new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.ClassNameProperty, "Shell_TrayWnd")
            );

            if (tray != null)
            {
                var startBtn = tray.FindFirst(
                    System.Windows.Automation.TreeScope.Descendants,
                    new System.Windows.Automation.OrCondition(
                        new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.AutomationIdProperty, "StartButton"),
                        new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "Start"),
                        new System.Windows.Automation.PropertyCondition(System.Windows.Automation.AutomationElement.NameProperty, "시작")
                    )
                );

                if (startBtn != null)
                {
                    rect = startBtn.Current.BoundingRectangle;
                    return true;
                }
            }
        }
        catch { }
        return false;
    }

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(IntPtr pTcpTable, ref int pdwSize, bool bOrder, int ulAf, int tableClass, int reserved);

    public static List<int> GetListeningPortsForPid(int targetPid)
    {
        var ports = new List<int>();
        int size = 0;
        int AF_INET = 2; // IPv4
        int TCP_TABLE_OWNER_PID_ALL = 5;

        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0);
        if (size <= 0) return ports;

        IntPtr buffer = Marshal.AllocHGlobal(size);
        try
        {
            if (GetExtendedTcpTable(buffer, ref size, true, AF_INET, TCP_TABLE_OWNER_PID_ALL, 0) == 0)
            {
                int rowCount = Marshal.ReadInt32(buffer);
                IntPtr rowPtr = IntPtr.Add(buffer, 4);
                int rowSize = 24;

                for (int i = 0; i < rowCount; i++)
                {
                    int state = Marshal.ReadInt32(rowPtr, 0);
                    int localPortRaw = Marshal.ReadInt32(rowPtr, 8);
                    int owningPid = Marshal.ReadInt32(rowPtr, 20);

                    // state 2 = MIB_TCP_STATE_LISTEN
                    if (state == 2 && owningPid == targetPid)
                    {
                        int port = ((localPortRaw & 0xFF) << 8) | ((localPortRaw >> 8) & 0xFF);
                        ports.Add(port);
                    }
                    rowPtr = IntPtr.Add(rowPtr, rowSize);
                }
            }
        }
        catch { }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }

        return ports;
    }
}

