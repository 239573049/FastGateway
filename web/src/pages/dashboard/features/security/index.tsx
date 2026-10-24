import { useMemo, useState } from "react";
import { Ban, ShieldAlert, ShieldX } from "lucide-react";
import { toast } from "sonner";
import { Stagger, StaggerItem, SwapFade } from "@/components/motion";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { AreaChart } from "@/components/ui/area-chart";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import {
  getStatisticsOverview,
  getStatisticsRequests,
  getStatisticsTimeseries,
} from "@/services/StatisticsService";
import { AddAbnormalIpToBlacklist, GetAbnormalIps } from "@/services/AbnormalIpService";
import { usePolling } from "../../hooks/usePolling";
import { useIsDark } from "../../hooks/useIsDark";
import { useDashboardStore } from "../../store";
import { DonutLegendList } from "../../shared/DonutLegendList";
import { StatTile } from "../../shared/StatTile";
import { MetricGroup } from "../../shared/MetricGroup";
import { MetricDetail, type MetricId } from "../../shared/MetricDetail";
import { ExpandableCard } from "../../shared/ExpandableCard";
import {
  formatCount,
  formatInt,
  formatPercent2,
  formatSeriesTime,
  type RequestLogItem,
  type StatisticsOverview,
  type TimeseriesPoint,
} from "../../types";

interface AbnormalIpRow {
  ip: string;
  windowErrorCount: number;
  totalErrorCount: number;
  lastSeen: string;
  topErrorDescription?: string;
  lastPath?: string;
  lastStatusCode?: number;
}

const BLOCK_LABELS: Record<number, string> = { 1: "黑名单", 2: "限流", 3: "白名单拒绝", 4: "地区封禁", 5: "机器人挑战", 6: "风控验证", 7: "风控封禁" };

