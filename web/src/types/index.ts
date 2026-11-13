export enum ClientIpSource {
    Default = 0,
    XForwardedFor = 1,
    XRealIp = 2,
    CfConnectingIp = 3,
}

export const clientIpSourceOptions = [
    {
        value: ClientIpSource.Default,
        label: "默认方式（直接获取 IP）",
        shortLabel: "直连 IP",
        description: "直接读取与网关建立连接的远端 IP，不读取转发请求头。",
    },
    {
        value: ClientIpSource.XForwardedFor,
        label: "X-Forwarded-For",
        shortLabel: "XFF",
        description: "读取 X-Forwarded-For 的首个 IP，缺失或无效时读取连接 IP。",
    },
    {
        value: ClientIpSource.XRealIp,
        label: "X-Real-IP",
        shortLabel: "X-Real-IP",
        description: "读取 X-Real-IP，缺失或无效时读取连接 IP。",
    },
    {
        value: ClientIpSource.CfConnectingIp,
        label: "CF-Connecting-IP",
        shortLabel: "CF",
        description: "读取 Cloudflare 写入的 CF-Connecting-IP，缺失或无效时读取连接 IP。",
    },
] as const;

export function getClientIpSourceShortLabel(source: ClientIpSource | null | undefined) {
    return (
        clientIpSourceOptions.find((option) => option.value === source)?.shortLabel ??
        clientIpSourceOptions[0].shortLabel
    );
}

/** 网关注入的 X-Frame-Options 策略；SAMEORIGIN 为默认，兼容依赖同源 iframe 的应用 */
export const frameOptionsOptions = [
    {
        value: "SAMEORIGIN",
        label: "同源允许（默认）",
        description: "允许本站页面嵌入本站页面，仍禁止第三方站点嵌套。使用同源 iframe 预览的应用必须选这项。",
    },
    {
        value: "DENY",
        label: "完全禁止嵌套",
        description: "任何页面都不能通过 iframe 嵌入，包括本站。会打断依赖同源 iframe 的应用。",
    },
    {
        value: "NONE",
        label: "不注入（由上游决定）",
        description: "网关不添加 X-Frame-Options，完全沿用源站响应头；上游声明过嵌入策略时网关本来就不会改写。",
    },
] as const;

export interface Server {
    id: string | null;
    listen: number;
    name: string;
    description: string | null;
    redirectHttps: boolean;
    enable: boolean;
    staticCompress: boolean;
    isHttps: boolean;
    /** X-Frame-Options 注入策略：空 / SAMEORIGIN / DENY / NONE */
    frameOptions: string | null;
    enableTunnel: boolean;
    enableBlacklist: boolean;
    enableWhitelist: boolean;
    clientIpSource: ClientIpSource;
    onLine: boolean;
    copyRequestHost: boolean;
    maxRequestBodySize: number | null;
    timeout: number;
    enableRequestFailover: boolean;
    failoverConnectTimeoutMs: number;
    failoverBudgetMs: number;
    maxConcurrentConnections: number | null;
    maxConcurrentUpgradedConnections: number | null;
}


export interface DomainName {
    id?: string | null;
    path: string;
    serverId: string;
    domains: string[];
    serviceType: ServiceType;
    headers: HeadersView[];
    tryFiles: string[];
    enable: boolean;
    enableBotProtection: boolean;
    service: string | null;
    upStreams: UpStream[];
    enableHealthCheck: boolean;
    healthCheckPath: string | null;
    healthCheckIntervalSeconds: number;
    healthCheckTimeoutSeconds: number;
    root: string | null;
    /** 集群模式下指定由哪个节点访问上游：空 = 本节点直连；"master" = 主网关；其他 = 从节点 Id */
    accessNodeId?: string | null;
    /** 登录风控（暴力破解、撞库防护） */
    riskProtection?: RiskProtection | null;
}

export enum RiskBlockScope {
    LoginOnly = 0,
    WholeRoute = 1
}

export interface LoginEndpointRule {
    method: string;
    /** 访问者请求的完整路径（不含查询字符串） */
    path: string;
    /** 请求体中用户名所在字段，为空则不按账号统计 */
    usernameField?: string | null;
    failureStatusCodes: number[];
    /** 上游声明登录结果的响应头（值为 fail / success） */
    resultHeader?: string | null;
}

export interface RiskProtection {
    enable: boolean;
    blockScope: RiskBlockScope;
    loginEndpoints: LoginEndpointRule[];
}

export interface RiskTag {
    id: string;
    /** block / account_under_attack / admin_lock */
    kind: string;
    /** ip / subnet / account */
    subjectType: string;
    subject: string;
    host?: string | null;
    reason: string;
    score: number;
    createdAt: string;
    expiresAt: string;
}

export type RiskChallengeProvider = "pow" | "turnstile";

export interface RiskSettings {
    challengeThreshold: number;
    strongChallengeThreshold: number;
    blockThreshold: number;
    mediumProvider: RiskChallengeProvider;
    strongProvider: RiskChallengeProvider;
    /** PoW 前导零位数，强验证在此基础上 +3 */
    powDifficulty: number;
    clearanceQuota: number;
    clearanceMinutes: number;
    failureDelay: boolean;
    adminLoginChallenge: boolean;
    adminLoginSlider: boolean;
}

/** 0 = 影子模式（只记录），1 = 参与决策 */
export enum RiskModelMode {
    Shadow = 0,
    Enforce = 1
}

