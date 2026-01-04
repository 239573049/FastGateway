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
