using FastGateway.Dto;
using FastGateway.Risk;
using Xunit;

namespace FastGateway.Tests;

public sealed class SliderCaptchaTests
{
    private const string Ip = "203.0.113.10";

    /// <summary>真人从看到图片到松手的耗时，覆盖 <see cref="HumanTrack" /> 的 750ms 拖动时长</summary>
    private static readonly TimeSpan SolveTime = TimeSpan.FromSeconds(1.5);

    [Fact]
    public void Verify_CorrectPositionAndHumanTrack_IssuesRedeemableTicket()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);
        var challenge = captcha.Create(Ip)!;
        time.Advance(SolveTime);

        var ticket = captcha.Verify(challenge.Id, challenge.Target + 3, HumanTrack(challenge.Target + 3), Ip);

        Assert.NotNull(ticket);
        Assert.True(captcha.RedeemTicket(ticket, Ip));
        Assert.False(captcha.RedeemTicket(ticket, Ip));
    }

    [Fact]
    public void Verify_WrongPosition_RejectsAndConsumesChallenge()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);
        var challenge = captcha.Create(Ip)!;
        time.Advance(SolveTime);
        var wrong = challenge.Target - 20;

        Assert.Null(captcha.Verify(challenge.Id, wrong, HumanTrack(wrong), Ip));

        // 同一挑战不能换位置再试
        Assert.Null(captcha.Verify(challenge.Id, challenge.Target, HumanTrack(challenge.Target), Ip));
    }

    [Fact]
    public void Verify_DifferentIp_Rejects()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);
        var challenge = captcha.Create(Ip)!;
        time.Advance(SolveTime);

        Assert.Null(captcha.Verify(challenge.Id, challenge.Target, HumanTrack(challenge.Target), "198.51.100.1"));
    }

    [Fact]
    public void Verify_ExpiredChallenge_Rejects()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);
        var challenge = captcha.Create(Ip)!;

        time.Advance(TimeSpan.FromMinutes(3));

        Assert.Null(captcha.Verify(challenge.Id, challenge.Target, HumanTrack(challenge.Target), Ip));
    }

    [Fact]
    public void Verify_SubmittedFasterThanTrackClaims_Rejects()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);
        var challenge = captcha.Create(Ip)!;

        // 轨迹声称拖了 750ms，服务端只过了 300ms：轨迹是预先伪造的
        time.Advance(TimeSpan.FromMilliseconds(300));

        Assert.Null(captcha.Verify(challenge.Id, challenge.Target, HumanTrack(challenge.Target), Ip));
    }

    [Fact]
    public void RedeemTicket_ExpiredOrOtherIp_Rejects()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);

        var first = captcha.Create(Ip)!;
        time.Advance(SolveTime);
        var expired = captcha.Verify(first.Id, first.Target, HumanTrack(first.Target), Ip);
        Assert.NotNull(expired);
        time.Advance(TimeSpan.FromMinutes(3));
        Assert.False(captcha.RedeemTicket(expired, Ip));

        var second = captcha.Create(Ip)!;
        time.Advance(SolveTime);
        var ticket = captcha.Verify(second.Id, second.Target, HumanTrack(second.Target), Ip);
        Assert.NotNull(ticket);
        Assert.False(captcha.RedeemTicket(ticket, "198.51.100.1"));
        Assert.False(captcha.RedeemTicket(null, Ip));
    }

    [Fact]
    public void IsHumanLikeTrack_HumanTrack_Accepts()
    {
        Assert.True(SliderCaptcha.IsHumanLikeTrack(HumanTrack(120), 120, 1500));

        // 手很稳、纵向完全不动：靠步长不均和末段减速两项仍能通过
        var steady = HumanTrack(120).Select(p => new SliderTrackPoint { X = p.X, Y = 0, T = p.T }).ToList();
        Assert.True(SliderCaptcha.IsHumanLikeTrack(steady, 120, 1500));
    }

    [Fact]
    public void IsHumanLikeTrack_ScriptedTracks_Rejects()
    {
        // 匀速插值：步长恒定、无抖动、不减速
        var linear = Enumerable.Range(0, 25).Select(i => new SliderTrackPoint { X = i * 5, T = i * 40 }).ToList();
        Assert.False(SliderCaptcha.IsHumanLikeTrack(linear, 120, 1500));

        // 拖动耗时过短
        var instant = HumanTrack(120).Select(p => new SliderTrackPoint { X = p.X, Y = p.Y, T = p.T / 20 }).ToList();
        Assert.False(SliderCaptcha.IsHumanLikeTrack(instant, 120, 1500));

        // 中途瞬移
        var teleport = HumanTrack(200);
        teleport.Insert(5, new SliderTrackPoint { X = teleport[4].X + 100, Y = 0, T = teleport[4].T + 1 });
        Assert.False(SliderCaptcha.IsHumanLikeTrack(teleport, 200, 1500));

        // 拿到图后立即提交，低于人的最低反应时间
        Assert.False(SliderCaptcha.IsHumanLikeTrack(HumanTrack(120), 120, 300));

        Assert.False(SliderCaptcha.IsHumanLikeTrack(null, 120, 1500));
        Assert.False(SliderCaptcha.IsHumanLikeTrack([new SliderTrackPoint { X = 120, T = 500 }], 120, 1500));
    }

    [Fact]
    public void Create_RendersValidPngImagesWithTargetInRange()
    {
        var captcha = new SliderCaptcha(new ManualTimeProvider());

        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        for (var i = 0; i < SliderCaptcha.MaxCreatesPerMinute; i++)
        {
            var challenge = captcha.Create(Ip)!;
            Assert.Equal(signature, challenge.Background[..8]);
            Assert.Equal(signature, challenge.Piece[..8]);
            Assert.InRange(challenge.Target, SliderCaptcha.PieceSize,
                SliderCaptcha.Width - SliderCaptcha.PieceSize);
        }
    }

    [Fact]
    public void Create_ExceedingPerIpRate_RejectsUntilWindowPasses()
    {
        var time = new ManualTimeProvider();
        var captcha = new SliderCaptcha(time);

        for (var i = 0; i < SliderCaptcha.MaxCreatesPerMinute; i++) Assert.NotNull(captcha.Create(Ip));

        Assert.Null(captcha.Create(Ip));
        Assert.NotNull(captcha.Create("198.51.100.1"));

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.NotNull(captcha.Create(Ip));
    }

    /// <summary>先快后慢、步长不均、纵向轻微抖动的拖动轨迹，耗时 750ms</summary>
    private static List<SliderTrackPoint> HumanTrack(int end)
    {
        var track = new List<SliderTrackPoint>();
        const int samples = 30;
        for (var i = 0; i <= samples; i++)
        {
            var progress = i / (double)samples;
            var eased = 1 - Math.Pow(1 - progress, 3);
            track.Add(new SliderTrackPoint { X = (int)Math.Round(end * eased), Y = i % 3 - 1, T = i * 25 });
        }

        return track;
    }
}
