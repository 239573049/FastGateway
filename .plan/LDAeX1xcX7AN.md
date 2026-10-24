# FastGateway 登录风控、自研人机验证与 Jev 模型接入实施计划

## 一、目标与边界

目标：

1. 识别针对登录接口的暴力破解、撞库和分布式爆破，按风险分级处置：放行 → 无感验证 → 强验证 → 临时封禁。
2. 自研 PoW 人机验证，不依赖第三方即可工作；Turnstile 保留为可选的强验证。
3. 接入 TypeSafe Jev 作为可选的近线风险评分模型；提供商地址、API Key、模型等在「系统设置」中配置和修改。
4. 同时覆盖两类登录：FastGateway 管理后台登录、网关代理的业务系统登录。

不做：

- 不锁定账号（否则攻击者可以借此让任意用户无法登录）。
- 不在请求热路径上同步调用外部模型。
- 不改变 TCP/UDP `StreamForward`。
- 不替代上游业务系统自身的认证逻辑。

前提假设（尚未确认，只影响优先级，不影响设计）：单节点和集群部署都要支持；管理后台和业务系统都可能被攻击。

## 二、现状结论

- 管理后台登录 `src/FastGateway/Services/AuthorizationService.cs:21-44`：单一共享密码，`==` 比较（:41），没有失败计数或限速；默认密码 `Aa123456`（`src/FastGateway/Options/FastGatewayOptions.cs:9`）；客户端 IP 取 `Connection.RemoteIpAddress`（:31），后台前面有反向代理时所有请求 IP 相同。
- 业务机器人保护 `src/FastGateway/Middleware/BotProtectionMiddleware.cs`：按 `DomainName.EnableBotProtection` 整域开/关，只支持 Turnstile；clearance 有效期内（默认 30 分钟）不限次数使用；配置来自 appsettings 的 `BotProtection` 节，`CookieSigningKey` 需手动配置。
- 网关管线 `src/FastGateway/Gateway/Gateway.cs:686-695`：限流 → 黑白名单 → BotProtection → 故障转移 → 代理错误 → 异常 IP 监控；`UseStatisticsCapture` 在更外层（:650）。
- 每个 Server 子应用有独立 DI 容器和独立的 `ConfigurationService` 实例（`Gateway.cs:444`）。跨 Server 共享的运行时状态沿用静态类模式（`AbnormalIpMonitor`、`ClientIpHelper`）。
- 转发前的内部 Cookie 剥离 `Gateway.cs:769-793`（`RemoveInternalRequestHeaders`）目前只剥离 `__FastGateway_BotClearance`。
- 系统设置：`GatewayConfig.Settings` 以明文 JSON 持久化在 `data/gateway.config`，通过 `ClusterHub`（`ExportSnapshot` → worker `ReplaceConfig`）同步到所有节点；`ConfigurationService.ConfigurationChanged` 静态事件在任何变更（包括 worker 应用快照）后触发。
- **`GET /api/v1/setting` 与 `GET /api/v1/setting/{key}`（`src/FastGateway/Services/SettingService.cs`）明文返回所有设置，`POST /api/v1/setting/{key}` 可以写任意键。** API Key 不能走这套通用接口。
- 前端系统设置页 `web/src/pages/system-setting/page.tsx` 目前只有「日志清理」一张卡片；`web/src/services/SettingService.ts` 只封装了通用 get/set。
- 统计拦截原因 `BlockReason`（`src/FastGateway/Services/Statistics/StatisticsCollector.cs:8`）已有 `BotChallenge = 5`。
- 安全页面 `web/src/pages/security/threats` 展示异常 IP，支持一键加入黑名单，交互可以复用。
- 测试项目 `tests/FastGateway.Tests` 已存在（`BotProtectionServiceTests.cs`）。

## 三、总体架构

```
                    ┌──────────────── 网关 Server 子应用（每个 Server 一份） ────────────────┐
请求 ─► 统计 ─► 限流 ─► 黑白名单 ─► RiskGuardMiddleware ─► BotProtection ─► … ─► YARP ─► 上游
                                       │  ▲  请求前：查标记、打分、决策
                                       │  │  响应前（OnStarting）：识别登录结果、剥离结果头、失败延迟
                                       ▼  │
                          RiskEngine（进程级静态单例，所有 Server 与管理后台共享）
                          ├─ 滑动窗口计数（IP / 子网 / 账号）
                          ├─ 风险标记 RiskTagStore（带 TTL）
                          └─ 规则评分 + 决策
                                       │ 灰区对象入队（有界 Channel）
                                       ▼
             主宿主 RiskModelScorerService（BackgroundService，读取系统设置中的提供商配置）
                          └─ TypeSafe Jev ─► 写回模型标记（Enforce）/ 只记录（Shadow）
```

新增功能目录 `src/FastGateway/Risk/`（与 `Cluster/`、`Tunnels/` 同级）：

