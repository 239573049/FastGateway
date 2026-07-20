import { useMemo, useState } from "react";
import { Ban, ShieldAlert, ShieldX } from "lucide-react";
import { toast } from "sonner";
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
import {
  formatCount,
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

const BLOCK_LABELS: Record<number, string> = { 1: "黑名单", 2: "限流", 3: "白名单拒绝" };

export default function SecurityTab() {
  const { range, host } = useDashboardStore();
  const isDark = useIsDark();

  const [overview, setOverview] = useState<StatisticsOverview | null>(null);
  const [series, setSeries] = useState<TimeseriesPoint[]>([]);
  const [abnormalIps, setAbnormalIps] = useState<AbnormalIpRow[]>([]);
  const [blockedRequests, setBlockedRequests] = useState<RequestLogItem[]>([]);
  const [loading, setLoading] = useState(true);

  usePolling(
    async (signal) => {
      const [overviewRes, seriesRes, abnormalRes, blockedRes]: any[] = await Promise.all([
        getStatisticsOverview(range, host, { signal }),
        getStatisticsTimeseries(range, host, { signal }),
        GetAbnormalIps(1, 10),
        getStatisticsRequests(range, { host, blocked: true, page: 1, pageSize: 10 }, { signal }),
      ]);
      if (overviewRes?.data) setOverview(overviewRes.data);
      if (Array.isArray(seriesRes?.data)) setSeries(seriesRes.data);
      if (Array.isArray(abnormalRes?.data?.items)) setAbnormalIps(abnormalRes.data.items);
      if (Array.isArray(blockedRes?.data?.items)) setBlockedRequests(blockedRes.data.items);
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
    const total = blocked403 + blocked429;
    if (total === 0) return [];
    return [
      { key: "黑名单/白名单 (403)", count: blocked403, percent: (blocked403 / total) * 100 },
      { key: "限流 (429)", count: blocked429, percent: (blocked429 / total) * 100 },
    ].filter((x) => x.count > 0);
  }, [overview]);

  const addToBlacklist = async (ip: string) => {
    try {
      await AddAbnormalIpToBlacklist(ip);
      toast.success(`已将 ${ip} 加入黑名单`);
    } catch (error: any) {
      toast.error(error?.message || "加入黑名单失败");
    }
  };

  return (
    <div className="space-y-4">
      <Card className="border-border/60 bg-card/80 shadow-sm">
        <CardContent className="grid grid-cols-2 divide-y divide-border/50 p-0 sm:grid-cols-3 sm:divide-y-0 xl:grid-cols-6 xl:divide-x">
          <StatTile label="拦截次数" value={formatCount(overview?.blocked ?? 0)} tone="danger" loading={loading} />
          <StatTile label="拦截率" value={`${(overview?.blockRate ?? 0).toFixed(2)}%`} loading={loading} />
          <StatTile label="黑名单拦截" value={formatCount(overview?.blocked403 ?? 0)} loading={loading} />
          <StatTile label="限流拦截" value={formatCount(overview?.blocked429 ?? 0)} loading={loading} />
          <StatTile label="攻击 IP" value={formatCount(overview?.attackIps ?? 0)} tone="danger" loading={loading} />
          <StatTile
            label="实时异常 IP"
            value={String(overview?.abnormalIpsLive ?? 0)}
            tone={(overview?.abnormalIpsLive ?? 0) > 0 ? "warning" : "default"}
            loading={loading}
          />
        </CardContent>
      </Card>

      <div className="grid gap-4 xl:grid-cols-3">
        <Card className="border-border/60 bg-card/80 shadow-sm xl:col-span-2">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <ShieldAlert className="h-4 w-4 text-orange-500" />
              拦截与 5xx 趋势
            </CardTitle>
          </CardHeader>
          <CardContent className="h-[220px] pt-0">
            {loading ? (
              <Skeleton className="h-full w-full" />
            ) : (
              <AreaChart
                data={chartData}
                categories={["blocked", "error5xx"]}
                categoryLabels={{ blocked: "拦截", error5xx: "5xx 错误" }}
                colors={[isDark ? "#d95926" : "#eb6834", "#d03b3b"]}
                index="time"
                valueFormatter={formatCount}
              />
            )}
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
      </div>

      <div className="grid gap-4 xl:grid-cols-2">
        <Card className="border-border/60 bg-card/80 shadow-sm">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <Ban className="h-4 w-4 text-red-500" />
              实时异常 IP（近 60 秒错误）
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
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
                {abnormalIps.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-xs text-muted-foreground">
                      当前没有异常 IP
                    </TableCell>
                  </TableRow>
                ) : (
                  abnormalIps.map((row) => (
                    <TableRow key={row.ip} className="hover:bg-muted/30">
                      <TableCell className="font-mono text-xs">{row.ip}</TableCell>
                      <TableCell className="tabular-nums">{row.windowErrorCount}</TableCell>
                      <TableCell className="tabular-nums">{row.totalErrorCount}</TableCell>
                      <TableCell className="max-w-[220px] truncate text-xs text-muted-foreground">
                        {row.topErrorDescription || "-"}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button variant="outline" size="sm" className="h-7 text-xs" onClick={() => addToBlacklist(row.ip)}>
                          加入黑名单
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        <Card className="border-border/60 bg-card/80 shadow-sm">
          <CardHeader className="pb-2">
            <CardTitle className="flex items-center gap-2 text-sm font-medium">
              <ShieldX className="h-4 w-4 text-orange-500" />
              最近被拦截请求
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0">
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
                {blockedRequests.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={5} className="py-8 text-center text-xs text-muted-foreground">
                      范围内没有拦截记录
                    </TableCell>
                  </TableRow>
                ) : (
                  blockedRequests.map((row) => (
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
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
