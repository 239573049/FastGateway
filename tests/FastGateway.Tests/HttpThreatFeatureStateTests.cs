using System.Text.Json;
using FastGateway.Infrastructure;
using FastGateway.Risk.Model;
using Xunit;

namespace FastGateway.Tests;

public sealed class HttpThreatFeatureStateTests
{
    [Fact]
    public void Serialize_RequestTargets_KeepsHostPathStatusPairing()
    {
        var request = new JevHttpThreatRequest
        {
            Model = "jev-latest",
            State = new HttpThreatFeatureState
            {
                RequestTargets =
                [
                    new HttpThreatRequestTarget
                    {
                        Host = "gateway.example.com",
                        Method = "GET",
                        Path = "/.git/config",
                        Status = 404,
                        Count = 3
                    }
                ]
            }
        };

        var json = JsonSerializer.Serialize(request, AppJsonContext.Default.JevHttpThreatRequest);

        Assert.Contains("\"request_targets\"", json);
        Assert.Contains("\"host\":\"gateway.example.com\"", json);
        Assert.Contains("\"path\":\"/.git/config\"", json);
        Assert.Contains("\"status\":404", json);
        Assert.Contains("\"count\":3", json);
    }
}
