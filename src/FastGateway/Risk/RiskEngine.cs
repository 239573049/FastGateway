using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Core.Entities;
using FastGateway.Dto;
using FastGateway.Infrastructure;
using FastGateway.Risk.Model;

namespace FastGateway.Risk;

public enum RiskAction : byte
{
    Allow,
    Challenge,
    Block
}

public enum LoginOutcome : byte
{
    Unknown,
    Success,
    Failure
}

public readonly record struct RiskAssessment(int Score, string Reason);

/// <summary>
///     单次登录请求的客户端特征，只保存分类结果，不保存原始请求头
/// </summary>
public readonly record struct LoginSignals(
    string ClientKind,
    bool SecFetchPresent,
    bool AcceptLanguagePresent,
    bool NonBrowser);

/// <summary>
///     风险标记：封禁、账号被攻击等带有效期的结论
/// </summary>
public sealed record RiskTag(
    string Id,
    string Kind,
    string SubjectType,
    string Subject,
    string? Host,
    string Reason,
    int Score,
    long CreatedAtTicks,
    long ExpiresAtTicks)
{
    public RiskTagDto ToDto()
    {
        return new RiskTagDto
        {
            Id = Id,
            Kind = Kind,
            SubjectType = SubjectType,
            Subject = Subject,
            Host = Host,
            Reason = Reason,
            Score = Score,
            CreatedAt = new DateTime(CreatedAtTicks, DateTimeKind.Utc).ToLocalTime(),
            ExpiresAt = new DateTime(ExpiresAtTicks, DateTimeKind.Utc).ToLocalTime()
        };
    }
}

/// <summary>
///     路由风控配置的运行时形态：规则的方法与路径已规范化，便于每请求匹配
/// </summary>
public sealed class CompiledRiskProtection
{
    private readonly (string Method, string Path, LoginEndpointRule Rule)[] _rules;

    public CompiledRiskProtection(RiskProtection protection)
    {
        BlockScope = protection.BlockScope;
        _rules = protection.LoginEndpoints
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .Select(x => (x.Method.Trim().ToUpperInvariant(), NormalizePath(x.Path), x))
            .ToArray();
    }

    public RiskBlockScope BlockScope { get; }

    public LoginEndpointRule? FindRule(string method, string? path)
    {
        if (_rules.Length == 0) return null;

        // 忽略大小写与末尾斜杠：上游多数框架如此路由，严格匹配会让 /API/Login 之类的写法绕过保护
        var normalized = NormalizePath(path);
        foreach (var (ruleMethod, rulePath, rule) in _rules)
        {
            if (string.Equals(ruleMethod, method, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(rulePath, normalized, StringComparison.OrdinalIgnoreCase))
                return rule;
        }

        return null;
    }

    public static string NormalizePath(string? path)
    {
        if (string.IsNullOrEmpty(path)) return "/";
        path = path.Trim();
        if (!path.StartsWith('/')) path = "/" + path;
        return path.Length > 1 ? path.TrimEnd('/') : path;
    }
}

/// <summary>
///     登录风控引擎：按 IP、子网、账号统计登录失败，规则打分后决定放行、要求验证或封禁。
///     全部状态在内存中，每个节点独立；网关子应用与管理 API 共用 <see cref="Shared" /> 实例。
/// </summary>
public sealed class RiskEngine
{
    public static RiskEngine Shared { get; } = new(TimeProvider.System);

    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private static readonly TimeSpan LoginWindow = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan AccountWindow = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan BlockHistoryWindow = TimeSpan.FromHours(24);
    private static readonly TimeSpan AccountTagLifetime = TimeSpan.FromHours(1);

    // 24 小时内第 1、2、3+ 次封禁的时长
    private static readonly TimeSpan[] BlockDurations =
        [TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(24)];

    private const int AccountUnderAttackIps = 10;

