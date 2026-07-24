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
    service: string | null;
    upStreams: UpStream[];
    enableHealthCheck: boolean;
    healthCheckPath: string | null;
    healthCheckIntervalSeconds: number;
    healthCheckTimeoutSeconds: number;
    root: string | null;
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