| 文件 | 职责 |
|---|---|
| `RiskEngine.cs` | 计数、标记、评分、决策的静态入口 |
| `SlidingWindowCounter.cs` | 环形桶计数和有上限的去重计数（从 `AbnormalIpMonitor` 的桶逻辑抽取） |
| `RiskTagStore.cs` | 标记存储、TTL、封禁升级记录 |
| `RiskRules.cs` | 默认规则与分数 |
| `RiskGuardMiddleware.cs` | 网关登录端点保护 |
| `LoginOutcomeDetector.cs` | 登录结果识别 |
| `RiskClearance.cs` | 带次数额度的 clearance 签发、校验、扣减 |
| `Challenges/IChallengeProvider.cs`、`TurnstileChallengeProvider.cs`、`PowChallengeProvider.cs` | 人机验证提供方 |
| `Model/RiskModelProviderSettings.cs`、`Model/JevClient.cs`、`Model/RiskFeatureBuilder.cs`、`Model/RiskModelScorerService.cs` | Jev 接入 |
| `RiskSettingsService.cs` | 风控与模型提供商的管理 API（`/api/v1/risk`） |

## 四、配置模型

### 4.1 域名级：登录保护

在 `src/Core/Entities/` 新增以下类型（`[MessagePackObject(true)]`，并注册到 `ConfigJsonContext` / `AppJsonContext`）：

```csharp
public sealed class RiskProtection
{
    public bool Enable { get; set; }
    public RiskBlockScope BlockScope { get; set; } = RiskBlockScope.LoginOnly; // LoginOnly | WholeRoute
    public List<LoginEndpointRule> LoginEndpoints { get; set; } = new();
}

public sealed class LoginEndpointRule
{
    public string Method { get; set; } = "POST";
    public string Path { get; set; } = string.Empty;          // 请求路径精确匹配，不含 query
    public string? UsernameField { get; set; }                // JSON 顶层字段或 form 字段
    public int[] FailureStatusCodes { get; set; } = [401, 403];
    public string? ResultHeader { get; set; }                 // 例如 X-Auth-Result，值为 fail / success
}
```

- `DomainName` 新增 `RiskProtection? RiskProtection`，默认 null，兼容现有配置；`EnableBotProtection`（始终 Turnstile）保持原语义。
- `Gateway.BuildConfig()` 在路由 metadata 写入 `FastGateway.RiskRouteId`。登录端点规则不塞进 metadata 字符串，而是在构建配置时写入 `RiskEngine` 的 `routeId → LoginEndpointRule[]` 静态表，随 `ReloadGateway()` 原子替换。relay route 保持同样的 metadata，与 BotProtection 一样只在第一跳执行，受信中继跳过。
- 前端 `CreateDomain.tsx` / `UpdateDomain.tsx` 增加「登录保护」区：开关、封禁范围、登录端点列表（方法、路径、用户名字段、失败状态码、结果头）。

### 4.2 全局：风控设置

设置键 `risk_settings`（值为 JSON 字符串，`Group = "risk"`），通过专用 API 读写并校验：

| 字段 | 默认 | 说明 |
|---|---|---|
| `AdminLoginMode` | `RiskBased` | `Off` / `RiskBased` / `Always`（`Always` 等价于现有 `EnabledForAdminLogin`） |
| `ChallengeThreshold` | 30 | ≥ 此分数要求验证 |
| `StrongChallengeThreshold` | 60 | ≥ 此分数要求强验证 |
| `BlockThreshold` | 90 | ≥ 此分数临时封禁 |
| `MediumProvider` / `StrongProvider` | `pow` / `turnstile` | Turnstile 未配置时，强验证回落为高难度 PoW |
| `PowBaseDifficulty` | 18 | 强验证档 +3，上限 22 |
| `ClearanceQuota` | 5 | 一次验证换取的登录尝试次数 |
| `ClearanceMinutes` | 10 | 风控 clearance 有效期 |
| `FailureDelay` | true | 登录失败渐进延迟 |

校验：三个阈值必须严格递增。规则的窗口和加分先作为代码默认值，第一版不开放编辑。

### 4.3 全局：Jev 模型提供商（在系统设置中配置）

设置键 `risk_model_provider`（值为 JSON 字符串，`Group = "risk"`）：

```csharp
public sealed class RiskModelProviderSettings
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "typesafe";                // 预留，第一版只实现 typesafe
    public string Endpoint { get; set; } = "https://api.typesafe.ai";  // 代码拼接 /v1/systemone
    public string ApiKey { get; set; } = string.Empty;                 // 只写不读
    public string Model { get; set; } = "jev-latest";
    public RiskModelMode Mode { get; set; } = RiskModelMode.Shadow;    // Shadow | Enforce
    public int TimeoutMs { get; set; } = 3000;
    public int MaxRequestsPerSecond { get; set; } = 5;
    public int MaxConcurrency { get; set; } = 4;
    public long DailyTokenBudget { get; set; } = 5_000_000;
}
```