    // 模型只评估规则拿不准的来源：至少几次登录尝试、规则分在验证档附近，同一来源 5 分钟内只评估一次
    private const int ModelMinAttempts = 3;
    private const int ModelGrayMargin = 10;
    private static readonly TimeSpan ModelEvaluationInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan ModelTagLifetime = TimeSpan.FromMinutes(10);
    private const int DistinctCapacity = 64;

    // 大规模分布式爆破时字典随来源数增长：满员先淘汰空闲条目，仍满则不再追踪新来源（内存优先）
    private const int MaxEntries = 100_000;
    private const int CleanupEveryRecords = 4096; // 必须是 2 的幂
    private const int MaxDecisions = 500;

    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<string, CompiledRiskProtection> _routes = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, IpState> _ips = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, SubnetState> _subnets = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, AccountState> _accounts = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, BlockHistory> _blockHistory = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, RiskTag> _tags = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, QuotaEntry> _clearanceQuotas = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, long> _consumedChallenges = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<RiskDecisionDto> _decisions = new();
    private byte[]? _signingKey;
    private RiskSettings _settings = RiskSettings.Default;
    private int _recordCounter;
    private volatile bool _modelEnabled;
    private volatile IRiskModelSink? _modelSink;

    public RiskEngine(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
    }

    private long Now => _timeProvider.GetUtcNow().UtcTicks;

    #region 路由配置

    public void SetRouteProtection(string routeId, RiskProtection? protection)
    {
        if (protection is { Enable: true, LoginEndpoints.Count: > 0 })
            _routes[routeId] = new CompiledRiskProtection(protection);
        else
            _routes.TryRemove(routeId, out _);
    }

    public CompiledRiskProtection? GetRouteProtection(string routeId)
    {
        return _routes.GetValueOrDefault(routeId);
    }

    #endregion

    #region 参数与签名密钥

    public RiskSettings Settings => Volatile.Read(ref _settings);

    public void UpdateSettings(RiskSettings settings)
    {
        Volatile.Write(ref _settings, settings);
    }

    public bool HasSigningKey => Volatile.Read(ref _signingKey) != null;

    public void SetSigningKey(byte[]? key)
    {
        Volatile.Write(ref _signingKey, key);
    }

    /// <summary>
    ///     账号只以带密钥的哈希参与统计，内存与界面中不出现明文用户名；无密钥时不按账号统计
    /// </summary>
    public string? HashAccount(string routeId, string username)
    {
        var key = Volatile.Read(ref _signingKey);
        var normalized = username.Trim().ToLowerInvariant();
        if (key == null || normalized.Length == 0) return null;

        var hash = HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(routeId + "\n" + normalized));
        return $"{routeId}:{Convert.ToHexStringLower(hash.AsSpan(0, 12))}";
    }

    /// <summary>
    ///     账号的脱敏提示，便于管理员辨认被攻击的账号（例如 a***n）
    /// </summary>
    public static string MaskUsername(string username)
    {
        var value = username.Trim();
        return value.Length switch
        {
            0 => string.Empty,
            <= 2 => value[0] + "*",
            _ => $"{value[0]}***{value[^1]}"
        };
    }

    #endregion

    #region 评分与决策

