import { Fragment, memo, useEffect, useState } from "react";
import type { ColumnDef } from "@tanstack/react-table";
import {
  Ban,
  BrainCircuit,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  Eye,
  RefreshCw,
  Undo2,
  Users,
} from "lucide-react";
import { Reveal } from "@/components/motion";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { DataTable } from "@/components/ui/data-table";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { message } from "@/utils/toast";
import { AddAbnormalIpToBlacklist, RemoveAbnormalIpFromBlacklist } from "@/services/AbnormalIpService";
import { getJevThreatStatus, startJevThreatScan } from "@/services/RiskService";
import type { JevThreatRunStatus } from "@/types";
import {
  getStatisticsRequests,
  getStatisticsVisitors,
  type JevBanInfo,
  type VisitorSummary,
} from "@/services/StatisticsService";
import type { RequestLogItem, StatRange } from "@/pages/dashboard/types";
import { useDashboardStore } from "@/pages/dashboard/store";
import { SecurityFilterBar } from "../shared/FilterBar";

const DEFAULT_PAGE_SIZE = 20;
const HISTORY_PAGE_SIZE = 20;
const PAGE_SIZE_STORAGE_KEY = "fastgateway.security.visitors.pageSize";
const PAGE_SIZE_OPTIONS = [10, 20, 50, 100];
const ACTION_CELL_CLASS = "sticky right-0 z-20 w-[180px] min-w-[180px] whitespace-nowrap border-l bg-background shadow-[-10px_0_14px_-14px_rgba(0,0,0,0.6)]";

const BLOCK_LABELS: Record<number, string> = {
  1: "黑名单",
  2: "限流",
  3: "白名单拒绝",
  4: "地区封禁",
  5: "机器人挑战",
  6: "风控验证",
  7: "风控封禁",
  8: "自适应拦截",
};

function blockReasonLabel(value: number) {
  return BLOCK_LABELS[value] ?? (value ? `拦截 ${value}` : "");
}

function formatTs(ts: number) {
  if (!ts) return "-";
  return new Date(ts * 1000).toLocaleString("zh-CN", { hour12: false });
}

// 解封接口只处理系统维护的分组（手动封禁的「自动黑名单」与 Jev 分组），用户自建分组需到黑白名单页面管理
const UNBANNABLE_GROUPS = ["自动黑名单", "Jev 自动封禁"];

function canUnban(item: VisitorSummary) {
  return Boolean(item.jevBan) || item.blacklistNames.some((name) => UNBANNABLE_GROUPS.includes(name));
}

function describeJevBan(ban: JevBanInfo) {
  const risk = ban.risk == null ? "-" : `${Math.round(ban.risk * 100)}%`;
  const expiry = ban.expiresAt ? `${formatTs(ban.expiresAt)} 到期` : "永久";
  return `Jev 判定风险 ${risk}，第 ${ban.offenseCount} 次封禁，${expiry}`;
}

function readStoredPageSize() {
  if (typeof window === "undefined") return DEFAULT_PAGE_SIZE;
  const stored = Number(window.localStorage.getItem(PAGE_SIZE_STORAGE_KEY));
  return PAGE_SIZE_OPTIONS.includes(stored) ? stored : DEFAULT_PAGE_SIZE;
}

function boolBadge(value: boolean, trueText = "是", falseText = "否") {
  return (
    <Badge variant="outline" className={value ? "border-destructive/40 bg-destructive/10 text-destructive" : "text-muted-foreground"}>
      {value ? trueText : falseText}
    </Badge>
  );
}

