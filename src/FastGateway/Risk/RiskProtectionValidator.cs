using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using Core.Entities;

namespace FastGateway.Risk;

public static partial class RiskProtectionValidator
{
    private const int MaxLoginEndpoints = 20;
    private static readonly string[] AllowedMethods = ["POST", "PUT", "PATCH", "GET"];

    /// <summary>
    ///     校验并规范化路由的登录风控配置，非法时抛出 <see cref="ValidationException" />
    /// </summary>
    public static void Normalize(DomainName domainName)
    {
        var protection = domainName.RiskProtection;
        if (protection == null) return;

        protection.LoginEndpoints ??= new List<LoginEndpointRule>();
        if (protection.LoginEndpoints.Count > MaxLoginEndpoints)
            throw new ValidationException($"登录接口最多配置 {MaxLoginEndpoints} 个");
        if (!Enum.IsDefined(protection.BlockScope))
            throw new ValidationException("不支持的封禁范围");

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in protection.LoginEndpoints)
        {
            rule.Method = rule.Method?.Trim().ToUpperInvariant() ?? string.Empty;
            if (!AllowedMethods.Contains(rule.Method))
                throw new ValidationException($"登录接口方法仅支持 {string.Join("、", AllowedMethods)}");

            rule.Path = rule.Path?.Trim() ?? string.Empty;
            if (!rule.Path.StartsWith('/') || rule.Path.Length > 512 || rule.Path.Contains('?'))
                throw new ValidationException("登录接口路径必须以 / 开头，且不能包含查询字符串");

            if (!seen.Add($"{rule.Method} {CompiledRiskProtection.NormalizePath(rule.Path)}"))
                throw new ValidationException($"登录接口重复：{rule.Method} {rule.Path}");

            rule.UsernameField = string.IsNullOrWhiteSpace(rule.UsernameField) ? null : rule.UsernameField.Trim();
            if (rule.UsernameField is { Length: > 64 } || rule.UsernameField?.Any(char.IsWhiteSpace) == true)
                throw new ValidationException("用户名字段不能包含空白，且不超过 64 个字符");

            rule.FailureStatusCodes = (rule.FailureStatusCodes ?? []).Distinct().ToArray();
            if (rule.FailureStatusCodes.Length > 10 || rule.FailureStatusCodes.Any(x => x is < 100 or > 599))
                throw new ValidationException("失败状态码需在 100-599 之间，最多 10 个");

            rule.ResultHeader = string.IsNullOrWhiteSpace(rule.ResultHeader) ? null : rule.ResultHeader.Trim();
            if (rule.ResultHeader != null && !HeaderNamePattern().IsMatch(rule.ResultHeader))
                throw new ValidationException("结果响应头名称只能包含字母、数字和连字符");

            if (rule.FailureStatusCodes.Length == 0 && rule.ResultHeader == null)
                throw new ValidationException($"登录接口 {rule.Path} 需要配置失败状态码或结果响应头，否则无法识别登录失败");
        }

        if (protection.Enable && protection.LoginEndpoints.Count == 0)
            throw new ValidationException("启用登录保护时至少需要配置一个登录接口");
    }

    [GeneratedRegex("^[A-Za-z0-9-]{1,64}$")]
    private static partial Regex HeaderNamePattern();
}
