namespace FastGateway.Options;

public class FastGatewayOptions
{
    public const int DefaultMaxConnectionsPerUpstream = 4096;

    public const string DefaultPassword = "Aa123456";

    public static string TunnelToken { get; set; } = "Aa123456.";

    public static string Password { get; set; } = DefaultPassword;

    public static bool IsDefaultPassword => Password == DefaultPassword;

    public static int MaxConnectionsPerUpstream { get; private set; } = DefaultMaxConnectionsPerUpstream;

    public static void Initialize(IConfiguration configuration)
    {
        var maxConnectionsValue = configuration["MaxConnectionsPerUpstream"]
                                  ?? configuration["MAX_CONNECTIONS_PER_UPSTREAM"];
        var maxConnections = DefaultMaxConnectionsPerUpstream;
        if (!string.IsNullOrWhiteSpace(maxConnectionsValue) &&
            (!int.TryParse(maxConnectionsValue, out maxConnections) || maxConnections is < 1 or > 65535))
            throw new ArgumentOutOfRangeException(nameof(MaxConnectionsPerUpstream),
                "MaxConnectionsPerUpstream must be between 1 and 65535.");

        MaxConnectionsPerUpstream = maxConnections;

        var tunnelToken = configuration["TunnelToken"];
        if (!string.IsNullOrEmpty(tunnelToken))
            TunnelToken = tunnelToken;
        else
            throw new ArgumentNullException(nameof(TunnelToken), "TunnelToken cannot be null or empty.");

        var password = configuration["Password"];

        if (string.IsNullOrEmpty(password)) password = configuration["PASSWORD"] ?? DefaultPassword;

        if (!string.IsNullOrEmpty(password))
            Password = password;
        else
            throw new ArgumentNullException(nameof(Password), "Password cannot be null or empty.");
    }
}