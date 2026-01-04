import { Highlight, HighlightItem } from "@/components/animate-ui/primitives/effects/highlight";
import {
    Accordion,
    AccordionContent,
    AccordionHeader,
    AccordionItem,
    AccordionTrigger,
} from "@/components/animate-ui/primitives/radix/accordion";
import { Badge } from "@/components/animate-ui/components/ui/badge";
import { Button } from "@/components/animate-ui/components/ui/button";
import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
} from "@/components/animate-ui/components/ui/card";
import {
    Dialog,
    DialogContent,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/animate-ui/components/ui/dialog";
import { Input } from "@/components/animate-ui/components/ui/input";
import { cn } from "@/lib/utils";
import { ApplyCert, DeleteCert, GetCert } from "@/services/CertService";
import { message } from "@/utils/toast";
import { differenceInCalendarDays, format } from "date-fns";
import {
    AlertTriangle,
    CheckCircle2,
    Copy,
    Loader2,
    Plus,
    RefreshCw,
    Search,
    Shield,
    Trash2,
} from "lucide-react";
import { useCallback, useEffect, useMemo, useState } from "react";

import CreateCertPage from "./features/CreateCert";
import TableList from "./features/TableList";

type CertRenewStats = 0 | 1 | 2;

type CertItem = {
    id: string;
    domain: string;
    email: string;
    autoRenew: boolean;
    renewStats: CertRenewStats;
    renewTime?: string | null;
    notAfter?: string | null;
};

type RenewFilter = "all" | "none" | "success" | "failed";

function safeParseDate(value?: string | null) {
    if (!value) return null;
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return null;
    return date;
}

function formatMaybeDate(value?: string | null, fmt = "yyyy-MM-dd HH:mm") {
    const date = safeParseDate(value);
    if (!date) return "-";
    return format(date, fmt);
}

function copyToClipboard(text: string) {
    const value = text?.trim();
    if (!value) return Promise.reject(new Error("empty"));

    if (navigator?.clipboard?.writeText) {
        return navigator.clipboard.writeText(value);
    }

    return new Promise<void>((resolve, reject) => {
        try {
            const textarea = document.createElement("textarea");
            textarea.value = value;
            textarea.style.position = "fixed";
            textarea.style.top = "0";
            textarea.style.left = "0";
            textarea.style.opacity = "0";
            document.body.appendChild(textarea);
            textarea.focus();
            textarea.select();
            const ok = document.execCommand("copy");
            document.body.removeChild(textarea);
            ok ? resolve() : reject(new Error("copy failed"));
        } catch (err) {
            reject(err);
        }
    });
}

export default function CertPage() {
    const [createVisible, setCreateVisible] = useState(false);
    const [pagination, setPagination] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [data, setData] = useState<CertItem[]>([]);
    const [loading, setLoading] = useState(false);

    const [query, setQuery] = useState("");
    const [renewFilter, setRenewFilter] = useState<RenewFilter>("all");
    const [autoRenewOnly, setAutoRenewOnly] = useState(false);
    const [applyingIds, setApplyingIds] = useState<Record<string, boolean>>({});

    const [deleteDialog, setDeleteDialog] = useState<{
        open: boolean;
        item: CertItem | null;
    }>({ open: false, item: null });
    const [deleting, setDeleting] = useState(false);

    const loadData = useCallback(async () => {
        setLoading(true);
        try {
            const res = await GetCert(pagination.page, pagination.pageSize);
            const result = res.data ?? {};
            setData((result.items ?? []) as CertItem[]);
            setPagination((prev) => ({
                ...prev,
                total: Number(result.total ?? 0),
            }));
        } catch (err) {
            message.error("获取证书列表失败");
        } finally {
            setLoading(false);
        }
    }, [pagination.page, pagination.pageSize]);

    useEffect(() => {
        loadData();
    }, [loadData]);

    const filteredData = useMemo(() => {
        const q = query.trim().toLowerCase();
        return data
            .filter((item) => {
                if (autoRenewOnly && !item.autoRenew) return false;
                if (renewFilter !== "all") {
                    if (renewFilter === "none" && item.renewStats !== 0) return false;
                    if (renewFilter === "success" && item.renewStats !== 1) return false;
                    if (renewFilter === "failed" && item.renewStats !== 2) return false;
                }

                if (!q) return true;
                const haystack = [item.domain ?? "", item.email ?? ""]
                    .join(" ")
                    .toLowerCase();
                return haystack.includes(q);
            })
            .sort((a, b) => {
                const aDate = safeParseDate(a.notAfter);
                const bDate = safeParseDate(b.notAfter);
                if (aDate && bDate) return aDate.getTime() - bDate.getTime();
                if (aDate) return -1;
                if (bDate) return 1;
                return String(a.domain ?? "").localeCompare(
                    String(b.domain ?? ""),
                    "zh-CN"
                );
            });
    }, [autoRenewOnly, data, query, renewFilter]);

    const stats = useMemo(() => {
        const now = new Date();
        const pageTotal = data.length;
        const autoRenewCount = data.filter((item) => item.autoRenew).length;
        const failedCount = data.filter((item) => item.renewStats === 2).length;
        const expiringSoon = data.filter((item) => {
            const d = safeParseDate(item.notAfter);
            if (!d) return false;
            const daysLeft = differenceInCalendarDays(d, now);
            return daysLeft >= 0 && daysLeft <= 15;
        }).length;

        return { pageTotal, autoRenewCount, failedCount, expiringSoon };
    }, [data]);

    const handleApply = useCallback(
        async (item: CertItem) => {
            setApplyingIds((prev) => ({ ...prev, [item.id]: true }));
            try {
                const res = await ApplyCert(item.id);
                if (res?.success) {
                    message.success("申请成功");
                    loadData();
                    return;
                }
                message.error(res?.message ?? "申请失败");
            } catch (err) {
                message.error("申请失败");
            } finally {
                setApplyingIds((prev) => {
                    const next = { ...prev };
                    delete next[item.id];
                    return next;
                });
            }
        },
        [loadData]
    );

    const handleConfirmDelete = useCallback(async () => {
        if (!deleteDialog.item) return;
        setDeleting(true);
        try {
            await DeleteCert(deleteDialog.item.id);
            message.success("删除成功");
            setDeleteDialog({ open: false, item: null });

            if (data.length <= 1 && pagination.page > 1) {
                setPagination((prev) => ({
                    ...prev,
                    page: Math.max(1, prev.page - 1),
                }));
                return;
            }

            loadData();
        } catch (err) {
            message.error("删除失败");
        } finally {
            setDeleting(false);
        }
    }, [data.length, deleteDialog.item, loadData, pagination.page]);

    const columns = useMemo(() => {
        return [
            {
                title: "域名",
                dataIndex: "domain",
                key: "domain",
                render: (_: unknown, item: CertItem) => {
                    return (
                        <div className="flex items-center gap-2">
                            <span className="font-medium">{item.domain || "-"}</span>
                            {item.domain ? (
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-7 w-7 text-muted-foreground hover:text-foreground"
                                    onClick={async () => {
                                        try {
                                            await copyToClipboard(item.domain);
                                            message.success("已复制域名");
                                        } catch {
                                            message.error("复制失败");
                                        }
                                    }}
                                >
                                    <Copy className="h-4 w-4" />
                                    <span className="sr-only">复制域名</span>
                                </Button>
                            ) : null}
                        </div>
                    );
                },
            },
            {
                title: "邮箱",
                dataIndex: "email",
                key: "email",
                render: (_: unknown, item: CertItem) => (
                    <span className="text-muted-foreground">
                        {item.email || "-"}
                    </span>
                ),
            },
            {
                title: "续期策略",
                dataIndex: "autoRenew",
                key: "autoRenew",
                render: (value: unknown) => {
                    const enabled =
                        value === true || value === 1 || value === "1" || value === "true";
                    return (
                        <Badge
                            variant="outline"
                            className={cn(
                                "font-normal",
                                enabled &&
                                    "border-blue-200 bg-blue-50 text-blue-700 dark:border-blue-900/60 dark:bg-blue-950/30 dark:text-blue-300"
                            )}
                        >
                            {enabled ? "自动续期" : "手动"}
                        </Badge>
                    );
                },
            },
            {
                title: "续期状态",
                dataIndex: "renewStats",
                key: "renewStats",
                render: (value: unknown) => {
                    const numeric =
                        typeof value === "number"
                            ? value
                            : typeof value === "string"
                              ? Number(value)
                              : Number.NaN;
                    const stats: CertRenewStats =
                        numeric === 0 || numeric === 1 || numeric === 2 ? (numeric as CertRenewStats) : 0;
                    const meta =
                        {
                            0: {
                                label: "未续期",
                                variant: "secondary" as const,
                                className: "font-normal",
                                Icon: Shield,
                            },
                            1: {
                                label: "续期成功",
                                variant: "default" as const,
                                className:
                                    "bg-emerald-500 hover:bg-emerald-500/90 font-normal",
                                Icon: CheckCircle2,
                            },
                            2: {
                                label: "续期失败",
                                variant: "destructive" as const,
                                className: "font-normal",
                                Icon: AlertTriangle,
                            },
                        }[stats] ?? {
                            label: "未知",
                            variant: "secondary" as const,
                            className: "font-normal",
                            Icon: Shield,
                        };

                    return (
                        <Badge variant={meta.variant} className={cn("gap-1", meta.className)}>
                            <meta.Icon className="h-3.5 w-3.5" />
                            {meta.label}
                        </Badge>
                    );
                },
            },
            {
                title: "续期时间",
                dataIndex: "renewTime",
                key: "renewTime",
                render: (value: unknown) => {
                    const raw =
                        typeof value === "string" || value == null
                            ? (value as string | null | undefined)
                            : String(value);
                    return (
                        <span className="text-muted-foreground">{formatMaybeDate(raw)}</span>
                    );
                },
            },
            {
                title: "证书过期",
                dataIndex: "notAfter",
                key: "notAfter",
                render: (value: unknown) => {
                    const raw =
                        typeof value === "string" || value == null
                            ? (value as string | null | undefined)
                            : String(value);
                    const date = safeParseDate(raw);
                    if (!date) return <span className="text-muted-foreground">-</span>;

                    const daysLeft = differenceInCalendarDays(date, new Date());
                    const isExpired = daysLeft < 0;
                    const isSoon = daysLeft >= 0 && daysLeft <= 15;

                    return (
                        <div className="flex flex-col gap-1">
                            <span className={cn(isExpired && "text-destructive")}>
                                {format(date, "yyyy-MM-dd HH:mm")}
                            </span>
                            <div>
                                <Badge
                                    variant={isExpired ? "destructive" : "outline"}
                                    className={cn(
                                        "font-normal",
                                        !isExpired &&
                                            isSoon &&
                                            "border-amber-200 bg-amber-50 text-amber-800 dark:border-amber-900/60 dark:bg-amber-950/30 dark:text-amber-300"
                                    )}
                                >
                                    {isExpired ? "已过期" : `剩余 ${daysLeft} 天`}
                                </Badge>
                            </div>
                        </div>
                    );
                },
            },
            {
                title: "操作",
                dataIndex: "action",
                key: "action",
                render: (_: unknown, item: CertItem) => {
                    const applying = Boolean(applyingIds[item.id]);
                    return (
                        <div className="flex items-center justify-end gap-2">
                            <Button
                                variant="outline"
                                size="sm"
                                disabled={applying || loading}
                                onClick={() => handleApply(item)}
                            >
                                {applying ? (
                                    <>
                                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                        申请中
                                    </>
                                ) : (
                                    "申请证书"
                                )}
                            </Button>
                            <Button
                                variant="ghost"
                                size="sm"
                                className="text-destructive hover:text-destructive/90"
                                disabled={loading}
                                onClick={() => setDeleteDialog({ open: true, item })}
                            >
                                <Trash2 className="mr-2 h-4 w-4" />
                                删除
                            </Button>
                        </div>
                    );
                },
            },
        ];
    }, [applyingIds, handleApply, loading]);

    const clearFilters = () => {
        setQuery("");
        setRenewFilter("all");
        setAutoRenewOnly(false);
    };

    return (
        <div className="space-y-6 p-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div className="space-y-1">
                    <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">
                        FastGateway
                    </p>
                    <h2 className="text-3xl font-semibold tracking-tight">证书管理</h2>
                    <p className="text-sm text-muted-foreground">
                        维护域名证书配置，支持一键申请与自动续期状态追踪。
                    </p>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={loadData}
                        disabled={loading}
                    >
                        <RefreshCw className="mr-2 h-4 w-4" />
                        刷新列表
                    </Button>
                    <Button size="sm" onClick={() => setCreateVisible(true)}>
                        <Plus className="mr-2 h-4 w-4" />
                        新增证书
                    </Button>
                </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                <Card className="border-border/60">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">
                            总证书
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-1">
                        <div className="text-2xl font-semibold tabular-nums">
                            {pagination.total}
                        </div>
                        <p className="text-xs text-muted-foreground">按分页接口统计总数</p>
                    </CardContent>
                </Card>

                <Card className="border-border/60">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">
                            本页证书
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-1">
                        <div className="text-2xl font-semibold tabular-nums">
                            {stats.pageTotal}
                        </div>
                        <p className="text-xs text-muted-foreground">已加载当前页数据量</p>
                    </CardContent>
                </Card>

                <Card className="border-border/60">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">
                            自动续期
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-1">
                        <div className="text-2xl font-semibold tabular-nums">
                            {stats.autoRenewCount}
                        </div>
                        <p className="text-xs text-muted-foreground">当前页启用自动续期</p>
                    </CardContent>
                </Card>

                <Card className="border-border/60">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">
                            风险提示
                        </CardTitle>
                    </CardHeader>
                    <CardContent className="space-y-1">
                        <div className="flex items-baseline gap-3">
                            <div className="text-2xl font-semibold tabular-nums">
                                {stats.expiringSoon}
                            </div>
                            <Badge
                                variant="outline"
                                className={cn(
                                    "font-normal",
                                    stats.failedCount > 0 &&
                                        "border-destructive/40 text-destructive"
                                )}
                            >
                                续期失败 {stats.failedCount}
                            </Badge>
                        </div>
                        <p className="text-xs text-muted-foreground">
                            15 天内到期（当前页）
                        </p>
                    </CardContent>
                </Card>
            </div>

            <Card className="border-border/60">
                <CardHeader className="space-y-2 pb-4">
                    <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                        <div>
                            <CardTitle className="text-base">证书列表</CardTitle>
                            <CardDescription>
                                搜索与筛选仅作用于当前页数据
                            </CardDescription>
                        </div>
                        <Badge variant="secondary" className="font-normal">
                            {filteredData.length}/{data.length}
                        </Badge>
                    </div>

                    <Accordion type="single" collapsible className="w-full">
                        <AccordionItem value="help" className="border-none">
                            <AccordionHeader className="flex">
                                <AccordionTrigger className="text-sm font-medium text-muted-foreground underline-offset-4 hover:text-foreground hover:underline">
                                    使用说明
                                </AccordionTrigger>
                            </AccordionHeader>
                            <AccordionContent className="pt-2">
                                <div className="rounded-md border bg-muted/30 p-3 text-sm text-muted-foreground">
                                    <ul className="list-disc space-y-1 pl-5">
                                        <li>先新增域名与邮箱配置，再点击“申请证书”触发签发/续期。</li>
                                        <li>建议开启自动续期，表格会显示续期结果与证书到期时间。</li>
                                        <li>表格按到期时间排序，15 天内到期会高亮提示。</li>
                                    </ul>
                                </div>
                            </AccordionContent>
                        </AccordionItem>
                    </Accordion>

                    <div className="flex flex-col gap-3 lg:flex-row lg:items-center lg:justify-between">
                        <div className="flex flex-1 flex-col gap-3 sm:flex-row sm:items-center">
                            <div className="relative flex-1">
                                <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                                <Input
                                    value={query}
                                    onChange={(e) => setQuery(e.target.value)}
                                    placeholder="搜索域名或邮箱（当前页）…"
                                    className="pl-9"
                                />
                            </div>

                            <Highlight
                                controlledItems
                                value={renewFilter}
                                onValueChange={(v) => setRenewFilter(v as RenewFilter)}
                                className="inset-0 bg-background shadow-sm rounded-md pointer-events-none"
                            >
                                <div className="inline-flex h-9 w-full items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground sm:w-auto">
                                    <HighlightItem
                                        value="all"
                                        className="flex-1 sm:flex-none"
                                    >
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                renewFilter === "all"
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            全部
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem
                                        value="none"
                                        className="flex-1 sm:flex-none"
                                    >
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                renewFilter === "none"
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            未续期
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem
                                        value="success"
                                        className="flex-1 sm:flex-none"
                                    >
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                renewFilter === "success"
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            成功
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem
                                        value="failed"
                                        className="flex-1 sm:flex-none"
                                    >
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                renewFilter === "failed"
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            失败
                                        </button>
                                    </HighlightItem>
                                </div>
                            </Highlight>

                            <Highlight
                                controlledItems
                                value={autoRenewOnly ? "auto" : "all"}
                                onValueChange={(v) => setAutoRenewOnly(v === "auto")}
                                className="inset-0 bg-background shadow-sm rounded-md pointer-events-none"
                            >
                                <div className="inline-flex h-9 w-full items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground sm:w-auto">
                                    <HighlightItem value="all" className="flex-1 sm:flex-none">
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                !autoRenewOnly
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            全部策略
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem value="auto" className="flex-1 sm:flex-none">
                                        <button
                                            type="button"
                                            className={cn(
                                                "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                autoRenewOnly
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            自动续期
                                        </button>
                                    </HighlightItem>
                                </div>
                            </Highlight>
                        </div>

                        <div className="flex items-center justify-between gap-2 lg:justify-end">
                            <Highlight
                                controlledItems
                                value={String(pagination.pageSize)}
                                onValueChange={(v) => {
                                    const next = Number(v);
                                    setPagination((prev) => ({
                                        ...prev,
                                        page: 1,
                                        pageSize: Number.isFinite(next)
                                            ? next
                                            : prev.pageSize,
                                    }));
                                }}
                                className="inset-0 bg-background shadow-sm rounded-md pointer-events-none"
                            >
                                <div className="inline-flex h-9 items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground">
                                    <HighlightItem value="10">
                                        <button
                                            type="button"
                                            className={cn(
                                                "rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                pagination.pageSize === 10
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            10/页
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem value="20">
                                        <button
                                            type="button"
                                            className={cn(
                                                "rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                pagination.pageSize === 20
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            20/页
                                        </button>
                                    </HighlightItem>
                                    <HighlightItem value="50">
                                        <button
                                            type="button"
                                            className={cn(
                                                "rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                                pagination.pageSize === 50
                                                    ? "text-foreground"
                                                    : "hover:text-foreground"
                                            )}
                                        >
                                            50/页
                                        </button>
                                    </HighlightItem>
                                </div>
                            </Highlight>

                            {(query || renewFilter !== "all" || autoRenewOnly) && (
                                <Button variant="ghost" size="sm" onClick={clearFilters}>
                                    清除筛选
                                </Button>
                            )}
                        </div>
                    </div>
                </CardHeader>

                <CardContent>
                    <TableList
                        columns={columns}
                        dataSources={filteredData}
                        total={pagination.total}
                        pageSize={pagination.pageSize}
                        current={pagination.page}
                        loading={loading}
                        onPaginationChange={(page: number, pageSize: number) => {
                            setPagination((prev) => ({
                                ...prev,
                                page,
                                pageSize,
                            }));
                        }}
                    />
                </CardContent>
            </Card>

            <CreateCertPage
                visible={createVisible}
                onClose={() => setCreateVisible(false)}
                onOk={() => {
                    setCreateVisible(false);
                    loadData();
                }}
            />

            <Dialog
                open={deleteDialog.open}
                onOpenChange={(open) => {
                    if (!open) setDeleteDialog({ open: false, item: null });
                }}
            >
                <DialogContent className="max-w-md">
                    <DialogHeader>
                        <DialogTitle>确认删除证书？</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-2 text-sm text-muted-foreground">
                        <p>此操作会删除证书配置记录，无法恢复。</p>
                        {deleteDialog.item?.domain ? (
                            <div className="rounded-md border bg-muted/30 px-3 py-2">
                                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                                    域名
                                </div>
                                <div className="mt-1 font-medium text-foreground">
                                    {deleteDialog.item.domain}
                                </div>
                                <div className="mt-1 text-xs text-muted-foreground">
                                    {deleteDialog.item.email}
                                </div>
                            </div>
                        ) : null}
                    </div>
                    <DialogFooter className="gap-2">
                        <Button
                            variant="outline"
                            onClick={() => setDeleteDialog({ open: false, item: null })}
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
}
