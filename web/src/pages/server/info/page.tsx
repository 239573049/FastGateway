import { memo, useCallback, useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { HeartPulse, Loader2, RefreshCw, ShieldAlert, ShieldCheck } from "lucide-react";
import { toast } from "sonner";

import DomainNamesList from "./features/DomainNamesList";
import Header from "./features/Header";
import { get } from "@/utils/fetch";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";

type DestinationHealth = "Unknown" | "Healthy" | "Unhealthy";

type ServerHealthSnapshot = {
    online: boolean;
    supported?: boolean;
    checkedAtUtc?: string;
    clusters?: Array<{
        clusterId: string;
        destinations: Array<{
            destinationId: string;
            address: string;
            health: {
                active: DestinationHealth;
                passive: DestinationHealth;
                effective: DestinationHealth;
            };
        }>;
    }>;
};

const ServerInfoPage = memo(() => {
    const { id } = useParams<{ id: string }>();
    const [health, setHealth] = useState<ServerHealthSnapshot | null>(null);
    const [healthLoading, setHealthLoading] = useState(false);

    const loadHealth = useCallback(() => {
        if (!id) return;

        setHealthLoading(true);
        get(`/api/v1/server/${id}/health`)
            .then((res) => {
                setHealth(res.data ?? null);
            })
            .catch((error) => {
                console.error(error);
                toast.error("获取健康状态失败");
            })
            .finally(() => {
                setHealthLoading(false);
            });
    }, [id]);

    useEffect(() => {
        loadHealth();

        const timer = window.setInterval(() => {
            loadHealth();
        }, 10_000);

        return () => window.clearInterval(timer);
    }, [loadHealth]);

    const summary = useMemo(() => {
        const destinations = (health?.clusters ?? []).flatMap((cluster) => cluster.destinations ?? []);
        const total = destinations.length;

        let healthy = 0;
        let unhealthy = 0;
        let unknown = 0;

        for (const destination of destinations) {
            const effective = destination.health?.effective ?? "Unknown";
            if (effective === "Healthy") healthy += 1;
            else if (effective === "Unhealthy") unhealthy += 1;
            else unknown += 1;
        }

        const unhealthyTargets = destinations
            .filter((destination) => (destination.health?.effective ?? "Unknown") === "Unhealthy")
            .slice(0, 6);

        return { total, healthy, unhealthy, unknown, unhealthyTargets };
    }, [health]);

    return (
        <div className="mx-auto max-w-7xl space-y-6 px-4 py-6 md:px-6 lg:px-10">
            <Header />
            <Card className="border-border/60">
                <CardHeader className="space-y-2">
                    <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                        <div className="flex items-center gap-2">
                            <HeartPulse className="h-4 w-4 text-muted-foreground" />
                            <CardTitle className="text-base">上游健康检查</CardTitle>
                            <Badge variant="secondary" className="font-normal">
                                自动
                            </Badge>
                        </div>

                        <Button
                            size="sm"
                            variant="outline"
                            onClick={loadHealth}
                            disabled={!id || healthLoading}
                            className="shrink-0"
                        >
                            {healthLoading ? (
                                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                            ) : (
                                <RefreshCw className="mr-2 h-4 w-4" />
                            )}
                            刷新
                        </Button>
                    </div>

                    <CardDescription>
                        网关会定期探测集群节点健康，自动屏蔽非健康节点（无可用节点时返回 503）。
                    </CardDescription>
                </CardHeader>

                <CardContent className="space-y-3">
                    {!id ? (
                        <div className="text-sm text-muted-foreground">服务 ID 无效</div>
                    ) : !health ? (
                        <div className="text-sm text-muted-foreground">暂无健康数据</div>
                    ) : !health.online ? (
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <ShieldAlert className="h-4 w-4" />
                            网关未在线
                        </div>
                    ) : (
                        <>
                            <div className="flex flex-wrap items-center gap-2">
                                <Badge
                                    variant="default"
                                    className={cn(
                                        "font-normal",
                                        summary.unhealthy === 0
                                            ? "bg-emerald-500 hover:bg-emerald-500/90"
                                            : "bg-amber-500 hover:bg-amber-500/90"
                                    )}
                                >
                                    {summary.unhealthy === 0 ? (
                                        <ShieldCheck className="mr-1 h-3.5 w-3.5" />
                                    ) : (
                                        <ShieldAlert className="mr-1 h-3.5 w-3.5" />
                                    )}
                                    {summary.unhealthy === 0 ? "健康" : "有异常"}
                                </Badge>
                                <Badge variant="secondary" className="font-normal">
                                    总节点 {summary.total}
                                </Badge>
                                <Badge variant="secondary" className="font-normal">
                                    Healthy {summary.healthy}
                                </Badge>
                                <Badge variant="secondary" className="font-normal">
                                    Unhealthy {summary.unhealthy}
                                </Badge>
                                <Badge variant="secondary" className="font-normal">
                                    Unknown {summary.unknown}
                                </Badge>
                            </div>

                            {summary.unhealthyTargets.length > 0 ? (
                                <div className="space-y-2 rounded-lg border bg-muted/20 px-3 py-2">
                                    <div className="text-xs text-muted-foreground">
                                        异常节点（最多显示 6 条）
                                    </div>
                                    <div className="space-y-1">
                                        {summary.unhealthyTargets.map((destination) => (
                                            <div
                                                key={destination.destinationId}
                                                className="flex flex-wrap items-center justify-between gap-2"
                                            >
                                                <code className="min-w-0 flex-1 truncate rounded bg-muted px-2 py-1 font-mono text-xs text-foreground">
                                                    {destination.address}
                                                </code>
                                                <Badge variant="outline" className="shrink-0 font-normal">
                                                    {destination.health?.active ?? "Unknown"}/
                                                    {destination.health?.passive ?? "Unknown"}
                                                </Badge>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            ) : null}
                        </>
                    )}
                </CardContent>
            </Card>
            <DomainNamesList />
        </div>
    );
});

export default ServerInfoPage;