    /// <param name="includeModel">是否叠加模型标记的分数；判断是否需要送模型评估时只看规则分</param>
    public RiskAssessment Assess(string ip, string? accountKey, bool nonBrowser, bool includeModel = true)
    {
        var now = Now;
        var score = 0;
        var reasons = new List<string>(4);

        if (_ips.TryGetValue(ip, out var ipState))
        {
            var failures = ipState.Failures.Count(now);
            if (failures >= 50) Add(90, $"IP 10 分钟内登录失败 {failures} 次");
            else if (failures >= 20) Add(60, $"IP 10 分钟内登录失败 {failures} 次");
            else if (failures >= 5) Add(30, $"IP 10 分钟内登录失败 {failures} 次");

            var accounts = ipState.Accounts.Count(now);
            if (accounts >= 15) Add(70, $"IP 10 分钟内尝试了 {accounts} 个账号");
            else if (accounts >= 5) Add(40, $"IP 10 分钟内尝试了 {accounts} 个账号");
        }

        var subnet = IpSubnet.GetKey(ip);
        if (subnet != null && _subnets.TryGetValue(subnet, out var subnetState))
        {
            var failures = subnetState.Failures.Count(now);
            if (failures >= 30) Add(30, $"网段 {subnet} 10 分钟内登录失败 {failures} 次");
        }

        if (nonBrowser) Add(20, "非浏览器客户端");

        if (_blockHistory.TryGetValue(ip, out var history) && history.Count(now, BlockHistoryWindow.Ticks) > 0)
            Add(30, "24 小时内被封禁过");

        var settings = Settings;
        if (includeModel && _tags.TryGetValue(ModelTagId(ip), out var modelTag) && modelTag.ExpiresAtTicks > now)
        {
            // 模型的概率校准只对一批预测整体成立：规则分未达强验证档时，模型最多把来源推到封禁线以下
            var ruleScore = score;
            Add(modelTag.Score, modelTag.Reason);
            if (ruleScore < settings.StrongChallengeThreshold) score = Math.Min(score, settings.BlockThreshold - 1);
        }

        var challengeThreshold = settings.ChallengeThreshold;
        if (accountKey != null && _tags.TryGetValue(AccountTagId(accountKey), out var accountTag) &&
            accountTag.ExpiresAtTicks > now && score < challengeThreshold)
        {
            reasons.Add("账号正遭受分布式爆破");
            score = challengeThreshold;
        }

        return new RiskAssessment(score, string.Join("；", reasons));

        void Add(int points, string reason)
        {
            score += points;
            reasons.Add(reason);
        }
    }

    public RiskAction Decide(int score, bool challengeAvailable)
    {
        var settings = Settings;
        if (score >= settings.BlockThreshold) return RiskAction.Block;
        if (score < settings.ChallengeThreshold) return RiskAction.Allow;
        if (challengeAvailable) return RiskAction.Challenge;

        // 没有可用的验证方式（从节点尚未收到签名密钥）：强验证档直接封禁，中等档只靠失败延迟
        return score >= settings.StrongChallengeThreshold ? RiskAction.Block : RiskAction.Allow;
    }

    /// <summary>
    ///     按风险档位选择验证方式：首选 Turnstile 但当前不可用时回落为 PoW，高风险的 PoW 加难
    /// </summary>
    public (string Provider, int Difficulty) ChooseChallenge(int score, bool turnstileAvailable)
    {
        var settings = Settings;
        var strong = score >= settings.StrongChallengeThreshold;
        var preferred = strong ? settings.StrongProvider : settings.MediumProvider;
        if (preferred == RiskSettings.ProviderTurnstile && turnstileAvailable)
            return (RiskSettings.ProviderTurnstile, 0);

        return (RiskSettings.ProviderPow, strong ? settings.StrongPowDifficulty : settings.PowDifficulty);
    }

    /// <summary>
    ///     记录一次登录结果，返回该 IP 窗口内的失败次数（用于渐进延迟）
    /// </summary>
    public int RecordLoginResult(string ip, string? accountKey, string? accountHint, LoginOutcome outcome,
        string host, LoginSignals signals = default)
    {
        var now = Now;
        if ((Interlocked.Increment(ref _recordCounter) & (CleanupEveryRecords - 1)) == 0) Cleanup(now);

        var ipState = GetOrCreate(_ips, ip, static () => new IpState(), now);
        ipState?.Touch(now);
        ipState?.RecordAttempt(now);
        if (outcome == LoginOutcome.Success) ipState?.MarkSuccess(now);

        var failures = 0;
        if (outcome == LoginOutcome.Failure) failures = RecordFailure(ip, ipState, accountKey, accountHint, host, now);

        TryQueueForModel(ip, host, ipState, signals, now);
        return failures;
    }

