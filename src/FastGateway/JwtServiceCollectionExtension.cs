using System.Text;
using FastGateway.Options;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace FastGateway;

public static class JwtServiceCollectionExtension
{
    /// <summary>
    ///     注册JWT Bearer认证服务的静态扩展方法
    /// </summary>
    /// <param name="services"></param>
    /// <param name="policies">路由信息</param>
    public static IServiceCollection AddJwtBearerAuthentication(this IServiceCollection services)
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

        return services;
    }
}