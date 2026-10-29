using System.Security.Cryptography;

namespace FastGateway.Infrastructure;

/// <summary>
///     未显式配置的机密（JWT 密钥、初始管理员密码）首次启动时随机生成并持久化到 data 目录，
///     避免所有安装共用仓库里的默认值。data 目录随容器卷保存，重启后保持不变。
/// </summary>
public static class LocalSecretStore
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789";

    public static string DataDirectory => Path.Combine(AppContext.BaseDirectory, "data");

    /// <returns>(值, 是否本次新生成)</returns>
    public static (string Value, bool Created) GetOrCreate(string fileName, int length)
    {
        var path = Path.Combine(DataDirectory, fileName);
        if (File.Exists(path))
        {
            var existing = File.ReadAllText(path).Trim();
            if (existing.Length >= length) return (existing, false);
        }

        Directory.CreateDirectory(DataDirectory);
        var value = RandomNumberGenerator.GetString(Alphabet, length);
        File.WriteAllText(path, value);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        return (value, true);
    }
}
