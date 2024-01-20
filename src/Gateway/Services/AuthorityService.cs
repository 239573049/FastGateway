using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace Gateway.Services;

public class AuthorityService
{
    /// <summary>
    /// 获取token
    /// </summary>
    /// <param name="username"></param>
    /// <param name="password"></param>
    /// <returns></returns>
    public async Task<ResultDto<string>> GetTokenAsync(string username, string password)
    {
        // 获取环境变量中的用户名和密码
        var envUsername = Environment.GetEnvironmentVariable("USER") ?? "root";
        var envPassword = Environment.GetEnvironmentVariable("PASS") ?? "Aa010426.";

        // 判断用户名和密码是否正确
        if (username != envUsername || password != envPassword)
            return ResultDto<string>.Error("账号密码错误", 400);

        // 创建一个JWT Token
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, username),
            new(ClaimTypes.Role, "admin"),
            new(ClaimTypes.NameIdentifier, username),
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.ASCII.GetBytes(JwtOptions.Secret);
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddDays(JwtOptions.ExpireDay),
            SigningCredentials =
                new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);

        return ResultDto<string>.Success(tokenHandler.WriteToken(token));
    }
}