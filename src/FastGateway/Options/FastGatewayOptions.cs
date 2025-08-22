namespace FastGateway.Options;

public class FastGatewayOptions
{
    public static string TunnelToken { get; set; } = "Aa123456.";

    public static string Password { get; set; } = "Aa123456";

    public static void Initialize(IConfiguration configuration)
    {
        var tunnelToken = configuration["TunnelToken"];
        if (!string.IsNullOrEmpty(tunnelToken))
            TunnelToken = tunnelToken;
        else
            throw new ArgumentNullException(nameof(TunnelToken), "TunnelToken cannot be null or empty.");

        var password = configuration["Password"];

        if (string.IsNullOrEmpty(password)) password = configuration["PASSWORD"] ?? "Aa123456";

        if (!string.IsNullOrEmpty(password))
            Password = password;
        else
            throw new ArgumentNullException(nameof(Password), "Password cannot be null or empty.");
    }
}