export default function SecurityTab() {
  const { range, host } = useDashboardStore();
  const isDark = useIsDark();

  const [overview, setOverview] = useState<StatisticsOverview | null>(null);
  const [series, setSeries] = useState<TimeseriesPoint[]>([]);
  const [abnormalIps, setAbnormalIps] = useState<AbnormalIpRow[]>([]);
  const [blockedRequests, setBlockedRequests] = useState<RequestLogItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [metric, setMetric] = useState<MetricId | null>(null);
  const [openAbnormal, setOpenAbnormal] = useState(false);
  const [openBlocked, setOpenBlocked] = useState(false);

  usePolling(
    async (signal) => {
      const [overviewRes, seriesRes, abnormalRes, blockedRes] = await Promise.all([
        getStatisticsOverview(range, host, { signal }),
        getStatisticsTimeseries(range, host, { signal }),
        GetAbnormalIps(1, 10),
        getStatisticsRequests(range, { host, blocked: true, page: 1, pageSize: 10 }, { signal }),
      ]);
      const overviewData = (overviewRes as { data?: StatisticsOverview })?.data;
      const seriesData = (seriesRes as { data?: TimeseriesPoint[] })?.data;
      const abnormalData = (abnormalRes as { data?: { items?: AbnormalIpRow[] } })?.data;
      const blockedData = (blockedRes as { data?: { items?: RequestLogItem[] } })?.data;
      if (overviewData) setOverview(overviewData);
      if (Array.isArray(seriesData)) setSeries(seriesData);
      if (Array.isArray(abnormalData?.items)) setAbnormalIps(abnormalData.items);
      if (Array.isArray(blockedData?.items)) setBlockedRequests(blockedData.items);
      setLoading(false);
    },
    30000,
    [range, host]
  );

  const chartData = useMemo(
    () =>
      series.map((p) => ({
        time: formatSeriesTime(p.time, range),
        blocked: p.blocked,
        error5xx: p.error5xx,
      })),
    [series, range]
  );

  const reasonItems = useMemo(() => {
    const blocked403 = overview?.blocked403 ?? 0;
    const blocked429 = overview?.blocked429 ?? 0;
    const blockedBot = overview?.blockedBot ?? 0;
    const total = blocked403 + blocked429;
    if (total === 0) return [];
    return [
      { key: "其他策略 (403)", count: Math.max(0, blocked403 - blockedBot), percent: (Math.max(0, blocked403 - blockedBot) / total) * 100 },
      { key: "机器人挑战 (403)", count: blockedBot, percent: (blockedBot / total) * 100 },
      { key: "限流 (429)", count: blocked429, percent: (blocked429 / total) * 100 },
    ].filter((x) => x.count > 0);
  }, [overview]);

  const addToBlacklist = async (ip: string) => {
    try {
      await AddAbnormalIpToBlacklist(ip);
      toast.success(`已将 ${ip} 加入黑名单`);
    } catch (error) {
      const message = error instanceof Error ? error.message : "加入黑名单失败";
      toast.error(message);
    }
  };

  const toggleMetric = (id: MetricId) => setMetric((prev) => (prev === id ? null : id));
  const metricIds: MetricId[] = ["blocked", "blockRate", "blocked403", "blocked429", "attackIps", "abnormalLive"];

  return (
    <Stagger className="space-y-4">
      <StaggerItem>
        <MetricGroup
          title="安全态势"
          columns="grid-cols-2 xl:grid-cols-6"
          open={metric !== null && metricIds.includes(metric)}
          detail={
            metric && metricIds.includes(metric) ? (
              <MetricDetail
                id={metric}
                overview={overview}
                series={series}
                rankings={{}}
                range={range}
                host={host}
              />
            ) : null
          }
        >
          <StatTile
            label="拦截次数"
            value={overview?.blocked ?? 0}
            format={formatCount}
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
            label="403 拦截"
            value={overview?.blocked403 ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "blocked403"}
            onSelect={() => toggleMetric("blocked403")}
          />
          <StatTile
            label="限流拦截"
            value={overview?.blocked429 ?? 0}
            format={formatCount}
            loading={loading}
            expandable
            selected={metric === "blocked429"}
            onSelect={() => toggleMetric("blocked429")}
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
          <StatTile
            label="实时异常 IP"
            value={overview?.abnormalIpsLive ?? 0}
            format={formatInt}
            tone={(overview?.abnormalIpsLive ?? 0) > 0 ? "warning" : "default"}
            loading={loading}
            expandable
            selected={metric === "abnormalLive"}
            onSelect={() => toggleMetric("abnormalLive")}
          />
        </MetricGroup>
      </StaggerItem>

      <StaggerItem className="grid gap-4 xl:grid-cols-3">
        <Card className="border-border/60 bg-card/80 shadow-sm xl:col-span-2">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <ShieldAlert className="h-4 w-4 text-orange-500" />
              拦截与 5xx 趋势
            </CardTitle>
          </CardHeader>
          <CardContent className="relative h-[220px] pt-0">
            <SwapFade loading={loading} skeleton={<Skeleton className="h-full w-full" />} className="h-full">
              <AreaChart
                data={chartData}
                categories={["blocked", "error5xx"]}
                categoryLabels={{ blocked: "拦截", error5xx: "5xx 错误" }}
                colors={[isDark ? "#d95926" : "#eb6834", "#d03b3b"]}
                index="time"
                valueFormatter={formatCount}
              />
            </SwapFade>
          </CardContent>
        </Card>

        <Card className="border-border/60 bg-card/80 shadow-sm">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <ShieldX className="h-4 w-4 text-red-500" />
              拦截原因
            </CardTitle>
          </CardHeader>
          <CardContent>
            <DonutLegendList
              title="按拦截来源"
              items={reasonItems}
              colors={[isDark ? "#9085e9" : "#4a3aa7", isDark ? "#c98500" : "#eda100"]}
              loading={loading}
            />
          </CardContent>
        </Card>
      </StaggerItem>

      <StaggerItem className="grid gap-4 xl:grid-cols-2">
        <ExpandableCard
          title="实时异常 IP"
          icon={<Ban className="h-4 w-4 text-red-500" />}
          badge={
            <span className="text-[10px] font-normal text-muted-foreground">近 60 秒错误</span>
          }
          expanded={openAbnormal}
          onToggle={() => setOpenAbnormal((v) => !v)}
          detail={
            abnormalIps.length > 4 ? (
              <AbnormalTable rows={abnormalIps.slice(4)} onBan={addToBlacklist} />
            ) : (
              <p className="px-1 py-2 text-xs text-muted-foreground">没有更多异常 IP</p>
            )
          }
        >
          <AbnormalTable rows={abnormalIps.slice(0, 4)} onBan={addToBlacklist} empty="当前没有异常 IP" />
        </ExpandableCard>

        <ExpandableCard
          title="最近被拦截请求"
          icon={<ShieldX className="h-4 w-4 text-orange-500" />}
          expanded={openBlocked}
          onToggle={() => setOpenBlocked((v) => !v)}
          detail={
            blockedRequests.length > 4 ? (
              <BlockedTable rows={blockedRequests.slice(4)} />
            ) : (
              <p className="px-1 py-2 text-xs text-muted-foreground">没有更多拦截记录</p>
            )
          }
        >
          <BlockedTable rows={blockedRequests.slice(0, 4)} empty="范围内没有拦截记录" />
        </ExpandableCard>
      </StaggerItem>
    </Stagger>
  );
}

