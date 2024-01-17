using Microsoft.AspNetCore.Identity;

namespace Gateway.Domain.Users.Aggregations;

public class User : IdentityUser<long>
{
    protected User()
    {
        
    }
    
    public User(string userName,string email) : base(userName)
    {
        Email = email;
    }
    
    public void SetPassword(string password)
    {
        PasswordHash = new PasswordHasher<User>().HashPassword(this, password);
    }
    
    public void SetPhoneNumber(string phoneNumber)
    {
        PhoneNumber = phoneNumber;
    }
    
    public void Lockout()
    {
        LockoutEnabled = true;
        LockoutEnd = DateTimeOffset.MaxValue;
    }
}