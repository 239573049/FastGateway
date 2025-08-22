using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FastGateway.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FastGateway.Infrastructure;

public class JwtHelper
{
    private readonly JwtOptions _jwtOptions;

    public JwtHelper(IOptions<JwtOptions> jwtOptions)
    {
        _jwtOptions = jwtOptions.Value;
    }

    public string CreateToken()
    {
        // 1. 定义需要使用到的Claims
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "admin"),
            new Claim(ClaimTypes.Role, "admin")
        };

        // 2. 从 appsettings.json 中读取SecretKey
        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Secret));

        // 3. 选择加密算法
        var algorithm = SecurityAlgorithms.HmacSha256;

        // 4. 生成Credentials
        var signingCredentials = new SigningCredentials(secretKey, algorithm);

        // 5. 根据以上，生成token
        var jwtSecurityToken = new JwtSecurityToken(
            "FastGateway", //Issuer
            "FastGateway", //Audience
            claims, //Claims,
            DateTime.Now, //notBefore
            DateTime.Now.AddDays(_jwtOptions.ExpireDay), //expires
            signingCredentials //Credentials
        );

        // 6. 将token变为string
        var token = new JwtSecurityTokenHandler().WriteToken(jwtSecurityToken);

        return token;
    }
}