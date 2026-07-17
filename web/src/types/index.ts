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
