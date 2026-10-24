using MessagePack;

namespace Core.Entities;

/// <summary>
/// 路由级登录风控配置
/// </summary>
[MessagePackObject(true)]
public sealed class RiskProtection
{
    /// <summary>
    /// 是否启用
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// 被封禁的来源受影响的范围
    /// </summary>
    public RiskBlockScope BlockScope { get; set; } = RiskBlockScope.LoginOnly;

    /// <summary>
    /// 需要保护的登录接口
    /// </summary>
    public List<LoginEndpointRule> LoginEndpoints { get; set; } = new();
}

public enum RiskBlockScope
{
    /// <summary>只拦截对登录接口的请求</summary>
    LoginOnly = 0,

    /// <summary>拦截对该路由的全部请求</summary>
    WholeRoute = 1
}

/// <summary>
/// 登录接口规则：网关据此识别登录请求，并从上游响应判断登录成败
/// </summary>
[MessagePackObject(true)]
public sealed class LoginEndpointRule
{
    public string Method { get; set; } = "POST";

    /// <summary>
    /// 访问者请求的完整路径（不含查询字符串），忽略大小写与末尾斜杠
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 请求体中用户名所在的字段（JSON 顶层字段或表单字段），为空则不按账号统计
    /// </summary>
    public string? UsernameField { get; set; }

    /// <summary>
    /// 视为登录失败的上游状态码
    /// </summary>
    public int[] FailureStatusCodes { get; set; } = [401, 403];

    /// <summary>
    /// 上游声明登录结果的响应头（值为 fail / success），配置后优先于状态码，且不会返回给客户端
    /// </summary>
    public string? ResultHeader { get; set; }
}
