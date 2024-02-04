using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Microsoft.Extensions.DependencyInjection;

public static class JwtServiceCollectionExtension
{
    /// <summary>
    ///     注册JWT Bearer认证服务的静态扩展方法
    /// </summary>
    /// <param name="services"></param>
    /// <param name="policies">路由信息</param>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services,
        List<RouteEntity>? policies = null)
    {
        //使用应用密钥得到一个加密密钥字节数组
        var authenticationBuilder = services.AddAuthentication(x =>
        {
            x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        });

        authenticationBuilder.AddCookie(cfg => cfg.SlidingExpiration = true)
            .AddJwtBearer(x =>
            {
                x.RequireHttpsMetadata = true;
                x.SaveToken = true;
                x.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(JwtOptions.Secret)),
                    ValidateIssuer = false,
                    ValidateAudience = false
                };
            });

        // 添加认证信息
        if (policies == null) return services;
        foreach (var policy in policies)
            authenticationBuilder.AddJwtBearer(policy.AuthorizationPolicy!, options =>
            {
                options.Authority = policy.AuthorizationPolicyAddress;
                options.RequireHttpsMetadata = policy.RequireHttpsMetadata ?? true;
                options.Audience = policy.AuthorizationPolicy!;
            });
        return services;
    }

    /// <summary>
    ///     使用自定义授权中间件
    /// </summary>
    /// <param name="app">应用程序生成器接口</param>
    /// <returns>应用程序生成器接口</returns>
    public static IApplicationBuilder UseCustomAuthentication(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<CustomAuthenticationMiddleware>();
    }
}