校验：

- `Endpoint` 必须是绝对 URL，只允许 `https`；`http` 只对 loopback 放行（本地 mock 用），避免 Key 明文出网。
- `Model` 匹配 `^[A-Za-z0-9._-]{1,64}$`；前端提供 `jev-latest`、`jev-1.14.0`、`jev-1.13.0` 选项，并允许自定义输入。
- `TimeoutMs` 500–10000，`MaxRequestsPerSecond` 1–100，`MaxConcurrency` 1–32，`DailyTokenBudget` ≥ 0（0 表示不限）。

### 4.4 机密配置处理

`risk_model_provider`（含 API Key）和 `risk_signing_key`（签名密钥，见 6.2）列为**受保护设置键**：

- 通用接口 `GET /api/v1/setting`、`GET /api/v1/setting/{key}` 过滤受保护键；`POST /api/v1/setting/{key}` 拒绝写入受保护键（否则可以绕过校验）。
- 专用接口（`RiskSettingsService.cs`，`RequireAuthorization()`）：
  - `GET /api/v1/risk/settings`、`PUT /api/v1/risk/settings`
  - `GET /api/v1/risk/model-provider`：返回除 Key 以外的所有字段，另加 `ApiKeyConfigured: bool` 和 `ApiKeyHint: "****abcd"`（末 4 位），**永不返回 Key 原文**。
  - `PUT /api/v1/risk/model-provider`：`ApiKey` 为 null 或空表示保留原值；`ClearApiKey: true` 表示清除；`Enabled = true` 但没有可用 Key 时拒绝保存。
  - `POST /api/v1/risk/model-provider/test`：用提交的配置（Key 为空时用已保存的 Key）发一次固定的最小请求（一个 noul 问题），返回 `{ success, latencyMs, model, errorCode }`；按管理员 IP 每分钟最多 10 次。
- Key 不写日志、不出现在异常信息中；Jev 专用的命名 HttpClient 不挂请求日志 handler。
- 落盘：与其他设置一样明文存于 `data/gateway.config`。第一版接受这一点，在设置页注明「请限制 data 目录权限」；静态加密作为后续项（需要先解决集群节点间密钥一致的问题）。
- 集群：设置随快照同步到 worker，worker 也会持有 Key（每个节点评估自己的灰区对象）。实现时确认 `ClusterHub` 通道是否走 TLS；如果允许明文接入，在文档中提示风险。
- 热更新：`RiskModelScorerService` 订阅 `ConfigurationService.ConfigurationChanged` 重新读取配置（覆盖 worker 应用快照的场景），用 `Volatile` 替换配置快照，无需重启。

### 4.5 前端：系统设置页

`web/src/pages/system-setting/page.tsx` 新增两张卡片，各自独立保存，沿用现有的「有未保存的修改」交互：

1. **登录风控**：管理后台模式、三个阈值、验证提供方、PoW 基础难度、clearance 额度与有效期、失败延迟开关。
2. **AI 风控模型（TypeSafe Jev）**：
   - 启用开关；运行模式（影子模式：只记录 / 参与决策）
   - 服务地址（默认 `https://api.typesafe.ai`）
   - API Key：密码输入框；已配置时占位符显示「已配置 ****abcd，留空则不修改」，旁边有「清除」按钮
   - 模型：可输入的下拉框（默认 `jev-latest`）
   - 超时、每秒请求上限、并发上限、每日 token 预算
   - 「测试连接」按钮：显示延迟（ms）、返回的模型版本、错误原因
   - 只读状态：今日已用 token、近 5 分钟调用数与失败数、熔断状态、最近一次错误
   - 说明文字：启用后，聚合统计特征会发送到所配置的提供商

新增 `web/src/services/RiskService.ts`，类型加到 `web/src/types/index.ts`。

## 五、风控引擎

### 5.1 计数维度

| 维度 | Key | 说明 |
|---|---|---|
| IP | `ClientIpHelper.GetClientIp` 的结果 | |
| 子网 | IPv4 /24、IPv6 /64 | |
| 账号 | `HMAC-SHA256(risk_signing_key, routeId + 规范化用户名)` | 不存明文；不用无密钥的 SHA-256，避免被字典反查 |

每个维度一个 `ConcurrentDictionary`，条目上限 10 万，沿用 `AbnormalIpMonitor` 的「按记录次数触发清理、满员时淘汰空闲条目、仍满则放弃记录新键」策略。去重计数（某 IP 尝试的账号数、某账号的来源 IP 数）每个窗口最多记 64 个，阈值远小于此，足够判断。

### 5.2 默认规则

