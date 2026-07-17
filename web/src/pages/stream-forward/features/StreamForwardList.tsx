import { memo, useCallback, useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import {
    Loader2,
    MoreVertical,
    Network,
    Play,
    RefreshCw,
    Search,
    Settings,
    Shield,
    Square,
    Trash2,
    Zap,
} from "lucide-react";

import {
    deleteStreamForward,
    enableStreamForward,
    getStreamForwards,
    onlineStreamForward,
    reloadStreamForward,
} from "@/services/StreamForwardService";
import { StreamForward, StreamLoadBalancing, StreamProtocol } from "@/types";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuSeparator,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

import StreamForwardDialog from "./StreamForwardDialog";

const protocolLabel = (protocol: StreamProtocol) => {
    switch (protocol) {
        case StreamProtocol.Udp:
            return "UDP";
        case StreamProtocol.Both:
            return "TCP+UDP";
        case StreamProtocol.Tcp:
        default:
            return "TCP";
    }
};

const lbLabel = (lb: StreamLoadBalancing) => {
    switch (lb) {
        case StreamLoadBalancing.LeastConnections:
            return "最少连接";
        case StreamLoadBalancing.Random:
            return "随机";
        case StreamLoadBalancing.RoundRobin:
        default:
            return "轮询";
    }
};

interface StreamForwardListProps {
    reloadFlag: number;
}

const StreamForwardList = memo(({ reloadFlag }: StreamForwardListProps) => {
    const [items, setItems] = useState<StreamForward[]>([]);
    const [isLoading, setIsLoading] = useState(false);
    const [query, setQuery] = useState("");
    const [editVisible, setEditVisible] = useState(false);
    const [editing, setEditing] = useState<StreamForward | null>(null);
    const [deleteTarget, setDeleteTarget] = useState<StreamForward | null>(null);
    const [deleting, setDeleting] = useState(false);

    const load = useCallback(() => {
        setIsLoading(true);
        getStreamForwards()
            .then((res) => setItems(res.data ?? []))
            .catch(() => toast.error("获取端口转发列表失败"))
            .finally(() => setIsLoading(false));
    }, []);

    useEffect(() => {
        load();
    }, [load, reloadFlag]);

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        return items
            .filter((item) => {
                if (!q) return true;
                const haystack = [item.name, item.description ?? "", String(item.listenPort)]
                    .join(" ")
                    .toLowerCase();
                return haystack.includes(q);
            })
            .sort((a, b) => {
                if (a.onLine !== b.onLine) return a.onLine ? -1 : 1;
                return a.name.localeCompare(b.name, "zh-CN");
            });
    }, [items, query]);

    const confirmDelete = async () => {
        if (!deleteTarget?.id) return;
        setDeleting(true);
        try {
            await deleteStreamForward(deleteTarget.id);
            toast.success("已删除");
        } catch {
            toast.error("删除失败");
        } finally {
            setDeleting(false);
            setDeleteTarget(null);
            load();
        }
    };

    const renderRow = (item: StreamForward) => (
        <div
            className={cn(
                "group flex flex-col gap-4 p-4 transition-colors hover:bg-muted/30 sm:flex-row sm:items-start sm:justify-between",
                !item.enable && "opacity-75"
            )}
        >
            <div className="flex min-w-0 flex-1 gap-3">
                <div
                    className={cn(
                        "mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-lg border",
                        item.onLine
                            ? "border-emerald-200 bg-emerald-50 text-emerald-700 dark:border-emerald-900/60 dark:bg-emerald-950/40 dark:text-emerald-300"
                            : "bg-muted text-muted-foreground"
                    )}
                >
                    <Network className="h-4 w-4" />
                </div>

                <div className="min-w-0 flex-1 space-y-3">
                    <div className="flex flex-wrap items-center gap-2">
                        <h3 className="min-w-0 truncate text-base font-semibold">{item.name}</h3>
                        <Badge
                            variant={item.onLine ? "default" : "secondary"}
                            className={cn("h-5 text-xs", item.onLine && "bg-emerald-500 hover:bg-emerald-500/90")}
                        >
                            {item.onLine ? "在线" : "离线"}
                        </Badge>
                        <Badge variant="outline" className="h-5 text-xs font-normal">
                            {item.enable ? "已启用" : "已禁用"}
                        </Badge>
                        {item.onLine ? (
                            <Badge variant="outline" className="h-5 text-xs font-normal text-muted-foreground">
                                {item.activeConnections} 连接 · {item.udpSessions} 会话
                            </Badge>
                        ) : null}
                    </div>

                    <p className="line-clamp-2 text-sm text-muted-foreground">
                        {item.description || "暂无描述"}
                    </p>

                    <div className="flex flex-wrap items-center gap-2">
                        <Badge variant="outline" className="h-6 gap-1 font-mono text-xs">
                            {protocolLabel(item.protocol)} {item.listenAddress}:{item.listenPort}
                        </Badge>
                        <Badge variant="outline" className="h-6 text-xs font-normal">
                            {item.upStreams.length} 上游 · {lbLabel(item.loadBalancing)}
                        </Badge>
                    </div>
                </div>
            </div>

            <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                <Button
                    size="sm"
                    variant="outline"
                    onClick={() => {
                        if (!item.id) return;
                        onlineStreamForward(item.id).then(() => load());
                    }}
                    disabled={!item.id}
                >
                    {item.onLine ? (
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
                                setEditing(item);
                                setEditVisible(true);
                            }}
                        >
                            <Settings className="mr-2 h-4 w-4" />
                            编辑
                        </DropdownMenuItem>
                        <DropdownMenuItem
                            onClick={() => {
                                if (!item.id) return;
                                enableStreamForward(item.id).then(() => load());
                            }}
                            disabled={!item.id}
                        >
                            <Shield className="mr-2 h-4 w-4" />
                            {item.enable ? "禁用" : "启用"}
                        </DropdownMenuItem>
                        <DropdownMenuItem
                            onClick={() => {
                                if (!item.id) return;
                                reloadStreamForward(item.id)
                                    .then(() => {
                                        toast.success("重载成功");
                                        load();
                                    })
                                    .catch(() => toast.error("重载失败"));
                            }}
                            disabled={!item.id}
                        >
                            <Zap className="mr-2 h-4 w-4" />
                            重载
                        </DropdownMenuItem>
                        <DropdownMenuSeparator />
                        <DropdownMenuItem
                            onClick={() => setDeleteTarget(item)}
                            className="text-destructive focus:text-destructive"
                            disabled={!item.id}
                        >
                            <Trash2 className="mr-2 h-4 w-4" />
                            删除
                        </DropdownMenuItem>
                    </DropdownMenuContent>
                </DropdownMenu>
            </div>
        </div>
    );

    return (
        <div className="space-y-4">
            <Card className="border-border/60">
                <CardHeader className="space-y-4">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                        <div className="relative flex-1">
                            <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                            <Input
                                value={query}
                                onChange={(e) => setQuery(e.target.value)}
                                placeholder="搜索名称、描述或端口…"
                                className="pl-9"
                            />
                        </div>
                        <div className="flex items-center gap-2 sm:justify-end">
                            <Badge variant="secondary" className="font-normal">
                                {filtered.length}/{items.length}
                            </Badge>
                            <Button variant="outline" size="sm" onClick={load}>
                                <RefreshCw className="mr-2 h-4 w-4" />
                                刷新
                            </Button>
                        </div>
                    </div>
                </CardHeader>

                <CardContent className="p-0">
                    {isLoading ? (
                        <div className="divide-y">
                            {Array.from({ length: 4 }).map((_, index) => (
                                <div key={index} className="flex gap-4 p-4">
                                    <Skeleton className="h-9 w-9 rounded-lg" />
                                    <div className="flex-1 space-y-3">
                                        <Skeleton className="h-4 w-40" />
                                        <Skeleton className="h-4 w-full max-w-xl" />
                                        <Skeleton className="h-6 w-48 rounded-full" />
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : items.length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-2 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <Network className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <h3 className="text-lg font-semibold">还没有端口转发规则</h3>
                            <p className="max-w-sm text-sm text-muted-foreground">
                                点击上方“新增转发”创建第一条 TCP/UDP 转发规则。
                            </p>
                        </div>
                    ) : filtered.length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <Search className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <h3 className="text-lg font-semibold">没有匹配的规则</h3>
                        </div>
                    ) : (
                        <div className="divide-y">
                            {filtered.map((item) => (
                                <div key={item.id ?? item.name}>{renderRow(item)}</div>
                            ))}
                        </div>
                    )}
                </CardContent>
            </Card>

            <StreamForwardDialog
                visible={editVisible}
                streamForward={editing}
                onClose={() => setEditVisible(false)}
                onOk={() => {
                    setEditVisible(false);
                    load();
                }}
            />

            <Dialog open={Boolean(deleteTarget)} onOpenChange={(open) => !open && setDeleteTarget(null)}>
                <DialogContent className="max-w-md">
                    <DialogHeader>
                        <DialogTitle>确认删除端口转发？</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-2 text-sm text-muted-foreground">
                        <p>此操作会删除转发规则并停止监听，无法恢复。</p>
                        {deleteTarget ? (
                            <div className="rounded-md border bg-muted/30 px-3 py-2">
                                <div className="font-medium text-foreground">{deleteTarget.name}</div>
                                <div className="text-xs text-muted-foreground">
                                    {protocolLabel(deleteTarget.protocol)} :{deleteTarget.listenPort}
                                </div>
                            </div>
                        ) : null}
                    </div>
                    <DialogFooter className="gap-2">
                        <Button
                            variant="outline"
                            onClick={() => setDeleteTarget(null)}
                            disabled={deleting}
                            className="flex-1"
                        >
                            取消
                        </Button>
                        <Button
                            variant="destructive"
                            onClick={confirmDelete}
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

export default StreamForwardList;
