import { get, postJson } from '@/utils/fetch';

const baseUrl = '/api/v1/setting';

export interface SettingResponse<T> {
  data: T;
  success: boolean;
  message?: string;
}

/** 日志保留天数设置键（与后端 LogRetention.SettingKey 一致） */
export const LOG_RETENTION_KEY = 'log_retention_days';

/** 日志保留天数可选值 */
export const LOG_RETENTION_OPTIONS = [
  { value: '1', label: '1 天' },
  { value: '7', label: '7 天（默认）' },
  { value: '15', label: '15 天' },
  { value: '30', label: '30 天' },
];

/** 管理后台客户端 IP 来源设置键（与后端 AdminLoginGuard.ClientIpSourceSettingKey 一致） */
export const ADMIN_CLIENT_IP_SOURCE_KEY = 'admin_client_ip_source';

/** 取值与后端 ClientIpSource 枚举名称一致 */
export const ADMIN_CLIENT_IP_SOURCE_OPTIONS = [
  { value: 'Default', label: '连接地址（默认）' },
  { value: 'XForwardedFor', label: 'X-Forwarded-For' },
  { value: 'XRealIp', label: 'X-Real-IP' },
  { value: 'CfConnectingIp', label: 'CF-Connecting-IP' },
];

/**
 * 获取单个设置值
 */
export const getSetting = (key: string): Promise<SettingResponse<string | null>> => {
  return get(`${baseUrl}/${encodeURIComponent(key)}`);
};

/**
 * 保存单个设置值
 */
export const setSetting = (key: string, value: string): Promise<SettingResponse<unknown>> => {
  return postJson(`${baseUrl}/${encodeURIComponent(key)}`, { key, value });
};