const historyColumns: ColumnDef<RequestLogItem>[] = [
  {
    accessorKey: "ts",
    header: "时间",
    size: 190,
    minSize: 150,
    maxSize: 300,
    cell: ({ row }) => <div className="whitespace-nowrap font-mono text-xs text-muted-foreground">{formatTs(row.original.ts)}</div>,
  },
  {
    id: "request",
    header: "请求",
    size: 180,
    minSize: 140,
    maxSize: 520,
    cell: ({ row }) => (
      <div className="flex min-w-0 items-center gap-2 overflow-hidden">
        <span className="shrink-0 font-mono text-xs text-muted-foreground">{row.original.method}</span>
        <span className="min-w-0 flex-1 truncate font-mono text-xs" title={row.original.path}>{row.original.path || "/"}</span>
      </div>
    ),
  },
  {
    accessorKey: "host",
    header: "受访域名",
    size: 115,
    minSize: 95,
    maxSize: 320,
    cell: ({ row }) => <div className="max-w-[180px] truncate font-mono text-xs">{row.original.host || "-"}</div>,
  },
  {
    accessorKey: "status",
    header: "状态",
    size: 64,
    minSize: 56,
    maxSize: 120,
    cell: ({ row }) => <span className="font-mono text-xs">{row.original.status}</span>,
  },
  {
    id: "geo",
    header: "地理",
    size: 80,
    minSize: 68,
    maxSize: 220,
    cell: ({ row }) => <span className="text-xs text-muted-foreground">{[row.original.country, row.original.province].filter(Boolean).join(" · ") || "-"}</span>,
  },
  {
    accessorKey: "refererUrl",
    header: "来源页面",
    size: 135,
    minSize: 110,
    maxSize: 480,
    cell: ({ row }) => <div className="max-w-[220px] truncate text-xs text-muted-foreground" title={row.original.refererUrl}>{row.original.refererUrl || "-"}</div>,
  },
  {
    id: "client",
    header: "客户端",
    size: 105,
    minSize: 90,
    maxSize: 280,
    cell: ({ row }) => <div className="text-xs text-muted-foreground">{[row.original.os, row.original.browser].filter(Boolean).join(" · ") || "-"}</div>,
  },
  {
    accessorKey: "blocked",
    header: "拦截原因",
    size: 90,
    minSize: 76,
    maxSize: 200,
    cell: ({ row }) => {
      const reason = blockReasonLabel(row.original.blocked);
      return reason ? <Badge variant="outline" className="border-destructive/40 text-destructive">{reason}</Badge> : <span className="text-xs text-muted-foreground">-</span>;
    },
  },
];

function VisitorHistory({ ip, range, host }: { ip: string; range: StatRange; host?: string }) {
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [items, setItems] = useState<RequestLogItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    let live = true;
    setLoading(true);
    getStatisticsRequests(range, { host, ip, page, pageSize: HISTORY_PAGE_SIZE })
      .then((res: any) => {
        if (!live) return;
        setItems(Array.isArray(res?.data?.items) ? res.data.items : []);
        setTotal(Number(res?.data?.total) || 0);
      })
      .catch(() => {
        if (live) message.error("加载访客历史失败");
      })
      .finally(() => {
        if (live) setLoading(false);
      });
    return () => { live = false; };
  }, [ip, range, host, page]);

  return (
    <div className="space-y-3 px-4 py-4">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <div className="text-sm font-medium">访客历史记录</div>
          <div className="text-xs text-muted-foreground">IP <span className="font-mono">{ip}</span> · 按最近访问时间降序</div>
        </div>
        <Badge variant="secondary">共 {total} 条</Badge>
      </div>
      <DataTable
        tableContainerClassName="overflow-hidden"
        innerTableContainerClassName="max-h-[500px] overflow-auto"
        footerClassName="shrink-0 border-t bg-background/95 px-3 py-3 backdrop-blur"
        resizable
        columns={historyColumns}
        data={items}
        loading={loading}
        total={total}
        pageSize={HISTORY_PAGE_SIZE}
        current={page}
        onPaginationChange={(next) => setPage(next)}
      />
    </div>
  );
}

