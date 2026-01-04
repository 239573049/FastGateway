import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useNavigate } from "react-router-dom";
import {
  Activity,
  AlertCircle,
  CheckCircle,
  Clock,
  Cpu,
  HardDrive,
  RefreshCw,
  Server as ServerIcon,
  Timer,
  TrendingDown,
  TrendingUp,
  Wifi,
  Zap,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { ProgressBar } from "@/components/ui/progress-bar";
import { BarChart } from "@/components/ui/bar-chart";
import { TooltipProvider } from "@/components/ui/tooltip";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";

import type { Server } from "@/types";
import { bytesToSize } from "@/utils/byte";
import { getQpsData } from "@/services/QpsService";
import { getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { getDomains } from "@/services/DomainNameService";

type HealthLevel = "good" | "warn" | "critical" | "info";

type UptimeInfo = {
  days: number;
  hours: number;
  minutes: number;
  seconds: number;
  totalSeconds: number;
};

type RouteStats = { total: number; enabled: number };

function clampPercent(value: number) {
  if (!Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(100, value));
}

function formatUptime(uptime?: UptimeInfo) {
  if (!uptime) return "-";
  const parts: string[] = [];
  if (uptime.days) parts.push(`${uptime.days} 天`);
  if (uptime.hours) parts.push(`${uptime.hours} 小时`);
  if (uptime.minutes || parts.length === 0) parts.push(`${uptime.minutes} 分钟`);
  return parts.join(" ");
}

function levelMeta(level: HealthLevel) {
  switch (level) {
    case "good":
      return { label: "健康", className: "bg-emerald-500 text-emerald-50 hover:bg-emerald-500/90" };
    case "warn":
      return { label: "关注", className: "bg-amber-500 text-amber-50 hover:bg-amber-500/90" };
    case "critical":
      return { label: "告警", className: "bg-red-500 text-red-50 hover:bg-red-500/90" };
    default:
      return { label: "信息", className: "bg-muted text-muted-foreground hover:bg-muted" };
  }
}

function calcSuccessLevel(successRate: number) {
  if (!Number.isFinite(successRate)) return "info" as const;
  if (successRate >= 99.9) return "good" as const;
  if (successRate >= 95.0) return "warn" as const;
  return "critical" as const;
}

function calcLatencyLevel(p95Ms: number) {
  if (!Number.isFinite(p95Ms)) return "info" as const;
  if (p95Ms <= 120) return "good" as const;
  if (p95Ms <= 300) return "warn" as const;
  return "critical" as const;
}

function calcUsageLevel(usagePercent: number) {
  if (!Number.isFinite(usagePercent)) return "info" as const;
  if (usagePercent <= 60) return "good" as const;
  if (usagePercent <= 80) return "warn" as const;
  return "critical" as const;
}

function StatSkeleton() {
  return (
    <div className="space-y-2">
      <Skeleton className="h-4 w-32" />
      <Skeleton className="h-9 w-24" />
      <Skeleton className="h-4 w-40" />
    </div>
  );
}

function MiniStat({ label, value }: { label: string; value: string }) {
  return (
    <div className="rounded-md border border-border/60 bg-muted/20 p-2">
      <div className="text-[10px] text-muted-foreground">{label}</div>
      <div className="mt-1 font-mono text-xs font-medium text-foreground">{value}</div>
    </div>
  );
}

function CircularGauge({
  label,
  value,
  center,
  title,
}: {
  label: string;
  value: number;
  center?: ReactNode;
  title?: string;
}) {
  const clamped = clampPercent(value);
  const level = calcUsageLevel(clamped);

  const strokeWidth = 10;
  const radius = 40;
  const circumference = 2 * Math.PI * radius;
  const dashOffset = circumference - (clamped / 100) * circumference;

  const colorClassName =
    level === "critical" ? "text-red-500" : level === "warn" ? "text-amber-500" : "text-emerald-500";

  return (
    <div title={title} className="flex flex-col items-center gap-1 rounded-lg border border-border/60 bg-muted/10 p-3">
      <div className={`relative h-16 w-16 ${colorClassName}`}>
        <svg viewBox="0 0 100 100" className="h-16 w-16">
          <circle cx="50" cy="50" r={radius} fill="transparent" strokeWidth={strokeWidth} stroke="hsl(var(--border))" />
          <circle
            cx="50"
            cy="50"
            r={radius}
            fill="transparent"
            strokeWidth={strokeWidth}
            stroke="currentColor"
            strokeLinecap="round"
            strokeDasharray={`${circumference} ${circumference}`}
            strokeDashoffset={dashOffset}
            transform="rotate(-90 50 50)"
            style={{ transition: "stroke-dashoffset 300ms ease" }}
          />
        </svg>
        <div className="absolute inset-0 flex items-center justify-center">
          {center ?? <span className="text-sm font-semibold text-foreground">{clamped.toFixed(0)}%</span>}
        </div>
      </div>
      <div className="text-xs font-medium text-muted-foreground">{label}</div>
    </div>
  );
}

export default function DashboardPage() {
  const navigate = useNavigate();

  const [isLoading, setIsLoading] = useState(true);
  const [isOnline, setIsOnline] = useState(true);
  const [lastUpdate, setLastUpdate] = useState<Date | null>(null);

  const [qps, setQps] = useState(0);
  const [qpsChart, setQpsChart] = useState<Array<{ time: string; QPS: number }>>([]);

  const [network, setNetwork] = useState({ upload: 0, download: 0 });
  const [networkHistory, setNetworkHistory] = useState<
    Array<{ time: string; upload: number; download: number; total: number }>
  >([]);

  const [requests, setRequests] = useState({
    total: 0,
    success: 0,
    failed: 0,
    successRate: 0,
  });

  const [responseTime, setResponseTime] = useState({
    avg: 0,
    p95: 0,
    p99: 0,
    min: 0,
    max: 0,
  });

  const [cpu, setCpu] = useState({ usage: 0, cores: 0 });
  const [memory, setMemory] = useState({
    usage: 0,
    total: 0,
    used: 0,
    available: 0,
    workingSet: 0,
    gc: 0,
  });

  const [diskHistory, setDiskHistory] = useState<Array<{ time: string; read: number; write: number }>>([]);
  const [workingSetHistory, setWorkingSetHistory] = useState<
    Array<{
      time: string;
      workingSet: number;
      gatewayGc: number;
      gatewayNonManaged: number;
      otherUsed: number;
      available: number;
    }>
  >([]);
  const [uptime, setUptime] = useState<UptimeInfo | undefined>(undefined);

  const [windowWidth, setWindowWidth] = useState(typeof window !== "undefined" ? window.innerWidth : 1200);

  const [serversLoading, setServersLoading] = useState(true);
  const [servers, setServers] = useState<Server[]>([]);
  const [routeStats, setRouteStats] = useState<Record<string, RouteStats>>({});
  const serversRef = useRef<Server[]>([]);

  useEffect(() => {
    serversRef.current = servers;
  }, [servers]);

  useEffect(() => {
    const now = new Date();

    setQpsChart(
      Array.from({ length: 50 }, (_, i) => {
        const time = new Date(now.getTime() - (49 - i) * 3000);
        return {
          time: time.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
          QPS: 0,
        };
      })
    );

    setNetworkHistory(
      Array.from({ length: 40 }, (_, i) => {
        const time = new Date(now.getTime() - (39 - i) * 3000);
        return {
          time: time.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
          upload: 0,
          download: 0,
          total: 0,
        };
      })
    );

    setDiskHistory(
      Array.from({ length: 25 }, (_, i) => {
        const time = new Date(now.getTime() - (24 - i) * 3000);
        return {
          time: time.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
          read: 0,
          write: 0,
        };
      })
    );

    setWorkingSetHistory(
      Array.from({ length: 25 }, (_, i) => {
        const time = new Date(now.getTime() - (24 - i) * 3000);
        return {
          time: time.toLocaleTimeString("zh-CN", { hour: "2-digit", minute: "2-digit", second: "2-digit" }),
          workingSet: 0,
          gatewayGc: 0,
          gatewayNonManaged: 0,
          otherUsed: 0,
          available: 0,
        };
      })
    );
  }, []);

  const loadRealtime = useCallback(async () => {
    try {
      const fetchedData: any = await getQpsData();
      if (!fetchedData) return;

      const currentTime = fetchedData.now;
      const currentQps = Number(fetchedData.qps) || 0;
      setQps(currentQps);

      const history = fetchedData?.qpsHistory;
      if (Array.isArray(history) && history.length > 0) {
        setQpsChart(history.map((item: any) => ({ time: item.time, QPS: item.qps })));
      } else {
        setQpsChart((prev) => [...prev, { time: currentTime, QPS: currentQps }].slice(-50));
      }

      const upload = Number(fetchedData.upload) || 0;
      const download = Number(fetchedData.download) || 0;
      setNetwork({ upload, download });

      setNetworkHistory((prev) => [...prev, { time: currentTime, upload, download, total: upload + download }].slice(-40));

      if (fetchedData.requests) {
        const total = Number(fetchedData.requests.total) || 0;
        const success = Number(fetchedData.requests.success) || 0;
        const failed = Number(fetchedData.requests.failed) || 0;
        const successRate =
          Number(fetchedData.requests.successRate) ||
          (total > 0 ? Math.round((success / total) * 10000) / 100 : 0);
        setRequests({ total, success, failed, successRate });
      }

      if (fetchedData.responseTime) {
        setResponseTime({
          avg: Number(fetchedData.responseTime.avg) || 0,
          p95: Number(fetchedData.responseTime.p95) || 0,
          p99: Number(fetchedData.responseTime.p99) || 0,
          min: Number(fetchedData.responseTime.min) || 0,
          max: Number(fetchedData.responseTime.max) || 0,
        });
      }

      if (fetchedData.system) {
        const workingSet = Number(fetchedData.system.memory?.workingSet) || 0;
        const gc = Number(fetchedData.system.memory?.gc) || 0;
        const total = Number(fetchedData.system.memory?.total) || 0;
        const availableRaw = Number(fetchedData.system.memory?.available) || 0;
        const usedRaw = Number(fetchedData.system.memory?.used) || 0;
        const used = total > 0 ? Math.max(0, total - availableRaw) : usedRaw;
        const available = total > 0 ? Math.max(0, total - used) : availableRaw;
        const memoryUsagePercent = Number(fetchedData.system.memory?.usage) || 0;

        const gatewayGc = Math.min(gc, workingSet);
        const gatewayNonManaged = Math.max(0, workingSet - gatewayGc);
        const otherUsed = Math.max(0, used - workingSet);
        const availableAdjusted = total > 0 ? Math.max(0, total - (gatewayGc + gatewayNonManaged + otherUsed)) : available;

        setCpu({
          usage: Number(fetchedData.system.cpu?.usage) || 0,
          cores: Number(fetchedData.system.cpu?.cores) || 0,
        });

        setMemory({
          usage: memoryUsagePercent,
          total,
          used,
          available,
          workingSet,
          gc,
        });

        setWorkingSetHistory((prev) =>
          [
            ...prev,
            { time: currentTime, workingSet, gatewayGc, gatewayNonManaged, otherUsed, available: availableAdjusted },
          ].slice(-25)
        );

        if (fetchedData.system.disk) {
          const read = Number(fetchedData.system.disk.readBytesPerSec) || 0;
          const write = Number(fetchedData.system.disk.writeBytesPerSec) || 0;
          setDiskHistory((prev) => [...prev, { time: currentTime, read, write }].slice(-25));
        }
      }

      if (fetchedData.service) {
        setIsOnline(Boolean(fetchedData.service.isOnline));
        setUptime(fetchedData.service.uptime);
      }

      setLastUpdate(new Date());
      setIsLoading(false);
    } catch (error) {
      console.error("Failed to load dashboard data:", error);
      setIsOnline(false);
      setIsLoading(false);
    }
  }, []);

  const loadServers = useCallback(async () => {
    setServersLoading(true);
    try {
      const res: any = await getServers();
      const list = Array.isArray(res?.data) ? (res.data as Server[]) : [];
      setServers(list);
      return list;
    } catch (error) {
      console.error("Failed to load servers:", error);
      setServers([]);
      return [];
    } finally {
      setServersLoading(false);
    }
  }, []);

  const loadRouteStats = useCallback(async (serverList: Server[]) => {
    const ids = serverList
      .map((s) => s.id)
      .filter((id): id is string => typeof id === "string" && id.length > 0);

    if (ids.length === 0) {
      setRouteStats({});
      return;
    }

    try {
      const pairs = await Promise.all(
        ids.map(async (id) => {
          const res: any = await getDomains(id);
          const items = Array.isArray(res?.data) ? res.data : [];
          const enabled = items.filter((x: any) => Boolean(x.enable)).length;
          return [id, { total: items.length, enabled }] as const;
        })
      );

      setRouteStats(Object.fromEntries(pairs));
    } catch (error) {
      console.error("Failed to load route stats:", error);
    }
  }, []);

  const refreshAll = useCallback(async () => {
    await loadRealtime();
    const list = await loadServers();
    await loadRouteStats(list);
  }, [loadRealtime, loadServers, loadRouteStats]);

  useEffect(() => {
    refreshAll();

    const realtimeTimer = setInterval(loadRealtime, 3000);
    const serversTimer = setInterval(loadServers, 15000);
    const routesTimer = setInterval(() => loadRouteStats(serversRef.current), 60000);

    return () => {
      clearInterval(realtimeTimer);
      clearInterval(serversTimer);
      clearInterval(routesTimer);
    };
  }, [loadRealtime, loadRouteStats, loadServers, refreshAll]);

  useEffect(() => {
    const handleResize = () => setWindowWidth(window.innerWidth);
    window.addEventListener("resize", handleResize);
    return () => window.removeEventListener("resize", handleResize);
  }, []);

  const qpsMetrics = useMemo(() => {
    const points = qpsChart.map((x) => x.QPS).filter((n) => Number.isFinite(n));
    const last = points[points.length - 1] ?? qps ?? 0;
    const prev = points[points.length - 2] ?? last;
    const avg = points.length ? Math.round(points.reduce((acc, v) => acc + v, 0) / points.length) : 0;
    const peak = points.length ? Math.max(...points) : 0;
    const delta = last - prev;
    return { last, avg, peak, delta };
  }, [qpsChart, qps]);

  const qpsBadge = useMemo(() => {
    if (qpsMetrics.last > 100) return { variant: "destructive" as const, label: "高峰" };
    if (qpsMetrics.last > 50) return { variant: "default" as const, label: "活跃" };
    return { variant: "secondary" as const, label: "平稳" };
  }, [qpsMetrics.last]);

  const serverSummary = useMemo(() => {
    const total = servers.length;
    const online = servers.filter((s) => s.onLine).length;
    const enabled = servers.filter((s) => s.enable).length;
    const https = servers.filter((s) => s.isHttps).length;
    const tunnels = servers.filter((s) => s.enableTunnel).length;
    const routeTotal = Object.values(routeStats).reduce((acc, v) => acc + v.total, 0);
    const routeEnabled = Object.values(routeStats).reduce((acc, v) => acc + v.enabled, 0);
    return { total, online, enabled, https, tunnels, routeTotal, routeEnabled };
  }, [routeStats, servers]);

  const insights = useMemo(() => {
    const rows: Array<{
      key: string;
      metric: string;
      current: string;
      baseline: string;
      level: HealthLevel;
      suggestion: string;
    }> = [];

    const successLevel = calcSuccessLevel(requests.successRate);
    rows.push({
      key: "success-rate",
      metric: "请求成功率",
      current: `${requests.successRate.toFixed(2)}%`,
      baseline: `成功 ${requests.success.toLocaleString()} / 失败 ${requests.failed.toLocaleString()}`,
      level: successLevel,
      suggestion:
        successLevel === "critical"
          ? "失败率偏高：优先检查上游健康、限流/黑白名单策略与路由命中情况"
          : successLevel === "warn"
            ? "建议观察失败率波动，并关注近期发布/配置变更"
            : "保持当前策略，必要时设置告警阈值",
    });

    const latencyLevel = calcLatencyLevel(responseTime.p95);
    rows.push({
      key: "latency",
      metric: "P95 响应延迟",
      current: `${responseTime.p95.toFixed(1)} ms`,
      baseline: `P99 ${responseTime.p99.toFixed(1)} ms · 平均 ${responseTime.avg.toFixed(1)} ms`,
      level: latencyLevel,
      suggestion:
        latencyLevel === "critical"
          ? "延迟偏高：检查慢路由、上游连接池、DNS/网络抖动与资源是否饱和"
          : latencyLevel === "warn"
            ? "建议定位长尾请求（P99）对应的路由与上游"
            : "延迟表现稳定，继续观察长尾",
    });

    const cpuLevel = calcUsageLevel(cpu.usage);
    rows.push({
      key: "cpu",
      metric: "CPU 使用率",
      current: `${cpu.usage.toFixed(1)}%`,
      baseline: `${cpu.cores} 核`,
      level: cpuLevel,
      suggestion:
        cpuLevel === "critical"
          ? "CPU 接近饱和：考虑扩容实例或降低昂贵规则（鉴权/日志/重写）开销"
          : cpuLevel === "warn"
            ? "CPU 较高：建议关注峰值时段与并发增长趋势"
            : "CPU 使用正常",
    });

    const memLevel = calcUsageLevel(memory.usage);
    rows.push({
      key: "memory",
      metric: "内存使用率",
      current: `${memory.usage.toFixed(1)}%`,
      baseline: `已用 ${bytesToSize(memory.used)} / 总计 ${bytesToSize(memory.total)}`,
      level: memLevel,
      suggestion:
        memLevel === "critical"
          ? "内存偏高：检查缓存策略、连接数、请求体大小上限与潜在泄漏"
          : memLevel === "warn"
            ? "建议观察 Working Set 与 GC 内存是否持续上涨"
            : "内存使用正常",
    });

    const offline = Math.max(0, serverSummary.total - serverSummary.online);
    const nodeLevel: HealthLevel = offline > 0 ? "warn" : "good";
    rows.push({
      key: "nodes",
      metric: "在线服务节点",
      current: `${serverSummary.online}/${serverSummary.total}`,
      baseline: `${serverSummary.enabled} 个已启用 · HTTPS ${serverSummary.https} · 隧道 ${serverSummary.tunnels}`,
      level: serverSummary.total === 0 ? "info" : nodeLevel,
      suggestion:
        serverSummary.total === 0
          ? "尚未创建服务：先去「服务管理」新增一个监听端口"
          : offline > 0
            ? "存在离线节点：检查端口占用、证书/配置加载与网关进程状态"
            : "节点健康，建议保持定期巡检",
    });

    if (serverSummary.total > 0) {
      const routesLevel: HealthLevel =
        serverSummary.routeTotal === 0 ? "warn" : serverSummary.routeEnabled === 0 ? "warn" : "good";

      rows.push({
        key: "routes",
        metric: "启用路由",
        current: `${serverSummary.routeEnabled}/${serverSummary.routeTotal}`,
        baseline: "按服务统计（每 60s 刷新一次）",
        level: routesLevel,
        suggestion:
          serverSummary.routeTotal === 0
            ? "暂无路由：去「服务管理」选择服务并创建路由"
            : serverSummary.routeEnabled === 0
              ? "路由均未启用：检查路由开关与匹配域名配置"
              : "路由配置正常，建议定期回顾无效/重复域名",
      });
    }

    return rows;
  }, [cpu.cores, cpu.usage, memory.total, memory.used, memory.usage, requests, responseTime, serverSummary]);

  const statusIcon = useMemo(() => {
    if (isLoading) return <Activity className="h-4 w-4 animate-spin text-muted-foreground" />;
    if (isOnline) return <CheckCircle className="h-4 w-4 text-emerald-500" />;
    return <AlertCircle className="h-4 w-4 text-red-500" />;
  }, [isLoading, isOnline]);

  const qpsDeltaLabel = useMemo(() => {
    const delta = qpsMetrics.delta;
    if (delta === 0) return "持平";
    if (delta > 0) return `上升 ${delta.toLocaleString()}`;
    return `下降 ${Math.abs(delta).toLocaleString()}`;
  }, [qpsMetrics.delta]);

  const bpsTotal = network.upload + network.download;
  const memoryUsagePercent = memory.total > 0 ? (memory.used / memory.total) * 100 : memory.usage;
  const workingSetNonManaged = Math.max(0, memory.workingSet - memory.gc);
  const workingSetShareOfTotal = memory.total > 0 ? (memory.workingSet / memory.total) * 100 : 0;
  const workingSetGcShare = memory.workingSet > 0 ? (memory.gc / memory.workingSet) * 100 : 0;

  const workingSetStats = useMemo(() => {
    const points = workingSetHistory.map((x) => x.workingSet).filter((n) => Number.isFinite(n));
    const last = points[points.length - 1] ?? memory.workingSet ?? 0;
    const prev = points[points.length - 2] ?? last;
    const peak = points.length ? Math.max(...points) : 0;
    return { last, prev, peak, delta: last - prev };
  }, [memory.workingSet, workingSetHistory]);

  const workingSetDeltaLabel = useMemo(() => {
    if (!Number.isFinite(workingSetStats.delta) || workingSetStats.delta === 0) return "0";
    const sign = workingSetStats.delta > 0 ? "+" : "-";
    return `${sign}${bytesToSize(Math.abs(workingSetStats.delta), 1)}`;
  }, [workingSetStats.delta]);

  return (
    <TooltipProvider>
      <div className="mx-auto max-w-7xl space-y-6 p-6">
        <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
          <div className="space-y-2">
            <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">FastGateway 控制台</p>
            <div>
              <h1 className="text-3xl font-semibold tracking-tight">运行概览</h1>
              <p className="text-sm text-muted-foreground">面向网关业务的实时健康与配置洞察</p>
            </div>
          </div>

          <div className="flex flex-wrap items-center gap-2">
            <div className="flex items-center gap-2 rounded-full border border-border bg-background/60 px-4 py-2 text-sm text-muted-foreground">
              {statusIcon}
              <span>{isLoading ? "同步中" : isOnline ? "服务在线" : "服务不可达"}</span>
              <span className="text-muted-foreground/70">·</span>
              <Clock className="h-4 w-4" />
              <span>{lastUpdate ? lastUpdate.toLocaleTimeString() : "-"}</span>
            </div>
            <Badge variant="outline" className="rounded-full">
              运行 {formatUptime(uptime)}
            </Badge>
            <Button variant="outline" size="sm" onClick={refreshAll} className="gap-2">
              <RefreshCw className="h-4 w-4" />
              刷新
            </Button>
          </div>
        </header>

        <section className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center justify-between text-sm font-medium">
                <span className="flex items-center gap-2">
                  <Zap className="h-4 w-4 text-yellow-500" />
                  实时 QPS
                </span>
                <Badge variant={qpsBadge.variant}>{qpsBadge.label}</Badge>
              </CardTitle>
              <CardDescription>3 秒窗口 · {qpsDeltaLabel}</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {isLoading ? (
                <StatSkeleton />
              ) : (
                <>
                  <div className="flex items-baseline gap-2">
                    <p className="text-3xl font-semibold text-yellow-600">{qpsMetrics.last.toLocaleString()}</p>
                    <p className="text-xs text-muted-foreground">req/s</p>
                  </div>
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>平均 {qpsMetrics.avg.toLocaleString()}</span>
                    <span>峰值 {qpsMetrics.peak.toLocaleString()}</span>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <CheckCircle className="h-4 w-4 text-emerald-500" />
                成功率与错误
              </CardTitle>
              <CardDescription>基于累计请求实时统计</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {isLoading ? (
                <StatSkeleton />
              ) : (
                <>
                  <div className="flex items-baseline justify-between">
                    <p className="text-3xl font-semibold text-emerald-600">{requests.successRate.toFixed(2)}%</p>
                    <Badge className={levelMeta(calcSuccessLevel(requests.successRate)).className}>
                      {levelMeta(calcSuccessLevel(requests.successRate)).label}
                    </Badge>
                  </div>
                  <ProgressBar
                    value={clampPercent(requests.successRate)}
                    size="sm"
                    color={requests.successRate >= 99 ? "green" : requests.successRate >= 95 ? "yellow" : "red"}
                  />
                  <div className="flex items-center justify-between text-xs text-muted-foreground">
                    <span>成功 {requests.success.toLocaleString()}</span>
                    <span>失败 {requests.failed.toLocaleString()}</span>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Timer className="h-4 w-4 text-purple-500" />
                延迟（长尾优先）
              </CardTitle>
              <CardDescription>P95 / P99 与平均值</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              {isLoading ? (
                <StatSkeleton />
              ) : (
                <>
                  <div className="flex items-baseline justify-between">
                    <p className="text-3xl font-semibold text-purple-600">{responseTime.p95.toFixed(1)} ms</p>
                    <Badge className={levelMeta(calcLatencyLevel(responseTime.p95)).className}>
                      {levelMeta(calcLatencyLevel(responseTime.p95)).label}
                    </Badge>
                  </div>
                  <div className="grid grid-cols-2 gap-3 text-xs text-muted-foreground">
                    <div className="rounded-lg border border-border/60 bg-muted/20 p-3">
                      <div className="flex items-center justify-between">
                        <span>P99</span>
                        <span className="font-medium text-foreground">{responseTime.p99.toFixed(1)} ms</span>
                      </div>
                    </div>
                    <div className="rounded-lg border border-border/60 bg-muted/20 p-3">
                      <div className="flex items-center justify-between">
                        <span>平均</span>
                        <span className="font-medium text-foreground">{responseTime.avg.toFixed(1)} ms</span>
                      </div>
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm lg:col-span-3">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Cpu className="h-4 w-4 text-orange-500" />
                资源饱和度
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {isLoading ? (
                <StatSkeleton />
              ) : (
                <>
                  <div className="grid gap-3 sm:grid-cols-2">
                    <CircularGauge
                      label="CPU"
                      value={cpu.usage}
                      title={`${cpu.usage.toFixed(1)}%${cpu.cores ? ` · ${cpu.cores} 核` : ""}`}
                    />
                    <CircularGauge
                      label="内存"
                      value={memoryUsagePercent}
                      title={`已用 ${bytesToSize(memory.used, 1)} / 总计 ${bytesToSize(memory.total, 1)} · 可用 ${bytesToSize(memory.available, 1)}`}
                      center={
                        <div className="text-center leading-none">
                          <div className="text-[11px] font-semibold text-foreground">{bytesToSize(memory.used, 1)}</div>
                          <div className="mt-0.5 text-[10px] text-muted-foreground">/ {bytesToSize(memory.total, 1)}</div>
                        </div>
                      }
                    />
                  </div>

                  <div className="rounded-lg border border-border/60 bg-muted/10 p-3">
                    <div className="flex items-center justify-between text-xs text-muted-foreground">
                      <span>工作集</span>
                      <span className="font-medium text-foreground">{bytesToSize(memory.workingSet, 1)}</span>
                    </div>
                    <div className="mt-2 grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
                      <MiniStat label="3s 变化" value={workingSetDeltaLabel} />
                      <MiniStat label="峰值(25点)" value={bytesToSize(workingSetStats.peak, 1)} />
                      <MiniStat label="GC" value={bytesToSize(memory.gc, 1)} />
                      <MiniStat label="非托管(估)" value={bytesToSize(workingSetNonManaged, 1)} />
                      <MiniStat label="GC 占比" value={`${clampPercent(workingSetGcShare).toFixed(0)}%`} />
                      <MiniStat label="占总内存" value={`${clampPercent(workingSetShareOfTotal).toFixed(1)}%`} />
                    </div>
                    <div className="mt-2 flex flex-wrap gap-3 text-[10px] text-muted-foreground">
                      <span className="inline-flex items-center gap-1">
                        <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: "#7c3aed" }} />
                        网关 GC
                      </span>
                      <span className="inline-flex items-center gap-1">
                        <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: "#3b82f6" }} />
                        网关 非托管
                      </span>
                      <span className="inline-flex items-center gap-1">
                        <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: "#64748b" }} />
                        其他已用
                      </span>
                      <span className="inline-flex items-center gap-1">
                        <span className="h-2 w-2 rounded-[2px]" style={{ backgroundColor: "#10b981" }} />
                        可用
                      </span>
                    </div>
                    <div className="mt-2 h-[120px]">
                      <BarChart
                        key={`working-set-chart-${windowWidth}`}
                        data={workingSetHistory}
                        categories={["gatewayGc", "gatewayNonManaged", "otherUsed", "available"]}
                        categoryLabels={{
                          gatewayGc: "网关 GC",
                          gatewayNonManaged: "网关 非托管",
                          otherUsed: "其他已用",
                          available: "可用",
                        }}
                        colors={["#7c3aed", "#3b82f6", "#64748b", "#10b981"]}
                        stackId="memory"
                        valueFormatter={(value: number) => {
                          const percent = memory.total > 0 ? (value / memory.total) * 100 : 0;
                          return `${bytesToSize(value, 1)}${memory.total > 0 ? ` (${percent.toFixed(1)}%)` : ""}`;
                        }}
                        index="time"
                        showXAxis={false}
                        showYAxis={false}
                        barRadius={0}
                        className="h-full w-full"
                      />
                    </div>
                  </div>
                </>
              )}
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base font-semibold">
                <Activity className="h-4 w-4 text-yellow-500" />
                QPS 趋势
              </CardTitle>
              <CardDescription>最近 50 个采样点（约 150 秒）</CardDescription>
            </CardHeader>
            <CardContent className="h-[260px]">
              <BarChart
                key={`qps-chart-${windowWidth}`}
                data={qpsChart}
                categories={["QPS"]}
                colors={["#f59e0b"]}
                valueFormatter={(value: number) => `${value} req/s`}
                index="time"
                showXAxis={false}
                showYAxis={false}
                className="h-full w-full"
              />
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base font-semibold">
                <Wifi className="h-4 w-4 text-purple-500" />
                网络吞吐
              </CardTitle>
              <CardDescription>
                总吞吐 {bytesToSize(bpsTotal)}/s · 上行 {bytesToSize(network.upload)}/s · 下行{" "}
                {bytesToSize(network.download)}/s
              </CardDescription>
            </CardHeader>
            <CardContent className="h-[260px]">
              <BarChart
                key={`network-chart-${windowWidth}`}
                data={networkHistory}
                categories={["upload", "download"]}
                colors={["#2563eb", "#10b981"]}
                valueFormatter={(value: number) => `${bytesToSize(value)}/s`}
                index="time"
                showXAxis={false}
                showYAxis={false}
                className="h-full w-full"
              />
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-6 xl:grid-cols-3">
          <Card className="border-border/60 bg-card/80 shadow-sm xl:col-span-2">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base font-semibold">
                <ServerIcon className="h-4 w-4 text-blue-500" />
                运行洞察
              </CardTitle>
              <CardDescription>把“指标”变成“可执行动作”与业务视角的配置概览</CardDescription>
            </CardHeader>
            <CardContent className="p-0">
              <Tabs defaultValue="insights" className="w-full">
                <div className="flex flex-col gap-3 border-b border-border/60 px-6 py-4 md:flex-row md:items-center md:justify-between">
                  <TabsList className="w-full justify-start md:w-auto">
                    <TabsTrigger value="insights">指标与建议</TabsTrigger>
                    <TabsTrigger value="services">服务与路由</TabsTrigger>
                  </TabsList>
                  <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                    <Badge variant="outline" className="rounded-full">
                      数据每 3 秒刷新
                    </Badge>
                    <Button variant="outline" size="sm" className="h-8 gap-2" onClick={() => navigate("/server")}>
                      <ServerIcon className="h-4 w-4" />
                      进入服务管理
                    </Button>
                  </div>
                </div>

                <TabsContent value="insights" className="m-0 p-6 pt-4">
                  <div className="rounded-lg border border-border/60">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-muted/30 hover:bg-muted/30">
                          <TableHead className="w-[160px]">维度</TableHead>
                          <TableHead>当前</TableHead>
                          <TableHead>参考</TableHead>
                          <TableHead className="w-[120px]">状态</TableHead>
                          <TableHead>建议</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {insights.map((row) => {
                          const meta = levelMeta(row.level);
                          return (
                            <TableRow key={row.key} className="hover:bg-muted/30">
                              <TableCell className="font-medium">{row.metric}</TableCell>
                              <TableCell className="font-mono text-sm">{row.current}</TableCell>
                              <TableCell className="text-muted-foreground">{row.baseline}</TableCell>
                              <TableCell>
                                <Badge className={meta.className}>{meta.label}</Badge>
                              </TableCell>
                              <TableCell className="text-muted-foreground">{row.suggestion}</TableCell>
                            </TableRow>
                          );
                        })}
                      </TableBody>
                    </Table>
                  </div>
                </TabsContent>

                <TabsContent value="services" className="m-0 p-6 pt-4">
                  <div className="flex flex-wrap items-center gap-2 pb-4 text-sm">
                    <Badge variant="outline" className="rounded-full">
                      总节点 {serverSummary.total}
                    </Badge>
                    <Badge variant="outline" className="rounded-full">
                      在线 {serverSummary.online}
                    </Badge>
                    <Badge variant="outline" className="rounded-full">
                      已启用 {serverSummary.enabled}
                    </Badge>
                    <Badge variant="outline" className="rounded-full">
                      路由 {serverSummary.routeEnabled}/{serverSummary.routeTotal}
                    </Badge>
                  </div>

                  <div className="rounded-lg border border-border/60">
                    <Table>
                      <TableHeader>
                        <TableRow className="bg-muted/30 hover:bg-muted/30">
                          <TableHead>服务</TableHead>
                          <TableHead className="w-[90px]">端口</TableHead>
                          <TableHead className="w-[110px]">在线</TableHead>
                          <TableHead className="w-[110px]">启用</TableHead>
                          <TableHead className="w-[120px]">路由</TableHead>
                          <TableHead>能力</TableHead>
                          <TableHead className="w-[210px] text-right">操作</TableHead>
                        </TableRow>
                      </TableHeader>
                      <TableBody>
                        {serversLoading ? (
                          Array.from({ length: 4 }).map((_, i) => (
                            <TableRow key={`server-skeleton-${i}`}>
                              <TableCell colSpan={7}>
                                <div className="flex items-center gap-3 py-2">
                                  <Skeleton className="h-8 w-8 rounded-md" />
                                  <div className="flex-1 space-y-2">
                                    <Skeleton className="h-4 w-64" />
                                    <Skeleton className="h-3 w-80" />
                                  </div>
                                </div>
                              </TableCell>
                            </TableRow>
                          ))
                        ) : servers.length === 0 ? (
                          <TableRow>
                            <TableCell colSpan={7} className="py-10 text-center text-muted-foreground">
                              暂无服务配置，先去「服务管理」创建一个监听端口。
                            </TableCell>
                          </TableRow>
                        ) : (
                          servers.map((server) => {
                            const id = typeof server.id === "string" ? server.id : "";
                            const routes = id && routeStats[id] ? routeStats[id] : undefined;

                            const capabilityTags = [
                              server.isHttps ? "HTTPS" : null,
                              server.enableTunnel ? "隧道" : null,
                              server.enableBlacklist ? "黑名单" : null,
                              server.enableWhitelist ? "白名单" : null,
                              server.redirectHttps ? "HTTPS重定向" : null,
                              server.staticCompress ? "静态压缩" : null,
                            ].filter(Boolean) as string[];

                            const shown = capabilityTags.slice(0, 3);
                            const extra = capabilityTags.length - shown.length;

                            return (
                              <TableRow key={id || server.name} className="hover:bg-muted/30">
                                <TableCell>
                                  <div className="flex items-start gap-3 py-1">
                                    <div
                                      className={`mt-0.5 flex h-8 w-8 items-center justify-center rounded-md ${
                                        server.onLine
                                          ? "bg-emerald-500/10 text-emerald-600"
                                          : "bg-muted text-muted-foreground"
                                      }`}
                                    >
                                      <ServerIcon className="h-4 w-4" />
                                    </div>
                                    <div className="min-w-0">
                                      <div className="flex items-center gap-2">
                                        <span className="truncate font-medium">{server.name}</span>
                                        {server.onLine ? (
                                          <Badge className="bg-emerald-500 text-emerald-50 hover:bg-emerald-500/90">
                                            在线
                                          </Badge>
                                        ) : (
                                          <Badge variant="secondary">离线</Badge>
                                        )}
                                      </div>
                                      <div className="truncate text-xs text-muted-foreground">
                                        {server.description || "暂无描述"}
                                      </div>
                                    </div>
                                  </div>
                                </TableCell>
                                <TableCell className="font-mono text-sm">:{server.listen}</TableCell>
                                <TableCell>
                                  {server.onLine ? (
                                    <span className="inline-flex items-center gap-1 text-emerald-600">
                                      <TrendingUp className="h-3 w-3" />
                                      在线
                                    </span>
                                  ) : (
                                    <span className="inline-flex items-center gap-1 text-muted-foreground">
                                      <TrendingDown className="h-3 w-3" />
                                      离线
                                    </span>
                                  )}
                                </TableCell>
                                <TableCell>
                                  {server.enable ? (
                                    <Badge className="bg-blue-600 text-blue-50 hover:bg-blue-600/90">已启用</Badge>
                                  ) : (
                                    <Badge variant="secondary">已禁用</Badge>
                                  )}
                                </TableCell>
                                <TableCell className="font-mono text-sm">
                                  {routes ? `${routes.enabled}/${routes.total}` : "-"}
                                </TableCell>
                                <TableCell>
                                  {shown.length ? (
                                    <div className="flex flex-wrap gap-1">
                                      {shown.map((t) => (
                                        <Badge key={t} variant="outline">
                                          {t}
                                        </Badge>
                                      ))}
                                      {extra > 0 ? <Badge variant="secondary">+{extra}</Badge> : null}
                                    </div>
                                  ) : (
                                    <span className="text-xs text-muted-foreground">无</span>
                                  )}
                                </TableCell>
                                <TableCell className="text-right">
                                  <div className="inline-flex flex-wrap justify-end gap-2">
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      className="h-8"
                                      onClick={() => navigate(`/server/${id}`)}
                                      disabled={!id}
                                    >
                                      管理
                                    </Button>
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      className="h-8"
                                      onClick={async () => {
                                        if (!id) return;
                                        await onlineServer(id);
                                        await loadServers();
                                      }}
                                      disabled={!id}
                                    >
                                      {server.onLine ? "停止" : "启动"}
                                    </Button>
                                    <Button
                                      variant="outline"
                                      size="sm"
                                      className="h-8"
                                      onClick={async () => {
                                        if (!id) return;
                                        await reloadServer(id);
                                      }}
                                      disabled={!id}
                                    >
                                      重载
                                    </Button>
                                  </div>
                                </TableCell>
                              </TableRow>
                            );
                          })
                        )}
                      </TableBody>
                    </Table>
                  </div>
                </TabsContent>
              </Tabs>
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base font-semibold">
                <HardDrive className="h-4 w-4 text-muted-foreground" />
                磁盘 I/O
              </CardTitle>
              <CardDescription>读取 / 写入（Bytes/s）</CardDescription>
            </CardHeader>
            <CardContent className="h-[340px]">
              <BarChart
                key={`disk-chart-${windowWidth}`}
                data={diskHistory}
                categories={["read", "write"]}
                colors={["#10b981", "#f59e0b"]}
                index="time"
                valueFormatter={(value: number) => bytesToSize(value)}
                showXAxis={false}
                showYAxis={false}
                barRadius={4}
                className="h-full"
              />
            </CardContent>
          </Card>
        </section>
      </div>
    </TooltipProvider>
  );
}
