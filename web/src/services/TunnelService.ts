import { del, get } from "@/utils/fetch";

export interface TunnelProxy {
    id: string;
    host?: string;
    route: string;
    localRemote: string;
    description: string;
    domains: string[];
    enabled: boolean;
}

export interface TunnelNode {
    name: string;
    description: string;
    token: string;
    serverUrl: string;
    reconnectInterval: number;
    heartbeatInterval: number;
    proxyCount: number;
    proxies: TunnelProxy[];
    isOnline: boolean;
    lastConnectTime?: string;
}

export interface ApiResponse<T> {
    data: T;
    success: boolean;
    message?: string;
}

/**
 * 获取节点列表
 */
export const getTunnelList = (): Promise<ApiResponse<TunnelNode[]>> => {
    return get('/api/v1/tunnel');
};

/**
 * 获取节点详情
 * @param name 节点名称
 */
export const getTunnelDetail = (name: string): Promise<ApiResponse<TunnelNode>> => {
    return get(`/api/v1/tunnel/${encodeURIComponent(name)}`);
};

/**
 * 删除节点
 * @param name 节点名称
 */
export const deleteTunnel = (name: string): Promise<ApiResponse<void>> => {
    return del(`/api/v1/tunnel/${encodeURIComponent(name)}`);
};