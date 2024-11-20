using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FastGateway.Service.Infrastructure;

public interface ISystemUsage
{
    Task<float> GetCpuUsage();

    (float memoryUsage, ulong totalMemory, ulong useMemory) GetMemoryUsage();

    (float read, float write) GetDiskUsage();
}

public static class SystemUsageExtensions
{
    public static IServiceCollection AddSystemUsage(this IServiceCollection services)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            services.AddSingleton<ISystemUsage, WindowsSystemUsage>();
        }
        else
        {
            services.AddSingleton<ISystemUsage, LinuxSystemUsage>();
        }

        return services;
    }
}

public class WindowsSystemUsage : ISystemUsage
{
    private PerformanceCounter cpuCounter;
    private readonly PerformanceCounter _ioReadCounter;
    private readonly PerformanceCounter _ioWriteCounter;

    public WindowsSystemUsage()
    {
        cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        _ioReadCounter = new("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        _ioWriteCounter = new("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
    }

    public async Task<float> GetCpuUsage()
    {
        await Task.Delay(1000);
        return cpuCounter.NextValue();
    }

    /// <summary>
    /// 获取内存使用率
    /// </summary>
    /// <returns></returns>
    /// <returns>(内存占用率，总计内存大小，已用内存大小)</returns>
    public (float memoryUsage, ulong totalMemory, ulong useMemory) GetMemoryUsage()
    {
        var memoryInfo = GetMemoryInfo();
        return (memoryInfo.DWMemoryLoad, memoryInfo.ullTotalPhys, memoryInfo.ullTotalPhys - memoryInfo.ullAvailPhys);
    }

    //定义内存的信息结构
    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORY_INFO
    {
        public uint DWLength; //当前结构体大小
        public uint DWMemoryLoad; //当前内存使用率
        public ulong ullTotalPhys; //总计物理内存大小
        public ulong ullAvailPhys; //可用物理内存大小
        public ulong ullTotalPagefile; //总计交换文件大小
        public ulong ullAvailPagefile; //总计交换文件大小
        public ulong ullTotalVirtual; //总计虚拟内存大小
        public ulong ullAvailVirtual; //可用虚拟内存大小
        public ulong ullAvailExtendedVirtual; //保留 这个值始终为0
    }

    [DllImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORY_INFO mi);

    private MEMORY_INFO GetMemoryInfo()
    {
        MEMORY_INFO MemoryInfo = new MEMORY_INFO();
        MemoryInfo.DWLength = (uint)Marshal.SizeOf(MemoryInfo);
        GlobalMemoryStatusEx(ref MemoryInfo);
        return MemoryInfo;
    }

    /// <summary>
    /// 获取IO占用情况(B/s)
    /// </summary>
    /// <returns></returns>
    public (float read, float write) GetDiskUsage()
    {
        return (_ioReadCounter.NextValue(), _ioWriteCounter.NextValue());
    }
}

public class LinuxSystemUsage : ISystemUsage
{
    public async Task<float> GetCpuUsage()
    {
        var cpuInfo1 = GetCpuTimes();
        await Task.Delay(1000);
        var cpuInfo2 = GetCpuTimes();

        var idleTime = cpuInfo2.idle - cpuInfo1.idle;
        var totalTime = cpuInfo2.total - cpuInfo1.total;

        return (1.0f - (float)idleTime / totalTime) * 100;
    }

    private (ulong idle, ulong total) GetCpuTimes()
    {
        var cpuInfo = ExecuteCommand("cat /proc/stat | grep 'cpu '", "/bin/bash");
        var parts = cpuInfo.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var user = ulong.Parse(parts[1]);
        var nice = ulong.Parse(parts[2]);
        var system = ulong.Parse(parts[3]);
        var idle = ulong.Parse(parts[4]);
        var iowait = ulong.Parse(parts[5]);
        var irq = ulong.Parse(parts[6]);
        var softirq = ulong.Parse(parts[7]);
        var steal = ulong.Parse(parts[8]);

        var total = user + nice + system + idle + iowait + irq + softirq + steal;

        return (idle, total);
    }

    public (float memoryUsage, ulong totalMemory, ulong useMemory) GetMemoryUsage()
	{
		var memInfo = ExecuteCommand("cat /proc/meminfo", "/bin/bash");
		var lines = memInfo.Split('\n');
		ulong totalMemory = 0;
		ulong availableMemory = 0;

		foreach (var line in lines)
		{
			var parts = line.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length < 2) continue;

			var key = parts[0].Trim();
			var value = ulong.Parse(parts[1].Trim().Split(' ')[0]);

			switch (key)
			{
				case "MemTotal":
					totalMemory = value;
					break;
				case "MemAvailable":
					availableMemory = value;
					break;
			}
		}

		// 默认单位是KB，需要转换byte
		totalMemory *= 1024;
		availableMemory *= 1024;

		ulong usedMemory = totalMemory - availableMemory;
		float memoryUsage = (float)usedMemory / totalMemory * 100;


		return (memoryUsage, totalMemory, usedMemory);
	}

    public (float read, float write) GetDiskUsage()
    {
        var diskStats = ExecuteCommand("cat /proc/diskstats", "/bin/bash");
        var lines = diskStats.Split('\n');
        float readBytes = 0;
        float writeBytes = 0;

        foreach (var line in lines)
        {
            var parts = line.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > 13)
            {
                readBytes += float.Parse(parts[5]) * 512 / 1024; // sectors read * 512 bytes per sector to KB
                writeBytes += float.Parse(parts[9]) * 512 / 1024; // sectors written * 512 bytes per sector to KB
            }
        }

        return (readBytes, writeBytes);
    }

    private static string ExecuteCommand(string pmCommand, string executor)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executor, "-c \"" + pmCommand + "\"")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}