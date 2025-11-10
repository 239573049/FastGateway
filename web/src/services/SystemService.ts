import { get } from '@/utils/fetch';

const baseUrl = '/api/v1/system';

export interface VersionInfo {
  version: string;
  framework: string;
  os: string;
}

/**
 * 获取系统版本信息
 */
export const getVersion = (): Promise<VersionInfo> => {
  return get(`${baseUrl}/version`);
};