| 规则 | 窗口 | 条件 | 加分 |
|---|---|---|---|
| IP 登录失败 | 10 分钟 | ≥5 / ≥20 | +30 / +60 |
| IP 尝试的不同账号数 | 10 分钟 | ≥5 / ≥15 | +40 / +70 |
| 子网登录失败 | 10 分钟 | ≥30 | +30 |
| 账号失败的来源 IP 数 | 30 分钟 | ≥10 | 账号打 `under_attack` 标记 |
| 非浏览器特征（UA 为常见脚本库，或同时缺少 `Sec-Fetch-Mode` 和 `Accept-Language`） | — | — | +20 |
| 24 小时内被封禁过 | — | — | +30 |
| 模型标记（仅 Enforce 模式） | 标记 TTL | 见 7.4 | 最多 +40 |

分数按当前窗口实时计算，随窗口滑动自然下降。

### 5.3 决策与处置

| 分数 | 处置 |
|---|---|
| < 30 | 放行 |
| 30–59 | 需要有效的风控 clearance，否则发起中等验证（默认 PoW） |
| 60–89 | 同上，使用强验证提供方；登录失败渐进延迟 |
| ≥ 90 | 429 + `Retry-After`；封禁 TTL 按 24 小时内的封禁次数升级：5 分钟 → 30 分钟 → 24 小时 |

- 账号带 `under_attack` 标记时，对该账号的登录请求至少按 30 分处理（需要验证）。**不锁账号。**
- 风控 clearance（cookie `__FastGateway_RiskClearance`）：签名 payload 包含 host、scope（routeId 或 `admin`）、IP、过期时间、nonce；服务端按 nonce 记录剩余额度（默认 5 次登录尝试），每次登录请求扣 1，用完重新验证。额度是节点本地状态，集群下最坏情况是额度 × 节点数，可以接受。
- 失败渐进延迟：登录失败时在 `Response.OnStarting` 中 `await Task.Delay(min(200ms × 2^(n-1), 3s))`。用全局 `SemaphoreSlim`（默认 200）限制同时处于延迟中的请求数，超出就不延迟，避免反过来拖垮网关。
- 24 小时内被封禁 3 次的 IP 在风控事件页提示「加入黑名单」，默认不自动写入黑名单。

### 5.4 RiskGuardMiddleware 流程

挂在 `Gateway.cs` 的黑白名单之后、`UseBotProtection()` 之前。

1. 绕过：`OPTIONS`、ACME、`/internal/gateway`、`/__fastgateway/*`、受信集群中继（把 `BotProtectionMiddleware.IsTrustedRelay` 改为 `internal` 复用）。
2. 路由未启用 `RiskProtection` → 放行。
3. **非登录请求**：
   - IP 有封禁标记且 `BlockScope = WholeRoute` → 429。
   - IP 分数 ≥ 30、请求是浏览器导航（GET/HEAD，且 `Sec-Fetch-Mode: navigate` 或 `Accept` 含 `text/html`）、没有有效 clearance → 返回验证页。这样被标记的正常用户刷新页面就会先完成验证、拿到 clearance，再回到登录页提交。
   - 其他情况放行。
4. **登录请求**（方法和路径命中规则）：
   - 配置了 `UsernameField` 且 `Content-Length ≤ 16KB` 时，`EnableBuffering()` 读取 JSON 顶层字段或 form 字段，然后把流位置倒回开头；超限或解析失败就跳过账号维度，不阻断请求。
   - 计算分数并决策：封禁 → 429；需要验证且没有可用额度 → 403 JSON `{ errorCode: "risk_challenge_required", redirectUrl }`（与现有 `bot_challenge_required` 结构一致）；否则扣减额度并继续。
   - 在 `Response.OnStarting` 中识别登录结果（见 5.5）、删除结果头、失败时执行渐进延迟。
   - `await _next` 之后记录计数，满足条件的对象投递到模型队列。
5. 被风控短路的请求设置 `context.Items[StatisticsCollector.BlockReasonKey]`；新增 `BlockReason.RiskChallenge = 6`、`BlockReason.RiskBlock = 7`（只追加，不改已有值，因为这些值已经持久化）。
6. `RemoveInternalRequestHeaders` 增加剥离 `__FastGateway_RiskClearance`。

### 5.5 登录结果识别

优先级：

1. 配置了 `ResultHeader`：读取上游响应头，值为 `fail` 或 `success`；读完从响应中删除，客户端看不到。
2. 状态码属于 `FailureStatusCodes` → 失败；2xx 且未配置结果头 → 视为成功。
3. 其他（3xx、5xx 等）→ 不计入。

响应体关键字识别需要缓冲响应体，放到 P3，只在用户明确配置时启用。文档中建议业务方优先加结果头。

## 六、人机验证

### 6.1 提供方抽象

```csharp
public interface IChallengeProvider
{
    string Name { get; }                       // "pow" / "turnstile"
    bool IsAvailable { get; }
    ChallengeDescriptor Create(ChallengeContext context, ChallengeStrength strength);
    ValueTask<bool> VerifyAsync(ChallengeContext context, ChallengeSubmission submission, CancellationToken ct);
}
```