export interface RiskModelProvider {
    enabled: boolean;
    provider: string;
    endpoint: string;
    model: string;
    mode: RiskModelMode;
    timeoutMs: number;
    maxRequestsPerSecond: number;
    maxConcurrency: number;
    dailyTokenBudget: number;
    httpThreatEnabled: boolean;
    httpThreatThreshold: number;
    httpThreatIntervalHours: number;
    httpThreatLookbackHours: number;
    httpThreatMaxIpsPerRun: number;
    /** 观察模式：只记录判断结果，不封禁 */
    httpThreatShadow: boolean;
    /** 首次封禁时长（小时），再犯翻倍；0 表示永久 */
    httpThreatBanHours: number;
    httpThreatBanMaxHours: number;
    /** 手动解封后不再交给 Jev 判断的天数；0 表示不豁免 */
    httpThreatExemptDays: number;
    /** 自称搜索引擎爬虫的来源做反向 DNS 校验 */
    httpThreatVerifyCrawlers: boolean;
    /** IPv6 来源按 /64 聚合评估与封禁 */
    httpThreatAggregateIpv6: boolean;
    /** 不受 Jev 影响的域名：不参与判断，被 Jev 封禁的 IP 访问时也放行；支持 *.example.com */
    httpThreatExcludedHosts: string[];
    apiKeyConfigured: boolean;
    apiKeyHint?: string | null;
}

export interface JevThreatRunStatus {
    batchId: string;
    triggerType: "auto" | "manual" | string;
    startedAt: string;
    finishedAt?: string | null;
    threshold: number;
    totalCandidates: number;
    evaluatedIps: number;
    blockedIps: number;
    failedIps: number;
    /** 风险达到阈值的 IP 数（观察模式下即本应封禁的数量） */
    highRiskIps: number;
    shadow: boolean;
    blacklistName?: string | null;
    status: "queued" | "running" | "completed" | "failed" | string;
    error?: string | null;
}

/** apiKey 为空表示保留已保存的 Key */
export interface RiskModelProviderInput extends Omit<RiskModelProvider, "apiKeyConfigured" | "apiKeyHint"> {
    apiKey?: string | null;
    clearApiKey?: boolean;
}

export interface RiskModelStatus {
    enabled: boolean;
    usable: boolean;
    mode: RiskModelMode;
    model: string;
    tokensUsedToday: number;
    dailyTokenBudget: number;
    calls5m: number;
    failures5m: number;
    dropped: number;
    skipped: number;
    circuitOpenUntil?: string | null;
    backoffUntil?: string | null;
    lastError?: string | null;
    lastErrorAt?: string | null;
    lastSuccessAt?: string | null;
}

export interface RiskModelTestResult {
    success: boolean;
    latencyMs: number;
    model?: string | null;
    automated?: number | null;
    message?: string | null;
}

export interface RiskModelInfo {
    name: string;
    description?: string | null;
    releaseDate?: string | null;
}

export interface RiskModelEvaluation {
    time: string;
    ip: string;
    host?: string | null;
    model: string;
    mode: RiskModelMode;
    applied: boolean;
    automated?: number | null;
    credentialStuffing?: number | null;
    passwordGuessing?: number | null;
    modelScore?: number | null;
    ruleScore: number;
    latencyMs: number;
    inputTokens?: number | null;
    error?: string | null;
    laterLoginSucceeded: boolean;
    laterChallengePassed: boolean;
}

export interface RiskDecision {
    time: string;
    ip: string;
    host: string;
    path: string;
    /** challenge / block */
    action: string;
    score: number;
    reason: string;
}

export enum ServiceType {
    Service = 0,
    ServiceCluster = 1,
    StaticFile = 2
}

export interface UpStream {
    service: string;
    weight: number;
}

export type DestinationHealth = "Unknown" | "Healthy" | "Unhealthy";

export interface DestinationHealthState {
    active: DestinationHealth;
    passive: DestinationHealth;
    effective: DestinationHealth;
}

export interface DestinationHealthInfo {
    destinationId: string;
    address?: string | null;
    health: DestinationHealthState;
}

export interface ClusterHealth {
    clusterId: string;
    healthCheck?: {
        enabled: boolean;
        path: string | null;
    };
    destinations: DestinationHealthInfo[];
}

export interface ServerHealthSnapshot {
    online: boolean;
    supported?: boolean;
    checkedAtUtc?: string;
    clusters?: ClusterHealth[];
}

export enum StreamProtocol {
    Tcp = 0,
    Udp = 1,
    Both = 2
}

export enum StreamLoadBalancing {
    RoundRobin = 0,
    LeastConnections = 1,
    Random = 2
}

export interface StreamUpStream {
    host: string;
    port: number;
    weight: number;
}

export interface StreamForward {
    id: string | null;
    name: string;
    description: string | null;
    enable: boolean;
    protocol: StreamProtocol;
    listenPort: number;
    listenAddress: string;
    upStreams: StreamUpStream[];
    loadBalancing: StreamLoadBalancing;
    connectTimeoutMs: number;
    idleTimeoutSeconds: number;
    maxTcpConnections: number;
    maxUdpSessions: number;
    enableBlacklist: boolean;
    enableWhitelist: boolean;
    onLine: boolean;
    activeConnections: number;
    udpSessions: number;
}

export interface HeadersView {
    key: string;
    value: string;
}
export interface RateLimit {
    id: string | null;
    name: string;
    enable: boolean;
    endpoint: string;
    period: string;
    limit: number;
    endpointWhitelist: string[];
    ipWhitelist: string[];
}

export interface BlacklistAndWhitelistItem {
    id: string;
    name: string;
    description?: string;
    ips?: string[];
    regions?: string[];
    enable: boolean;
    isBlacklist: boolean;
}
