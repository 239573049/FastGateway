import { memo, useEffect, useState } from "react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { ColumnDef } from "@tanstack/react-table";
import { ScrollText } from "lucide-react";
import { getStatisticsRequests } from "@/services/StatisticsService";
import { useDashboardStore } from "@/pages/dashboard/store";
import type { RequestLogItem } from "@/pages/dashboard/types";
import { SecurityFilterBar } from "../shared/FilterBar";

const BLOCK_LABELS: Record<number, string> = { 1: "黑名单", 2: "限流", 3: "白名单拒绝" };
const BLOCK_CLASSES: Record<number, string> = {
  1: "border-destructive/40 bg-destructive/10 text-destructive",
  2: "border-amber-500/40 bg-amber-500/10 text-amber-600 dark:text-amber-400",
  3: "border-primary/40 bg-primary/10 text-primary",
};

function formatTs(ts: number) {
  const d = new Date(ts * 1000);
  return d.toLocaleString("zh-CN", { hour12: false });
}

const BlockedLogPage = memo(() => {
  const { range, host } = useDashboardStore();
  const [input, setInput] = useState({ page: 1, pageSize: 10, total: 0 });
  const [data, setData] = useState<RequestLogItem[]>([]);
  const [loading, setLoading] = useState(false);

  function loadData(page = input.page, pageSize = input.pageSize) {
    setLoading(true);
    getStatisticsRequests(range, { host, blocked: true, page, pageSize })
      .then((res: any) => {
        const result = res?.data;
        setData(Array.isArray(result?.items) ? result.items : []);
        setInput((prev) => ({ ...prev, page, pageSize, total: result?.total ?? 0 }));
      })
      .finally(() => setLoading(false));
  }

  // 范围 / 应用变化时回到第一页重新查询
  useEffect(() => {
    loadData(1, input.pageSize);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [range, host]);

  const columns: ColumnDef<RequestLogItem>[] = [
    {
      accessorKey: "ts",
      header: "时间",
      cell: ({ row }) => <div className="font-mono text-xs text-muted-foreground">{formatTs(row.getValue("ts"))}</div>,
    },
    {
      accessorKey: "ip",
      header: "IP",
      cell: ({ row }) => <div className="font-mono text-xs">{row.getValue("ip")}</div>,
    },
    {
      id: "geo",
      header: "归属地",
      cell: ({ row }) => {
        const item = row.original;
        const geo = [item.country, item.province].filter(Boolean).join(" · ");
        return <div className="text-xs text-muted-foreground">{geo || "-"}</div>;
      },
    },
    {
      accessorKey: "host",
      header: "Host",
      cell: ({ row }) => <div className="max-w-[160px] truncate font-mono text-xs">{row.getValue("host") || "-"}</div>,
    },
    {
      accessorKey: "path",
      header: "路径",
      cell: ({ row }) => (
        <div className="max-w-[220px] truncate font-mono text-xs" title={row.getValue("path")}>
          {row.getValue("path")}
        </div>
      ),
    },
    {
      accessorKey: "method",
      header: "方法",
      cell: ({ row }) => <div className="font-mono text-xs text-muted-foreground">{row.getValue("method")}</div>,
    },
    {
      accessorKey: "status",
      header: "状态",
      cell: ({ row }) => <div className="font-mono text-xs tabular-nums">{row.getValue("status")}</div>,
    },
    {
      accessorKey: "blocked",
      header: "拦截原因",
      cell: ({ row }) => {
        const code = row.getValue("blocked") as number;
        return (
          <Badge variant="outline" className={BLOCK_CLASSES[code] || ""}>
            {BLOCK_LABELS[code] ?? "拦截"}
          </Badge>
        );
      },
    },
  ];

  return (
    <div className="container mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <div className="flex items-center gap-2">
            <ScrollText className="h-6 w-6 text-primary" />
            <h1 className="text-3xl font-bold tracking-tight text-foreground">拦截日志</h1>
          </div>
          <p className="mt-2 text-muted-foreground">按时间、来源、原因追溯每一次被拦截的请求，用于安全审计与复盘。</p>
        </div>
        <SecurityFilterBar />
      </div>

      <Card className="border shadow-sm">
        <CardHeader className="border-b bg-muted/50">
          <CardTitle className="text-lg font-semibold">被拦截请求</CardTitle>
          <CardDescription className="text-sm text-muted-foreground">
            共 <span className="font-mono font-medium text-foreground">{input.total}</span> 条拦截记录
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
              onPaginationChange={(page, pageSize) => loadData(page, pageSize)}
            />
          </div>
        </CardContent>
      </Card>
    </div>
  );
});

export default BlockedLogPage;
