namespace TaskbarAiMonitor.Services;

public class SystemMetricService
{
    private ulong _prevIdleTime;
    private ulong _prevKernelTime;
    private ulong _prevUserTime;
    private bool _hasInitialized = false;

    public (int CpuPercent, string CpuDetail) GetCpuUsage()
    {
        if (!Win32Helper.GetSystemTimes(out var idleTime, out var kernelTime, out var userTime))
        {
            return (0, "CPU: 측정 실패");
        }

        ulong idle = idleTime.ToUInt64();
        ulong kernel = kernelTime.ToUInt64();
        ulong user = userTime.ToUInt64();

        if (!_hasInitialized)
        {
            _prevIdleTime = idle;
            _prevKernelTime = kernel;
            _prevUserTime = user;
            _hasInitialized = true;
            return (0, "CPU: 초기화 중...");
        }

        ulong diffIdle = idle - _prevIdleTime;
        ulong diffKernel = kernel - _prevKernelTime;
        ulong diffUser = user - _prevUserTime;

        _prevIdleTime = idle;
        _prevKernelTime = kernel;
        _prevUserTime = user;

        // kernel time includes idle time
        ulong totalSystem = diffKernel + diffUser;
        if (totalSystem == 0)
        {
            return (0, "CPU: 0%");
        }

        double usage = (1.0 - ((double)diffIdle / totalSystem)) * 100.0;
        int percent = Math.Clamp((int)Math.Round(usage), 0, 100);

        string detail = $"CPU 점유율: {percent}%\n논리 코어: {Environment.ProcessorCount}개";
        return (percent, detail);
    }

    public (int RamPercent, string RamDetail) GetRamUsage()
    {
        var memStatus = new Win32Helper.MEMORYSTATUSEX();
        if (!Win32Helper.GlobalMemoryStatusEx(memStatus))
        {
            return (0, "RAM: 측정 실패");
        }

        int percent = Math.Clamp((int)memStatus.dwMemoryLoad, 0, 100);
        double totalGb = memStatus.ullTotalPhys / (1024.0 * 1024.0 * 1024.0);
        double usedGb = (memStatus.ullTotalPhys - memStatus.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0);

        string detail = $"RAM 사용량: {percent}%\n{usedGb:F1} GB / {totalGb:F1} GB 사용 중";
        return (percent, detail);
    }
}
