namespace Gateway.Helpers;

public static class StringHelper
{
    /// <summary>
    /// 字节转换为可读的字符串
    /// </summary>
    /// <param name="bytes"></param>
    /// <returns></returns>
    public static string FormatBytes(long bytes)
    {
        const int scale = 1024;
        string[] units = ["B", "KB", "MB", "GB", "TB", "PB", "EB"]; 

        double formattedSize = bytes;
        var unitIndex = 0;

        while (formattedSize >= scale && unitIndex < units.Length - 1)
        {
            formattedSize /= scale;
            unitIndex++;
        }

        return $"{formattedSize:0.##} {units[unitIndex]}";
    }
}