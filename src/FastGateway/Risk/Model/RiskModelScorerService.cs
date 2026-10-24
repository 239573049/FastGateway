using System.Collections.Concurrent;
using System.Threading.Channels;

namespace FastGateway.Risk.Model;

/// <summary>
///     近线模型评估：从队列取出规则拿不准的来源，调用模型后按运行模式记录或写入风险标记。
///     不在请求路径上；任何失败都只记录，风控回落到规则
/// </summary>
public sealed class RiskModelScorerService(JevClient client, ILogger<RiskModelScorerService> logger)
    : BackgroundService, IRiskModelSink
{
    public const string QuestionAutomated = "automated";
    public const string QuestionCredentialStuffing = "credential_stuffing";
    public const string QuestionPasswordGuessing = "password_guessing";

    private const int QueueCapacity = 1024;
    private const int MaxEvaluations = 1000;
    private static readonly TimeSpan StatsWindow = TimeSpan.FromMinutes(5);

    /// <summary>原子问题：模型只判断事实，处置由规则与 <see cref="RiskModelScoring" /> 组合决定</summary>
    public static readonly IReadOnlyDictionary<string, string> Questions = new Dictionary<string, string>
    {
        [QuestionAutomated] =
            "The login traffic described by these statistics is produced by an automated program rather than a person using a web browser.",
        [QuestionCredentialStuffing] =
            "The client is testing leaked username and password pairs across many different accounts.",
        [QuestionPasswordGuessing] =
            "The client is repeatedly guessing passwords for a small number of accounts."
    };

    private readonly Channel<RiskModelSubject> _queue = Channel.CreateBounded<RiskModelSubject>(
        new BoundedChannelOptions(QueueCapacity) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    private readonly ConcurrentQueue<Evaluation> _evaluations = new();
    private readonly SlidingWindowCounter _recentCalls = new(StatsWindow, 5);
    private readonly SlidingWindowCounter _recentFailures = new(StatsWindow, 5);
    private long _dropped;
    private long _skipped;
    private string? _lastError;
    private DateTimeOffset? _lastErrorAt;
    private DateTimeOffset? _lastSuccessAt;

    public RiskModelGate Gate { get; } = new(TimeProvider.System);

    public bool TryEnqueue(RiskModelSubject subject)
    {
        if (_queue.Writer.TryWrite(subject)) return true;

        Interlocked.Increment(ref _dropped);
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        RiskEngine.Shared.ModelSink = this;
        try
        {
            await foreach (var subject in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                var settings = RiskModelProviderStore.Current;
                if (!settings.IsUsable) continue;

                if (!Gate.TryAcquire(settings, out _))
                {
                    Interlocked.Increment(ref _skipped);
                    continue;
                }

                // 并发由 Gate 限制；评估在后台进行，不阻塞队列读取
                _ = EvaluateAsync(subject, settings, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            RiskEngine.Shared.ModelSink = null;
        }
    }

    private async Task EvaluateAsync(RiskModelSubject subject, RiskModelProviderSettings settings,
        CancellationToken cancellationToken)
    {
        JevResult result;
        try
        {
            result = await client.EvaluateAsync(settings, CreateRequest(settings.Model, subject.Features),
                cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "风控模型评估异常");
            result = new JevResult(JevErrorKind.BadResponse, "评估异常", null, 0);
        }
        finally
        {
            Gate.Release();
        }

        var now = DateTimeOffset.UtcNow;
        _recentCalls.Add(now.UtcTicks);

        var answers = result.Response?.Answers;
        var automated = Probability(answers, QuestionAutomated);
        var stuffing = Probability(answers, QuestionCredentialStuffing);
        var guessing = Probability(answers, QuestionPasswordGuessing);
        if (result.Success && (automated == null || stuffing == null || guessing == null))
            result = result with { Error = JevErrorKind.BadResponse, Message = "响应缺少问题的答案" };

        if (!result.Success)
        {
            Gate.RecordFailure(result);
            _recentFailures.Add(now.UtcTicks);
            _lastError = result.Message;
            _lastErrorAt = now;
            Record(subject, settings, result, null, false);
            return;
        }

        Gate.RecordSuccess(result.Response!.Usage?.InputTokens ?? 0);
        _lastSuccessAt = now;

        var score = RiskModelScoring.Combine(automated!.Value, stuffing!.Value, guessing!.Value);
        var applied = settings.Mode == RiskModelMode.Enforce && score >= 5;
        if (applied)
            RiskEngine.Shared.ApplyModelScore(subject.Ip, subject.Host, score,
                $"AI 模型判定可疑（脚本 {automated:P0}，撞库 {stuffing:P0}，猜密码 {guessing:P0}）");

        Record(subject, settings, result, (automated.Value, stuffing.Value, guessing.Value, score), applied);
    }

    public static JevRequest CreateRequest(string model, RiskFeatureState state)
    {
        return new JevRequest
        {
            Model = model,
            State = state,
            Questions = Questions.ToDictionary(x => x.Key,
                x => new JevQuestion { Type = "noul", Instructions = x.Value })
        };
    }

    public IReadOnlyList<RiskModelEvaluationDto> GetEvaluations()
    {
        return _evaluations.Reverse().Select(x =>
        {
            var (loginSucceeded, challengePassed) = RiskEngine.Shared.GetFollowUp(x.Ip, x.Time.UtcTicks);
            return new RiskModelEvaluationDto
            {
                Time = x.Time.LocalDateTime,
                Ip = x.Ip,
                Host = x.Host,
                Model = x.Model,
                Mode = x.Mode,
                Applied = x.Applied,
                Automated = x.Scores?.Automated,
                CredentialStuffing = x.Scores?.Stuffing,
                PasswordGuessing = x.Scores?.Guessing,
                ModelScore = x.Scores?.Score,
                RuleScore = x.Features.RuleScore,
                LatencyMs = x.LatencyMs,
                InputTokens = x.InputTokens,
                Error = x.Error,
                LaterLoginSucceeded = loginSucceeded,
                LaterChallengePassed = challengePassed,
                Features = x.Features
            };
        }).ToList();
    }

    public RiskModelStatusDto GetStatus()
    {
        var settings = RiskModelProviderStore.Current;
        var now = DateTimeOffset.UtcNow.UtcTicks;
        return new RiskModelStatusDto
        {
            Enabled = settings.Enabled,
            Usable = settings.IsUsable,
            Mode = settings.Mode,
            Model = settings.Model,
            TokensUsedToday = Gate.TokensUsedToday,
            DailyTokenBudget = settings.DailyTokenBudget,
            Calls5m = _recentCalls.Count(now),
            Failures5m = _recentFailures.Count(now),
            Dropped = Interlocked.Read(ref _dropped),
            Skipped = Interlocked.Read(ref _skipped),
            CircuitOpenUntil = Gate.CircuitOpenUntil?.LocalDateTime,
            BackoffUntil = Gate.BackoffUntil?.LocalDateTime,
            LastError = _lastError,
            LastErrorAt = _lastErrorAt?.LocalDateTime,
            LastSuccessAt = _lastSuccessAt?.LocalDateTime
        };
    }

    private static double? Probability(Dictionary<string, JevAnswer>? answers, string question)
    {
        return answers != null && answers.TryGetValue(question, out var answer) ? answer.Noul : null;
    }

    private void Record(RiskModelSubject subject, RiskModelProviderSettings settings, JevResult result,
        (double Automated, double Stuffing, double Guessing, int Score)? scores, bool applied)
    {
        _evaluations.Enqueue(new Evaluation(DateTimeOffset.UtcNow, subject.Ip, subject.Host,
            result.Response?.Model ?? settings.Model, settings.Mode, applied, scores, result.LatencyMs,
            result.Response?.Usage?.InputTokens, result.Success ? null : result.Message, subject.Features));
        while (_evaluations.Count > MaxEvaluations) _evaluations.TryDequeue(out _);
    }

    private sealed record Evaluation(
        DateTimeOffset Time,
        string Ip,
        string? Host,
        string Model,
        RiskModelMode Mode,
        bool Applied,
        (double Automated, double Stuffing, double Guessing, int Score)? Scores,
        long LatencyMs,
        long? InputTokens,
        string? Error,
        RiskFeatureState Features);
}

public sealed class RiskModelEvaluationDto
{
    public DateTime Time { get; set; }
    public string Ip { get; set; } = string.Empty;
    public string? Host { get; set; }
    public string Model { get; set; } = string.Empty;
    public RiskModelMode Mode { get; set; }

    /// <summary>是否写入了风险标记（仅 Enforce 模式）</summary>
    public bool Applied { get; set; }

    public double? Automated { get; set; }
    public double? CredentialStuffing { get; set; }
    public double? PasswordGuessing { get; set; }
    public int? ModelScore { get; set; }
    public int RuleScore { get; set; }
    public long LatencyMs { get; set; }
    public long? InputTokens { get; set; }
    public string? Error { get; set; }

    /// <summary>评估之后该来源是否登录成功（用于判断模型是否误判）</summary>
    public bool LaterLoginSucceeded { get; set; }

    public bool LaterChallengePassed { get; set; }
    public RiskFeatureState Features { get; set; } = new();
}

public sealed class RiskModelStatusDto
{
    public bool Enabled { get; set; }
    public bool Usable { get; set; }
    public RiskModelMode Mode { get; set; }
    public string Model { get; set; } = string.Empty;
    public long TokensUsedToday { get; set; }
    public long DailyTokenBudget { get; set; }
    public int Calls5m { get; set; }
    public int Failures5m { get; set; }

    /// <summary>队列满被丢弃的评估数</summary>
    public long Dropped { get; set; }

    /// <summary>因限流、并发、预算或熔断跳过的评估数</summary>
    public long Skipped { get; set; }

    public DateTime? CircuitOpenUntil { get; set; }
    public DateTime? BackoffUntil { get; set; }
    public string? LastError { get; set; }
    public DateTime? LastErrorAt { get; set; }
    public DateTime? LastSuccessAt { get; set; }
}

public sealed class RiskModelTestResult
{
    public bool Success { get; set; }
    public long LatencyMs { get; set; }
    public string? Model { get; set; }
    public double? Automated { get; set; }
    public string? Message { get; set; }
}
