export interface AuthorizeInput {
    password: string;
}

export interface ServiceInput {
    serviceNames: string[];
    listen: number;
    enableHttp3: boolean;
    isHttps: boolean;
    enableFlowMonitoring: boolean;
    enableRequestSource: boolean;
    enable: boolean;
    enableTunnel: boolean;
    locations: LocationInput[];
    sslCertificate: string | null;
    sslCertificatePassword: string | null;
}

export interface LocationInput {
    serviceId?: string;
    path: string;
    proxyPass?: string | null;
    addHeader: { [key: string]: string; };
    root?: string | null;
    tryFiles?: string[] | null;
    loadType: LoadType;
    upStreams?: UpStreamInput[];
    type?: number;
}

export interface UpStreamInput {
    server: string | null;
    weight: number;
}

export enum LoadType {
    IpHash = 1,
    RoundRobin = 2,
    WeightRoundRobin = 3
}

export interface CertInput {
    domains: string[];
    autoRenew: boolean;
    certFile: string;
    certPassword: string;
    email: string;
}


export enum RenewStats {
    None,
    Success,
    Fail
}

export interface Cert {
    id: string;
    domains: string[];
    expired: boolean;
    autoRenew: boolean;
    issuer: string;
    renewTime: string | null;
    renewStats: RenewStats;
    notAfter: string | null;
    certFile: string;
    certPassword: string;
}

export interface Service {
    id: string;
    serviceNames: string[];
    listen: number;
    enableHttp3: boolean;
    isHttps: boolean;
    enableFlowMonitoring: boolean;
    enableRequestSource: boolean;
    enable: boolean;
    enableTunnel: boolean;
    locations: Location[];
    sslCertificate: string | null;
    sslCertificatePassword: string | null;
}

export interface Location {
    id: string;
    path: string;
    serviceId: string;
    proxyPass: string | null;
    addHeader: { [key: string]: string; };
    root: string | null;
    tryFiles: string[] | null;
    loadType: LoadType;
    upStreams: UpStream[];
}

export interface UpStream {
    server: string | null;
    weight: number;
}