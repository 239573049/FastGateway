import { useMemo, useState } from "react";
import { Activity, MonitorSmartphone, PieChart, ShieldAlert } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { AreaChart } from "@/components/ui/area-chart";
import { Skeleton } from "@/components/ui/skeleton";
import {
  getStatisticsOverview,
  getStatisticsRankings,
  getStatisticsTimeseries,
  type RankingType,
} from "@/services/StatisticsService";
import { usePolling } from "../../hooks/usePolling";
import { useIsDark } from "../../hooks/useIsDark";
import { useDashboardStore } from "../../store";
import {
  formatCount,
  formatSeriesTime,
  type RankingItem,
  type StatisticsOverview,
  type TimeseriesPoint,
} from "../../types";
import { StatTile } from "../../shared/StatTile";
import { QpsSparkCard } from "../../shared/QpsSparkCard";
import { RankList } from "../../shared/RankList";
import { RankListDialog } from "../../shared/RankListDialog";
import { DonutLegendList } from "../../shared/DonutLegendList";
import { getCategorical, statusColorOf } from "../../shared/chart-colors";
import { GeoCard } from "./GeoCard";

const RANK_TYPES: RankingType[] = ["os", "browser", "status", "referer_host", "referer_url", "host", "path"];

type Rankings = Partial<Record<RankingType, RankingItem[]>>;

