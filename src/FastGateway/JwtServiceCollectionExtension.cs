namespace FastGateway;

public static class JwtServiceCollectionExtension
{
    /// <summary>
    ///     注册JWT Bearer认证服务的静态扩展方法
    /// </summary>
    /// <param name="services"></param>
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
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidIssuer = "FastGateway",
                    ValidAudience = "FastGateway",
                };
            });

        return services;
    }

    public static IServiceCollection AddEnvironmentVariable(this IServiceCollection services)
    {
        var secret = Environment.GetEnvironmentVariable("JWT_SECRET", EnvironmentVariableTarget.Machine);
        if (string.IsNullOrWhiteSpace(secret))
        {
            JwtOptions.Secret = Guid.NewGuid().ToString("N");
            Environment.SetEnvironmentVariable("JWT_SECRET", JwtOptions.Secret);
        }
        else
        {
            JwtOptions.Secret = secret;
        }

        return services;
    }
}