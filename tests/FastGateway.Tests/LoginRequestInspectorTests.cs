using System.ComponentModel.DataAnnotations;
using System.Text;
using Core.Entities;
using FastGateway.Risk;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace FastGateway.Tests;

public sealed class LoginRequestInspectorTests
{
    [Theory]
    [InlineData("""{"username":"alice","password":"x"}""", "alice")]
    [InlineData("""{"UserName":"alice"}""", "alice")]
    [InlineData("""{"profile":{"username":"nested"},"username":"top"}""", "top")]
    [InlineData("""{"username":12345}""", "12345")]
    [InlineData("""{"username":null}""", null)]
    [InlineData("""{"profile":{"username":"nested"}}""", null)]
    [InlineData("""["username","alice"]""", null)]
    [InlineData("""{"username":""", null)]
    public void ReadJsonField_VariousBodies_ReturnsTopLevelValue(string json, string? expected)
    {
        Assert.Equal(expected, LoginRequestInspector.ReadJsonField(Encoding.UTF8.GetBytes(json), "username"));
    }

    [Theory]
    [InlineData("username=alice&password=x", "alice")]
    [InlineData("USERNAME=al%40ce", "al@ce")]
    [InlineData("password=x", null)]
    public void ReadFormField_VariousBodies_ReturnsValue(string form, string? expected)
    {
        Assert.Equal(expected, LoginRequestInspector.ReadFormField(Encoding.UTF8.GetBytes(form), "username"));
    }

    [Fact]
    public async Task TryReadUsernameAsync_JsonBody_RewindsBodyForUpstream()
    {
        const string body = """{"username":"alice","password":"secret"}""";
        var context = CreateContext(body, "application/json");

        var username = await LoginRequestInspector.TryReadUsernameAsync(context.Request, "username", default);

        Assert.Equal("alice", username);
        Assert.Equal(0, context.Request.Body.Position);
        Assert.Equal(body, await new StreamReader(context.Request.Body).ReadToEndAsync());
    }

    [Fact]
    public async Task TryReadUsernameAsync_OversizedOrUnsupported_ReturnsNull()
    {
        var oversized = CreateContext("{\"username\":\"" + new string('a', 20_000) + "\"}", "application/json");
        var multipart = CreateContext("username=alice", "multipart/form-data; boundary=x");

        Assert.Null(await LoginRequestInspector.TryReadUsernameAsync(oversized.Request, "username", default));
        Assert.Null(await LoginRequestInspector.TryReadUsernameAsync(multipart.Request, "username", default));
    }

    [Fact]
    public async Task TryReadUsernameAsync_UnknownLengthOversized_ReturnsNullAndKeepsBody()
    {
        var body = "{\"username\":\"" + new string('a', 20_000) + "\"}";
        var context = CreateContext(body, "application/json");
        context.Request.ContentLength = null;

        Assert.Null(await LoginRequestInspector.TryReadUsernameAsync(context.Request, "username", default));
        Assert.Equal(body, await new StreamReader(context.Request.Body).ReadToEndAsync());
    }

    [Theory]
    [InlineData("curl/8.4.0", true, true)]
    [InlineData("python-requests/2.31", true, true)]
    [InlineData("", true, true)]
    [InlineData("Mozilla/5.0 Chrome/120", true, false)]
    [InlineData("Mozilla/5.0 Chrome/120", false, true)]
    public void IsNonBrowser_UserAgentAndBrowserHeaders_Classifies(string userAgent, bool browserHeaders,
        bool expected)
    {
        var headers = new HeaderDictionary { ["User-Agent"] = userAgent };
        if (browserHeaders)
        {
            headers["Sec-Fetch-Mode"] = "cors";
            headers["Accept-Language"] = "zh-CN";
        }

        Assert.Equal(expected, LoginRequestInspector.IsNonBrowser(headers));
    }

    [Theory]
    [InlineData("GET", "navigate", null, true)]
    [InlineData("GET", "cors", "text/html", false)]
    [InlineData("GET", null, "text/html,application/xhtml+xml", true)]
    [InlineData("GET", null, "application/json", false)]
    [InlineData("POST", "navigate", "text/html", false)]
    public void IsNavigation_FetchModeAndAccept_Classifies(string method, string? mode, string? accept,
        bool expected)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        if (mode != null) context.Request.Headers["Sec-Fetch-Mode"] = mode;
        if (accept != null) context.Request.Headers.Accept = accept;