export default function TrafficTab() {
  const { range, host } = useDashboardStore();
  const isDark = useIsDark();
  const categorical = getCategorical(isDark);

  const [overview, setOverview] = useState<StatisticsOverview | null>(null);
  const [series, setSeries] = useState<TimeseriesPoint[]>([]);
  const [rankings, setRankings] = useState<Rankings>({});
  const [loading, setLoading] = useState(true);
  const [dialog, setDialog] = useState<{ type: RankingType; title: string } | null>(null);

  usePolling(
    async (signal) => {
      const [overviewRes, seriesRes]: any[] = await Promise.all([
        getStatisticsOverview(range, host, { signal }),
        getStatisticsTimeseries(range, host, { signal }),
      ]);
      if (overviewRes?.data) setOverview(overviewRes.data);
      if (Array.isArray(seriesRes?.data)) setSeries(seriesRes.data);
      setLoading(false);
    },
    30000,
    [range, host]
  );

  usePolling(
    async (signal) => {
      const results = await Promise.all(
        RANK_TYPES.map((type) => getStatisticsRankings(range, type, host, 10, "all", { signal }))
      );
      const next: Rankings = {};
      RANK_TYPES.forEach((type, i) => {
        const data = (results[i] as any)?.data;
        next[type] = Array.isArray(data?.items) ? data.items : [];
      });
      setRankings(next);
    },
    60000,
    [range, host]
  );

  const chartData = useMemo(
    () =>
      series.map((p) => ({
        time: formatSeriesTime(p.time, range),
        requests: p.requests,
        blocked: p.blocked,
      })),
    [series, range]
  );

  const requestsPeak = useMemo(() => series.reduce((acc, p) => Math.max(acc, p.requests), 0), [series]);
  const blockedPeak = useMemo(() => series.reduce((acc, p) => Math.max(acc, p.blocked), 0), [series]);

  const statusItems = useMemo(
    () => (rankings.status ?? []).map((x) => ({ ...x, key: x.key })),
    [rankings.status]
  );

  return (
    <div className="space-y-4">
      {/* 指标条 + 实时QPS */}
      <div className="grid gap-4 xl:grid-cols-[1fr_280px]">
        <div className="space-y-4">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardContent className="grid grid-cols-2 divide-y divide-border/50 p-0 sm:grid-cols-3 sm:divide-y-0 xl:grid-cols-6 xl:divide-x">
              <StatTile label="请求次数" value={formatCount(overview?.requests ?? 0)} loading={loading} />
              <StatTile label="访问次数（PV）" value={formatCount(overview?.pageViews ?? 0)} loading={loading} />
              <StatTile label="独立访客（UV）" value={formatCount(overview?.uniqueVisitors ?? 0)} loading={loading} />
              <StatTile label="独立 IP" value={formatCount(overview?.uniqueIps ?? 0)} loading={loading} />
              <StatTile
                label="拦截次数"
                value={formatCount(overview?.blocked ?? 0)}
                sub={`黑名单 ${formatCount(overview?.blocked403 ?? 0)} · 限流 ${formatCount(overview?.blocked429 ?? 0)}`}
                tone="danger"
                loading={loading}
              />
              <StatTile
                label="攻击 IP"
                value={formatCount(overview?.attackIps ?? 0)}
                sub={overview?.abnormalIpsLive ? `实时异常 ${overview.abnormalIpsLive}` : undefined}
                tone="danger"
                loading={loading}
              />
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardContent className="grid grid-cols-2 divide-y divide-border/50 p-0 sm:grid-cols-3 sm:divide-y-0 xl:grid-cols-6 xl:divide-x">
              <StatTile label="4xx 错误数" value={formatCount(overview?.error4xx ?? 0)} tone="warning" loading={loading} />
              <StatTile label="4xx 错误率" value={`${(overview?.error4xxRate ?? 0).toFixed(2)}%`} tone="warning" loading={loading} />
              <StatTile label="拦截率" value={`${(overview?.blockRate ?? 0).toFixed(2)}%`} loading={loading} />
              <StatTile label="平均耗时" value={`${overview?.avgElapsedMs ?? 0} ms`} loading={loading} />
              <StatTile label="5xx 错误数" value={formatCount(overview?.error5xx ?? 0)} tone="danger" loading={loading} />
              <StatTile label="5xx 错误率" value={`${(overview?.error5xxRate ?? 0).toFixed(2)}%`} tone="danger" loading={loading} />
            </CardContent>
          </Card>
        </div>

        <QpsSparkCard />
      </div>

      {/* 主体：地理 + 趋势 */}
      <div className="grid gap-4 xl:grid-cols-3">
        <div className="space-y-4 xl:col-span-2">
          <GeoCard />

          <div className="grid gap-4 md:grid-cols-2">
            <Card className="border-border/60 bg-card/80 shadow-sm">
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-sm font-medium">
                  <MonitorSmartphone className="h-4 w-4 text-emerald-600" />
                  客户端
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <DonutLegendList
                  title="操作系统"
                  items={rankings.os ?? []}
                  colors={categorical}
                  loading={loading}
                />
                <DonutLegendList
                  title="浏览器"
                  items={rankings.browser ?? []}
                  colors={categorical}
                  loading={loading}
                />
              </CardContent>
            </Card>

            <Card className="border-border/60 bg-card/80 shadow-sm">
              <CardHeader className="pb-2">
                <CardTitle className="flex items-center gap-2 text-sm font-medium">
                  <PieChart className="h-4 w-4 text-blue-500" />
                  响应状态
                </CardTitle>
              </CardHeader>
              <CardContent>
                <DonutLegendList
                  title="状态码分布"
                  items={statusItems}
                  colors={statusItems.map((x) => statusColorOf(x.key))}
                  loading={loading}
                  maxRows={8}
                />
              </CardContent>
            </Card>
          </div>
        </div>

        <div className="space-y-4">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center justify-between text-sm font-medium">
                <span className="flex items-center gap-2">
                  <Activity className="h-4 w-4 text-blue-500" />
                  访问情况
                </span>
                <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-normal text-muted-foreground">
                  峰值 {formatCount(requestsPeak)}
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent className="h-[180px] pt-0">
              {loading ? (
                <Skeleton className="h-full w-full" />
              ) : (
                <AreaChart
                  data={chartData}
                  categories={["requests"]}
                  categoryLabels={{ requests: "请求" }}
                  colors={[isDark ? "#3987e5" : "#2a78d6"]}
                  index="time"
                  valueFormatter={formatCount}
                  markPeak
                />
              )}
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="pb-2">
              <CardTitle className="flex items-center justify-between text-sm font-medium">
                <span className="flex items-center gap-2">
                  <ShieldAlert className="h-4 w-4 text-orange-500" />
                  拦截情况
                </span>
                <span className="rounded-full bg-muted px-2 py-0.5 text-[10px] font-normal text-muted-foreground">
                  峰值 {formatCount(blockedPeak)}
                </span>
              </CardTitle>
            </CardHeader>
            <CardContent className="h-[180px] pt-0">
              {loading ? (
                <Skeleton className="h-full w-full" />
              ) : (
                <AreaChart
                  data={chartData}
                  categories={["blocked"]}
                  categoryLabels={{ blocked: "拦截" }}
                  colors={[isDark ? "#d95926" : "#eb6834"]}
                  index="time"
                  valueFormatter={formatCount}
                  markPeak
                />
              )}
            </CardContent>
          </Card>
        </div>
      </div>

      {/* 来源与受访排行 */}
      <div className="grid gap-4 md:grid-cols-2">
        {(
          [
            { type: "referer_host", title: "外部来源域名" },
            { type: "referer_url", title: "外部来源页面" },
            { type: "host", title: "受访域名" },
            { type: "path", title: "受访页面" },
          ] as Array<{ type: RankingType; title: string }>
        ).map(({ type, title }) => (
          <Card key={type} className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
              <CardTitle className="text-sm font-medium">{title}</CardTitle>
              <Button
                variant="ghost"
                size="sm"
                className="h-6 px-2 text-xs text-muted-foreground"
                onClick={() => setDialog({ type, title })}
              >
                查看更多
              </Button>
            </CardHeader>
            <CardContent>
              <RankList
                items={rankings[type] ?? []}
                loading={loading}
                maxRows={5}
                color={type.startsWith("referer") ? (isDark ? "#3987e5" : "#2a78d6") : isDark ? "#d55181" : "#e87ba4"}
              />
            </CardContent>
          </Card>
        ))}
      </div>

      {dialog && (
        <RankListDialog
          open
          onOpenChange={(open) => !open && setDialog(null)}
          title={dialog.title}
          type={dialog.type}
          range={range}
          host={host}
        />
      )}
    </div>
  );
}
