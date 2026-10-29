using System.Collections.Concurrent;
using System.Security.Cryptography;
using FastGateway.Dto;
using FastGateway.Infrastructure;

namespace FastGateway.Risk;

public sealed record SliderChallenge(string Id, byte[] Background, byte[] Piece, int Target);

/// <summary>
///     管理后台登录的滑块验证。缺口位置只保存在服务端内存，图片由服务端渲染，客户端拿不到坐标；
///     每张图有形状不同的干扰缺口，拖动轨迹要通过行为检查；验证通过换取一次性票据，登录时兑换。它能挡住直接调接口的通用爆破脚本，
///     但挡不住做图像识别的定制攻击，强对抗场景仍应启用 Turnstile。
///     状态是节点本地的：管理后台登录只落在当前进程，不需要集群共享。
/// </summary>
public sealed class SliderCaptcha
{
    public const int Width = 300;
    public const int Height = 160;
    public const int PieceSize = Square + Knob * 2;

    /// <summary>拼图块：36px 方块，四边可各带一个半径 7 的凸起或凹陷</summary>
    private const int Square = 36;
    private const int Knob = 7;

    /// <summary>干扰缺口数量：只靠找暗区定位的脚本最多只有 1/(Decoys+1) 的命中率，失败还会计入锁定</summary>
    private const int Decoys = 2;

    private const int GapMargin = 4;

    // 缺口不能落在拼图块起始位置附近，必须拖动一段距离
    private const int MinGapX = PieceSize + GapMargin;
    private const int MaxGapX = Width - PieceSize - 2;

    private const int Tolerance = 5;
    private const int MaxStep = 60;
    private const int MinTrackPoints = 10;
    private const int MaxTrackPoints = 600;
    private const int MinDurationMs = 250;
    private const int MaxDurationMs = 30_000;
    private const int MinSolveMs = 600;
    private const int ClockSlackMs = 50;
    private const int MaxEntries = 10_000;

    /// <summary>单 IP 每分钟最多取图次数：渲染图片要消耗 CPU，匿名接口必须限频</summary>
    public const int MaxCreatesPerMinute = 20;

    private static readonly TimeSpan CreateWindow = TimeSpan.FromMinutes(1);

    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(2);

    public static SliderCaptcha Shared { get; } = new(TimeProvider.System);

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, PendingChallenge> _challenges = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, PendingTicket> _tickets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SlidingWindowCounter> _creates = new(StringComparer.Ordinal);

