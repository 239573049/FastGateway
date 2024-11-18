using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FastGateway.Service.Infrastructure;

public interface ISystemUsage
{
    float GetCpuUsage();

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

    public float GetCpuUsage()
    {
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
    public float GetCpuUsage()
    {
        var result = ExecuteCommand("mpstat 1 1 | awk '/^Average:/ {print 100 - $NF}'", "/bin/bash");
        if (string.IsNullOrWhiteSpace(result))
        {
            return 0;
        }

        var lines = result.Split('\n');
        if (lines.Length == 0 || !float.TryParse(lines[0].Trim(), out var usage))
        {
            return 0;
        }

        return usage;
    }

    public (float memoryUsage, ulong totalMemory, ulong useMemory) GetMemoryUsage()
    {
        var result = ExecuteCommand("free -m | awk '/Mem:/ {print}'", "/bin/bash");
        var lines = result.Split(" ", StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0 || !ulong.TryParse(lines[1].Trim(), out var total) ||
            !ulong.TryParse(lines[2].Trim(), out var usage))
        {
            return (0, 0, 0);
        }

        return ((float)Math.Round(Math.Round((double)usage / total, 1), 1), total * 1024 * 1024, usage * 1024 * 1024);
    }

    public (float read, float write) GetDiskUsage()
    {
        var result = ExecuteCommand("iostat -dx 1 1", "/bin/bash");
        if (string.IsNullOrWhiteSpace(result))
        {
            return (0, 0);
        }

        var lines = result.Split('\n');

        float read = 0;
        float write = 0;
        foreach (var line in lines)
        {
            if (!string.IsNullOrWhiteSpace(line))
            {
                var data = line.Split(" ", StringSplitOptions.RemoveEmptyEntries);
                if (data.Length > 8)
                {
                    //读
                    if (float.TryParse(data[2], out var readKbs))
                    {
                        read += readKbs;
                    }

                    //写
                    if (float.TryParse(data[8], out var writeKbs))
                    {
                        write += writeKbs;
                    }
                }
            }
        }

        return (read, write);
    }


    private static string ExecuteCommand(string pmCommand, string executor)
    {
        using var process = new Process();
        process.StartInfo = new ProcessStartInfo(executor, string.Empty)
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        process.Start();
        process.StandardInput.WriteLine(pmCommand);
        //process.StandardInput.WriteLine("netstat -an |grep ESTABLISHED |wc -l");
        process.StandardInput.Close();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        return output;
    }
}