import { memo, useMemo, useState } from "react";
import {
    Activity,
    Plus,
    RefreshCw,
    Server as ServerIcon,
    ShieldCheck,
    Zap,
} from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useServerStore } from "@/store/server";

import CreateServer from "./CreateServer";

type StatItem = {
    key: string;
    label: string;
    value: number;
    description: string;
    tag: string;
    Icon: typeof ServerIcon;
};

const Header = memo(() => {
    const [createVisible, setCreateVisible] = useState(false);
    const { servers, setLoadingServers, loadingServers } = useServerStore();

    const stats = useMemo<StatItem[]>(() => {
        const total = servers.length;
        const online = servers.filter((server) => server.onLine).length;
        const enabled = servers.filter((server) => server.enable).length;
        const httpsCount = servers.filter((server) => server.isHttps).length;
        const tunnelCount = servers.filter((server) => server.enableTunnel).length;
        const offline = total - online;
        const onlinePercent = total ? Math.round((online / total) * 100) : 0;

        return [
            {
                key: "total",
                label: "总节点",
                value: total,
                description: `${enabled} 个已启用`,
                tag: "配置",
                Icon: ServerIcon,
            },
            {
                key: "online",
                label: "在线节点",
                value: online,
                description: `${onlinePercent}% 在线 · ${offline} 离线`,
                tag: "实时",
                Icon: Activity,
            },
            {
                key: "https",
                label: "HTTPS 终端",
                value: httpsCount,
                description: httpsCount ? "TLS 证书已配置" : "未配置 TLS 证书",
                tag: "安全",
                Icon: ShieldCheck,
            },
            {
                key: "tunnel",
                label: "隧道转发",
                value: tunnelCount,
                description: tunnelCount ? "已启用穿透路由" : "未启用隧道",
                tag: "流量",
                Icon: Zap,
            },
        ];
    }, [servers]);

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div className="space-y-1">
                    <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">
                        FastGateway
                    </p>
                    <h2 className="text-3xl font-semibold tracking-tight">服务管理</h2>
                    <p className="text-sm text-muted-foreground">
                        创建、启停并监控您的代理节点
                    </p>
                </div>

                <div className="flex w-full items-center gap-2 overflow-x-auto pb-1 md:w-auto md:overflow-visible">
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setLoadingServers(!loadingServers)}
                        aria-label="刷新列表"
                        className="shrink-0"
                    >
                        <RefreshCw className="mr-2 h-4 w-4" />
                        刷新列表
                    </Button>
                    <Button
                        size="sm"
                        onClick={() => setCreateVisible(true)}
                        aria-label="新增服务"
                        className="shrink-0"
                    >
                        <Plus className="mr-2 h-4 w-4" />
                        新增服务
                    </Button>
                </div>
            </div>

            <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
                {stats.map(({ key, label, value, description, tag, Icon }) => (
                    <Card key={key} className="border-border/60">
                        <CardHeader className="pb-2">
                            <div className="flex items-center justify-between gap-3">
                                <CardTitle className="text-sm font-medium text-muted-foreground">
                                    {label}
                                </CardTitle>
                                <Icon className="h-4 w-4 text-muted-foreground" />
                            </div>
                        </CardHeader>
                        <CardContent className="space-y-1">
                            <div className="flex items-baseline justify-between gap-2">
                                <div className="text-2xl font-semibold tabular-nums">
                                    {value}
                                </div>
                                <Badge variant="outline" className="font-normal">
                                    {tag}
                                </Badge>
                            </div>
                            <p className="text-xs text-muted-foreground">{description}</p>
                        </CardContent>
                    </Card>
                ))}
            </div>

            <CreateServer
                visible={createVisible}
                onClose={() => {
                    setCreateVisible(false);
                }}
                onOk={() => {
                    setCreateVisible(false);
                    setLoadingServers(!loadingServers);
                }}
            />
        </div>
    );
});

export default Header;
