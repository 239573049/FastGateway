import { memo, useEffect, useMemo, useState } from "react";
import { motion } from "motion/react";
import { Reveal, TRANSITION } from "@/components/motion";
import { Button } from "@/components/ui/button";
import { message } from "@/utils/toast";
import CreateBlacklistAndWhitelist from "@/pages/protect-config/features/CreateBlacklistAndWhitelist";
import { DeleteBlacklist, GetBlacklist } from "@/services/BlacklistAndWhitelistService";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { ArrowRight, Ban, Pencil, Plus, ShieldCheck, Trash2 } from "lucide-react";
import { ColumnDef } from "@tanstack/react-table";
import { cn } from "@/lib/utils";
import { BlacklistAndWhitelistItem } from "@/types";

type AccessKind = "deny" | "allow";

const AccessControlPage = memo(() => {
  const [kind, setKind] = useState<AccessKind>("deny");
  const [input, setInput] = useState({ page: 1, pageSize: 10, total: 0 });
  const [data, setData] = useState<any[]>([]);
  const [loading, setLoading] = useState(false);
  const [createVisible, setCreateVisible] = useState(false);
  const [editing, setEditing] = useState<BlacklistAndWhitelistItem | null>(null);

  const isBlacklist = kind === "deny";

  function loadData() {
    setLoading(true);
    GetBlacklist(isBlacklist, input.page, input.pageSize)
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
  }, [kind, input.page, input.pageSize]);

  const handleDelete = (id: string) => {
    DeleteBlacklist(id)
      .then(() => {
        message.success("删除成功");
        loadData();
      })
      .catch(() => message.error("删除失败"));
  };

  const columns: ColumnDef<any>[] = useMemo(() => {
    const allColumns: ColumnDef<any>[] = [
      {
        accessorKey: "name",
        header: "名称",
        cell: ({ row }) => <div className="font-medium text-foreground">{row.getValue("name")}</div>,
      },
      {
        accessorKey: "enable",
        header: "状态",
        cell: ({ row }) => {
          if (isBlacklist) {
            // 黑名单强制启用，不提供关闭设定
            return <Badge className="bg-green-500 font-medium hover:bg-green-600">强制生效</Badge>;
          }
          const enabled = row.getValue("enable") as boolean;
          return (
            <Badge variant={enabled ? "default" : "secondary"} className={cn(enabled ? "bg-green-500 hover:bg-green-600" : "")}>
              {enabled ? "已启用" : "已禁用"}
            </Badge>
          );
        },
      },
      {
        accessorKey: "ips",
        header: "IP 数量",
        cell: ({ row }) => {
          const ips = row.getValue("ips") as string[];
          return (
            <Badge variant="outline" className="font-mono text-xs">
              {ips?.length || 0}
            </Badge>
          );
        },
      },
      {
        id: "regions",
        accessorKey: "regions",
        header: "封禁地区",
        cell: ({ row }) => {
          const regions = (row.getValue("regions") as string[]) || [];
          if (!regions.length) return <span className="text-sm text-muted-foreground">-</span>;
          return (
            <div className="flex max-w-xs flex-wrap gap-1">
              {regions.slice(0, 3).map((region) => (
                <Badge key={region} variant="secondary" className="text-xs">
                  {region.replace("|", " · ")}
                </Badge>
              ))}
              {regions.length > 3 && (
                <Badge variant="outline" className="text-xs">
                  +{regions.length - 3}
                </Badge>
              )}
            </div>
          );
        },
      },
      {
        accessorKey: "description",
        header: "描述",
        cell: ({ row }) => (
          <div className="max-w-xs truncate text-sm text-muted-foreground">{row.getValue("description") || "-"}</div>
        ),
      },
      {
        accessorKey: "createdAt",
        header: "更新时间",
        cell: ({ row }) => <div className="font-mono text-sm text-muted-foreground">{row.getValue("createdAt")}</div>,
      },
      {
        id: "actions",
        header: "操作",
        cell: ({ row }) => {
          const item = row.original;
          return (
            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8"
                onClick={() => {
                  setEditing(item);
                  setCreateVisible(true);
                }}
              >
                <Pencil className="h-4 w-4" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                onClick={() => handleDelete(item.id)}
              >
                <Trash2 className="h-4 w-4" />
              </Button>
            </div>
          );
        },
      },
    ];
    // 地区列仅黑名单视图展示
    return isBlacklist ? allColumns : allColumns.filter((col) => col.id !== "regions");
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [isBlacklist]);

  const switchKind = (next: AccessKind) => {
    if (next === kind) return;
    setKind(next);
    setInput((prev) => ({ ...prev, page: 1 }));
  };

  return (
    <div className="container mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <Reveal className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-3xl font-bold tracking-tight text-foreground">访问控制</h1>
          <p className="mt-2 text-muted-foreground">统一管理 IP 黑名单与白名单，控制哪些来源可以访问网关。</p>
        </div>
        <Button
          onClick={() => {
            setEditing(null);
            setCreateVisible(true);
          }}
        >
          <Plus className="mr-2 h-4 w-4" />
          新增{isBlacklist ? "黑名单" : "白名单"}
        </Button>
      </Reveal>

      {/* 生效优先级说明 */}
      <Reveal index={1} className="mb-6 rounded-lg border bg-muted/40 p-4">
        <div className="mb-3 flex items-center gap-2 text-sm font-medium">
          <ShieldCheck className="h-4 w-4 text-primary" />
          生效优先级
        </div>
        <div className="flex flex-wrap items-center gap-2 text-sm">
          <span className="rounded-md border bg-background px-3 py-1.5 font-medium">请求 IP</span>
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
          <span className="rounded-md border border-green-500/40 bg-green-500/10 px-3 py-1.5 font-medium text-green-700 dark:text-green-400">
            命中白名单 → 放行
          </span>
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
          <span className="rounded-md border border-destructive/40 bg-destructive/10 px-3 py-1.5 font-medium text-destructive">
            命中黑名单 → 403
          </span>
          <ArrowRight className="h-4 w-4 text-muted-foreground" />
          <span className="rounded-md border bg-background px-3 py-1.5 font-medium">其余 → 放行</span>
        </div>
        <p className="mt-3 text-xs text-muted-foreground">
          白名单启用后仅名单内 IP 可访问；黑名单<span className="font-medium text-foreground">强制启用、不可关闭</span>，用于阻断已知恶意来源。
        </p>
      </Reveal>

      {/* 黑 / 白名单切换：layoutId 滑块跟随选中项移动 */}
      <Reveal index={2} className="mb-4 flex flex-wrap items-center gap-3">
        <div className="inline-flex rounded-lg border bg-muted/40 p-1">
          <button
            onClick={() => switchKind("deny")}
            className={cn(
              "relative flex items-center gap-2 rounded-md px-4 py-1.5 text-sm font-medium transition-colors",
              isBlacklist ? "text-destructive" : "text-muted-foreground hover:text-foreground"
            )}
          >
            {isBlacklist && (
              <motion.span
                layoutId="access-kind-indicator"
                className="absolute inset-0 rounded-md bg-background shadow-sm"
                transition={TRANSITION.layout}
              />
            )}
            <Ban className="relative h-4 w-4" />
            <span className="relative">黑名单</span>
          </button>
          <button
            onClick={() => switchKind("allow")}
            className={cn(
              "relative flex items-center gap-2 rounded-md px-4 py-1.5 text-sm font-medium transition-colors",
              !isBlacklist ? "text-green-600 dark:text-green-400" : "text-muted-foreground hover:text-foreground"
            )}
          >
            {!isBlacklist && (
              <motion.span
                layoutId="access-kind-indicator"
                className="absolute inset-0 rounded-md bg-background shadow-sm"
                transition={TRANSITION.layout}
              />
            )}
            <ShieldCheck className="relative h-4 w-4" />
            <span className="relative">白名单</span>
          </button>
        </div>
        <span className="text-sm text-muted-foreground">
          {isBlacklist ? "黑名单强制生效，用于阻断已知恶意来源。" : "白名单启用后，仅名单内 IP 可访问该服务。"}
        </span>
      </Reveal>

      <Reveal index={3}>
      <Card className="border shadow-sm">
        <CardHeader className="border-b bg-muted/50">
          <CardTitle className="text-lg font-semibold">{isBlacklist ? "IP 黑名单" : "IP 白名单"}</CardTitle>
          <CardDescription className="text-sm text-muted-foreground">
            {isBlacklist ? "阻断恶意访问，保护系统安全" : "仅允许可信来源访问"}
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
              onPaginationChange={(page, pageSize) => setInput((prev) => ({ ...prev, page, pageSize }))}
            />
          </div>
        </CardContent>
      </Card>
      </Reveal>

      <CreateBlacklistAndWhitelist
        isBlacklist={isBlacklist}
        visible={createVisible}
        editing={editing}
        onClose={() => setCreateVisible(false)}
        onOk={() => {
          setCreateVisible(false);
          loadData();
        }}
      />
    </div>
  );
});

export default AccessControlPage;
