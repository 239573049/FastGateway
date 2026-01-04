import { memo, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { message } from "@/utils/toast";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { ColumnDef } from "@tanstack/react-table";
import { AlertTriangle, Ban, RefreshCw, Undo2 } from "lucide-react";
import { cn } from "@/lib/utils";
import type { AbnormalIp } from "@/types";
import { AddAbnormalIpToBlacklist, GetAbnormalIps, RemoveAbnormalIpFromBlacklist } from "@/services/AbnormalIpService";

const ABNORMAL_THRESHOLD = 20;

const AbnormalIpPage = memo(() => {
  const [input, setInput] = useState({
    page: 1,
    pageSize: 10,
    total: 0,
  });
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
        setInput((prev) => ({
          ...prev,
          total: result.total,
        }));
      })
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    loadData();
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
          <Badge
            variant={abnormal ? "destructive" : "secondary"}
            className={cn("font-mono", abnormal ? "" : "bg-muted text-foreground")}
          >
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
            <div className="text-sm font-medium truncate">{primary}</div>
            {secondary ? (
              <div className="text-xs text-muted-foreground truncate">{secondary}</div>
            ) : null}
            <div className="text-xs text-muted-foreground font-mono truncate">
              {requestInfoParts.length ? requestInfoParts.join(" ") : "-"}
            </div>
          </div>
        );
      },
    },
    {
      accessorKey: "lastSeen",
      header: "最后时间",
      cell: ({ row }) => (
        <div className="text-sm text-muted-foreground font-mono">{row.getValue("lastSeen")}</div>
      ),
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
            <Button
              size="sm"
              variant="destructive"
              className="gap-2"
              disabled={busy}
              onClick={() => handleAddToBlacklist(item.ip)}
            >
              <Ban className="h-4 w-4" />
              {busyAdd ? "处理中..." : "加入黑名单"}
            </Button>
            <Button
              size="sm"
              variant="outline"
              className="gap-2"
              disabled={busy}
              onClick={() => handleRemoveFromBlacklist(item.ip)}
            >
              <Undo2 className="h-4 w-4" />
              {busyRemove ? "处理中..." : "移出黑名单"}
            </Button>
          </div>
        );
      },
    },
  ];

  return (
    <div className="container mx-auto py-8 px-4 sm:px-6 lg:px-8 max-w-7xl">
      <div className="mb-8">
        <div className="flex items-center gap-2">
          <AlertTriangle className="h-6 w-6 text-destructive" />
          <h1 className="text-3xl font-bold tracking-tight text-foreground">异常IP</h1>
        </div>
        <p className="text-muted-foreground mt-2">
          自动统计近1分钟高频异常IP，支持一键加入/移出黑名单
        </p>
      </div>

      <div className="flex justify-between items-center mb-6">
        <div className="flex items-center space-x-2">
          <div className="h-8 w-1 bg-destructive rounded-full"></div>
          <h2 className="text-xl font-semibold text-foreground">异常IP列表</h2>
        </div>
        <Button variant="outline" className="gap-2" onClick={() => loadData()}>
          <RefreshCw className="h-4 w-4" />
          刷新
        </Button>
      </div>

      <Card className="border shadow-sm">
        <CardHeader className="border-b bg-muted/50">
          <CardTitle className="text-lg font-semibold">实时异常分析</CardTitle>
          <CardDescription className="text-sm text-muted-foreground">
            仅展示达到阈值或近期触发过阈值的IP
          </CardDescription>
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
              onPaginationChange={(page, pageSize) => {
                setInput((prev) => ({
                  ...prev,
                  page,
                  pageSize,
                }));
              }}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
});

export default AbnormalIpPage;
