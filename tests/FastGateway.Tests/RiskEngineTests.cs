using Core.Entities;
using FastGateway.Risk;
using Xunit;

namespace FastGateway.Tests;

public sealed class RiskEngineTests
{
    private const string Ip = "203.0.113.10";
    private const string Host = "example.com";
    private const string RouteId = "route-a";

    [Fact]
    public void Assess_NoHistory_ScoresZero()
    {
        var engine = CreateEngine(out _);

        var assessment = engine.Assess(Ip, null, nonBrowser: false);

        Assert.Equal(0, assessment.Score);
        Assert.Equal(string.Empty, assessment.Reason);
    }

    [Fact]
    public void Assess_FiveFailures_ReachesChallengeThreshold()
    {
        var engine = CreateEngine(out _);
        Fail(engine, Ip, times: 5);

        var assessment = engine.Assess(Ip, null, nonBrowser: false);

        Assert.Equal(RiskSettings.Default.ChallengeThreshold, assessment.Score);
        Assert.Contains("登录失败 5 次", assessment.Reason);
    }

    [Fact]
    public void Assess_ManyAccountsFromOneIp_AddsCredentialStuffingScore()
    {
        var engine = CreateEngine(out _);
        for (var i = 0; i < 5; i++)
            engine.RecordLoginResult(Ip, $"{RouteId}:account-{i}", null, LoginOutcome.Failure, Host);

        var assessment = engine.Assess(Ip, null, nonBrowser: false);

        Assert.Equal(30 + 40, assessment.Score);
        Assert.Contains("尝试了 5 个账号", assessment.Reason);
    }

    [Fact]
    public void Assess_FailuresOutsideWindow_DoNotCount()
    {
        var engine = CreateEngine(out var time);
        Fail(engine, Ip, times: 20);

        time.Advance(TimeSpan.FromMinutes(11));

        Assert.Equal(0, engine.Assess(Ip, null, nonBrowser: false).Score);
    }

    [Fact]
    public void Assess_SubnetFailures_ScoreNeighbourIp()
    {
        var engine = CreateEngine(out _);
        for (var i = 1; i <= 30; i++) Fail(engine, $"203.0.113.{i}", times: 1);

        var assessment = engine.Assess("203.0.113.200", null, nonBrowser: false);

        Assert.Equal(30, assessment.Score);
        Assert.Contains("203.0.113.0/24", assessment.Reason);
    }

    [Fact]
    public void Assess_SuccessfulLogins_AreNotCounted()
    {
        var engine = CreateEngine(out _);
        for (var i = 0; i < 10; i++) engine.RecordLoginResult(Ip, null, null, LoginOutcome.Success, Host);

        Assert.Equal(0, engine.Assess(Ip, null, nonBrowser: false).Score);
    }

    [Theory]
    [InlineData(29, true, RiskAction.Allow)]
    [InlineData(30, true, RiskAction.Challenge)]
    [InlineData(60, true, RiskAction.Challenge)]
    [InlineData(90, true, RiskAction.Block)]
    [InlineData(30, false, RiskAction.Allow)]
    [InlineData(60, false, RiskAction.Block)]
    public void Decide_ScoreAndChallengeAvailability_PicksTier(int score, bool challengeAvailable, RiskAction expected)
    {
        Assert.Equal(expected, CreateEngine(out _).Decide(score, challengeAvailable));
    }

    [Fact]
    public void Decide_CustomThresholds_FollowSettings()
    {
        var engine = CreateEngine(out _);
        engine.UpdateSettings(new RiskSettings
            { ChallengeThreshold = 10, StrongChallengeThreshold = 20, BlockThreshold = 40 });

        Assert.Equal(RiskAction.Challenge, engine.Decide(10, challengeAvailable: true));
        Assert.Equal(RiskAction.Block, engine.Decide(40, challengeAvailable: true));
        Assert.Equal(RiskAction.Block, engine.Decide(20, challengeAvailable: false));
    }

    [Theory]
    [InlineData(30, true, RiskSettings.ProviderPow, 18)]
    [InlineData(60, true, RiskSettings.ProviderTurnstile, 0)]
    [InlineData(60, false, RiskSettings.ProviderPow, 21)]
    public void ChooseChallenge_DefaultSettings_PowForMediumTurnstileForStrong(int score, bool turnstileAvailable,
        string provider, int difficulty)
    {
        Assert.Equal((provider, difficulty), CreateEngine(out _).ChooseChallenge(score, turnstileAvailable));
    }

