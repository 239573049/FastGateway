export type StatRange = "1h" | "24h" | "7d" | "30d";

export interface StatisticsOverview {
  requests: number;
  pageViews: number;
  uniqueVisitors: number;
  uniqueIps: number;
  blocked: number;
  blocked403: number;
  blocked429: number;
  blockRate: number;
  attackIps: number;
  abnormalIpsLive: number;
  error4xx: number;
  error4xxRate: number;
  error5xx: number;
  error5xxRate: number;
  avgElapsedMs: number;
  droppedEntries: number;
}

export interface TimeseriesPoint {
  time: number;
  requests: number;
  pageViews: number;
  blocked: number;
  error4xx: number;
  error5xx: number;
}

export interface GeoItem {
  name: string;
  count: number;
  blocked: number;
  percent: number;
}

export interface GeoResult {
  items: GeoItem[];
  total: number;
}

export interface RankingItem {
  key: string;
  count: number;
  blocked: number;
  percent: number;
}

export interface RequestLogItem {
  id: number;
  ts: number;
  host: string;
  path: string;
  method: string;
  status: number;
  elapsedMs: number;
  ip: string;
  country: string;
  province: string;
  os: string;
  browser: string;
  refererUrl: string;
  blocked: number;
}

export const RANGE_OPTIONS: Array<{ value: StatRange; label: string }> = [
  { value: "1h", label: "近 1 小时" },
  { value: "24h", label: "近 24 小时" },
  { value: "7d", label: "近 7 天" },
  { value: "30d", label: "近 30 天" },
];

export function formatSeriesTime(ts: number, range: StatRange): string {
  const date = new Date(ts * 1000);
  if (range === "1h" || range === "24h")
    return date.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit" });
  if (range === "7d")
    return `${date.getMonth() + 1}/${date.getDate()} ${date.getHours()}:00`;
  return `${date.getMonth() + 1}/${date.getDate()}`;
}

export function formatCount(value: number): string {
  if (!Number.isFinite(value)) return "0";
  if (value >= 1_000_000) return `${(value / 1_000_000).toFixed(1)}M`;
  if (value >= 10_000) return `${(value / 1000).toFixed(1)}k`;
  if (value >= 1000) return `${(value / 1000).toFixed(2)}k`;
  return value.toLocaleString();
}
