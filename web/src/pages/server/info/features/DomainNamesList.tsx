import { DomainName, ServiceType } from "@/types";
import { memo, useCallback, useEffect, useMemo, useState } from "react";
import { useDomainStore } from "@/store/server";
import {
    AlertTriangle,
    Edit3,
    FileText,
    HelpCircle,
    Layers,
    ListChecks,
    Loader2,
    MoreVertical,
    Power,
    Search,
    SearchX,
    Server as ServerIcon,
    Trash2,
    X,
} from "lucide-react";
import { useParams } from "react-router-dom";
import { deleteDomain, enableService, getDomains } from "@/services/DomainNameService";
import UpdateDomain from "./UpdateDomain";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader } from "@/components/ui/card";
import { Checkbox } from "@/components/ui/checkbox";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import {
    DropdownMenu,
    DropdownMenuContent,
    DropdownMenuItem,
    DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { cn } from "@/lib/utils";
import { toast } from "sonner";

const DomainNamesList = memo(() => {
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateDomain, setUpdateDomain] = useState<DomainName | null>(null);
    const [isLoading, setIsLoading] = useState(false);
    const [query, setQuery] = useState("");
    const [enabledOnly, setEnabledOnly] = useState(false);
    const [selectedTags, setSelectedTags] = useState<string[]>([]);
    const [batchMode, setBatchMode] = useState(false);
    const [selectedIds, setSelectedIds] = useState<string[]>([]);
    const [batchActing, setBatchActing] = useState(false);
    const [deleteDialog, setDeleteDialog] = useState<{
        open: boolean;
        items: DomainName[];
    }>({ open: false, items: [] });
    const [deleting, setDeleting] = useState(false);

    const { id } = useParams<{ id: string }>();
    const { domains: storeDomains, setDomains, loadingDomains } = useDomainStore();
    const domains = storeDomains as DomainName[];

    const loadDomainName = useCallback(() => {
        if (!id) {
            return;
        }

        setIsLoading(true);
        getDomains(id)
            .then((res) => {
                setDomains(res.data);
            })
            .catch(() => {
                toast.error("获取路由列表失败");
            })
            .finally(() => {
                setIsLoading(false);
            });
    }, [id, setDomains]);
    useEffect(() => {
        loadDomainName();
    }, [loadDomainName, loadingDomains]);

    const tags = useMemo(() => {
        const values = domains.flatMap((item) => item.domains ?? []);
        const unique = Array.from(new Set(values));
        unique.sort((a, b) => a.localeCompare(b, "zh-CN"));
        return unique;
    }, [domains]);

    const filteredDomains = useMemo(() => {
        const q = query.trim().toLowerCase();
        return domains
            .filter((item) => {
                if (enabledOnly && !item.enable) return false;
                if (
                    selectedTags.length > 0 &&
                    !item.domains.some((domain) => selectedTags.includes(domain))
                ) {
                    return false;
                }

                if (!q) return true;

                const haystack = [
                    item.path ?? "",
                    item.service ?? "",
                    item.root ?? "",
                    item.domains.join(" "),
                    item.upStreams?.map((x) => x.service).join(" ") ?? "",
                ]
                    .join(" ")
                    .toLowerCase();

                return haystack.includes(q);
            })
            .sort((a, b) => {
                if (a.enable !== b.enable) return a.enable ? -1 : 1;
                return String(a.path).localeCompare(String(b.path), "zh-CN");
            });
    }, [domains, enabledOnly, query, selectedTags]);

    const selectableIds = useMemo(() => {
        return filteredDomains
            .map((item) => item.id)
            .filter((id): id is string => Boolean(id));
    }, [filteredDomains]);

    const selectedIdSet = useMemo(() => new Set(selectedIds), [selectedIds]);

    const selectedDomains = useMemo(() => {
        return domains.filter((item) => item.id && selectedIdSet.has(item.id));
    }, [domains, selectedIdSet]);

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
        setEnabledOnly(false);
        setSelectedTags([]);
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
        targets: DomainName[],
        action: (id: string) => Promise<unknown>
    ) => {
        if (selectedIds.length === 0) {
            toast.error("请先选择路由");
            return;
        }

        const ids = targets.map((item) => item.id).filter((id): id is string => Boolean(id));
        if (ids.length === 0) {
            toast(`没有需要${label}的路由`);
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
        loadDomainName();

        if (failedCount === 0) {
            toast.success(`${label}成功：${successCount} 条`);
        } else {
            toast.error(`${label}：成功 ${successCount} 条，失败 ${failedCount} 条`);
        }
    };

    const handleConfirmDelete = async () => {
        const targets = deleteDialog.items.filter((item) => item.id);
        if (targets.length === 0) {
            toast.error("路由 ID 无效");
            setDeleteDialog({ open: false, items: [] });
            return;
        }

        setDeleting(true);
        let successCount = 0;
        let failedCount = 0;

        for (const item of targets) {
            try {
                await deleteDomain(item.id as string);
                successCount += 1;
            } catch {
                failedCount += 1;
            }
        }

        setDeleting(false);
        setDeleteDialog({ open: false, items: [] });
        const deletedIds = new Set(targets.map((item) => item.id as string));
        setSelectedIds((prev) => prev.filter((id) => !deletedIds.has(id)));
        loadDomainName();

        if (failedCount === 0) {
            toast.success(`已删除 ${successCount} 条路由`);
        } else {
            toast.error(`删除失败 ${failedCount} 条，成功 ${successCount} 条`);
        }
    };


    const getServiceMeta = (type: ServiceType) => {
        switch (type) {
            case ServiceType.Service:
                return {
                    label: "单一服务",
                    Icon: ServerIcon,
                    className:
                        "bg-blue-50 text-blue-700 dark:bg-blue-950/40 dark:text-blue-300",
                };
            case ServiceType.ServiceCluster:
                return {
                    label: "服务集群",
                    Icon: Layers,
                    className:
                        "bg-emerald-50 text-emerald-700 dark:bg-emerald-950/40 dark:text-emerald-300",
                };
            case ServiceType.StaticFile:
                return {
                    label: "静态文件",
                    Icon: FileText,
                    className:
                        "bg-amber-50 text-amber-700 dark:bg-amber-950/40 dark:text-amber-300",
                };
            default:
                return {
                    label: "未知",
                    Icon: HelpCircle,
                    className: "bg-muted text-muted-foreground",
                };
        }
    };

    const renderRow = (item: DomainName) => {
        const serviceMeta = getServiceMeta(item.serviceType);
        const ServiceTypeIcon = serviceMeta.Icon;
        const domainsPreview = item.domains.slice(0, 4);
        const domainsHidden = item.domains.length - domainsPreview.length;
        const selected = Boolean(item.id && selectedIdSet.has(item.id));

        return (
            <div
                key={item.id ?? item.path}
                className={cn(
                    "flex flex-col gap-4 p-4 transition-colors hover:bg-muted/30 sm:flex-row sm:items-start sm:justify-between",
                    !item.enable && "opacity-75"
                )}
            >
                <div className="min-w-0 flex-1 space-y-3">
                    <div className="flex flex-wrap items-center gap-2">
                        {batchMode ? (
                            <Checkbox
                                checked={selected}
                                onCheckedChange={() => {
                                    if (!item.id) return;
                                    toggleSelected(item.id);
                                }}
                                disabled={!item.id || batchActing}
                                aria-label={`选择路由 ${item.path}`}
                            />
                        ) : null}
                        <Badge
                            variant="secondary"
                            className={cn(
                                "h-5 gap-1 px-2 text-xs font-medium",
                                serviceMeta.className
                            )}
                        >
                            <ServiceTypeIcon className="h-3.5 w-3.5" />
                            {serviceMeta.label}
                        </Badge>
                        <Badge
                            variant={item.enable ? "default" : "secondary"}
                            className={cn(
                                "h-5 text-xs",
                                item.enable && "bg-emerald-500 hover:bg-emerald-500/90"
                            )}
                        >
                            {item.enable ? "运行中" : "已禁用"}
                        </Badge>
                        <h3 className="min-w-0 flex-1 truncate font-mono text-base font-semibold text-foreground">
                            {item.path}
                        </h3>
                    </div>

                    <div className="space-y-2 text-sm text-muted-foreground">
                        {item.serviceType === ServiceType.Service ? (
                            <div className="flex min-w-0 flex-wrap items-center gap-2">
                                <span className="shrink-0 text-xs text-muted-foreground">
                                    代理:
                                </span>
                                <code className="min-w-0 flex-1 truncate rounded bg-muted px-2 py-1 font-mono text-xs text-foreground">
                                    {item.service || "-"}
                                </code>
                            </div>
                        ) : null}

                        {item.serviceType === ServiceType.StaticFile ? (
                            <div className="flex min-w-0 flex-wrap items-center gap-2">
                                <span className="shrink-0 text-xs text-muted-foreground">
                                    目录:
                                </span>
                                <code className="min-w-0 flex-1 truncate rounded bg-muted px-2 py-1 font-mono text-xs text-foreground">
                                    {item.root || "-"}
                                </code>
                            </div>
                        ) : null}

                        {item.serviceType === ServiceType.ServiceCluster ? (
                            <div className="space-y-2">
                                <div className="text-xs text-muted-foreground">
                                    集群节点 ({item.upStreams?.length ?? 0})
                                </div>
                                <div className="flex flex-wrap gap-1">
                                    {(item.upStreams ?? []).slice(0, 4).map((x, i) => (
                                        <Badge key={i} variant="outline" className="text-xs">
                                            {x.service}
                                        </Badge>
                                    ))}
                                    {(item.upStreams?.length ?? 0) > 4 ? (
                                        <Badge variant="secondary" className="text-xs">
                                            +{(item.upStreams?.length ?? 0) - 4}
                                        </Badge>
                                    ) : null}
                                </div>
                            </div>
                        ) : null}

                        <div className="flex flex-wrap items-start justify-between gap-2 rounded-lg border bg-muted/20 px-3 py-2">
                            <div className="flex items-center gap-2">
                                <span className="text-xs text-muted-foreground">
                                    域名 ({item.domains.length})
                                </span>
                            </div>
                            <div className="flex flex-wrap justify-end gap-1.5">
                                {domainsPreview.map((x, i) => (
                                    <Badge key={i} variant="outline" className="text-xs">
                                        {x}
                                    </Badge>
                                ))}
                                {domainsHidden > 0 ? (
                                    <Badge variant="secondary" className="text-xs">
                                        +{domainsHidden}
                                    </Badge>
                                ) : null}
                            </div>
                        </div>

                        {item.headers?.length || item.tryFiles?.length ? (
                            <div className="flex flex-wrap items-center gap-2">
                                {(item.headers?.length ?? 0) > 0 ? (
                                    <Badge
                                        variant="outline"
                                        className="h-5 text-xs font-normal"
                                    >
                                        Headers {item.headers.length}
                                    </Badge>
                                ) : null}
                                {(item.tryFiles?.length ?? 0) > 0 ? (
                                    <Badge
                                        variant="outline"
                                        className="h-5 text-xs font-normal"
                                    >
                                        try_files {item.tryFiles.length}
                                    </Badge>
                                ) : null}
                            </div>
                        ) : null}
                    </div>
                </div>

                {batchMode ? null : (
                    <div className="flex flex-wrap items-center gap-2 sm:justify-end">
                        <Button
                            size="sm"
                            variant="ghost"
                            className="h-8"
                            onClick={() => {
                                setUpdateDomain(item);
                                setUpdateVisible(true);
                            }}
                        >
                            <Edit3 className="mr-2 h-4 w-4" />
                            编辑
                        </Button>

                        <Button
                            size="sm"
                            variant="outline"
                            className={cn(
                                "h-8",
                                item.enable && "text-destructive hover:text-destructive"
                            )}
                            onClick={() => {
                                if (!item.id) return;
                                enableService(item.id).then(() => {
                                    loadDomainName();
                                });
                            }}
                            disabled={!item.id}
                        >
                            <Power className="mr-2 h-4 w-4" />
                            {item.enable ? "禁用" : "启用"}
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
                                        if (!item.id) return;
                                        setDeleteDialog({ open: true, items: [item] });
                                    }}
                                    className="text-destructive focus:text-destructive"
                                    disabled={!item.id}
                                >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    删除路由
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
                        <div className="relative flex-1">
                            <Search className="absolute left-3 top-2.5 h-4 w-4 text-muted-foreground" />
                            <Input
                                value={query}
                                onChange={(e) => setQuery(e.target.value)}
                                placeholder="搜索路径、域名或服务地址…"
                                className="pl-9"
                            />
                        </div>

                        <div className="flex w-full items-center gap-3 overflow-x-auto pb-1 sm:w-auto sm:overflow-visible sm:pb-0">
                            <div className="flex items-center gap-2">
                                <Switch
                                    id="routes-enabled-only"
                                    checked={enabledOnly}
                                    onCheckedChange={setEnabledOnly}
                                />
                                <Label
                                    htmlFor="routes-enabled-only"
                                    className="text-sm font-medium text-muted-foreground"
                                >
                                    仅启用
                                </Label>
                            </div>

                            <Badge variant="secondary" className="font-normal">
                                {filteredDomains.length}/{(domains ?? []).length}
                            </Badge>

                            {(query || enabledOnly || selectedTags.length > 0) && (
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
                                    checked={
                                        allSelected ? true : someSelected ? "indeterminate" : false
                                    }
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
                                            selectedDomains.filter((x) => !x.enable),
                                            enableService
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
                                            selectedDomains.filter((x) => x.enable),
                                            enableService
                                        )
                                    }
                                >
                                    禁用
                                </Button>
                                <Button
                                    size="sm"
                                    variant="destructive"
                                    disabled={selectedIds.length === 0 || deleting || batchActing}
                                    onClick={() => {
                                        if (selectedDomains.length === 0) {
                                            toast.error("请先选择路由");
                                            return;
                                        }
                                        setDeleteDialog({ open: true, items: selectedDomains });
                                    }}
                                >
                                    删除
                                </Button>
                            </div>
                        </div>
                    ) : null}

                    {tags.length > 0 && (
                        <div className="flex items-center gap-2 overflow-x-auto pb-1">
                            <Button
                                size="sm"
                                variant={selectedTags.length === 0 ? "default" : "outline"}
                                className="shrink-0"
                                onClick={() => setSelectedTags([])}
                            >
                                全部
                            </Button>
                            {tags.map((tag) => (
                                <Button
                                    key={tag}
                                    variant={selectedTags.includes(tag) ? "default" : "outline"}
                                    size="sm"
                                    className="shrink-0"
                                    onClick={() => {
                                        setSelectedTags((prev) =>
                                            prev.includes(tag)
                                                ? prev.filter((x) => x !== tag)
                                                : [...prev, tag]
                                        );
                                    }}
                                >
                                    {tag}
                                </Button>
                            ))}
                        </div>
                    )}
                </CardHeader>

                <CardContent className="p-0">
                    {isLoading ? (
                        <div className="divide-y">
                            {Array.from({ length: 6 }).map((_, index) => (
                                <div
                                    key={index}
                                    className="flex flex-col gap-4 p-4 sm:flex-row"
                                >
                                    <div className="flex-1 space-y-3">
                                        <div className="flex flex-wrap items-center gap-2">
                                            <Skeleton className="h-5 w-20 rounded-full" />
                                            <Skeleton className="h-5 w-16 rounded-full" />
                                            <Skeleton className="h-5 w-48" />
                                        </div>
                                        <Skeleton className="h-4 w-full max-w-xl" />
                                        <div className="flex flex-wrap gap-2">
                                            <Skeleton className="h-6 w-24 rounded-full" />
                                            <Skeleton className="h-6 w-24 rounded-full" />
                                            <Skeleton className="h-6 w-12 rounded-full" />
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
                    ) : (domains ?? []).length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <AlertTriangle className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <div className="space-y-1">
                                <h3 className="text-lg font-semibold">暂无路由配置</h3>
                                <p className="text-sm text-muted-foreground">
                                    点击右上角“新增路由”开始配置。
                                </p>
                            </div>
                        </div>
                    ) : filteredDomains.length === 0 ? (
                        <div className="flex flex-col items-center justify-center gap-3 px-6 py-16 text-center">
                            <div className="rounded-full bg-muted p-4">
                                <SearchX className="h-8 w-8 text-muted-foreground" />
                            </div>
                            <div className="space-y-1">
                                <h3 className="text-lg font-semibold">没有匹配的路由</h3>
                                <p className="text-sm text-muted-foreground">
                                    尝试更改搜索关键词或清除筛选条件。
                                </p>
                            </div>
                            <Button variant="outline" size="sm" onClick={clearFilters}>
                                清除筛选
                            </Button>
                        </div>
                    ) : (
                        <div className="divide-y">
                            {filteredDomains.map(renderRow)}
                        </div>
                    )}
                </CardContent>
            </Card>

            <UpdateDomain
                visible={updateVisible}
                domainName={updateDomain}
                onClose={() => {
                    setUpdateVisible(false);
                }}
                onOk={() => {
                    setUpdateVisible(false);
                    loadDomainName();
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
                        <DialogTitle>确认删除路由？</DialogTitle>
                    </DialogHeader>
                    <div className="space-y-2 text-sm text-muted-foreground">
                        <p>此操作会删除路由配置记录，无法恢复。</p>
                        {deleteDialog.items.length > 0 ? (
                            <div className="rounded-md border bg-muted/30 px-3 py-2">
                                <div className="text-xs uppercase tracking-wide text-muted-foreground">
                                    待删除
                                </div>
                                <div className="mt-2 space-y-1">
                                    {deleteDialog.items.slice(0, 3).map((item) => (
                                        <div
                                            key={item.id ?? item.path}
                                            className="flex items-start justify-between gap-3"
                                        >
                                            <div className="min-w-0">
                                                <div className="truncate font-mono font-medium text-foreground">
                                                    {item.path}
                                                </div>
                                                <div className="mt-1 flex flex-wrap gap-1.5">
                                                    {item.domains.slice(0, 2).map((d) => (
                                                        <Badge
                                                            key={d}
                                                            variant="outline"
                                                            className="text-xs"
                                                        >
                                                            {d}
                                                        </Badge>
                                                    ))}
                                                    {item.domains.length > 2 ? (
                                                        <Badge
                                                            variant="secondary"
                                                            className="text-xs"
                                                        >
                                                            +{item.domains.length - 2}
                                                        </Badge>
                                                    ) : null}
                                                </div>
                                            </div>
                                            <Badge
                                                variant="outline"
                                                className="shrink-0 font-normal"
                                            >
                                                {item.enable ? "运行中" : "已禁用"}
                                            </Badge>
                                        </div>
                                    ))}
                                    {deleteDialog.items.length > 3 ? (
                                        <div className="text-xs text-muted-foreground">
                                            以及另外 {deleteDialog.items.length - 3} 条…
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

export default DomainNamesList;