    private int RecordFailure(string ip, IpState? ipState, string? accountKey, string? accountHint, string host,
        long now)
    {
        var failures = 0;
        if (ipState != null)
        {
            failures = ipState.Failures.Add(now);
            if (accountKey != null) ipState.Accounts.Add(accountKey, now);
        }

        var subnet = IpSubnet.GetKey(ip);
        if (subnet != null)
        {
            var subnetState = GetOrCreate(_subnets, subnet, static () => new SubnetState(), now);
            if (subnetState != null)
            {
                subnetState.Touch(now);
                subnetState.Failures.Add(now);
            }
        }

        if (accountKey != null)
        {
            var accountState = GetOrCreate(_accounts, accountKey, static () => new AccountState(), now);
            if (accountState != null)
            {
                accountState.Touch(now);
                var ips = accountState.FailingIps.Add(ip, now);
                if (ips >= AccountUnderAttackIps) TagAccountUnderAttack(accountKey, accountHint, host, ips, now);
            }
        }

        return failures;
    }

    /// <summary>
    ///     IP 处于封禁期时返回剩余时长，否则返回 null
    /// </summary>
    public TimeSpan? GetBlockRemaining(string ip)
    {
        if (!_tags.TryGetValue(BlockTagId(ip), out var tag)) return null;

        var remaining = tag.ExpiresAtTicks - Now;
        return remaining > 0 ? TimeSpan.FromTicks(remaining) : null;
    }

    /// <summary>
    ///     封禁 IP：时长按 24 小时内的封禁次数逐级升级；已在封禁期内则沿用现有封禁。返回剩余封禁时长
    /// </summary>
    public TimeSpan BlockIp(string ip, RiskAssessment assessment, string host, string path)
    {
        if (GetBlockRemaining(ip) is { } existing) return existing;

        var now = Now;
        var history = GetOrCreate(_blockHistory, ip, static () => new BlockHistory(), now);
        var previous = history?.Record(now, BlockHistoryWindow.Ticks) - 1 ?? 0;
        var duration = BlockDurations[Math.Min(previous, BlockDurations.Length - 1)];

        var tag = new RiskTag(BlockTagId(ip), "block", "ip", ip, host, assessment.Reason, assessment.Score,
            now, now + duration.Ticks);
        _tags[tag.Id] = tag;
        RecordDecision(ip, host, path, RiskAction.Block, assessment);
        return duration;
    }

    public IReadOnlyList<RiskTagDto> GetActiveTags()
    {
        var now = Now;
        return _tags.Values
            .Where(x => x.ExpiresAtTicks > now)
            .OrderByDescending(x => x.CreatedAtTicks)
            .Select(x => x.ToDto())
            .ToList();
    }

    public bool RemoveTag(string id)
    {
        if (!_tags.TryRemove(id, out var tag)) return false;

        // 手动解除视为误判：同时清空失败计数、封禁历史与模型标记，否则下一次登录请求会立即再次触发
        if (tag.SubjectType == "ip")
        {
            _ips.TryRemove(tag.Subject, out _);
            _blockHistory.TryRemove(tag.Subject, out _);
            _tags.TryRemove(ModelTagId(tag.Subject), out _);
        }

        return true;
    }

    public void RecordDecision(string ip, string host, string path, RiskAction action, RiskAssessment assessment)
    {
        _decisions.Enqueue(new RiskDecisionDto
        {
            Time = ToLocal(Now),
            Ip = ip,
            Host = host,
            Path = path,
            Action = action == RiskAction.Block ? "block" : "challenge",
            Score = assessment.Score,
            Reason = assessment.Reason
        });

        while (_decisions.Count > MaxDecisions) _decisions.TryDequeue(out _);
    }

    public IReadOnlyList<RiskDecisionDto> GetRecentDecisions()
    {
        return _decisions.Reverse().ToList();
    }

    #region 模型

