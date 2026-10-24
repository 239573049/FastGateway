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

export interface Server {
    id: string | null;
    listen: number;
    name: string;
    description: string | null;
    redirectHttps: boolean;
    enable: boolean;
    staticCompress: boolean;
    isHttps: boolean;
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
    apiKeyConfigured: boolean;
    apiKeyHint?: string | null;
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

export interface AbnormalIp {
    ip: string;
    windowErrorCount: number;
    totalErrorCount: number;
    firstSeen: string;
    lastSeen: string;
    lastErrorDescription?: string | null;
    topErrorDescription?: string | null;
    lastPath?: string | null;
    lastMethod?: string | null;
    lastStatusCode?: number | null;
    lastServerId?: string | null;
}
