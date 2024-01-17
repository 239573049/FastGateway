using System.Security.Claims;
using CoreFlex.Jwt;
using CoreFlex.Module;
using Gateway.Domain.Users.Aggregations;
using LiteDB;
using Microsoft.AspNetCore.Identity;

namespace Gateway.Services;

public class AuthorityService(ILiteDatabase database) : IScopedDependency
{
    public async Task<string> GetTokenAsync(string userName, string password)
    {
        var userCollection = database.GetCollection<User>();

        var user = userCollection.FindOne(x => x.UserName == userName);

        if (user == null)
        {
            throw new Exception("用户不存在");
        }

        var passwordHasher = new PasswordHasher<User>();

        var verifyResult = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);

        if (verifyResult == PasswordVerificationResult.Failed)
        {
            throw new Exception("密码错误");
        }

        if (verifyResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.SetPassword(password);

            userCollection.Update(user);
        }

        var doc = new Dictionary<string, object>();
        // 使用微软提供的角色常量
        doc.Add(ClaimTypes.Role, "admin");
        doc.Add(ClaimTypes.NameIdentifier, user!.Id.ToString());
        doc.Add(ClaimTypes.Name, user.UserName);
        doc.Add(ClaimTypes.Email, user.Email);
        var token = JwtHelper.GeneratorAccessToken(doc);

        return token;
    }
}