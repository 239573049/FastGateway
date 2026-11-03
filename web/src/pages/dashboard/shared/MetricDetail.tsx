import { useEffect, useMemo, useState } from "react";
import { useNavigate } from "react-router-dom";
import { ArrowRight, Loader2 } from "lucide-react";
import { AreaChart } from "@/components/ui/area-chart";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { getStatisticsRequests, type RankingType } from "@/services/StatisticsService";
import { useIsDark } from "../hooks/useIsDark";
import {
  formatCount,
  formatSeriesTime,
  type RankingItem,
  type RequestLogItem,
  type StatisticsOverview,
  type StatRange,
  type TimeseriesPoint,
} from "../types";
import { RankList } from "./RankList";

export type MetricId =
  | "requests"
  | "pv"
  | "uv"
  | "ip"
  | "error4xx"
  | "error4xxRate"
  | "error5xx"
  | "avgMs"
  | "blocked"
  | "blockRate"
  | "attackIps"
  | "blocked403"
  | "blocked429";

type Rankings = Partial<Record<RankingType, RankingItem[]>>;

const COPY: Record<MetricId, { title: string; hint: string }> = {
  requests: { title: "请求趋势与受访页面", hint: "范围内全部 HTTP 请求，含资源与接口。" },
  pv: { title: "访问次数（PV）", hint: "按页面浏览计次，同一访客多次打开会累计。" },
  uv: { title: "独立访客（UV）", hint: "按访客标识去重后的人数。" },
  ip: { title: "独立 IP", hint: "按源 IP 去重，适合观察出口与爬虫集中度。" },
  error4xx: { title: "4xx 状态码与路径", hint: "客户端错误，常见为 404 / 401 / 403。" },
  error4xxRate: { title: "4xx 错误率", hint: "4xx 次数 / 总请求。偏高通常是错误入口或扫描。" },
  error5xx: { title: "5xx 上游故障", hint: "网关或上游返回 5xx，需要立刻核对健康检查。" },
  avgMs: { title: "平均耗时", hint: "范围内请求平均 elapsed。突发升高看 5xx 与慢路径。" },
  blocked: { title: "拦截明细", hint: "黑名单、限流等策略实际挡住的请求。" },
  blockRate: { title: "拦截率", hint: "拦截次数 / 总请求。" },
  attackIps: { title: "攻击 IP", hint: "统计窗口内被拦截过的源 IP，以及最近的拦截记录。" },
  blocked403: { title: "403 / 访问控制", hint: "策略或机器人保护返回 403 的拦截。" },
  blocked429: { title: "限流拦截", hint: "触发 Rate Limit 返回 429 的请求。" },
};

function filterStatus(items: RankingItem[] | undefined, prefix: string) {
  return (items ?? []).filter((x) => String(x.key).startsWith(prefix));
}

