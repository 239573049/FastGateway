import { useMemo, useState } from "react";
import { Activity, MonitorSmartphone, PieChart, ShieldAlert } from "lucide-react";
import { Reveal, Stagger, StaggerItem, SwapFade } from "@/components/motion";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
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
  formatMs,
  formatPercent2,
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
import { MetricGroup } from "../../shared/MetricGroup";
import { MetricDetail, type MetricId } from "../../shared/MetricDetail";
import { ExpandableCard } from "../../shared/ExpandableCard";
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
  const [metric, setMetric] = useState<MetricId | null>(null);
  const [openClient, setOpenClient] = useState(false);
  const [openStatus, setOpenStatus] = useState(false);
  const [openRank, setOpenRank] = useState<RankingType | null>(null);

  usePolling(
    async (signal) => {
      const [overviewRes, seriesRes] = await Promise.all([
        getStatisticsOverview(range, host, { signal }),
        getStatisticsTimeseries(range, host, { signal }),
      ]);
      const overviewData = (overviewRes as { data?: StatisticsOverview })?.data;
      const seriesData = (seriesRes as { data?: TimeseriesPoint[] })?.data;
      if (overviewData) setOverview(overviewData);
      if (Array.isArray(seriesData)) setSeries(seriesData);
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
        const data = (results[i] as { data?: { items?: RankingItem[] } })?.data;
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

  const toggleMetric = (id: MetricId) => setMetric((prev) => (prev === id ? null : id));

  const detailFor = (ids: MetricId[]) =>
    metric && ids.includes(metric) ? (
      <MetricDetail
        id={metric}
        overview={overview}
        series={series}
        rankings={rankings}
        range={range}
        host={host}
      />
    ) : null;

  return (
    <Stagger className="space-y-4">
      {overview?.available === false && (
        <Alert variant="destructive">
          <AlertTitle>统计服务不可用</AlertTitle>
          <AlertDescription>
            {overview.unavailableReason?.trim()
              ? overview.unavailableReason
              : "统计数据库未能启动，仪表盘无法记录流量（网关转发不受影响）。请查看运行日志里的「统计数据库初始化失败」。"}
          </AlertDescription>
        </Alert>
      )}

      <StaggerItem className="grid gap-4 xl:grid-cols-[1fr_280px]">
        <MetricGroup
          title="流量"
          columns="grid-cols-2 xl:grid-cols-4"
          open={metric === "requests" || metric === "pv" || metric === "uv" || metric === "ip"}
          detail={detailFor(["requests", "pv", "uv", "ip"])}
        >
          <StatTile
            label="请求次数"
            value={overview?.requests ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "requests"}
            onSelect={() => toggleMetric("requests")}
          />
          <StatTile
            label="访问次数（PV）"
            value={overview?.pageViews ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "pv"}
            onSelect={() => toggleMetric("pv")}
          />
          <StatTile
            label="独立访客（UV）"
            value={overview?.uniqueVisitors ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "uv"}
            onSelect={() => toggleMetric("uv")}
          />
          <StatTile
            label="独立 IP"
            value={overview?.uniqueIps ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "ip"}
            onSelect={() => toggleMetric("ip")}
          />
        </MetricGroup>
        <QpsSparkCard />
      </StaggerItem>

      <StaggerItem className="grid gap-4 md:grid-cols-2">
        <MetricGroup
          title="健康"
          columns="grid-cols-2"
          open={metric === "error4xx" || metric === "error4xxRate" || metric === "error5xx" || metric === "avgMs"}
          detail={detailFor(["error4xx", "error4xxRate", "error5xx", "avgMs"])}
        >
          <StatTile
            label="4xx 错误数"
            value={overview?.error4xx ?? 0}
            format={formatCount}
            tone="warning"
            loading={loading}
            expandable
            selected={metric === "error4xx"}
            onSelect={() => toggleMetric("error4xx")}
          />
          <StatTile
            label="4xx 错误率"
            value={overview?.error4xxRate ?? 0}
            format={formatPercent2}
            round={2}
            tone="warning"
            loading={loading}
            expandable
            selected={metric === "error4xxRate"}
            onSelect={() => toggleMetric("error4xxRate")}
          />
          <StatTile
            label="5xx 错误数"
            value={overview?.error5xx ?? 0}
            format={formatCount}
            tone="danger"
            loading={loading}
            expandable
            selected={metric === "error5xx"}
            onSelect={() => toggleMetric("error5xx")}
          />
          <StatTile
            label="平均耗时"
            value={overview?.avgElapsedMs ?? 0}
            format={formatMs}
            loading={loading}
            expandable
            selected={metric === "avgMs"}
            onSelect={() => toggleMetric("avgMs")}
          />
        </MetricGroup>

        <MetricGroup
          title="防护"
          columns="grid-cols-3"
          open={metric === "blocked" || metric === "blockRate" || metric === "attackIps"}
          detail={detailFor(["blocked", "blockRate", "attackIps"])}
        >
          <StatTile
            label="拦截次数"
            value={overview?.blocked ?? 0}
            format={formatCount}
            sub={`黑名单 ${formatCount(overview?.blocked403 ?? 0)} · 限流 ${formatCount(overview?.blocked429 ?? 0)}`}
            tone="danger"
            loading={loading}
            expandable
            selected={metric === "blocked"}
            onSelect={() => toggleMetric("blocked")}
          />
          <StatTile
            label="拦截率"
            value={overview?.blockRate ?? 0}
            format={formatPercent2}
            round={2}
            loading={loading}
            expandable
            selected={metric === "blockRate"}
            onSelect={() => toggleMetric("blockRate")}
          />
          <StatTile
            label="攻击 IP"
            value={overview?.attackIps ?? 0}
            format={formatCount}
            tone="danger"
            loading={loading}
            expandable
            selected={metric === "attackIps"}
            onSelect={() => toggleMetric("attackIps")}
          />
        </MetricGroup>
      </StaggerItem>

      <StaggerItem className="grid gap-4 xl:grid-cols-3">
        <div className="space-y-4 xl:col-span-2">
          <Reveal y={0}>
            <GeoCard />
          </Reveal>
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
            <CardContent className="relative h-[180px] pt-0">
              <SwapFade loading={loading} skeleton={<Skeleton className="h-full w-full" />} className="h-full">
                <AreaChart
                  data={chartData}
                  categories={["requests"]}
                  categoryLabels={{ requests: "请求" }}
                  colors={[isDark ? "#3987e5" : "#2a78d6"]}
                  index="time"
                  valueFormatter={formatCount}
                  markPeak
                />
              </SwapFade>
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
            <CardContent className="relative h-[180px] pt-0">
              <SwapFade loading={loading} skeleton={<Skeleton className="h-full w-full" />} className="h-full">
                <AreaChart
                  data={chartData}
                  categories={["blocked"]}
                  categoryLabels={{ blocked: "拦截" }}
                  colors={[isDark ? "#d95926" : "#eb6834"]}
                  index="time"
                  valueFormatter={formatCount}
                  markPeak
                />
              </SwapFade>
            </CardContent>
          </Card>
        </div>
      </StaggerItem>

      <StaggerItem className="grid gap-4 md:grid-cols-2">
        <ExpandableCard
          title="客户端"
          icon={<MonitorSmartphone className="h-4 w-4 text-emerald-600" />}
          expanded={openClient}
          onToggle={() => setOpenClient((v) => !v)}
          detail={
            <DonutLegendList
              title="浏览器"
              items={rankings.browser ?? []}
              colors={categorical}
              loading={loading}
            />
          }
        >
          <DonutLegendList title="操作系统" items={rankings.os ?? []} colors={categorical} loading={loading} />
        </ExpandableCard>

        <ExpandableCard
          title="响应状态"
          icon={<PieChart className="h-4 w-4 text-blue-500" />}
          expanded={openStatus}
          onToggle={() => setOpenStatus((v) => !v)}
          extra={
            statusItems.length > 4 ? (
              <span className="text-[10px] text-muted-foreground">{statusItems.length} 种状态码</span>
            ) : null
          }
          detail={
            <DonutLegendList
              title="其余状态码"
              items={statusItems.slice(4)}
              colors={statusItems.slice(4).map((x) => statusColorOf(x.key))}
              loading={loading}
              maxRows={8}
            />
          }
        >
          <DonutLegendList
            title="状态码分布"
            items={statusItems}
            colors={statusItems.map((x) => statusColorOf(x.key))}
            loading={loading}
            maxRows={4}
          />
        </ExpandableCard>
      </StaggerItem>

      <StaggerItem className="grid gap-4 md:grid-cols-2">
        {(
          [
            { type: "referer_host", title: "外部来源域名" },
            { type: "referer_url", title: "外部来源页面" },
            { type: "host", title: "受访域名" },
            { type: "path", title: "受访页面" },
          ] as Array<{ type: RankingType; title: string }>
        ).map(({ type, title }) => (
          <StaggerItem key={type} y={0} hoverLift className="rounded-xl">
            <ExpandableCard
              title={title}
              expanded={openRank === type}
              onToggle={() => setOpenRank((prev) => (prev === type ? null : type))}
              extra={
                <Button
                  variant="ghost"
                  size="sm"
                  className="h-6 px-2 text-xs text-muted-foreground"
                  onClick={() => setDialog({ type, title })}
                >
                  全部
                </Button>
              }
              detail={
                <RankList
                  items={(rankings[type] ?? []).slice(5)}
                  loading={loading}
                  maxRows={5}
                  emptyText="没有更多条目"
                  color={type.startsWith("referer") ? (isDark ? "#3987e5" : "#2a78d6") : isDark ? "#d55181" : "#e87ba4"}
                />
              }
            >
              <RankList
                items={rankings[type] ?? []}
                loading={loading}
                maxRows={5}
                color={type.startsWith("referer") ? (isDark ? "#3987e5" : "#2a78d6") : isDark ? "#d55181" : "#e87ba4"}
              />
            </ExpandableCard>
          </StaggerItem>
        ))}
      </StaggerItem>

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
    </Stagger>
  );
}