    public bool ModelEnabled
    {
        get => _modelEnabled;
        set => _modelEnabled = value;
    }

    public IRiskModelSink? ModelSink
    {
        get => _modelSink;
        set => _modelSink = value;
    }

    /// <summary>
    ///     模型判定为可疑：写入带有效期的标记，在 <see cref="Assess" /> 中与规则分叠加
    /// </summary>
    public void ApplyModelScore(string ip, string? host, int score, string reason)
    {
        var now = Now;
        var id = ModelTagId(ip);
        _tags[id] = new RiskTag(id, "model_suspect", "ip", ip, host, reason, score, now, now + ModelTagLifetime.Ticks);
    }

    public void RecordChallengePassed(string ip)
    {
        if (_ips.TryGetValue(ip, out var ipState)) ipState.MarkChallengePassed(Now);
    }

    /// <summary>
    ///     评估之后该来源是否登录成功、是否通过验证，用于对比模型判断与实际结果
    /// </summary>
    public (bool LoginSucceeded, bool ChallengePassed) GetFollowUp(string ip, long sinceTicks)
    {
        return _ips.TryGetValue(ip, out var ipState)
            ? (ipState.LastSuccess > sinceTicks, ipState.LastChallengePassed > sinceTicks)
            : (false, false);
    }

    private void TryQueueForModel(string ip, string host, IpState? ipState, LoginSignals signals, long now)
    {
        var sink = _modelSink;
        if (!_modelEnabled || sink == null || ipState == null) return;
        if (ipState.Attempts.Count(now) < ModelMinAttempts || GetBlockRemaining(ip) != null) return;

        var settings = Settings;
        var ruleScore = Assess(ip, null, signals.NonBrowser, includeModel: false).Score;
        if (ruleScore < settings.ChallengeThreshold - ModelGrayMargin ||
            ruleScore > settings.StrongChallengeThreshold + ModelGrayMargin)
            return;

        if (!ipState.TryMarkModelEvaluation(now, ModelEvaluationInterval.Ticks)) return;
        sink.TryEnqueue(new RiskModelSubject(ip, host, BuildFeatures(ip, ipState, signals, ruleScore, now), now));
    }

    private RiskFeatureState BuildFeatures(string ip, IpState ipState, LoginSignals signals, int ruleScore,
        long now)
    {
        var attempts = ipState.Attempts.Count(now);
        var failures = ipState.Failures.Count(now);
        var (median, stddev) = ipState.GetIntervalStats(now, LoginWindow.Ticks);
        var subnet = IpSubnet.GetKey(ip);

        return new RiskFeatureState
        {
            WindowMinutes = (int)LoginWindow.TotalMinutes,
            LoginAttempts = attempts,
            LoginFailures = failures,
            FailureRatio = attempts == 0 ? 0 : Math.Round((double)failures / attempts, 2),
            DistinctAccounts = ipState.Accounts.Count(now),
            SubnetFailures = subnet != null && _subnets.TryGetValue(subnet, out var subnetState)
                ? subnetState.Failures.Count(now)
                : 0,
            MedianIntervalMs = median,
            IntervalStddevMs = stddev,
            ClientKind = signals.ClientKind ?? ClientKinds.Unknown,
            SecFetchPresent = signals.SecFetchPresent,
            AcceptLanguagePresent = signals.AcceptLanguagePresent,
            NetworkType = NetworkTypes.Resolve(ip),
            PreviousBlocks24h = _blockHistory.TryGetValue(ip, out var history)
                ? history.Count(now, BlockHistoryWindow.Ticks)
                : 0,
            RuleScore = ruleScore
        };
    }

    #endregion

    private void TagAccountUnderAttack(string accountKey, string? accountHint, string host, int ips, long now)
    {
        var id = AccountTagId(accountKey);
        if (_tags.TryGetValue(id, out var existing) && existing.ExpiresAtTicks > now) return;

        _tags[id] = new RiskTag(id, "account_under_attack", "account",
            string.IsNullOrEmpty(accountHint) ? "（未知账号）" : accountHint, host,
            $"30 分钟内有 {ips} 个 IP 登录该账号失败，该账号的登录一律要求验证",
            Settings.ChallengeThreshold, now, now + AccountTagLifetime.Ticks);
    }

