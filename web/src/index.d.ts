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