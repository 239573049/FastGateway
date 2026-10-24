using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using FastGateway.Risk;
using Xunit;

namespace FastGateway.Tests;

public sealed class ProofOfWorkTests
{
    [Fact]
    public void Verify_SolvedChallenge_Accepts()
    {
        var solution = Solve("challenge-a", 10);

        Assert.True(ProofOfWork.Verify("challenge-a", solution, 10));

        // 比求解难度高的要求：只有恰好满足更多前导零时才通过，结果与服务端逐位计算一致
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes("challenge-a:" + solution));
        Assert.Equal(ProofOfWork.LeadingZeroBits(hash) >= 16, ProofOfWork.Verify("challenge-a", solution, 16));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("12a")]
    [InlineData("-1")]
    [InlineData("12345678901234567")]
    [InlineData("１２")]
    public void Verify_MalformedSolution_Rejects(string? solution)
    {
        Assert.False(ProofOfWork.Verify("challenge-a", solution, ProofOfWork.MinDifficulty));
    }

    [Fact]
    public void Verify_DifficultyOutOfRange_Rejects()
    {
        var solution = Solve("challenge-a", ProofOfWork.MinDifficulty);

        Assert.False(ProofOfWork.Verify("challenge-a", solution, ProofOfWork.MinDifficulty - 1));
        Assert.False(ProofOfWork.Verify("challenge-a", solution, ProofOfWork.MaxDifficulty + 1));
    }

    [Theory]
    [InlineData(new byte[] { 0x80 }, 0)]
    [InlineData(new byte[] { 0x01 }, 7)]
    [InlineData(new byte[] { 0x00, 0x40 }, 9)]
    [InlineData(new byte[] { 0x00, 0x00 }, 16)]
    public void LeadingZeroBits_CountsAcrossBytes(byte[] hash, int expected)
    {
        Assert.Equal(expected, ProofOfWork.LeadingZeroBits(hash));
    }

    [Fact]
    public void RiskSettings_InvalidValues_Throw()
    {
        Assert.Throws<ValidationException>(() =>
            new RiskSettings { ChallengeThreshold = 60, StrongChallengeThreshold = 60 }.Validate());
        Assert.Throws<ValidationException>(() => new RiskSettings { MediumProvider = "captcha" }.Validate());
        Assert.Throws<ValidationException>(() =>
            new RiskSettings { PowDifficulty = ProofOfWork.MaxDifficulty }.Validate());
        Assert.Throws<ValidationException>(() => new RiskSettings { ClearanceQuota = 0 }.Validate());
        new RiskSettings().Validate();
    }

    [Fact]
    public void RiskSettingsStore_Parse_ValidJsonOrFallsBackToDefaults()
    {
        var parsed = RiskSettingsStore.Parse("""{"challengeThreshold":20,"powDifficulty":16}""");
        Assert.Equal(20, parsed.ChallengeThreshold);
        Assert.Equal(16, parsed.PowDifficulty);
        Assert.Equal(19, parsed.StrongPowDifficulty);

        Assert.Equal(30, RiskSettingsStore.Parse("not json").ChallengeThreshold);
        Assert.Equal(30, RiskSettingsStore.Parse("""{"challengeThreshold":95}""").ChallengeThreshold);
        Assert.Equal(30, RiskSettingsStore.Parse(null).ChallengeThreshold);
    }

    internal static string Solve(string challenge, int difficulty)
    {
        for (var n = 0;; n++)
        {
            var solution = n.ToString(CultureInfo.InvariantCulture);
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(challenge + ":" + solution));
            if (ProofOfWork.LeadingZeroBits(hash) >= difficulty) return solution;
        }
    }
}