    #endregion

    #region 验证状态与 clearance

    public string? CreateChallengeState(string host, string routeId, string returnUrl, string ip,
        string provider, int difficulty)
    {
        var key = Volatile.Read(ref _signingKey);
        if (key == null) return null;

        var now = UnixSeconds();
        return SignedToken.Sign(new RiskChallengeState
        {
            Host = NormalizeHost(host),
            RouteId = routeId,
            ReturnUrl = IsSafeReturnUrl(returnUrl) ? returnUrl : "/",
            Ip = ip,
            Provider = provider,
            Difficulty = difficulty,
            IssuedAt = now,
            ExpiresAt = now + (long)ChallengeLifetime.TotalSeconds,
            Nonce = SignedToken.CreateNonce()
        }, AppJsonContext.Default.RiskChallengeState, key);
    }

    public bool TryReadChallengeState(string? encoded, string host, string ip, out RiskChallengeState state)
    {
        state = new RiskChallengeState();
        var key = Volatile.Read(ref _signingKey);
        if (key == null || !SignedToken.TryRead(encoded, AppJsonContext.Default.RiskChallengeState, key, out state))
            return false;

        var now = UnixSeconds();
        return state.IssuedAt <= now + 60 &&
               state.ExpiresAt >= now &&
               !string.IsNullOrEmpty(state.Nonce) &&
               !string.IsNullOrEmpty(state.RouteId) &&
               string.Equals(state.Host, NormalizeHost(host), StringComparison.Ordinal) &&
               string.Equals(state.Ip, ip, StringComparison.Ordinal) &&
               IsSafeReturnUrl(state.ReturnUrl);
    }

    /// <summary>
    ///     校验 PoW 解并消费挑战状态：用于管理后台登录这类不经过 clearance、每次尝试都要新解的场景
    /// </summary>
    public bool TryRedeemPow(string? encodedState, string? solution, string host, string ip, string routeId)
    {
        return TryReadChallengeState(encodedState, host, ip, out var state) &&
               state.Provider == RiskSettings.ProviderPow &&
               string.Equals(state.RouteId, routeId, StringComparison.Ordinal) &&
               ProofOfWork.Verify(state.Nonce, solution, state.Difficulty) &&
               TryConsumeChallengeState(state);
    }

    /// <summary>
    ///     挑战状态只能兑换一次 clearance，防止一次验证结果被重放
    /// </summary>
    public bool TryConsumeChallengeState(RiskChallengeState state)
    {
        var now = UnixSeconds();
        foreach (var (nonce, expiresAt) in _consumedChallenges)
        {
            if (expiresAt < now) _consumedChallenges.TryRemove(nonce, out _);
        }

        return _consumedChallenges.TryAdd(state.Nonce, state.ExpiresAt);
    }

    public string? IssueClearance(string host, string routeId, string ip)
    {
        var key = Volatile.Read(ref _signingKey);
        if (key == null) return null;

        var now = UnixSeconds();
        var settings = Settings;
        var payload = new RiskClearancePayload
        {
            Host = NormalizeHost(host),
            RouteId = routeId,
            Ip = ip,
            IssuedAt = now,
            ExpiresAt = now + settings.ClearanceMinutes * 60L,
            Nonce = SignedToken.CreateNonce()
        };
        _clearanceQuotas[payload.Nonce] = new QuotaEntry(settings.ClearanceQuota, payload.ExpiresAt);
        return SignedToken.Sign(payload, AppJsonContext.Default.RiskClearancePayload, key);
    }