const VisitorDetailsPage = memo(() => {
  const { range, host, setRange } = useDashboardStore();
  const filterKey = `${range}:${host ?? ""}`;
  const [rangeReady, setRangeReady] = useState(false);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(readStoredPageSize);
  const [total, setTotal] = useState(0);
  const [items, setItems] = useState<VisitorSummary[]>([]);
  const [loading, setLoading] = useState(false);
  const [jevScanning, setJevScanning] = useState(false);
  const [jevLastRun, setJevLastRun] = useState<JevThreatRunStatus | null>(null);
  const [refreshKey, setRefreshKey] = useState(0);
  const [expandedIps, setExpandedIps] = useState<Set<string>>(() => new Set());
  const [banningIp, setBanningIp] = useState<string | null>(null);
  const [selected, setSelected] = useState<VisitorSummary | null>(null);

  useEffect(() => {
    setRange("1h");
    setRangeReady(true);
  }, [setRange]);

  useEffect(() => {
    let live = true;
    const refreshTaskState = async () => {
      try {
        const response = await getJevThreatStatus();
        const status = response?.data;
        if (!live) return;
        setJevScanning(status?.status === "queued" || status?.status === "running");
        setJevLastRun(status ?? null);
      } catch {
        // 后台任务状态不影响访客数据加载
      }
    };

    void refreshTaskState();
    const timer = window.setInterval(refreshTaskState, 5000);
    return () => {
      live = false;
      window.clearInterval(timer);
    };
  }, []);

  useEffect(() => {
    setPage(1);
  }, [filterKey]);

  useEffect(() => {
    if (!rangeReady) return;
    let live = true;
    setLoading(true);
    getStatisticsVisitors(range, { host, page, pageSize })
      .then((res: any) => {
        if (!live) return;
        setItems(Array.isArray(res?.data?.items) ? res.data.items : []);
        setTotal(Number(res?.data?.total) || 0);
      })
      .catch(() => {
        if (live) message.error("加载访客详情失败");
      })
      .finally(() => {
        if (live) setLoading(false);
      });
    return () => { live = false; };
  }, [range, host, page, pageSize, refreshKey, filterKey, rangeReady]);

  const pageCount = Math.max(1, Math.ceil(total / pageSize));

  function changePageSize(value: string) {
    const next = Number(value);
    if (!PAGE_SIZE_OPTIONS.includes(next)) return;
    setPageSize(next);
    setPage(1);
    window.localStorage.setItem(PAGE_SIZE_STORAGE_KEY, String(next));
  }

  function toggleExpanded(ip: string) {
    setExpandedIps((previous) => {
      const next = new Set(previous);
      if (next.has(ip)) next.delete(ip);
      else next.add(ip);
      return next;
    });
  }

  async function handleBan(ip: string) {
    try {
      setBanningIp(ip);
      await AddAbnormalIpToBlacklist(ip);
      message.success(`已封禁 ${ip}`);
      setRefreshKey((value) => value + 1);
    } catch {
      message.error("封禁失败");
    } finally {
      setBanningIp(null);
    }
  }

  async function handleUnban(ip: string) {
    try {
      setBanningIp(ip);
      const res = (await RemoveAbnormalIpFromBlacklist(ip)) as { success?: boolean; message?: string } | undefined;
      if (res?.success === false) throw new Error(res.message);
      message.success(res?.message || `已解封 ${ip}`);
      setRefreshKey((value) => value + 1);
    } catch (error) {
      message.error(error instanceof Error && error.message ? error.message : "解封失败");
    } finally {
      setBanningIp(null);
    }
  }

  async function handleJevScan() {
    setJevScanning(true);
    try {
      const started = await startJevThreatScan();
      if (!started?.success || !started.data) {
        setJevScanning(false);
        message.error(started?.message || "启动 Jev 判断失败");
        return;
      }

      setJevScanning(true);
      message.success("Jev 判断已转入后台运行，可安全切换或关闭页面");
    } catch (error: any) {
      setJevScanning(false);
      message.error(error?.message || "启动 Jev 判断失败");
    }
  }

  return (
    <div className="container mx-auto max-w-[1600px] px-4 py-8 sm:px-6 lg:px-8">
      <Reveal className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <Users className="h-6 w-6 text-primary" />
            <h1 className="text-3xl font-bold tracking-tight text-foreground">访客详情</h1>
          </div>
          <p className="mt-2 text-muted-foreground">按访客 IP 汇总最近访问信息，按最近访问时间降序排列。</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <SecurityFilterBar />
          <Button variant="outline" size="sm" className="h-8 gap-2" onClick={() => setRefreshKey((value) => value + 1)} disabled={loading}>
            <RefreshCw className="h-3.5 w-3.5" />
            刷新
          </Button>
          <Button variant="outline" size="sm" className="h-8 gap-2" onClick={handleJevScan} disabled={jevScanning} title="在后台运行，可安全切换或关闭页面">
            <BrainCircuit className="h-3.5 w-3.5" />
            {jevScanning ? "判断中..." : "Jev 判断"}
          </Button>
          {jevLastRun?.status === "completed" ? (
            <span className="text-xs text-muted-foreground" title={`批次 ${jevLastRun.batchId}`}>
              {jevLastRun.shadow
                ? `上次（观察模式）：评估 ${jevLastRun.evaluatedIps}，${jevLastRun.highRiskIps} 个达到阈值未封禁`
                : `上次：评估 ${jevLastRun.evaluatedIps}，封禁 ${jevLastRun.blockedIps}`}
            </span>
          ) : null}
        </div>
      </Reveal>

      <Reveal index={1}>
        <Card className="border shadow-sm">
          <CardHeader className="flex flex-row items-center justify-between gap-3 border-b bg-muted/50 space-y-0">
            <div>
              <CardTitle className="text-lg font-semibold">访客汇总</CardTitle>
              <CardDescription>共 {total} 个访客 IP，点击行首箭头查看历史请求。</CardDescription>
            </div>
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              每页
              <Select value={String(pageSize)} onValueChange={changePageSize}>
                <SelectTrigger className="h-8 w-[105px] text-xs"><SelectValue /></SelectTrigger>
                <SelectContent>
                  {PAGE_SIZE_OPTIONS.map((size) => <SelectItem key={size} value={String(size)}>{size} 条</SelectItem>)}
                </SelectContent>
              </Select>
            </div>
          </CardHeader>
          <CardContent className="p-0">
            <div className="overflow-x-auto [container-type:inline-size]">
              <Table className="min-w-[2050px]">
                <TableHeader>
                  <TableRow>
                    <TableHead className="w-10" />
                    <TableHead className="whitespace-nowrap">最近访问时间</TableHead>
                    <TableHead>访客 IP</TableHead>
                    <TableHead>IP 地理</TableHead>
                    <TableHead>Status code</TableHead>
                    <TableHead>最近受访域名</TableHead>
                    <TableHead>最近受访页面</TableHead>
                    <TableHead>最近来源域名</TableHead>
                    <TableHead>最近来源页面</TableHead>
                    <TableHead>是否拦截</TableHead>
                    <TableHead>拦截原因</TableHead>
                    <TableHead>异常 IP</TableHead>
                    <TableHead>触发风控</TableHead>
                    <TableHead>触发限流</TableHead>
                    <TableHead className={`${ACTION_CELL_CLASS} z-30`}>操作</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {loading ? (
                    <TableRow><TableCell colSpan={15} className="h-28 text-center text-sm text-muted-foreground">加载中...</TableCell></TableRow>
                  ) : items.length === 0 ? (
                    <TableRow><TableCell colSpan={15} className="h-28 text-center text-sm text-muted-foreground">当前范围暂无访客数据</TableCell></TableRow>
                  ) : items.map((item) => {
                    const expanded = expandedIps.has(item.ip);
                    const busy = banningIp === item.ip;
                    const geo = [item.country, item.province].filter(Boolean).join(" · ") || "-";
                    return (
                      <Fragment key={item.ip}>
                        <TableRow className="align-top">
                          <TableCell>
                            <Button variant="ghost" size="icon" className="h-7 w-7" onClick={() => toggleExpanded(item.ip)} aria-label={expanded ? "收起历史" : "展开历史"}>
                              {expanded ? <ChevronDown className="h-4 w-4" /> : <ChevronRight className="h-4 w-4" />}
                            </Button>
                          </TableCell>
                          <TableCell><span className="whitespace-nowrap font-mono text-xs text-muted-foreground">{formatTs(item.lastVisitAt)}</span></TableCell>
                          <TableCell>
                            <div className="flex items-center gap-1.5 whitespace-nowrap">
                              <Badge variant="outline" className="font-mono text-xs">{item.ip}</Badge>
                              {item.blacklisted ? (
                                <Badge
                                  variant="destructive"
                                  className="cursor-help text-[10px]"
                                  title={[`所在黑名单：${item.blacklistNames.join("、")}`, item.jevBan ? describeJevBan(item.jevBan) : ""].filter(Boolean).join("\n")}
                                >
                                  封禁
                                </Badge>
                              ) : null}
                            </div>
                          </TableCell>
                          <TableCell><span className="whitespace-nowrap text-xs">{geo}</span></TableCell>
                          <TableCell><span className="font-mono text-xs">{item.status || "-"}</span></TableCell>
                          <TableCell><div className="max-w-[160px] truncate font-mono text-xs" title={item.host}>{item.host || "-"}</div></TableCell>
                          <TableCell><div className="max-w-[220px] truncate font-mono text-xs" title={item.path}>{item.path || "-"}</div></TableCell>
                          <TableCell><div className="max-w-[150px] truncate font-mono text-xs" title={item.refererHost}>{item.refererHost || "-"}</div></TableCell>
                          <TableCell><div className="max-w-[210px] truncate text-xs text-muted-foreground" title={item.refererUrl}>{item.refererUrl || "-"}</div></TableCell>
                          <TableCell>{boolBadge(item.blocked, "被拦截", "正常")}</TableCell>
                          <TableCell><span className="whitespace-nowrap text-xs text-muted-foreground">{blockReasonLabel(item.blockReason) || "-"}</span></TableCell>
                          <TableCell>{boolBadge(item.abnormalIp, "异常", "正常")}</TableCell>
                          <TableCell>{boolBadge(item.riskTriggered, "已触发", "未触发")}</TableCell>
                          <TableCell>{boolBadge(item.rateLimited, "已触发", "未触发")}</TableCell>
                          <TableCell className={ACTION_CELL_CLASS}>
                            <div className="flex items-center gap-2 whitespace-nowrap">
                              {item.blacklisted && canUnban(item) ? (
                                <Button size="sm" variant="outline" className="h-8 gap-1.5" title={item.jevBan ? describeJevBan(item.jevBan) : `所在黑名单：${item.blacklistNames.join("、")}`} disabled={busy} onClick={() => handleUnban(item.ip)}>
                                  <Undo2 className="h-3.5 w-3.5" />
                                  {busy ? "处理中" : "解封"}
                                </Button>
                              ) : (
                                <Button size="sm" variant="destructive" className="h-8 gap-1.5" title={item.blacklisted ? `所在黑名单：${item.blacklistNames.join("、")}` : undefined} disabled={busy || item.blacklisted} onClick={() => handleBan(item.ip)}>
                                  <Ban className="h-3.5 w-3.5" />
                                  {item.blacklisted ? "已封禁" : busy ? "处理中" : "封禁"}
                                </Button>
                              )}
                              <Button size="sm" variant="outline" className="h-8 gap-1.5" onClick={() => setSelected(item)}>
                                <Eye className="h-3.5 w-3.5" />
                                详情
                              </Button>
                            </div>
                          </TableCell>
                        </TableRow>
                        {expanded && (
                          <TableRow>
                            <TableCell colSpan={15} className="bg-muted/20 p-0">
                              <div className="sticky left-0 w-[100cqw] max-w-[100cqw] overflow-hidden">
                                <VisitorHistory key={`${item.ip}:${filterKey}`} ip={item.ip} range={range} host={host} />
                              </div>
                            </TableCell>
                          </TableRow>
                        )}
                      </Fragment>
                    );
                  })}
                </TableBody>
              </Table>
            </div>
            {total > 0 && (
              <div className="flex flex-wrap items-center justify-between gap-3 border-t px-4 py-3">
                <div className="text-sm text-muted-foreground">
                  第 {(page - 1) * pageSize + 1}-{Math.min(page * pageSize, total)} 条 / 共 {total} 条
                </div>
                <div className="flex items-center gap-2">
                  <Button variant="outline" size="sm" onClick={() => setPage((value) => Math.max(1, value - 1))} disabled={page <= 1}><ChevronLeft className="h-4 w-4" />上一页</Button>
                  <span className="min-w-16 text-center text-xs text-muted-foreground">{page} / {pageCount}</span>
                  <Button variant="outline" size="sm" onClick={() => setPage((value) => Math.min(pageCount, value + 1))} disabled={page >= pageCount}>下一页<ChevronRight className="h-4 w-4" /></Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>
      </Reveal>

      <Dialog open={selected !== null} onOpenChange={(open) => { if (!open) setSelected(null); }}>
        <DialogContent className="max-h-[85vh] overflow-y-auto sm:max-w-2xl">
          <DialogHeader>
            <DialogTitle>访客详情</DialogTitle>
            <DialogDescription>最近一次访问的聚合信息</DialogDescription>
          </DialogHeader>
          {selected && (
            <div className="grid gap-3 text-sm sm:grid-cols-2">
              {[
                ["访客 IP", selected.ip],
                ["当前是否已封禁", selected.blacklisted ? "是" : "否"],
                ["所在黑名单", selected.blacklistNames.join("、") || "-"],
                ["最近访问时间", formatTs(selected.lastVisitAt)],
                ["IP 地理", [selected.country, selected.province].filter(Boolean).join(" · ") || "-"],
                ["Status code", String(selected.status || "-")],
                ["历史请求数", String(selected.requestCount)],
                ["最近来源域名", selected.refererHost || "-"],
                ["最近来源页面", selected.refererUrl || "-"],
                ["最近受访域名", selected.host || "-"],
                ["最近受访页面", selected.path || "-"],
                ["是否被拦截", selected.blocked ? "是" : "否"],
                ["拦截原因", blockReasonLabel(selected.blockReason) || "-"],
                ["异常 IP", selected.abnormalIp ? "是" : "否"],
                ["触发风控", selected.riskTriggered ? "是" : "否"],
                ["触发限流", selected.rateLimited ? "是" : "否"],
              ].map(([label, value]) => (
                <div key={label} className="min-w-0 rounded-md border border-border/60 bg-muted/20 p-3">
                  <div className="text-xs text-muted-foreground">{label}</div>
                  <div className="mt-1 break-all font-mono text-xs">{value}</div>
                </div>
              ))}
              {selected.jevBan ? (
                <div className="min-w-0 space-y-2 rounded-md border border-destructive/40 bg-destructive/5 p-3 sm:col-span-2">
                  <div className="text-xs font-medium text-destructive">Jev 封禁依据</div>
                  <div className="grid gap-2 text-xs sm:grid-cols-2">
                    {[
                      ["封禁主体", selected.jevBan.subject],
                      ["判定风险", selected.jevBan.risk == null ? "-" : `${Math.round(selected.jevBan.risk * 100)}%`],
                      ["违规次数", String(selected.jevBan.offenseCount)],
                      ["封禁时间", selected.jevBan.bannedAt ? formatTs(selected.jevBan.bannedAt) : "-"],
                      ["到期时间", selected.jevBan.expiresAt ? formatTs(selected.jevBan.expiresAt) : "永久"],
                      ["判定批次", selected.jevBan.batchId || "-"],
                    ].map(([label, value]) => (
                      <div key={label} className="min-w-0">
                        <span className="text-muted-foreground">{label}：</span>
                        <span className="break-all font-mono">{value}</span>
                      </div>
                    ))}
                  </div>
                  {selected.jevBan.evidence.length > 0 ? (
                    <div className="space-y-1">
                      <div className="text-xs text-muted-foreground">可疑请求路径</div>
                      <ul className="space-y-0.5">
                        {selected.jevBan.evidence.map((path) => (
                          <li key={path} className="break-all font-mono text-xs">{path}</li>
                        ))}
                      </ul>
                    </div>
                  ) : null}
                </div>
              ) : null}
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
});

VisitorDetailsPage.displayName = "VisitorDetailsPage";

export default VisitorDetailsPage;
