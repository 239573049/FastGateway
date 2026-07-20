import { get } from "@/utils/fetch";
import type { StatRange } from "@/pages/dashboard/types";

const baseUrl = "/api/v1/statistics";

type Opts = { signal?: AbortSignal };

export type RankingType =
  | "host"
  | "path"
  | "referer_host"
  | "referer_url"
  | "os"
  | "browser"
  | "status";

const qs = (params: Record<string, string | number | boolean | undefined>) =>
  Object.entries(params)
    .filter(([, v]) => v !== undefined && v !== "")
    .map(([k, v]) => `${k}=${encodeURIComponent(String(v))}`)
    .join("&");

export const getStatisticsOverview = (range: StatRange, host?: string, opts?: Opts) =>
  get(`${baseUrl}/overview?${qs({ range, host })}`, opts);

export const getStatisticsTimeseries = (range: StatRange, host?: string, opts?: Opts) =>
  get(`${baseUrl}/timeseries?${qs({ range, host })}`, opts);

export const getStatisticsGeo = (
  range: StatRange,
  scope: "world" | "china",
  mode: "all" | "blocked",
  host?: string,
  top = 20,
  opts?: Opts
) => get(`${baseUrl}/geo?${qs({ range, scope, mode, host, top })}`, opts);

export const getStatisticsRankings = (
  range: StatRange,
  type: RankingType,
  host?: string,
  top = 10,
  mode: "all" | "blocked" = "all",
  opts?: Opts
) => get(`${baseUrl}/rankings?${qs({ range, type, host, top, mode })}`, opts);

export const getStatisticsRankingsPaged = (
  range: StatRange,
  type: RankingType,
  page: number,
  pageSize: number,
  host?: string,
  opts?: Opts
) => get(`${baseUrl}/rankings?${qs({ range, type, page, pageSize, host })}`, opts);

export const getStatisticsRequests = (
  range: StatRange,
  params: { host?: string; ip?: string; status?: number; blocked?: boolean; page?: number; pageSize?: number },
  opts?: Opts
) => get(`${baseUrl}/requests?${qs({ range, ...params })}`, opts);
