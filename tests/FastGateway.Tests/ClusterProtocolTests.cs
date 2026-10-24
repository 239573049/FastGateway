using Core.Entities;
using FastGateway.Cluster;
using FastGateway.Services;
using Xunit;

namespace FastGateway.Tests;

public sealed class ClusterProtocolTests
{
    [Fact]
    public void EncodeDecode_DomainWithRiskProtection_RoundTripsToWorker()
    {
        var config = new GatewayConfig();
        config.DomainNames.Add(new DomainName
        {
            Id = "route-a",
            RiskProtection = new RiskProtection
            {
                Enable = true,
                BlockScope = RiskBlockScope.WholeRoute,
                LoginEndpoints =
                [
                    new LoginEndpointRule
                    {
                        Method = "POST", Path = "/api/login", UsernameField = "username",
                        FailureStatusCodes = [401, 403], ResultHeader = "X-Auth-Result"
                    }
                ]
            }
        });
        config.Settings.Add(new Setting { Key = "risk_signing_key", Value = "secret", Group = "risk" });

        var frame = ClusterProtocol.Encode(new ClusterMessage
        {
            Type = ClusterMessage.TypeConfig,
            Config = new ClusterConfigPayload { Config = config }
        });
        var stream = new MemoryStream();
        stream.Write(frame);
        var decoded = ClusterProtocol.Decode(stream)!.Config!.Config;

        var protection = decoded.DomainNames.Single().RiskProtection!;
        Assert.True(protection.Enable);
        Assert.Equal(RiskBlockScope.WholeRoute, protection.BlockScope);
        var rule = Assert.Single(protection.LoginEndpoints);
        Assert.Equal("/api/login", rule.Path);
        Assert.Equal("username", rule.UsernameField);
        Assert.Equal([401, 403], rule.FailureStatusCodes);
        Assert.Equal("X-Auth-Result", rule.ResultHeader);
        Assert.Equal("secret", decoded.Settings.Single(x => x.Key == "risk_signing_key").Value);
    }
}