        Assert.Equal(expected, LoginRequestInspector.IsNavigation(context.Request));
    }

    [Theory]
    [InlineData(200, "fail", LoginOutcome.Failure)]
    [InlineData(401, "success", LoginOutcome.Success)]
    [InlineData(200, null, LoginOutcome.Unknown)]
    [InlineData(401, null, LoginOutcome.Failure)]
    public void DetectOutcome_WithResultHeader_HeaderWins(int status, string? headerValue, LoginOutcome expected)
    {
        var rule = new LoginEndpointRule { ResultHeader = "X-Auth-Result" };
        var headers = new HeaderDictionary();
        if (headerValue != null) headers["X-Auth-Result"] = headerValue;

        Assert.Equal(expected, LoginRequestInspector.DetectOutcome(status, headers, rule));
    }

    [Theory]
    [InlineData(401, LoginOutcome.Failure)]
    [InlineData(403, LoginOutcome.Failure)]
    [InlineData(200, LoginOutcome.Success)]
    [InlineData(302, LoginOutcome.Unknown)]
    [InlineData(500, LoginOutcome.Unknown)]
    public void DetectOutcome_StatusCodesOnly_Classifies(int status, LoginOutcome expected)
    {
        var rule = new LoginEndpointRule();

        Assert.Equal(expected, LoginRequestInspector.DetectOutcome(status, new HeaderDictionary(), rule));
    }

    [Fact]
    public void Normalize_ValidConfig_TrimsAndUppercases()
    {
        var domain = DomainWith(new LoginEndpointRule
        {
            Method = " post ", Path = " /api/login ", UsernameField = " username ", ResultHeader = " ",
            FailureStatusCodes = [401, 401, 403]
        });

        RiskProtectionValidator.Normalize(domain);

        var rule = domain.RiskProtection!.LoginEndpoints[0];
        Assert.Equal("POST", rule.Method);
        Assert.Equal("/api/login", rule.Path);
        Assert.Equal("username", rule.UsernameField);
        Assert.Null(rule.ResultHeader);
        Assert.Equal([401, 403], rule.FailureStatusCodes);
    }

    [Theory]
    [InlineData("DELETE", "/api/login", null, new[] { 401 })]
    [InlineData("POST", "api/login", null, new[] { 401 })]
    [InlineData("POST", "/api/login?x=1", null, new[] { 401 })]
    [InlineData("POST", "/api/login", "X Auth", new[] { 401 })]
    [InlineData("POST", "/api/login", null, new[] { 99 })]
    [InlineData("POST", "/api/login", null, new int[0])]
    public void Normalize_InvalidRule_Throws(string method, string path, string? resultHeader, int[] statusCodes)
    {
        var domain = DomainWith(new LoginEndpointRule
        {
            Method = method, Path = path, ResultHeader = resultHeader, FailureStatusCodes = statusCodes
        });

        Assert.Throws<ValidationException>(() => RiskProtectionValidator.Normalize(domain));
    }

    [Fact]
    public void Normalize_DuplicateOrMissingEndpoints_Throws()
    {
        var duplicate = DomainWith(
            new LoginEndpointRule { Path = "/api/login" },
            new LoginEndpointRule { Path = "/API/login/" });
        var empty = new DomainName { RiskProtection = new RiskProtection { Enable = true } };

        Assert.Throws<ValidationException>(() => RiskProtectionValidator.Normalize(duplicate));
        Assert.Throws<ValidationException>(() => RiskProtectionValidator.Normalize(empty));
        RiskProtectionValidator.Normalize(new DomainName { RiskProtection = new RiskProtection { Enable = false } });
    }

    private static DomainName DomainWith(params LoginEndpointRule[] rules)
    {
        return new DomainName { RiskProtection = new RiskProtection { Enable = true, LoginEndpoints = [.. rules] } };
    }

    private static DefaultHttpContext CreateContext(string body, string contentType)
    {
        var bytes = Encoding.UTF8.GetBytes(body);
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.ContentType = contentType;
        context.Request.ContentLength = bytes.Length;
        context.Request.Body = new MemoryStream(bytes);
        return context;
    }
}