- `TurnstileChallengeProvider` 包装现有的 `BotProtectionService.VerifyTurnstileAsync`，不改变现有 Turnstile 行为。
- 风控验证端点（每个 Server 子应用挂载，与现有 bot 端点并列）：
  - `GET /__fastgateway/risk/challenge?state=`：返回内联 HTML 验证页（不依赖 React 控制台），按 state 中的提供方渲染 PoW 或 Turnstile。
  - `POST /__fastgateway/risk/verify`：校验通过后签发风控 clearance，303 回原 URL。
  - 两个路径加入 `BotProtectionMiddleware.ShouldBypass` 和统计排除规则；verify 复用 `BotProtectionService.TryConsumeVerifyRequest` 的按 IP 限频。

### 6.2 签名密钥

新增受保护设置 `risk_signing_key`（32 字节随机数，Base64），用于风控 clearance、PoW state 和账号 HMAC。master 或单节点首次需要时若不存在就自动生成并保存，随快照同步到 worker，保证集群内一致。worker 拿到快照前不自行生成；此时验证不可用，验证档回落为「只延迟不验证」，并记录告警。这样不再强依赖手动配置的 `CookieSigningKey`。

### 6.3 自研 PoW

协议：

1. 服务端生成 `state = Sign({ host, scope, returnUrl, provider: "pow", challengeId（16 字节随机）, difficulty, ip, issuedAt, expiresAt（+3 分钟） })`。
2. 浏览器在内联的 Blob Web Worker 中运行内联的纯 JS SHA-256（不用 `crypto.subtle`，逐次异步调用太慢；不加载任何外部脚本），寻找 `n` 使 `SHA-256(challengeId + ":" + n)` 的前 `difficulty` 位为 0，页面显示进度；`<noscript>` 给出提示。
3. 提交 `{ state, n }`。服务端依次校验：签名、过期、host、IP 一致、nonce 一次性消费（同 `BotProtectionService.TryConsumeChallengeState` 的模式）、计算一次哈希检查前导零位数。
4. 通过后签发风控 clearance（带额度），303 回原 URL。

CSP：`script-src 'nonce-…'; worker-src blob:; connect-src 'self'`，不允许任何第三方来源。

难度：中等验证档用 `PowBaseDifficulty`（默认 18 位，期望约 26 万次哈希），强验证档 +3（约 210 万次）。上线前在低端手机上实测耗时，再调整默认值。

定位（写进文档）：PoW 能拦住不执行 JS 的通用爆破脚本，并提高每次尝试的成本；但对用原生代码或 GPU 的定制攻击者来说成本很低。因此只用于中风险档，并且必须配合额度。

### 6.4 管理后台登录接入

- `AdminLoginMode = RiskBased` 时，`POST /api/v1/authorization` 走 `RiskEngine`（scope = `admin`，维度：IP、子网）：
  - 需要验证：返回 403 `{ errorCode: "risk_challenge_required", provider, challenge }`；前端在登录页内完成 PoW（Web Worker）或渲染现有的 `TurnstileWidget`，再带上 `riskSolution` 或 `turnstileToken` 重新提交。
  - 封禁：429 + `Retry-After`。
- `AdminLoginMode = Always` 保持现有 `EnabledForAdminLogin` 的行为。
- 新增匿名接口 `GET /api/v1/authorization/challenge` 获取 PoW 挑战，按 IP 限频。
- `AuthorizationRequest` 增加 `RiskSolution` 字段；相应修改前端 `web/src/pages/login/index.tsx`、`web/src/services/AuthorizationService.ts`。

## 七、Jev 模型接入

### 7.1 运行方式

`RiskModelScorerService : BackgroundService`，只在主宿主（`Program.cs`）注册，不在 Server 子应用中注册：

- 输入：`Channel<RiskSubjectSnapshot>`，容量 1024，满了丢弃新条目，并累计丢弃数用于展示。
- 入队条件（`RiskEngine` 在记录登录请求后判断）：规则分在 20–70 的灰区，窗口内至少 3 次登录尝试，同一对象 5 分钟内没有评估过，当前未被封禁。只评估 IP 和子网维度。
- 流控：`TokenBucketRateLimiter`（`MaxRequestsPerSecond`）+ `SemaphoreSlim`（`MaxConcurrency`）+ 每日 token 预算（累加响应中的 `usage.input_tokens`，UTC 零点重置），超出预算当天停止调用。
- 失败处理：超时、5xx、网络错误一律不写标记，**完全回落到规则**；429 遵守 `Retry-After`；连续失败 5 次熔断 60 秒。
- 模型不可用不会让任何登录请求失败或变慢。

### 7.2 请求

按 TypeSafe 文档（quickstart）：`POST {Endpoint}/v1/systemone`，请求头 `Authorization: Bearer <ApiKey>`、`Content-Type: application/json`。

