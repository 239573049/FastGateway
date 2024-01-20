export interface RouteEntity {
    routeId: string;
    routeName: string;
    description: string;
    clusterId: string | null;
    maxRequestBodySize: number | null;
    matchEntities: RouteMatchEntity;
    clusterEntity: ClusterEntity | null;
}

export interface RouteMatchEntity {
    path: string;
    hosts: string[];
}

export interface ClusterEntity {
    clusterId: string;
    clusterName: string;
    description: string;
    destinationsEntities: DestinationsEntity[];
}

export interface DestinationsEntity {
    id: string;
    address: string;
    host: string | null;
}

export interface CertificateEntity {
    id: string;
    name: string;
    description: string | null;
    host: string;
    password: string;
    path: string;
    createTime: string;
    updateTime: string | null;
    expirationTime: string | null;
    type: CertificateType;
}

export enum CertificateType {
    Pfx,
    Pem
}

