using System.ComponentModel.DataAnnotations;
using FastGateway.Infrastructure;
using FastGateway.Options;

namespace FastGateway.Services;

public static class AuthorizationServiceExtensions
{
    public static IEndpointRouteBuilder MapAuthorizationService(this IEndpointRouteBuilder app)
    {
        var routeGroupBuilder = app.MapGroup("/api/v1/authorization")
            .WithTags("授权")
            .WithDescription("授权")
            .AddEndpointFilter<ResultFilter>()
            .WithDisplayName("授权");

        routeGroupBuilder.MapPost(string.Empty, (JwtHelper jwtHelper, string password) =>
        {
            if (password == FastGatewayOptions.Password)
            {
                var token = jwtHelper.CreateToken();

                return token;
            }


            throw new ValidationException("密码错误");
        });

        return app;
    }
}