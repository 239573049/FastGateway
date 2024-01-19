export interface RouteEntity {
    routeId: string;
    clusterId: string | null;
    maxRequestBodySize: number | null;
    matchEntities: RouteMatchEntity;
}

export interface RouteMatchEntity {
    path: string;
    hosts: string[];
}

export interface ClusterEntity {
    clusterId: string;
    destinationsEntities: DestinationsEntity[];
}

export interface DestinationsEntity {
    id: string;
    address: string;
    host: string | null;
}