    [Fact]
    public void ChooseChallenge_MediumConfiguredAsTurnstile_UsesTurnstileWhenAvailable()
    {
        var engine = CreateEngine(out _);
        engine.UpdateSettings(new RiskSettings { MediumProvider = RiskSettings.ProviderTurnstile });

        Assert.Equal(RiskSettings.ProviderTurnstile, engine.ChooseChallenge(30, turnstileAvailable: true).Provider);
        Assert.Equal(RiskSettings.ProviderPow, engine.ChooseChallenge(30, turnstileAvailable: false).Provider);
    }

    [Fact]
    public void IssueClearance_CustomQuota_FollowsSettings()
    {
        var engine = CreateEngine(out _);
        engine.UpdateSettings(new RiskSettings { ClearanceQuota = 2 });
        var clearance = engine.IssueClearance(Host, RouteId, Ip);

        Assert.True(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));
        Assert.True(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));
        Assert.False(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));
    }

    [Fact]
    public void TryRedeemPow_ValidSolution_RedeemsOnce()
    {
        var engine = CreateEngine(out _);
        var state = engine.CreateChallengeState(Host, "admin", "/", Ip, RiskSettings.ProviderPow, 8);
        Assert.True(engine.TryReadChallengeState(state, Host, Ip, out var payload));
        var solution = ProofOfWorkTests.Solve(payload.Nonce, 8);

        Assert.False(engine.TryRedeemPow(state, solution, Host, Ip, "route-a"));
        Assert.False(engine.TryRedeemPow(state, solution, Host, "203.0.113.11", "admin"));
        Assert.True(engine.TryRedeemPow(state, solution, Host, Ip, "admin"));
        Assert.False(engine.TryRedeemPow(state, solution, Host, Ip, "admin"));
    }

    [Fact]
    public void TryRedeemPow_TurnstileStateOrWrongSolution_Rejects()
    {
        var engine = CreateEngine(out _);
        var turnstile = engine.CreateChallengeState(Host, "admin", "/", Ip, RiskSettings.ProviderTurnstile, 0);
        var pow = engine.CreateChallengeState(Host, "admin", "/", Ip, RiskSettings.ProviderPow, 24);

        Assert.False(engine.TryRedeemPow(turnstile, "1", Host, Ip, "admin"));
        Assert.False(engine.TryRedeemPow(pow, "1", Host, Ip, "admin"));
    }

    [Fact]
    public void BlockIp_RepeatedWithin24Hours_EscalatesDuration()
    {
        var engine = CreateEngine(out var time);
        var assessment = new RiskAssessment(100, "test");

        Assert.Equal(TimeSpan.FromMinutes(5), engine.BlockIp(Ip, assessment, Host, "/login"));
        time.Advance(TimeSpan.FromMinutes(6));
        Assert.Null(engine.GetBlockRemaining(Ip));

        Assert.Equal(TimeSpan.FromMinutes(30), engine.BlockIp(Ip, assessment, Host, "/login"));
        time.Advance(TimeSpan.FromMinutes(31));

        Assert.Equal(TimeSpan.FromHours(24), engine.BlockIp(Ip, assessment, Host, "/login"));
    }

    [Fact]
    public void BlockIp_WhileBlocked_KeepsExistingBlock()
    {
        var engine = CreateEngine(out var time);
        var assessment = new RiskAssessment(100, "test");
        engine.BlockIp(Ip, assessment, Host, "/login");

        time.Advance(TimeSpan.FromMinutes(1));

        Assert.Equal(TimeSpan.FromMinutes(4), engine.BlockIp(Ip, assessment, Host, "/login"));
        Assert.Single(engine.GetActiveTags());
    }

    [Fact]
    public void Assess_AfterBlockExpires_KeepsRepeatOffenderScore()
    {
        var engine = CreateEngine(out var time);
        engine.BlockIp(Ip, new RiskAssessment(100, "test"), Host, "/login");
        time.Advance(TimeSpan.FromMinutes(6));

        var assessment = engine.Assess(Ip, null, nonBrowser: false);

        Assert.Equal(30, assessment.Score);
        Assert.Contains("24 小时内被封禁过", assessment.Reason);
    }

    [Fact]
    public void RemoveTag_Block_ClearsFailuresAndHistory()
    {
        var engine = CreateEngine(out _);
        Fail(engine, Ip, times: 20);
        engine.BlockIp(Ip, new RiskAssessment(100, "test"), Host, "/login");

        Assert.True(engine.RemoveTag(RiskEngine.BlockTagId(Ip)));

        Assert.Null(engine.GetBlockRemaining(Ip));
        Assert.Equal(0, engine.Assess(Ip, null, nonBrowser: false).Score);
        Assert.False(engine.RemoveTag(RiskEngine.BlockTagId(Ip)));
    }

    [Fact]
    public void RecordLoginResult_TenIpsFailOneAccount_TagsAccountAndRequiresChallenge()
    {
        var engine = CreateEngine(out _);
        var account = engine.HashAccount(RouteId, "alice")!;
        for (var i = 1; i <= 10; i++)
            engine.RecordLoginResult($"198.51.{i}.1", account, "a***e", LoginOutcome.Failure, Host);

        var tag = Assert.Single(engine.GetActiveTags());
        Assert.Equal("account_under_attack", tag.Kind);
        Assert.Equal("a***e", tag.Subject);

        // 一个从未失败过的新 IP 登录该账号也要验证
        var assessment = engine.Assess("192.0.2.1", account, nonBrowser: false);
        Assert.Equal(RiskSettings.Default.ChallengeThreshold, assessment.Score);
        Assert.Equal(0, engine.Assess("192.0.2.1", null, nonBrowser: false).Score);
    }

    [Fact]
    public void TryUseClearance_ConsumesQuotaThenRejects()
    {
        var engine = CreateEngine(out _);
        var clearance = engine.IssueClearance(Host, RouteId, Ip);

        for (var i = 0; i < RiskSettings.Default.ClearanceQuota; i++)
            Assert.True(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));

        Assert.False(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));
        Assert.False(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: false));
    }

    [Fact]
    public void TryUseClearance_PeekDoesNotConsume()
    {
        var engine = CreateEngine(out _);
        var clearance = engine.IssueClearance(Host, RouteId, Ip);

        for (var i = 0; i < RiskSettings.Default.ClearanceQuota * 2; i++)
            Assert.True(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: false));
    }

    [Fact]
    public void TryUseClearance_MismatchedOrExpired_Rejects()
    {
        var engine = CreateEngine(out var time);
        var clearance = engine.IssueClearance("Example.COM", RouteId, Ip);

        Assert.True(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: false));
        Assert.False(engine.TryUseClearance(clearance, "other.example.com", RouteId, Ip, consume: false));
        Assert.False(engine.TryUseClearance(clearance, Host, "route-b", Ip, consume: false));
        Assert.False(engine.TryUseClearance(clearance, Host, RouteId, "203.0.113.11", consume: false));
        Assert.False(engine.TryUseClearance(clearance + "x", Host, RouteId, Ip, consume: false));

        time.Advance(TimeSpan.FromMinutes(RiskSettings.Default.ClearanceMinutes) + TimeSpan.FromSeconds(1));
        Assert.False(engine.TryUseClearance(clearance, Host, RouteId, Ip, consume: false));
    }

    [Fact]
    public void TryUseClearance_SignedWithAnotherKey_Rejects()
    {
        var issuer = CreateEngine(out _);
        var verifier = CreateEngine(out _);
        var clearance = issuer.IssueClearance(Host, RouteId, Ip);

        Assert.False(verifier.TryUseClearance(clearance, Host, RouteId, Ip, consume: false));
    }

    [Fact]
    public void TryUseClearance_UnknownNonceOnThisNode_StartsWithFullQuota()
    {
        var key = new byte[32];
        var issuer = new RiskEngine(new ManualTimeProvider());
        var otherNode = new RiskEngine(new ManualTimeProvider());
        issuer.SetSigningKey(key);
        otherNode.SetSigningKey(key);
        var clearance = issuer.IssueClearance(Host, RouteId, Ip);

        Assert.True(otherNode.TryUseClearance(clearance, Host, RouteId, Ip, consume: true));
    }

    [Fact]
    public void ChallengeState_BoundToHostAndIp_ConsumedOnce()
    {
        var engine = CreateEngine(out _);
        var encoded = engine.CreateChallengeState(Host, RouteId, "/login?next=1", Ip, RiskSettings.ProviderPow, 18);

        Assert.True(engine.TryReadChallengeState(encoded, "EXAMPLE.com", Ip, out var state));
        Assert.Equal("/login?next=1", state.ReturnUrl);
        Assert.False(engine.TryReadChallengeState(encoded, "other.example.com", Ip, out _));
        Assert.False(engine.TryReadChallengeState(encoded, Host, "203.0.113.11", out _));

        Assert.True(engine.TryConsumeChallengeState(state));
        Assert.False(engine.TryConsumeChallengeState(state));
    }

    [Fact]
    public void CreateChallengeState_UnsafeReturnUrl_FallsBackToRoot()
    {
        var engine = CreateEngine(out _);
        var encoded = engine.CreateChallengeState(Host, RouteId, "//evil.example/", Ip, RiskSettings.ProviderPow, 18);

        Assert.True(engine.TryReadChallengeState(encoded, Host, Ip, out var state));
        Assert.Equal("/", state.ReturnUrl);
    }

    [Fact]
    public void ChallengeState_AfterLifetime_Rejected()
    {
        var engine = CreateEngine(out var time);
        var encoded = engine.CreateChallengeState(Host, RouteId, "/", Ip, RiskSettings.ProviderPow, 18);

        time.Advance(RiskEngine.ChallengeLifetime + TimeSpan.FromSeconds(1));

        Assert.False(engine.TryReadChallengeState(encoded, Host, Ip, out _));
    }

    [Fact]
    public void HashAccount_NormalizesCaseAndScopesByRoute()
    {
        var engine = CreateEngine(out _);

        Assert.Equal(engine.HashAccount(RouteId, "Alice "), engine.HashAccount(RouteId, "alice"));
        Assert.NotEqual(engine.HashAccount(RouteId, "alice"), engine.HashAccount("route-b", "alice"));
        Assert.DoesNotContain("alice", engine.HashAccount(RouteId, "alice"));
        Assert.Null(engine.HashAccount(RouteId, "   "));
        Assert.Null(new RiskEngine(new ManualTimeProvider()).HashAccount(RouteId, "alice"));
    }

    [Theory]
    [InlineData("alice", "a***e")]
    [InlineData("ab", "a*")]
    [InlineData("a", "a*")]
    [InlineData("  ", "")]
    public void MaskUsername_VariousLengths_HidesMiddle(string username, string expected)
    {
        Assert.Equal(expected, RiskEngine.MaskUsername(username));
    }

    [Fact]
    public void SetRouteProtection_DisabledOrEmpty_RemovesRoute()
    {
        var engine = CreateEngine(out _);
        engine.SetRouteProtection(RouteId, Protection(enable: true));
        Assert.NotNull(engine.GetRouteProtection(RouteId));

        engine.SetRouteProtection(RouteId, Protection(enable: false));
        Assert.Null(engine.GetRouteProtection(RouteId));

        engine.SetRouteProtection(RouteId, new RiskProtection { Enable = true });
        Assert.Null(engine.GetRouteProtection(RouteId));
    }

    [Theory]
    [InlineData("POST", "/api/login", true)]
    [InlineData("post", "/API/Login/", true)]
    [InlineData("GET", "/api/login", false)]
    [InlineData("POST", "/api/login/extra", false)]
    public void FindRule_IgnoresCaseAndTrailingSlash(string method, string path, bool matches)
    {
        var compiled = new CompiledRiskProtection(Protection(enable: true));

        Assert.Equal(matches, compiled.FindRule(method, path) != null);
    }

    private static RiskProtection Protection(bool enable)
    {
        return new RiskProtection
        {
            Enable = enable,
            LoginEndpoints = [new LoginEndpointRule { Method = "POST", Path = "/api/login" }]
        };
    }

    private static void Fail(RiskEngine engine, string ip, int times)
    {
        for (var i = 0; i < times; i++) engine.RecordLoginResult(ip, null, null, LoginOutcome.Failure, Host);
    }

    private static RiskEngine CreateEngine(out ManualTimeProvider time)
    {
        time = new ManualTimeProvider();
        var engine = new RiskEngine(time);
        engine.SetSigningKey(System.Security.Cryptography.RandomNumberGenerator.GetBytes(32));
        return engine;
    }
}