    public SliderCaptcha(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    private long Now => _timeProvider.GetUtcNow().UtcTicks;

    /// <summary>该 IP 取图过于频繁，或容量已满且清理过期项后仍满时返回 null，防止匿名接口被刷爆</summary>
    public SliderChallenge? Create(string ip)
    {
        var now = Now;
        if (!TryCountCreate(ip, now)) return null;

        if (_challenges.Count >= MaxEntries) Purge(_challenges, now, static c => c.ExpiresAt);
        if (_challenges.Count >= MaxEntries) return null;

        // 坐标用密码学随机数，图案细节用普通随机数即可
        var target = RandomNumberGenerator.GetInt32(MinGapX, MaxGapX + 1);
        var top = RandomNumberGenerator.GetInt32(GapMargin, Height - PieceSize - GapMargin + 1);
        var (background, piece) = Render(RandomShape(Random.Shared), target, top);

        var id = SignedToken.CreateNonce();
        _challenges[id] = new PendingChallenge(target, ip, now, now + ChallengeLifetime.Ticks);
        return new SliderChallenge(id, background, piece, target);
    }

    /// <summary>
    ///     校验拖动结果，通过时返回一次性票据。挑战无论成败只能提交一次，不能反复试位置
    /// </summary>
    public string? Verify(string? id, int x, IReadOnlyList<SliderTrackPoint>? track, string ip)
    {
        if (string.IsNullOrEmpty(id) || !_challenges.TryRemove(id, out var challenge)) return null;

        var now = Now;
        if (challenge.ExpiresAt < now ||
            !string.Equals(challenge.Ip, ip, StringComparison.Ordinal) ||
            Math.Abs(x - challenge.Target) > Tolerance ||
            !IsHumanLikeTrack(track, x, (now - challenge.IssuedAt) / TimeSpan.TicksPerMillisecond))
            return null;

        if (_tickets.Count >= MaxEntries) Purge(_tickets, now, static t => t.ExpiresAt);
        if (_tickets.Count >= MaxEntries) return null;

        var ticket = SignedToken.CreateNonce();
        _tickets[ticket] = new PendingTicket(ip, now + TicketLifetime.Ticks);
        return ticket;
    }

    private bool TryCountCreate(string ip, long now)
    {
        if (!_creates.TryGetValue(ip, out var counter))
        {
            if (_creates.Count >= MaxEntries)
            {
                foreach (var (key, value) in _creates)
                    if (value.Count(now) == 0)
                        _creates.TryRemove(key, out _);

                // 仍然满员说明正被大量来源同时刷，宁可拒绝也不无限增长
                if (_creates.Count >= MaxEntries) return false;
            }

            counter = _creates.GetOrAdd(ip, static _ => new SlidingWindowCounter(CreateWindow, 6));
        }

        return counter.Add(now) <= MaxCreatesPerMinute;
    }

    /// <summary>票据只能兑换一次，并且必须来自完成验证的同一 IP</summary>
    public bool RedeemTicket(string? ticket, string ip)
    {
        return !string.IsNullOrEmpty(ticket) &&
               _tickets.TryRemove(ticket, out var entry) &&
               entry.ExpiresAt >= Now &&
               string.Equals(entry.Ip, ip, StringComparison.Ordinal);
    }

    /// <summary>
    ///     行为检查分两层。硬性条件拒绝直接提交坐标、伪造耗时或瞬移的轨迹；
    ///     软性特征（步长不均、纵向抖动、末段减速）满足两项即可，避免误伤手很稳或用触屏的用户。
    ///     <paramref name="elapsedMs" /> 是服务端测得的发图到提交的耗时，客户端无法伪造
    /// </summary>
    public static bool IsHumanLikeTrack(IReadOnlyList<SliderTrackPoint>? track, int x, long elapsedMs)
    {
        if (track is null || track.Count < MinTrackPoints || track.Count > MaxTrackPoints) return false;

        var first = track[0];
        var last = track[^1];
        if (first.T < 0 || first.X > Tolerance || Math.Abs(last.X - x) > 2) return false;

        var duration = last.T - first.T;
        if (duration is < MinDurationMs or > MaxDurationMs) return false;

        // 轨迹自称的拖动时长不可能超过服务端实际经过的时间；看图到松手也需要最低反应时间
        if (elapsedMs < MinSolveMs || elapsedMs + ClockSlackMs < duration) return false;

        var steps = new HashSet<int>();
        var yVaries = false;
        for (var i = 1; i < track.Count; i++)
        {
            if (track[i].T < track[i - 1].T) return false;
            var step = track[i].X - track[i - 1].X;
            if (Math.Abs(step) > MaxStep) return false;
            if (step != 0) steps.Add(step);
            if (track[i].Y != first.Y) yVaries = true;
        }

        var signals = (steps.Count >= 4 ? 1 : 0) + (yVaries ? 1 : 0) + (DeceleratesAtEnd(track) ? 1 : 0);
        return signals >= 2;
    }

    /// <summary>瞄准缺口时人会减速：末段速度明显低于途中峰值。匀速或恒加速脚本做不到</summary>
    private static bool DeceleratesAtEnd(IReadOnlyList<SliderTrackPoint> track)
    {
        const int span = 3;
        var peak = 0d;
        for (var i = span; i < track.Count; i++)
            peak = Math.Max(peak, Speed(track[i - span], track[i]));

        var end = Speed(track[^(span + 1)], track[^1]);
        return peak > 0 && end < peak * 0.6;

        static double Speed(SliderTrackPoint from, SliderTrackPoint to)
        {
            var dt = to.T - from.T;
            return dt > 0 ? Math.Abs(to.X - from.X) / (double)dt : 0;
        }
    }

    private static (byte[] Background, byte[] Piece) Render(sbyte[] shape, int target, int top)
    {
        var random = Random.Shared;
        var pixels = new byte[Width * Height * 3];

        // 渐变打底，叠加半透明圆形、线条和噪点，制造大量与缺口无关的边缘，干扰边缘检测
        Span<byte> from = stackalloc byte[3];
        Span<byte> to = stackalloc byte[3];
        RandomColor(random, from, 60, 200);
        RandomColor(random, to, 60, 200);
        var mirrored = random.Next(2) == 0;
        for (var y = 0; y < Height; y++)
        for (var x = 0; x < Width; x++)
        {
            var t = ((mirrored ? Width - x : x) + y) / (float)(Width + Height);
            var offset = (y * Width + x) * 3;
            for (var c = 0; c < 3; c++)
                pixels[offset + c] = (byte)(from[c] + (to[c] - from[c]) * t);
        }

        Span<byte> color = stackalloc byte[3];
        var circles = random.Next(10, 16);
        for (var i = 0; i < circles; i++)
        {
            RandomColor(random, color, 30, 240);
            var cx = random.Next(Width);
            var cy = random.Next(Height);
            var radius = random.Next(12, 48);
            var opacity = 0.25f + random.NextSingle() * 0.35f;
            for (var y = Math.Max(0, cy - radius); y < Math.Min(Height, cy + radius); y++)
            for (var x = Math.Max(0, cx - radius); x < Math.Min(Width, cx + radius); x++)
            {
                if ((x - cx) * (x - cx) + (y - cy) * (y - cy) > radius * radius) continue;
                BlendPixel(pixels, x, y, color, opacity);
            }
        }

        var lines = random.Next(4, 8);
        for (var i = 0; i < lines; i++)
        {
            RandomColor(random, color, 20, 250);
            DrawLine(pixels, random.Next(Width), random.Next(Height), random.Next(Width), random.Next(Height),
                random.Next(1, 4), color, 0.35f + random.NextSingle() * 0.3f);
        }

        AddNoise(pixels, random, 8);

        // 拼图块取原始像素后再整体调亮并叠加独立噪点：与缺口区域不再逐像素对应，削弱模板匹配
        var mask = BuildMask(shape);
        var brightness = 1f + random.NextSingle() * 0.12f;
        var piece = new byte[PieceSize * Height * 4];
        for (var ly = 0; ly < PieceSize; ly++)
        for (var lx = 0; lx < PieceSize; lx++)
        {
            if (!mask[lx, ly]) continue;
            var source = ((top + ly) * Width + target + lx) * 3;
            var dest = ((top + ly) * PieceSize + lx) * 4;
            var edge = IsEdge(mask, lx, ly);
            for (var c = 0; c < 3; c++)
                piece[dest + c] = edge
                    ? (byte)235
                    : (byte)Math.Clamp(pixels[source + c] * brightness + random.Next(-8, 9), 0, 255);
            piece[dest + 3] = 255;
        }

        // 真假缺口用同一套明暗参数、位于同一高度带，只有形状不同：按暗度、按行扫描都分不出真假，
        // 人则可以对照拼图块的形状认出真缺口
        var shade = 0.4f + random.NextSingle() * 0.15f;
        var highlight = 0.35f + random.NextSingle() * 0.15f;
        foreach (var (decoyX, decoyY, decoyShape) in PlaceDecoys(random, shape, target, top))
            CarveGap(pixels, BuildMask(decoyShape), decoyX, decoyY, shade, highlight, random);
        CarveGap(pixels, mask, target, top, shade, highlight, random);

        return (PngEncoder.Encode(pixels, Width, Height, false), PngEncoder.Encode(piece, PieceSize, Height, true));
    }

    private static List<(int X, int Y, sbyte[] Shape)> PlaceDecoys(Random random, sbyte[] shape, int target, int top)
    {
        var decoys = new List<(int, int, sbyte[])>(Decoys);
        var taken = new List<int> { target };
        for (var attempt = 0; attempt < 50 && decoys.Count < Decoys; attempt++)
        {
            var x = random.Next(MinGapX, MaxGapX + 1);
            if (taken.Any(other => Math.Abs(other - x) < PieceSize + 6)) continue;

            var y = Math.Clamp(top + random.Next(-8, 9), GapMargin, Height - PieceSize - GapMargin);
            taken.Add(x);
            decoys.Add((x, y, RandomShape(random, differentFrom: shape)));
        }

        return decoys;
    }

    private static void CarveGap(byte[] pixels, bool[,] mask, int left, int top, float shade, float highlight,
        Random random)
    {
        for (var ly = 0; ly < PieceSize; ly++)
        for (var lx = 0; lx < PieceSize; lx++)
        {
            if (!mask[lx, ly]) continue;
            var offset = ((top + ly) * Width + left + lx) * 3;
            var edge = IsEdge(mask, lx, ly);
            for (var c = 0; c < 3; c++)
                pixels[offset + c] = edge
                    ? Blend(pixels[offset + c], 255, highlight)
                    : (byte)Math.Clamp(pixels[offset + c] * shade + random.Next(-6, 7), 0, 255);
        }
    }

    /// <summary>
    ///     拼图形状：上右下左四条边各为 1 凸起、-1 凹陷或 0 平直，至少两条边有凹凸，每次挑战随机。
    ///     干扰形状至少两条边与真形状不同，只差一条边时在小图上肉眼很难分辨
    /// </summary>
    private static sbyte[] RandomShape(Random random, sbyte[]? differentFrom = null)
    {
        while (true)
        {
            var shape = new sbyte[4];
            for (var i = 0; i < 4; i++) shape[i] = (sbyte)random.Next(-1, 2);
            if (shape.Count(side => side != 0) < 2) continue;
            if (differentFrom != null && Enumerable.Range(0, 4).Count(i => shape[i] != differentFrom[i]) < 2)
                continue;
            return shape;
        }
    }

    private static bool[,] BuildMask(sbyte[] shape)
    {
        // 方块居中，四周各留一个凸起半径的空间；凸起/凹陷的圆心落在对应边的中点
        ReadOnlySpan<(int X, int Y)> knobs =
        [
            (Knob + Square / 2, Knob), (Knob + Square, Knob + Square / 2),
            (Knob + Square / 2, Knob + Square), (Knob, Knob + Square / 2)
        ];

        var mask = new bool[PieceSize, PieceSize];
        for (var ly = 0; ly < PieceSize; ly++)
        for (var lx = 0; lx < PieceSize; lx++)
        {
            var inside = lx is >= Knob and < Knob + Square && ly is >= Knob and < Knob + Square;
            for (var side = 0; side < 4; side++)
            {
                if (shape[side] == 0) continue;
                var dx = lx - knobs[side].X;
                var dy = ly - knobs[side].Y;
                if (dx * dx + dy * dy <= Knob * Knob) inside = shape[side] > 0;
            }

            mask[lx, ly] = inside;
        }

        return mask;
    }

    private static bool IsEdge(bool[,] mask, int lx, int ly)
    {
        return !InMask(mask, lx - 1, ly) || !InMask(mask, lx + 1, ly) ||
               !InMask(mask, lx, ly - 1) || !InMask(mask, lx, ly + 1);
    }

    private static bool InMask(bool[,] mask, int lx, int ly)
    {
        return lx is >= 0 and < PieceSize && ly is >= 0 and < PieceSize && mask[lx, ly];
    }

    private static void DrawLine(byte[] pixels, int x0, int y0, int x1, int y1, int thickness,
        ReadOnlySpan<byte> color, float opacity)
    {
        var steps = Math.Max(Math.Abs(x1 - x0), Math.Abs(y1 - y0));
        for (var i = 0; i <= steps; i++)
        {
            var t = steps == 0 ? 0 : i / (float)steps;
            var cx = (int)(x0 + (x1 - x0) * t);
            var cy = (int)(y0 + (y1 - y0) * t);
            for (var y = cy; y < cy + thickness && y < Height; y++)
            for (var x = cx; x < cx + thickness && x < Width; x++)
                BlendPixel(pixels, x, y, color, opacity);
        }
    }

    private static void BlendPixel(byte[] pixels, int x, int y, ReadOnlySpan<byte> color, float opacity)
    {
        var offset = (y * Width + x) * 3;
        for (var c = 0; c < 3; c++)
            pixels[offset + c] = Blend(pixels[offset + c], color[c], opacity);
    }

    private static void AddNoise(byte[] pixels, Random random, int amplitude)
    {
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = (byte)Math.Clamp(pixels[i] + random.Next(-amplitude, amplitude + 1), 0, 255);
    }

    private static void RandomColor(Random random, Span<byte> color, int min, int max)
    {
        for (var c = 0; c < 3; c++) color[c] = (byte)random.Next(min, max);
    }

    private static byte Blend(byte value, byte overlay, float opacity)
    {
        return (byte)(value + (overlay - value) * opacity);
    }

    private static void Purge<T>(ConcurrentDictionary<string, T> entries, long now, Func<T, long> expiresAt)
    {
        foreach (var (key, value) in entries)
            if (expiresAt(value) < now)
                entries.TryRemove(key, out _);
    }

    private readonly record struct PendingChallenge(int Target, string Ip, long IssuedAt, long ExpiresAt);

    private readonly record struct PendingTicket(string Ip, long ExpiresAt);
}
