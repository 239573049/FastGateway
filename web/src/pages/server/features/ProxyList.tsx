import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Badge } from "@/components/ui/badge";
import { memo, useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { useServerStore } from "@/store/server";
import { 
    Server as ServerIcon, 
    Globe, 
    Search,
    ListChecks,
    Shield,
    Zap,
    Settings, 
    Play, 
    Square, 
    MoreVertical,
    Trash2,
    Loader2,
    X,
} from 'lucide-react';
import { useNavigate } from "react-router-dom";
import UpdateServer from "./UpdateServer";
import { Button } from "@/components/ui/button";
import { 
    DropdownMenu, 
    DropdownMenuContent, 
    DropdownMenuItem, 
    DropdownMenuSeparator,
    DropdownMenuTrigger 
} from "@/components/ui/dropdown-menu";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

const ProxyList = memo(() => {
    const { servers, setServers, loadingServers } = useServerStore();
    const navigate = useNavigate();
    const [editVisible, setEditVisible] = useState(false);
    const [serverToEdit, setServerToEdit] = useState<Server | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [query, setQuery] = useState("");
    const [statusFilter, setStatusFilter] = useState<"all" | "online" | "offline">("all");
    const [enabledOnly, setEnabledOnly] = useState(false);
    const [batchMode, setBatchMode] = useState(false);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [batchActing, setBatchActing] = useState(false);
    const [deleteDialog, setDeleteDialog] = useState<{
        open: boolean;
        items: Server[];
    }>({ open: false, items: [] });
    const [deleting, setDeleting] = useState(false);

    const loadServers = useCallback(() => {
        setIsLoading(true);
        getServers()
            .then((res) => {
                setServers(res.data);
            })
            .catch(() => {
                toast.error("获取服务列表失败");
            })
            .finally(() => {
                setIsLoading(false);
            });
    }, [setServers]);

    useEffect(() => {
        loadServers();
    }, [loadServers, loadingServers]);

    const filteredServers = useMemo(() => {
        const q = query.trim().toLowerCase();
        return servers
            .filter((server) => {
                if (statusFilter === "online" && !server.onLine) return false;
                if (statusFilter === "offline" && server.onLine) return false;
                if (enabledOnly && !server.enable) return false;
                if (!q) return true;

                const haystack = [server.name, server.description ?? "", String(server.listen)]
                    .join(" ")
                    .toLowerCase();
                return haystack.includes(q);
            })
            .sort((a, b) => {
                if (a.onLine !== b.onLine) return a.onLine ? -1 : 1;
                if (a.enable !== b.enable) return a.enable ? -1 : 1;
                return a.name.localeCompare(b.name, "zh-CN");
            });
    }, [enabledOnly, query, servers, statusFilter]);

    const selectableIds = useMemo(() => {
        return filteredServers
            .map((server) => server.id)
            .filter((id): id is string => Boolean(id));
    }, [filteredServers]);

    const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);

    const selectedServers = useMemo(() => {
        return servers.filter((server) => server.id && selectedIdSet.has(server.id));
    }, [selectedIdSet, servers]);

    const allSelected = useMemo(() => {
        return selectableIds.length > 0 && selectableIds.every((id) => selectedIdSet.has(id));
    }, [selectableIds, selectedIdSet]);

    const someSelected = useMemo(() => {
        return selectableIds.some((id) => selectedIdSet.has(id)) && !allSelected;
    }, [allSelected, selectableIds, selectedIdSet]);

    useEffect(() => {
        if (!batchMode) return;
        const selectable = new Set(selectableIds);
        setSelectedIds((prev) => prev.filter((id) => selectable.has(id)));
    }, [batchMode, selectableIds]);

    const clearFilters = () => {
        setQuery("");
        setStatusFilter("all");
        setEnabledOnly(false);
    };

    const toggleBatchMode = () => {
        setBatchMode((prev) => {
            const next = !prev;
            if (!next) {
                setSelectedIds([]);
            }
            return next;
        });
    };

    const toggleSelected = (id: string) => {
        setSelectedIds((prev) => (prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]));
    };

    const runBatchAction = async (
        label: string,
        targets: Server[],
        action: (id: string) => Promise<unknown>
    ) => {
        if (selectedIds.length === 0) {
            toast.error("请先选择服务");
            return;
        }

        const ids = targets.map((item) => item.id).filter((id): id is string => Boolean(id));
        if (ids.length === 0) {
            toast(`没有需要${label}的服务`);
            return;
        }

        setBatchActing(true);
        let successCount = 0;
        let failedCount = 0;

        for (const id of ids) {
            try {
                await action(id);
                successCount += 1;
            } catch {
                failedCount += 1;
            }
        }

        setBatchActing(false);
        setSelectedIds([]);
        loadServers();

        if (failedCount === 0) {
            toast.success(`${label}成功：${successCount} 个`);
        } else {
            toast.error(`${label}：成功 ${successCount} 个，失败 ${failedCount} 个`);
        }
    };

    const handleConfirmDelete = async () => {
        const targets = deleteDialog.items.filter((item) => item.id);
        if (targets.length === 0) {
            toast.error("服务 ID 无效");
            setDeleteDialog({ open: false, items: [] });
            return;
        }

        setDeleting(true);
        let successCount = 0;
        let failedCount = 0;

        for (const item of targets) {
            try {
                await deleteServer(item.id as string);
                successCount += 1;
            } catch {
                failedCount += 1;
            }
        }

        setDeleting(false);
        setDeleteDialog({ open: false, items: [] });
        const deletedIds = new Set(targets.map((item) => item.id as string));
        setSelectedIds((prev) => prev.filter((id) => !deletedIds.has(id)));
        loadServers();

        if (failedCount === 0) {
            toast.success(`已删除 ${successCount} 个服务`);
        } else {
            toast.error(`删除失败 ${failedCount} 个，成功 ${successCount} 个`);
        }
    };

    const renderServerRow = (server: Server) => {
        const hasFeatures =
            server.isHttps ||
            server.enableBlacklist ||
            server.enableWhitelist ||
            server.enableTunnel ||
            server.redirectHttps ||
            server.staticCompress;

        const selected = Boolean(server.id && selectedIdSet.has(server.id));

        return (
            <div
                className={cn(
                    "group flex flex-col gap-4 p-4 transition-colors hover:bg-muted/30 sm:flex-row sm:items-start sm:justify-between",
                    !server.enable && "opacity-75"
                )}
            >
                <div className="flex min-w-0 flex-1 gap-3">
                    {batchMode ? (
                        <div className="mt-1 flex h-9 items-center">
                            <Checkbox
                                checked={selected}
                                onCheckedChange={() => {
                                    if (!server.id) return;
                                    toggleSelected(server.id);
                                }}
                                disabled={!server.id || batchActing}
                                aria-label={`选择服务 ${server.name}`}
                            />
                        </div>
                    ) : null}
                    <div
                        className={cn(
                            "mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border",
                            server.onLine
                                ? "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300"
                                : "bg-muted text-muted-foreground"
                        )}
                    >
                        <ServerIcon className="h-4 w-4" />
                    </div>

                    <div className="min-w-0 flex-1 space-y-3">
                        <div className="flex flex-wrap items-center gap-2">
                            <h3 className="min-w-0 truncate font-semibold text-base">
                                {server.name}
                            </h3>
                            <Badge
                                variant={server.onLine ? "default" : "secondary"}
                                className={cn(
                                    "h-5 text-xs",
                                    server.onLine && "bg-emerald-500 hover:bg-emerald-500/90"
                                )}
                            >
                                {server.onLine ? "在线" : "离线"}
                            </Badge>
                            <Badge variant="outline" className="h-5 text-xs font-normal">
                                {server.enable ? "已启用" : "已禁用"}
                            </Badge>
                        </div>

                        <button
                            type="button"
                            onClick={() => {
                                if (!server.id) return;
                                navigate(`/server/${server.id}`);
                            }}
                            disabled={!server.id || batchMode}
                            className="w-full text-left text-sm text-muted-foreground transition-colors hover:text-foreground line-clamp-2 disabled:cursor-not-allowed disabled:opacity-60"
                        >
                            {server.description || "暂无描述"}
                        </button>

                        <div className="flex flex-wrap items-center gap-2">
                            <Badge variant="outline" className="h-6 gap-1 font-mono text-xs">
                                <Globe className="h-3.5 w-3.5" />
                                :{server.listen}
                            </Badge>
                            {hasFeatures ? (
                                <div className="flex flex-wrap gap-1.5">
                                    {server.isHttps && (
                                        <Badge variant="outline" className="text-xs">
                                            HTTPS
                                        </Badge>
                                    )}
                                    {server.enableBlacklist && (
                                        <Badge variant="outline" className="text-xs">
                                            黑名单
                                        </Badge>
                                    )}
                                    {server.enableWhitelist && (
                                        <Badge variant="outline" className="text-xs">
                                            白名单
                                        </Badge>
                                    )}
                                    {server.enableTunnel && (
                                        <Badge variant="outline" className="text-xs">
                                            隧道
                                        </Badge>
                                    )}
                                    {server.redirectHttps && (
                                        <Badge variant="outline" className="text-xs">
                                            HTTPS重定向
                                        </Badge>
                                    )}
                                    {server.staticCompress && (
                                        <Badge variant="outline" className="text-xs">
                                            静态压缩
                                        </Badge>
                                    )}
                                </div>
                            ) : null}
                        </div>
                    </div>
                </div>

                {batchMode ? null : (
                    <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                    <Button
                        size="sm"
                        variant="secondary"
                        onClick={() => navigate(`/server/${server.id}`)}
                        disabled={!server.id}
                    >
                        路由
                    </Button>
                    <Button
                        size="sm"
                        variant="outline"
                        onClick={() => {
                            if (!server.id) return;
                            onlineServer(server.id).then(() => {
                                loadServers();
                            });
                        }}
                        disabled={!server.id}
                    >
                        {server.onLine ? (
                            <>
                                <Square className="mr-2 h-4 w-4" />
                                停止
                            </>
                        ) : (
                            <>
                                <Play className="mr-2 h-4 w-4" />
                                启动
                            </>
                        )}
                    </Button>

                    <DropdownMenu>
                        <DropdownMenuTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-8 w-8">
                                <MoreVertical className="h-4 w-4" />
                                <span className="sr-only">打开菜单</span>
                            </Button>
                        </DropdownMenuTrigger>
                        <DropdownMenuContent align="end" className="w-40">
                            <DropdownMenuItem
                                onClick={() => {
                                    setServerToEdit(server);
                                    setEditVisible(true);
                                }}
                            >
                                <Settings className="mr-2 h-4 w-4" />
                                编辑
                            </DropdownMenuItem>
                            <DropdownMenuItem
                                onClick={() => {
                                    if (!server.id) return;
                                    enableServer(server.id).then(() => {
                                        loadServers();
                                    });
                                }}
                                disabled={!server.id}
                            >
                                <Shield className="mr-2 h-4 w-4" />
                                {server.enable ? "禁用" : "启用"}
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                                onClick={() => {
                                    if (!server.id) return;
                                    reloadServer(server.id)
                                        .then(() => {
                                            toast.success("刷新成功");
                                        })
                                        .catch(() => {
                                            toast.error("刷新失败");
                                        });
                                }}
                                disabled={!server.id}
                            >
                                <Zap className="mr-2 h-4 w-4" />
                                刷新路由
                            </DropdownMenuItem>
                            <DropdownMenuSeparator />
                            <DropdownMenuItem
                                onClick={() => {
                                    if (!server.id) return;
                                    setDeleteDialog({ open: true, items: [server] });
                                }}
                                className="text-destructive focus:text-destructive"
                                disabled={!server.id}
                            >
                                <Trash2 className="mr-2 h-4 w-4" />
                                删除
                            </DropdownMenuItem>
                        </DropdownMenuContent>
                    </DropdownMenu>
                    </div>
                )}
            </div>
        );
    };

    return (
        <div className="space-y-4">
            <Card className="border-border/60">
                <CardHeader className="space-y-4">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-center">
                            <div className="relative flex-1">
                                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    value={query}
                                    onChange={(e) => setQuery(e.target.value)}
                                    placeholder="搜索名称、描述或端口…"
                                    className="pl-9"
                                />
                            </div>

                            <Tabs
                                value={statusFilter}
                                onValueChange={(v) =>
                                    setStatusFilter(v as "all" | "online" | "offline")
                                }
                                className="w-full sm:w-auto"
                            >
                                <TabsList className="w-full sm:w-auto">
                                    <TabsTrigger value="all" className="flex-1">
                                        全部
                                    </TabsTrigger>
                                    <TabsTrigger value="online" className="flex-1">
                                        在线
                                    </TabsTrigger>
                                    <TabsTrigger value="offline" className="flex-1">
                                        离线
                                    </TabsTrigger>
                                </TabsList>
                            </Tabs>

                            <div className="flex items-center gap-2">
                                <Switch
                                    id="enabled-only"
                                    checked={enabledOnly}
                                    onCheckedChange={setEnabledOnly}
                                />
                                <Label
                                    htmlFor="enabled-only"
                                    className="text-sm font-medium text-muted-foreground"
                                >
                                    仅启用
                                </Label>
                            </div>
                        </div>

                        <div className="flex w-full items-center gap-2 overflow-x-auto pb-1 sm:w-auto sm:justify-end sm:overflow-visible sm:pb-0">
                            <Badge variant="secondary" className="font-normal">
                                {filteredServers.length}/{servers.length}
                            </Badge>
                            {(query || statusFilter !== "all" || enabledOnly) && (
                                <Button variant="ghost" size="sm" onClick={clearFilters}>
                                    <X className="mr-2 h-4 w-4" />
                                    清除
                                </Button>
                            )}
                            <Button
                                variant={batchMode ? "secondary" : "outline"}
                                size="sm"
                                onClick={toggleBatchMode}
                            >
                                <ListChecks className="mr-2 h-4 w-4" />
                                {batchMode ? "完成" : "批量"}
                            </Button>
                        </div>
                    </div>

                    {batchMode ? (
                        <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                            <div className="flex items-center gap-2">
                                <Checkbox
                                    checked={allSelected ? true : someSelected ? "indeterminate" : false}
                                    onCheckedChange={(checked) => {
                                        if (checked === "indeterminate") return;
                                        setSelectedIds(checked ? selectableIds : []);
                                    }}
                                    disabled={batchActing || selectableIds.length === 0}
                                    aria-label="全选"
                                />
                                <span className="text-sm text-muted-foreground">
                                    已选 {selectedIds.length}/{selectableIds.length}
                                </span>
                                {selectedIds.length > 0 ? (
                                    <Button
                                        variant="ghost"
                                        size="sm"
                                        onClick={() => setSelectedIds([])}
                                        disabled={batchActing}
                                    >
                                        清空
                                    </Button>
                                ) : null}
                            </div>

                            <div className="flex w-full items-center gap-2 overflow-x-auto pb-1 sm:w-auto sm:overflow-visible sm:pb-0">
                                <Button
                                    size="sm"
                                    variant="outline"
                                    disabled={selectedIds.length === 0 || batchActing}
                                    onClick={() =>
                                        runBatchAction(
                                            "启用",
                                            selectedServers.filter((s) => !s.enable),
                                            enableServer
                                        )
                                    }
                                >
                                    启用
                                </Button>
                                <Button
                                    size="sm"
                                    variant="outline"
                                    disabled={selectedIds.length === 0 || batchActing}
                                    onClick={() =>
                                        runBatchAction(
                                            "禁用",
                                            selectedServers.filter((s) => s.enable),
                                            enableServer
                                        )
                                    }
                                >
                                    禁用
                                </Button>
                                <Button
                                    size="sm"
                                    variant="outline"
                                    disabled={selectedIds.length === 0 || batchActing}
                                    onClick={() =>
                                        runBatchAction(
                                            "启动",
                                            selectedServers.filter((s) => !s.onLine),
                                            onlineServer
                                        )
                                    }
                                >
                                    启动
                                </Button>
                                <Button
                                    size="sm"
                                    variant="outline"
                                    disabled={selectedIds.length === 0 || batchActing}
                                    onClick={() =>
                                        runBatchAction(
                                            "停止",
                                            selectedServers.filter((s) => s.onLine),
                                            onlineServer
                                        )
                                    }
                                >
                                    停止
                                </Button>
                                <Button
                                    size="sm"
                                    variant="outline"
                                    disabled={selectedIds.length === 0 || batchActing}
                                    onClick={() =>
                                        runBatchAction("刷新路由", selectedServers, reloadServer)
                                    }
                                >
                                    刷新路由
                                </Button>
                                <Button
                                    size="sm"
                                    variant="destructive"
                                    disabled={selectedIds.length === 0 || deleting || batchActing}
                                    onClick={() => {
                                        if (selectedServers.length === 0) {
                                            toast.error("请先选择服务");
                                            return;
                                        }
                                        setDeleteDialog({ open: true, items: selectedServers });
                                    }}
                                >
                                    删除
                                </Button>
                            </div>
                        </div>
                    ) : null}
                </CardHeader>

                <CardContent className="p-0">
                    {isLoading ? (
                        <div className="divide-y">
                            {Array.from({ length: 6 }).map((_, index) => (
                                <div
                                    key={index}
                                    className="flex flex-col gap-4 p-4 sm:flex-row"
                                >
                                    <Skeleton className="h-9 w-9 rounded-lg" />
                                    <div className="flex-1 space-y-3">
                                        <div className="flex flex-wrap items-center gap-2">
                                            <Skeleton className="h-4 w-40" />
                                            <Skeleton className="h-5 w-16 rounded-full" />
                                            <Skeleton className="h-5 w-16 rounded-full" />
                                        </div>
                                        <Skeleton className="h-4 w-full max-w-xl" />
                                        <div className="flex flex-wrap items-center gap-2">
                                            <Skeleton className="h-6 w-24 rounded-full" />
                                            <Skeleton className="h-6 w-16 rounded-full" />
                                            <Skeleton className="h-6 w-16 rounded-full" />
                                        </div>
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <Skeleton className="h-8 w-16" />
                                        <Skeleton className="h-8 w-20" />
                                        <Skeleton className="h-8 w-8 rounded-md" />
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : servers.length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-2 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <ServerIcon className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <h3 className="text-lg font-semibold">还没有服务节点</h3>
                            <p className="max-w-sm text-sm text-muted-foreground">
                                点击上方“新增服务”开始创建第一个代理服务配置。
                            </p>
                        </div>
                    ) : filteredServers.length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <Search className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <div className="space-y-1">
                                <h3 className="text-lg font-semibold">没有匹配的服务</h3>
                                <p className="text-sm text-muted-foreground">
                                    尝试调整搜索关键词或筛选条件。
                                </p>
                            </div>
                            <Button variant="outline" size="sm" onClick={clearFilters}>
                                清除筛选
                            </Button>
                        </div>
                    ) : (
                        <div className="divide-y">
                            {filteredServers.map((server) => (
                                <div key={server.id ?? server.name}>
                                    {renderServerRow(server)}
                                </div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            <UpdateServer
                visible={editVisible}
                server={serverToEdit}
                onClose={() => setEditVisible(false)}
                onOk={() => {
                    setEditVisible(false);
                    loadServers();
                }}
            />

            <Dialog
                open={deleteDialog.open}
                onOpenChange={(open) => {
                    if (!open) setDeleteDialog({ open: false, items: [] });
                }}
            >
                <DialogContent className="max-w-md">
                    <DialogHeader>
                        <DialogTitle>确认删除服务？</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-2 text-sm text-muted-foreground">
                        <p>此操作会删除服务节点配置记录，无法恢复。</p>
                        {deleteDialog.items.length > 0 ? (
                            <div className="rounded-md border bg-muted/30 px-3 py-2">
                                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                                    待删除
                                </div>
                                <div className="mt-2 space-y-1">
                                    {deleteDialog.items.slice(0, 3).map((item) => (
                                        <div
                                            key={item.id ?? item.name}
                                            className="flex items-center justify-between gap-3"
                                        >
                                            <div className="min-w-0">
                                                <div className="truncate font-medium text-foreground">
                                                    {item.name}
                                                </div>
                                                <div className="text-xs text-muted-foreground">
                                                    :{item.listen}
                                                </div>
                                            </div>
                                            <Badge variant="outline" className="shrink-0 font-normal">
                                                {item.enable ? "已启用" : "已禁用"}
                                            </Badge>
                                        </div>
                                    ))}
                                    {deleteDialog.items.length > 3 ? (
                                        <div className="text-xs text-muted-foreground">
                                            以及另外 {deleteDialog.items.length - 3} 个…
                                        </div>
                                    ) : null}
                                </div>
                            </div>
                        ) : null}
                    </div>
                    <DialogFooter className="gap-2">
                        <Button
                            variant="outline"
                            onClick={() => setDeleteDialog({ open: false, items: [] })}
                            disabled={deleting}
                            className="flex-1"
                        >
                            取消
                        </Button>
                        <Button
                            variant="destructive"
                            onClick={handleConfirmDelete}
                            disabled={deleting}
                            className="flex-1"
                        >
                            {deleting ? (
                                <>
                                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                    删除中
                                </>
                            ) : (
                                "确认删除"
                            )}
                        </Button>
                    </DialogFooter>
                </DialogContent>
            </Dialog>
        </div>
    );
});

export default ProxyList;