```json
{
  "model": "jev-latest",
  "state": {
    "subject_type": "ip",
    "window_minutes": 10,
    "login_attempts": 37,
    "login_failures": 35,
    "distinct_accounts": 22,
    "median_interval_ms": 410,
    "interval_stddev_ms": 12,
    "browser_navigation_seen": false,
    "client_kind": "script_library",
    "sec_fetch_present": false,
    "accept_language_present": false,
    "network_type": "hosting",
    "previous_blocks_24h": 1
  },
  "questions": {
    "automated": { "type": "noul", "instructions": "The login traffic is produced by an automated program rather than a person using a web browser." },
    "credential_stuffing": { "type": "noul", "instructions": "The client is testing leaked username and password pairs across many different accounts." },
    "password_guessing": { "type": "noul", "instructions": "The client is repeatedly guessing passwords for a small number of accounts." }
  }
}
```

- 问题固定写在代码中，按文档建议拆成原子判断；最终动作由我们的代码组合，不让模型直接回答「封不封」。
- 请求/响应 DTO（`JevRequest`、`JevQuestion`、`JevResponse`、`JevAnswer`）注册到 `AppJsonContext`；忽略未知字段，以兼容模型版本变化。
- 实现前对照 `https://docs.typesafe.ai/api` 确认：错误响应结构，以及是否有模型列表接口（SDK 有 `Models.list`；如果有，「测试连接」成功后用它填充模型下拉框）。

### 7.3 输入安全与隐私

`RiskFeatureBuilder` 只输出数字、布尔值和固定枚举：

- **不发送任何攻击者可控的原始字符串**（UA、用户名、路径、请求头值），防止攻击者借此影响模型判断。UA 先由本地解析器映射为固定枚举（`browser` / `script_library` / `cli_tool` / `empty` / `unknown`）。
- 不发送 IP 和用户名（包括哈希）；`network_type` 由 ip2region 的 ISP 字段映射为枚举（`residential` / `mobile` / `hosting` / `unknown`），映射表写在代码中。
- 默认关闭，由管理员在系统设置中主动启用。

### 7.4 结果使用

- 组合：`modelScore = min(40, 25·p(automated) + 30·p(credential_stuffing) + 20·p(password_guessing))`。
- **Shadow 模式**（默认）：只记录评估结果，不写标记，不影响决策。
- **Enforce 模式**：写入 `model_suspect` 标记（TTL 10 分钟，携带 `modelScore`），叠加到规则分上。模型分最多能把对象单独推到「需要验证」档；进入封禁档要求规则分本身 ≥ 60。原因：文档说明概率校准是按一批预测整体统计的，不保证单条结果准确。
- 每次评估记录到内存环形缓冲（最近 1000 条）：时间、对象、特征、三个概率、组合分、延迟、实际模型版本（响应中的 `model`）、token 数，以及事后结果（之后 30 分钟内该对象是否通过验证、是否登录成功）。风控事件页展示这些数据，作为从 Shadow 切换到 Enforce 的依据。

## 八、管理界面与可观测

- 安全菜单新增「风控事件」页（`web/src/pages/security/risk/`）：
  - 当前标记列表：对象、类型、原因（规则名或模型）、分数、过期时间；操作：解除、延长、加入黑名单（复用 threats 页的交互）。
  - 最近的验证与封禁决策。
  - 模型评估记录（用于 Shadow 模式下的对比）。
  - 后端接口：`GET /api/v1/risk/tags`、`DELETE /api/v1/risk/tags/{id}`、`GET /api/v1/risk/decisions`、`GET /api/v1/risk/model-evaluations`。
- 管理后台登录失败也记为风控事件（管理后台没有挂统计采集，需要单独记录）。
- 登录后如果仍在使用默认密码，在面板顶部强提醒。**不能通过匿名接口暴露「是否在用默认密码」**，否则等于告诉攻击者默认密码可用。

## 九、集群

- 域名的 `RiskProtection`、`risk_settings`、`risk_model_provider`、`risk_signing_key` 都在 `GatewayConfig` 中，随现有快照同步，不需要新增协议。
- 计数器和 clearance 额度是节点本地状态：攻击分散到 N 个节点时，每个节点只看到约 1/N。第一版接受这一点，依靠子网和账号维度部分弥补。
- P3：通过 `ClusterHub` 新增 `RiskTagUpsert` 消息同步标记（数量少，适合广播），让一个节点的封禁和模型判断在全集群生效。

## 十、分阶段交付

每个阶段可以独立合并，每阶段一个 commit（`feat(risk): …`）。

**P0：管理后台登录加固**（已完成）

- 按 IP 和 /24 子网统计失败次数：10 分钟内失败 5 次后开始渐进延迟，20 次后封禁 15 分钟（按 IP，不是全局）。
- 密码比较：两边先做 SHA-256，再用 `CryptographicOperations.FixedTimeEquals`，避免长度差异泄露。
- 新增设置 `admin_client_ip_source`（复用 `ClientIpSource` 枚举，默认 `Default`）；后台在反向代理之后时改用转发头。设置页说明只有确实有代理时才开启，否则 IP 可以被伪造。
- 登录后在面板中提醒默认密码。
- 计数器先用简单实现，P1 时迁移到 `RiskEngine`。