    /// <summary>
    ///     校验 clearance 并按需扣减一次登录额度。额度是节点本地状态：本节点没有记录（重启或集群中由
    ///     其他节点签发）时按满额度起算，最坏情况是额度乘以节点数，可以接受
    /// </summary>
    public bool TryUseClearance(string? encoded, string host, string routeId, string ip, bool consume)
    {
        var key = Volatile.Read(ref _signingKey);
        if (key == null ||
            !SignedToken.TryRead(encoded, AppJsonContext.Default.RiskClearancePayload, key, out var payload))
            return false;

        var now = UnixSeconds();
        if (payload.IssuedAt > now + 60 || payload.ExpiresAt < now ||
            string.IsNullOrEmpty(payload.Nonce) ||
            !string.Equals(payload.Host, NormalizeHost(host), StringComparison.Ordinal) ||
            !string.Equals(payload.RouteId, routeId, StringComparison.Ordinal) ||
            !string.Equals(payload.Ip, ip, StringComparison.Ordinal))
            return false;

        var entry = _clearanceQuotas.GetOrAdd(payload.Nonce,
            static (_, state) => new QuotaEntry(state.Quota, state.ExpiresAt),
            (Quota: Settings.ClearanceQuota, payload.ExpiresAt));
        return consume ? entry.TryConsume() : entry.Remaining > 0;
    }

    #endregion

    #region 内部

    public static string BlockTagId(string ip) => "block:ip:" + ip;

    public static string ModelTagId(string ip) => "model:ip:" + ip;

    private static string AccountTagId(string accountKey) => "attack:account:" + accountKey;

    private long UnixSeconds()
    {
        return (Now - DateTimeOffset.UnixEpoch.UtcTicks) / TimeSpan.TicksPerSecond;
    }

    private static DateTime ToLocal(long utcTicks)
    {
        return new DateTime(utcTicks, DateTimeKind.Utc).ToLocalTime();
    }

    private static string NormalizeHost(string host)
    {
        return host.Trim().TrimEnd('.').ToLowerInvariant();
    }

    private static bool IsSafeReturnUrl(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.StartsWith('/') &&
               !value.StartsWith("//", StringComparison.Ordinal) &&
               !value.Contains('\\');
    }

    private T? GetOrCreate<T>(ConcurrentDictionary<string, T> entries, string key, Func<T> factory, long now)
        where T : class
    {
        if (entries.TryGetValue(key, out var existing)) return existing;

        if (entries.Count >= MaxEntries)
        {
            Cleanup(now);
            if (entries.Count >= MaxEntries) return null;
        }

        return entries.GetOrAdd(key, static (_, create) => create(), factory);
    }

    private void Cleanup(long now)
    {
        Evict(_ips, x => x.IsIdle(now, LoginWindow.Ticks));
        Evict(_subnets, x => x.IsIdle(now, LoginWindow.Ticks));
        Evict(_accounts, x => x.IsIdle(now, AccountWindow.Ticks));
        Evict(_blockHistory, x => x.Count(now, BlockHistoryWindow.Ticks) == 0);

        Evict(_tags, x => x.ExpiresAtTicks <= now);

        var unixNow = UnixSeconds();
        Evict(_clearanceQuotas, x => x.ExpiresAt < unixNow);
    }

    private static void Evict<T>(ConcurrentDictionary<string, T> entries, Func<T, bool> shouldEvict)
    {
        foreach (var (key, value) in entries)
        {
            if (shouldEvict(value)) entries.TryRemove(key, out _);
        }
    }

    private abstract class ActivityState
    {
        private long _lastActivity;

        public void Touch(long now) => Volatile.Write(ref _lastActivity, now);

        public bool IsIdle(long now, long windowTicks) => now - Volatile.Read(ref _lastActivity) >= windowTicks;
    }

    private sealed class IpState : ActivityState
    {
        private const int MaxAttemptTimes = 32;

        private readonly Lock _sync = new();
        private readonly long[] _attemptTimes = new long[MaxAttemptTimes];
        private int _nextAttempt;
        private long _lastSuccess;
        private long _lastChallengePassed;
        private long _lastModelEvaluation;

