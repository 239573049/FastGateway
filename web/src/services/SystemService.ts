import { get } from '@/utils/fetch';

const baseUrl = '/api/v1/system';

export interface ApiResponse<T> {
  data: T;
  success: boolean;
  message?: string;
}

export interface VersionInfo {
  version: string;
  framework: string;
  os: string;
}

export interface GatewayInfo extends VersionInfo {
  name?: string;
  informationalVersion?: string;
  fileVersion?: string;
  product?: string;
  description?: string;
  company?: string;
  yarpVersion?: string;
  osArchitecture?: string;
  processArchitecture?: string;
  machineName?: string;
  environmentName?: string;
  serverTime?: string;
  processId?: number;
  processStartTime?: string;
  uptimeSeconds?: number;
}

/**
 * 获取系统版本信息
 */
export const getVersion = (): Promise<ApiResponse<VersionInfo>> => {
  return get(`${baseUrl}/version`);
};

/**
 * 获取网关版本与运行信息
 */
export const getGatewayInfo = (): Promise<ApiResponse<GatewayInfo>> => {
  return get(`${baseUrl}/info`);
};