**P1：风控引擎核心 + 业务登录保护**（已完成）

- `RiskEngine`、`SlidingWindowCounter`、`RiskTagStore`、默认规则、决策。
- `risk_signing_key` 的自动生成与同步；受保护设置键的过滤。
- `DomainName.RiskProtection` 与前端登录保护配置。
- `RiskGuardMiddleware`：账号字段提取、结果识别（结果头 / 状态码）、失败延迟、429 封禁。
- `IChallengeProvider` + Turnstile 提供方、风控验证端点、带额度的风控 clearance。Turnstile 未配置时，验证档只延迟、不验证，并在事件页提示配置验证提供方。
- `BlockReason.RiskChallenge` / `RiskBlock`；风控事件页（标记列表与解除）。
- 管理后台登录从 P0 的临时计数迁移到 `RiskEngine`。

P1 实际落地与上文的差异：

- 管理后台登录**仍使用 P0 的 `AdminLoginGuard`**（阈值与业务规则不同，且已验证可用），其锁定记录合并展示在风控事件页并支持解除；与 `RiskEngine` 的统一放到 P2a 接入 PoW 时进行。
- 第一版只有 Turnstile 一种验证方式，`IChallengeProvider` 抽象推迟到 P2a 引入 PoW 时再抽取，避免只有一个实现的空抽象。
- 无可用验证方式（未配置 Turnstile 或站点为明文 HTTP）时：30–59 分只做失败延迟，≥60 分直接临时封禁；另增加「IP 10 分钟内失败 ≥50 次 +90」规则，保证有验证方式时持续失败的来源也会被封禁。
- 风控管理 API 放在 `Services/RiskService.cs`（`/api/v1/risk/tags`、`/api/v1/risk/decisions`），与其他端点分组保持一致。
- `BotProtectionService` 的签名逻辑抽取为 `Infrastructure/SignedToken.cs`，与风控共用。

**P2a：自研 PoW 与风控设置**（已完成）

- PoW 提供方、验证页与校验。
- `/api/v1/risk/settings` 与系统设置页「登录风控」卡片。
- 管理后台登录的 `RiskBased` 模式（登录页内完成 PoW / Turnstile）。

P2a 实际落地与上文的差异：

- 只有 PoW 与 Turnstile 两种验证方式，没有引入 `IChallengeProvider` 接口：`RiskEngine.ChooseChallenge` 按档位选择，验证端点按签名状态里的 `Provider` 分支校验。第三种方式出现时再抽象。
- PoW 不依赖外部服务，明文 HTTP 站点也可用（clearance cookie 仅在 HTTPS 下带 Secure），因此「无可用验证方式」只剩从节点尚未收到签名密钥这一种情况。
- 求解器脚本只有一份（`ProofOfWork.WorkerScript`）：网关验证页内联，管理后台登录页从 `/api/v1/authorization/pow-worker.js` 下载后在 Web Worker 中运行。
- 管理后台仍由 `AdminLoginGuard` 计数，不并入 `RiskEngine`：`AdminLoginMode` 简化为开关 `AdminLoginChallenge`——失败 5 次后每次登录都需一个新的 PoW 解（失败 10 次起难度 +3），始终要求 Turnstile 仍由 appsettings 的 `EnabledForAdminLogin` 控制。
- `risk_settings` 可以通过通用设置接口读取，但只能经 `/api/v1/risk/settings` 校验后写入。

**P2b：Jev 提供商配置与评分服务**（已完成）

- `/api/v1/risk/model-provider` 系列接口；系统设置页「AI 风控模型」卡片与测试连接。
- `RiskFeatureBuilder`、`JevClient`、`RiskModelScorerService`、流控、熔断、预算。
- 默认 Shadow 模式；评估记录与事后结果的对比展示。

P2b 实际落地与上文的差异：

- 对照 API Reference 与 OpenAPI 确认了接口：`GET /v1/models` 返回 `{ models: [{ name, description, release_date }] }`，设置页据此提供「获取列表」；错误响应只文档化了 422，401/429/5xx 按状态码通用处理，`Retry-After` 有则遵守、无则退避 10 秒。
- 修改服务地址的**主机**时必须重新填写 API Key（保存、测试连接、获取模型列表同一规则），防止通过改地址把已保存的 Key 发往别处。
- 请求体先序列化再发送并带 `Content-Length`（`JsonContent` 默认分块传输，部分网关/代理不接受）；该 HttpClient 移除了默认请求日志。
- 第一版只评估 IP 维度；网段失败数作为特征 `subnet_failures` 一并发送。特征构建在 `RiskEngine.BuildFeatures`，特征类型 `RiskFeatureState` 只有数字、布尔与固定枚举字段，UA 先经 `LoginRequestInspector.ClassifyClient` 归类。
- 评估记录、运行状态与 token 用量都在内存中、按节点统计；「之后是否登录成功 / 通过验证」在查询时从引擎读取。
- 未用真实 TypeSafe Key 验证：端到端测试使用按文档契约实现的本地模拟服务；部署地区的真实延迟需上线前用「测试连接」实测。