        public SlidingWindowCounter Failures { get; } = new(LoginWindow, 10);
        public SlidingWindowCounter Attempts { get; } = new(LoginWindow, 10);
        public DistinctWindowSet Accounts { get; } = new(LoginWindow, DistinctCapacity);

        public long LastSuccess => Volatile.Read(ref _lastSuccess);
        public long LastChallengePassed => Volatile.Read(ref _lastChallengePassed);

        public void RecordAttempt(long now)
        {
            Attempts.Add(now);
            lock (_sync)
            {
                _attemptTimes[_nextAttempt] = now;
                _nextAttempt = (_nextAttempt + 1) % MaxAttemptTimes;
            }
        }

        public void MarkSuccess(long now) => Volatile.Write(ref _lastSuccess, now);

        public void MarkChallengePassed(long now) => Volatile.Write(ref _lastChallengePassed, now);

        public bool TryMarkModelEvaluation(long now, long intervalTicks)
        {
            var last = Volatile.Read(ref _lastModelEvaluation);
            return now - last >= intervalTicks &&
                   Interlocked.CompareExchange(ref _lastModelEvaluation, now, last) == last;
        }

        /// <summary>
        ///     窗口内相邻登录尝试间隔的中位数与标准差（毫秒）；脚本的间隔通常非常均匀
        /// </summary>
        public (long? Median, long? Stddev) GetIntervalStats(long now, long windowTicks)
        {
            long[] times;
            lock (_sync)
            {
                times = _attemptTimes.Where(x => x != 0 && now - x < windowTicks).Order().ToArray();
            }

            if (times.Length < 3) return (null, null);

            var intervals = new double[times.Length - 1];
            for (var i = 1; i < times.Length; i++)
                intervals[i - 1] = (times[i] - times[i - 1]) / (double)TimeSpan.TicksPerMillisecond;

            var sorted = intervals.Order().ToArray();
            var median = sorted.Length % 2 == 1
                ? sorted[sorted.Length / 2]
                : (sorted[sorted.Length / 2 - 1] + sorted[sorted.Length / 2]) / 2;
            var mean = intervals.Average();
            var stddev = Math.Sqrt(intervals.Average(x => (x - mean) * (x - mean)));
            return ((long)Math.Round(median), (long)Math.Round(stddev));
        }
    }

    private sealed class SubnetState : ActivityState
    {
        public SlidingWindowCounter Failures { get; } = new(LoginWindow, 10);
    }

    private sealed class AccountState : ActivityState
    {
        public DistinctWindowSet FailingIps { get; } = new(AccountWindow, DistinctCapacity);
    }

    private sealed class BlockHistory
    {
        private readonly Lock _sync = new();
        private readonly List<long> _blocks = new(BlockDurations.Length);

        /// <summary>记录一次封禁，返回窗口内的封禁次数（含本次）</summary>
        public int Record(long now, long windowTicks)
        {
            lock (_sync)
            {
                _blocks.RemoveAll(x => now - x >= windowTicks);
                _blocks.Add(now);
                if (_blocks.Count > BlockDurations.Length) _blocks.RemoveAt(0);
                return _blocks.Count;
            }
        }

        public int Count(long now, long windowTicks)
        {
            lock (_sync)
            {
                return _blocks.Count(x => now - x < windowTicks);
            }
        }
    }

    private sealed class QuotaEntry(int remaining, long expiresAt)
    {
        private int _remaining = remaining;

        public long ExpiresAt { get; } = expiresAt;

        public int Remaining => Volatile.Read(ref _remaining);

        public bool TryConsume()
        {
            while (true)
            {
                var current = Volatile.Read(ref _remaining);
                if (current <= 0) return false;
                if (Interlocked.CompareExchange(ref _remaining, current - 1, current) == current) return true;
            }
        }
    }

    #endregion
}