function AbnormalTable({
  rows,
  onBan,
  empty,
}: {
  rows: AbnormalIpRow[];
  onBan: (ip: string) => void;
  empty?: string;
}) {
  return (
    <Table>
      <TableHeader>
        <TableRow className="bg-muted/30 hover:bg-muted/30">
          <TableHead>IP</TableHead>
          <TableHead className="w-[90px]">窗口错误</TableHead>
          <TableHead className="w-[90px]">累计错误</TableHead>
          <TableHead>最近错误</TableHead>
          <TableHead className="w-[110px] text-right">操作</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.length === 0 ? (
          <TableRow>
            <TableCell colSpan={5} className="py-8 text-center text-xs text-muted-foreground">
              {empty ?? "没有更多数据"}
            </TableCell>
          </TableRow>
        ) : (
          rows.map((row) => (
            <TableRow key={row.ip} className="hover:bg-muted/30">
              <TableCell className="font-mono text-xs">{row.ip}</TableCell>
              <TableCell className="tabular-nums">{row.windowErrorCount}</TableCell>
              <TableCell className="tabular-nums">{row.totalErrorCount}</TableCell>
              <TableCell className="max-w-[220px] truncate text-xs text-muted-foreground">
                {row.topErrorDescription || "-"}
              </TableCell>
              <TableCell className="text-right">
                <Button variant="outline" size="sm" className="h-7 text-xs" onClick={() => onBan(row.ip)}>
                  加入黑名单
                </Button>
              </TableCell>
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  );
}

function BlockedTable({ rows, empty }: { rows: RequestLogItem[]; empty?: string }) {
  return (
    <Table>
      <TableHeader>
        <TableRow className="bg-muted/30 hover:bg-muted/30">
          <TableHead>IP</TableHead>
          <TableHead>归属地</TableHead>
          <TableHead>路径</TableHead>
          <TableHead className="w-[90px]">原因</TableHead>
          <TableHead className="w-[80px]">状态</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {rows.length === 0 ? (
          <TableRow>
            <TableCell colSpan={5} className="py-8 text-center text-xs text-muted-foreground">
              {empty ?? "没有更多数据"}
            </TableCell>
          </TableRow>
        ) : (
          rows.map((row) => (
            <TableRow key={row.id} className="hover:bg-muted/30">
              <TableCell className="font-mono text-xs">{row.ip}</TableCell>
              <TableCell className="text-xs">
                {row.country}
                {row.province ? ` · ${row.province}` : ""}
              </TableCell>
              <TableCell className="max-w-[180px] truncate font-mono text-xs" title={row.path}>
                {row.path}
              </TableCell>
              <TableCell>
                <Badge variant="outline" className="text-[10px]">
                  {BLOCK_LABELS[row.blocked] ?? "拦截"}
                </Badge>
              </TableCell>
              <TableCell className="tabular-nums text-xs">{row.status}</TableCell>
            </TableRow>
          ))
        )}
      </TableBody>
    </Table>
  );
}
