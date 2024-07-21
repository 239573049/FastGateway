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
}


export interface DomainName {
    id?: string | null;
    path: string;
    serverId: string;
    domains: string[];
    serviceType: ServiceType;
    headers: HeadersView[];
    enable: boolean;
    service: string | null;
    upStreams: UpStream[];
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