export function MetricDetail({
  id,
  overview,
  series,
  rankings,
  range,
  host,
}: {
  id: MetricId;
  overview: StatisticsOverview | null;
  series: TimeseriesPoint[];
  rankings: Rankings;
  range: StatRange;
  host?: string;
}) {
  const isDark = useIsDark();
  const navigate = useNavigate();
  const meta = COPY[id];
  const chartData = useMemo(
    () =>
      series.map((p) => ({
        time: formatSeriesTime(p.time, range),
        requests: p.requests,
        blocked: p.blocked,
        error4xx: p.error4xx,
        error5xx: p.error5xx,
      })),
    [series, range]
  );

  const needBlockedLogs =
    id === "blocked" || id === "blockRate" || id === "blocked403" || id === "blocked429" || id === "attackIps";

  const [logs, setLogs] = useState<RequestLogItem[]>([]);
  const [loadingExtra, setLoadingExtra] = useState(false);

  useEffect(() => {
    let live = true;
    if (!needBlockedLogs) return;

    setLoadingExtra(true);
    const tasks: Promise<void>[] = [];

    if (needBlockedLogs) {
      tasks.push(
        getStatisticsRequests(range, { host, blocked: true, page: 1, pageSize: 6 }).then((res) => {
          if (!live) return;
          const items = Array.isArray((res as { data?: { items?: RequestLogItem[] } })?.data?.items)
            ? (res as { data: { items: RequestLogItem[] } }).data.items
            : [];
          setLogs(items);
        })
      );
    }

    Promise.all(tasks)
      .catch(() => undefined)
      .finally(() => {
        if (live) setLoadingExtra(false);
      });

    return () => {
      live = false;
    };
  }, [id, range, host, needBlockedLogs]);

  const status4xx = filterStatus(rankings.status, "4");
  const status5xx = filterStatus(rankings.status, "5");
  const requestColor = isDark ? "#3987e5" : "#2a78d6";
  const warnColor = isDark ? "#c98500" : "#eda100";
  const dangerColor = isDark ? "#d95926" : "#eb6834";

  const showTrafficChart = id === "requests" || id === "pv" || id === "uv" || id === "ip" || id === "avgMs";
  const show4xxChart = id === "error4xx" || id === "error4xxRate";
  const show5xxChart = id === "error5xx";
  const showBlockChart = id === "blocked" || id === "blockRate" || id === "blocked403" || id === "blocked429";

  return (
    <div className="space-y-3 p-4">
      <div className="flex flex-wrap items-start justify-between gap-2">
        <div>
          <div className="text-sm font-medium">{meta.title}</div>
          <p className="mt-0.5 text-xs text-muted-foreground">{meta.hint}</p>
        </div>
        {(id === "attackIps" || showBlockChart) && (
          <Button variant="ghost" size="sm" className="h-7 text-xs" onClick={() => navigate("/security/overview")}>
            打开安全中心
            <ArrowRight className="ml-1 h-3.5 w-3.5" />
          </Button>
        )}
      </div>

      <div className="grid gap-4 lg:grid-cols-[1.2fr_1fr]">
        <div className="relative h-[160px] min-w-0">
          {showTrafficChart && (
            <AreaChart
              data={chartData}
              categories={["requests"]}
              categoryLabels={{ requests: "请求" }}
              colors={[requestColor]}
              index="time"
              valueFormatter={formatCount}
              markPeak
            />
          )}
          {show4xxChart && (
            <AreaChart
              data={chartData}
              categories={["error4xx"]}
              categoryLabels={{ error4xx: "4xx" }}
              colors={[warnColor]}
              index="time"
              valueFormatter={formatCount}
              markPeak
            />
          )}
          {show5xxChart && (
            <AreaChart
              data={chartData}
              categories={["error5xx"]}
              categoryLabels={{ error5xx: "5xx" }}
              colors={["#d03b3b"]}
              index="time"
              valueFormatter={formatCount}
              markPeak
            />
          )}
          {showBlockChart && (
            <AreaChart
              data={chartData}
              categories={["blocked"]}
              categoryLabels={{ blocked: "拦截" }}
              colors={[dangerColor]}
              index="time"
              valueFormatter={formatCount}
              markPeak
            />
          )}
          {id === "attackIps" && (
            <div className="flex h-full flex-col justify-center gap-2 rounded-lg border border-border/60 bg-muted/20 px-3">
              <div className="flex items-baseline justify-between text-sm">
                <span className="text-muted-foreground">攻击 IP</span>
                <span className="font-semibold tabular-nums">{formatCount(overview?.attackIps ?? 0)}</span>
              </div>
              <div className="flex items-baseline justify-between text-sm">
                <span className="text-muted-foreground">拦截</span>
                <span className="font-semibold tabular-nums">
                  {formatCount(overview?.blocked ?? 0)}
                  <span className="ml-1 text-xs font-normal text-muted-foreground">
                    403 {formatCount(overview?.blocked403 ?? 0)} · 机器人 {formatCount(overview?.blockedBot ?? 0)} · 429 {formatCount(overview?.blocked429 ?? 0)}
                  </span>
                </span>
              </div>
            </div>
          )}
        </div>

        <div className="min-w-0 space-y-3">
          {(showTrafficChart || show4xxChart) && (
            <RankList
              items={rankings.path ?? []}
              maxRows={5}
              color={show4xxChart ? warnColor : requestColor}
            />
          )}
          {show5xxChart && (
            <RankList items={status5xx} maxRows={5} color="#d03b3b" emptyText="范围内没有 5xx" />
          )}
          {show4xxChart && status4xx.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {status4xx.slice(0, 6).map((item) => (
                <Badge key={item.key} variant="outline" className="font-mono text-[11px] font-normal">
                  {item.key} · {formatCount(item.count)}
                </Badge>
              ))}
            </div>
          )}
          {showBlockChart && (
            <div className="space-y-2">
              <div className="flex flex-wrap gap-1.5">
                <Badge variant="outline" className="font-normal">
                  403 拦截 {formatCount(overview?.blocked403 ?? 0)}
                </Badge>
                <Badge variant="outline" className="font-normal">
                  机器人挑战 {formatCount(overview?.blockedBot ?? 0)}
                </Badge>
                <Badge variant="outline" className="font-normal">
                  限流 {formatCount(overview?.blocked429 ?? 0)}
                </Badge>
              </div>
              {loadingExtra ? (
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  加载最近拦截…
                </div>
              ) : logs.length === 0 ? (
                <p className="text-xs text-muted-foreground">范围内没有拦截记录</p>
              ) : (
                <ul className="space-y-1.5">
                  {logs.map((row) => (
                    <li key={row.id} className="flex items-center justify-between gap-2 text-xs">
                      <code className="min-w-0 truncate font-mono">{row.path}</code>
                      <span className="shrink-0 tabular-nums text-muted-foreground">
                        {row.ip} · {row.status}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
          {id === "attackIps" && (
            <div>
              {loadingExtra ? (
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                  加载最近拦截…
                </div>
              ) : logs.length === 0 ? (
                <p className="text-xs text-muted-foreground">范围内没有拦截记录</p>
              ) : (
                <ul className="space-y-1.5">
                  {logs.map((row) => (
                    <li key={row.id} className="flex items-center justify-between gap-2 text-xs">
                      <code className="font-mono">{row.ip}</code>
                      <span className="min-w-0 truncate text-muted-foreground">
                        {row.path} · {row.status}
                      </span>
                    </li>
                  ))}
                </ul>
              )}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