**P3：增强**

- 设备 cookie：受保护站点的 HTML 响应下发签名设备 ID，用于「没加载过页面就直接 POST 登录」规则；同时加入 Cookie 剥离。
- 响应体关键字识别登录结果。
- 集群标记同步。
- 评估记录和决策记录持久化（随日志保留天数清理）。

**P4：可选**

- 滑块 / 点选类交互验证（先评估 AOT 下的图像库：SkiaSharp 有原生依赖，ImageSharp 有许可问题）。
- TLS 指纹（JA4）作为特征。

## 十一、测试计划

新增测试放在 `tests/FastGateway.Tests`，命名 `<Method>_<Scenario>_<Expectation>`。

引擎与处置：

- `SlidingWindowCounter`：跨桶滚动、窗口过期、去重上限。
- 规则：各阈值命中正确档位；分数随窗口滑动下降；标记 TTL 过期；封禁 TTL 升级。
- 账号 `under_attack` 时只要求验证，不拒绝合法登录。
- 失败延迟受信号量上限约束。

登录结果识别与中间件：

- 结果头优先于状态码；结果头不会出现在客户端响应中。
- 请求体超过 16KB 或不是 JSON/form 时跳过账号维度，请求照常转发，上游收到的 body 与原始一致。
- 风控 clearance cookie 不会转发到上游。
- 受信中继、ACME、内部路径不受影响。

PoW 与 clearance：

- 合法解通过；难度不足、过期、签名被篡改、IP 不一致、重放都被拒绝。
- 额度逐次扣减，用完后重新要求验证。
- 没有签名密钥时按设计回落。

设置与机密：

- `GET /api/v1/setting`（列表和按 key）不返回受保护键；`POST /api/v1/setting/{key}` 拒绝写受保护键。
- `GET /api/v1/risk/model-provider` 永不返回 Key 原文；`PUT` 留空保留原 Key，`ClearApiKey` 清除，启用但没有 Key 时拒绝。
- Endpoint、模型名、数值范围的校验。
- 配置变更（包括模拟 worker 的 `ReplaceConfig`）后评分服务读到新配置。

Jev 客户端（用 mock `HttpMessageHandler`）：

- 请求 JSON 结构与文档一致，带 Bearer 头，地址为 `{Endpoint}/v1/systemone`。
- 能解析文档示例响应，忽略未知字段。
- 429 + `Retry-After`、超时、5xx、熔断、超出每日预算时都不写标记。
- Shadow 模式不写标记；Enforce 模式下模型分不能单独把对象推到封禁档。
- `RiskFeatureBuilder`：UA 中包含注入文本时，state 里只有枚举值，不含原始字符串、IP 或用户名。

管理后台登录：

- 不同 IP 的失败计数互不影响；`admin_client_ip_source` 各取值下的 IP 解析正确；密码比较正确。

手工验证：

1. 用 curl / hydra 脚本爆破测试站点的登录接口，依次观察到延迟 → 403 验证 → 429 封禁，并在风控事件页看到对应数据。
2. 浏览器在被标记的 IP 下刷新页面，完成 PoW 后正常登录；额度用完后再次要求验证。
3. 在低端手机上测量各难度的 PoW 耗时。
4. 填入真实的 TypeSafe Key，用「测试连接」测量部署地区的延迟和可用性；断网或填错 Key 时登录不受影响。
5. 集群下修改提供商配置，确认 worker 生效，且日志中没有 Key。

最终运行：

- `dotnet build FastGateway.sln`
- `dotnet test FastGateway.sln`
- `cd web && npm run build`
- `cd web && npm run lint`

## 十二、风险与待确认

- **被攻击的是哪个登录**：如果是管理后台，先做 P0；如果是业务系统，P1 优先。设计本身两者都覆盖。
- **Jev 的实际延迟与可用性**：文档没有给出，需要用「测试连接」在部署地区实测。因为是近线异步调用，延迟只影响标记生效的快慢，不影响请求本身。
- **TypeSafe API 细节**：错误响应结构、模型列表接口，以实现时的 API Reference 为准。
- **数据出境**：默认关闭，只发聚合特征；启用前由管理员确认合规要求。
- **Key 明文落盘**：第一版依赖 data 目录权限；静态加密是后续项。
- **PoW 强度有限**：只用于中风险档，配合额度和封禁。
- **业务前端兼容**：SPA 收到 403 `risk_challenge_required` 时可能只显示通用错误；被标记的用户刷新页面就会进入验证页。文档中给业务方提供处理该错误码（跳转 `redirectUrl`）的示例。
- **AGENTS.md 中「No test projects exist yet」已过时**，可以顺手更新。
