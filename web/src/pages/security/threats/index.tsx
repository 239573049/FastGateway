import { memo, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { message } from "@/utils/toast";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { ColumnDef } from "@tanstack/react-table";
import { ArrowRight, Ban, Radar, RefreshCw, Undo2 } from "lucide-react";
import { cn } from "@/lib/utils";
import type { AbnormalIp } from "@/types";
import { AddAbnormalIpToBlacklist, GetAbnormalIps, RemoveAbnormalIpFromBlacklist } from "@/services/AbnormalIpService";

const ABNORMAL_THRESHOLD = 20;
const WINDOW_SECONDS = 60;
const RETENTION_MINUTES = 30;

const ThreatDetectionPage = memo(() => {
  const [input, setInput] = useState({ page: 1, pageSize: 10, total: 0 });
  const [data, setData] = useState<AbnormalIp[]>([]);
  const [loading, setLoading] = useState(false);
  const [blacklistingIp, setBlacklistingIp] = useState<string | null>(null);
  const [unblacklistingIp, setUnblacklistingIp] = useState<string | null>(null);

  function loadData() {
    setLoading(true);
    GetAbnormalIps(input.page, input.pageSize)
      .then((res) => {
        const result = res.data;
        setData(result.items);
        setInput((prev) => ({ ...prev, total: result.total }));
      })
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [input.page, input.pageSize]);

  const handleAddToBlacklist = async (ip: string) => {
    try {
      setBlacklistingIp(ip);
      await AddAbnormalIpToBlacklist(ip);
      message.success("已加入黑名单");
      loadData();
    } catch {
      message.error("加入黑名单失败");
    } finally {
      setBlacklistingIp(null);
    }
  };

  const handleRemoveFromBlacklist = async (ip: string) => {
    try {
      setUnblacklistingIp(ip);
      const res = await RemoveAbnormalIpFromBlacklist(ip);
      message.success(res?.message || "已移出黑名单");
      loadData();
    } catch {
      message.error("移出黑名单失败");
    } finally {
      setUnblacklistingIp(null);
    }
  };

  const columns: ColumnDef<AbnormalIp>[] = [
    {
      accessorKey: "ip",
      header: "IP",
      cell: ({ row }) => (
        <Badge variant="outline" className="font-mono text-xs">
          {row.getValue("ip")}
        </Badge>
      ),
    },
    {
      accessorKey: "windowErrorCount",
      header: "近1分钟",
      cell: ({ row }) => {
        const count = row.getValue("windowErrorCount") as number;
        const abnormal = count >= ABNORMAL_THRESHOLD;
        return (
          <Badge variant={abnormal ? "destructive" : "secondary"} className={cn("font-mono", abnormal ? "" : "bg-muted text-foreground")}>
            {count}
          </Badge>
        );
      },
    },
    {
      accessorKey: "totalErrorCount",
      header: "累计错误",
      cell: ({ row }) => (
        <Badge variant="outline" className="font-mono">
          {row.getValue("totalErrorCount")}
        </Badge>
      ),
    },
    {
      id: "error",
      header: "异常信息",
      cell: ({ row }) => {
        const item = row.original;
        const primary = item.topErrorDescription || item.lastErrorDescription || "-";
        const secondary =
          item.topErrorDescription && item.lastErrorDescription && item.topErrorDescription !== item.lastErrorDescription
            ? item.lastErrorDescription
            : null;
        const requestInfoParts = [
          item.lastMethod ? item.lastMethod.toUpperCase() : null,
          item.lastPath || null,
          item.lastStatusCode ? `(${item.lastStatusCode})` : null,
        ].filter(Boolean);

        return (
          <div className="max-w-[520px]">
            <div className="truncate text-sm font-medium">{primary}</div>
            {secondary ? <div className="truncate text-xs text-muted-foreground">{secondary}</div> : null}
            <div className="truncate font-mono text-xs text-muted-foreground">
              {requestInfoParts.length ? requestInfoParts.join(" ") : "-"}
            </div>
          </div>
        );
      },
    },
    {
      accessorKey: "lastSeen",
      header: "最后时间",
      cell: ({ row }) => <div className="font-mono text-sm text-muted-foreground">{row.getValue("lastSeen")}</div>,
    },
    {
      id: "actions",
      header: "操作",
      cell: ({ row }) => {
        const item = row.original;
        const busyAdd = blacklistingIp === item.ip;
        const busyRemove = unblacklistingIp === item.ip;
        const busy = busyAdd || busyRemove;
        return (
          <div className="flex items-center gap-2">
            <Button size="sm" variant="destructive" className="gap-2" disabled={busy} onClick={() => handleAddToBlacklist(item.ip)}>
              <Ban className="h-4 w-4" />
              {busyAdd ? "处理中..." : "封禁"}
            </Button>
            <Button size="sm" variant="outline" className="gap-2" disabled={busy} onClick={() => handleRemoveFromBlacklist(item.ip)}>
              <Undo2 className="h-4 w-4" />
              {busyRemove ? "处理中..." : "移出"}
            </Button>
          </div>
        );
      },
    },
  ];

  return (
    <div className="container mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-6">
        <div className="flex items-center gap-2">
          <Radar className="h-6 w-6 text-destructive" />
          <h1 className="text-3xl font-bold tracking-tight text-foreground">威胁检测</h1>
        </div>
        <p className="mt-2 text-muted-foreground">
          基于 {WINDOW_SECONDS} 秒滑动窗口自动识别高频错误来源，支持一键封禁或移出黑名单。
        </p>
      </div>

      <div className="grid gap-6 lg:grid-cols-[minmax(0,340px)_1fr]">
        {/* 自动封禁规则 */}
        <Card className="h-fit border shadow-sm">
          <CardHeader className="border-b bg-muted/50">
            <CardTitle className="text-base font-semibold">检测规则</CardTitle>
            <CardDescription className="text-sm text-muted-foreground">异常判定与处置参数</CardDescription>
          </CardHeader>
          <CardContent className="space-y-4 pt-4">
            <div className="flex flex-wrap items-center gap-2 text-xs">
              <span className="rounded-md border bg-background px-2.5 py-1 font-medium">{WINDOW_SECONDS}s 窗口</span>
              <ArrowRight className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="rounded-md border border-primary/40 bg-primary/10 px-2.5 py-1 font-medium text-primary">
                错误 ≥ 阈值
              </span>
              <ArrowRight className="h-3.5 w-3.5 text-muted-foreground" />
              <span className="rounded-md border bg-background px-2.5 py-1 font-medium">判定异常</span>
            </div>

            <div className="flex items-center justify-between border-t pt-3 text-sm">
              <span className="text-muted-foreground">异常判定阈值（错误数）</span>
              <span className="font-mono text-base font-bold tabular-nums">{ABNORMAL_THRESHOLD}</span>
            </div>
            <div className="flex items-center justify-between text-sm">
              <span className="text-muted-foreground">异常保留时长</span>
              <span className="font-mono">{RETENTION_MINUTES} 分钟</span>
            </div>

            <div className="rounded-md border border-primary/30 bg-primary/5 p-3 text-xs leading-relaxed text-muted-foreground">
              达到阈值的 IP 会在下方实时列表中标红，可点击「封禁」写入自动黑名单立即生效。
            </div>
          </CardContent>
        </Card>

        {/* 实时异常 IP */}
        <Card className="border shadow-sm">
          <CardHeader className="flex flex-row items-center justify-between border-b bg-muted/50 space-y-0">
            <div>
              <CardTitle className="text-base font-semibold">实时异常 IP</CardTitle>
              <CardDescription className="text-sm text-muted-foreground">仅展示达到阈值或近期触发过阈值的 IP</CardDescription>
            </div>
            <Button variant="outline" size="sm" className="gap-2" onClick={() => loadData()}>
              <RefreshCw className="h-4 w-4" />
              刷新
            </Button>
          </CardHeader>
          <CardContent className="p-0">
            <div className="p-4">
              <DataTable
                columns={columns}
                data={data}
                loading={loading}
                total={input.total}
                pageSize={input.pageSize}
                current={input.page}
                onPaginationChange={(page, pageSize) => setInput((prev) => ({ ...prev, page, pageSize }))}
              />
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
});

export default ThreatDetectionPage;
