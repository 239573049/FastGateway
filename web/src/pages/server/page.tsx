import { memo, useMemo } from "react";
import { Activity, Server as ServerIcon, ShieldCheck, Zap } from "lucide-react";

import { Card, CardContent } from "@/components/ui/card";
import { useServerStore } from "@/store/server";

import Header from "./features/Header";
import ProxyList from "./features/ProxyList";

const ServerPage = memo(() => {
    const { servers } = useServerStore();

    useMemo(() => {
        const total = servers.length;
        const online = servers.filter((server) => server.onLine).length;
        const enabled = servers.filter((server) => server.enable).length;
        const httpsCount = servers.filter((server) => server.isHttps).length;
        const tunnelCount = servers.filter((server) => server.enableTunnel).length;
        const offline = total - online;
        const onlinePercent = total ? Math.round((online / total) * 100) : 0;

        return [
            {
                label: "总节点",
                value: total,
                description: `${enabled} 个已启用`,
                tag: "启用管理",
                Icon: ServerIcon,
            },
            {
                label: "在线节点",
                value: online,
                description: `${onlinePercent}% 在线 · ${offline} 离线`,
                tag: "实时",
                Icon: Activity,
            },
            {
                label: "HTTPS 终端",
                value: httpsCount,
                description: httpsCount ? "TLS 证书已激活" : "尚未配置安全证书",
                tag: "安全",
                Icon: ShieldCheck,
            },
            {
                label: "隧道转发",
                value: tunnelCount,
                description: tunnelCount ? "开放穿透路由" : "未启用隧道",
                tag: "流量",
                Icon: Zap,
            },
        ];
    }, [servers]);

    return (
        <div className="min-h-screen bg-background">
            <div className="mx-auto max-w-7xl space-y-6 px-6 py-6 pb-10">
                <div className="grid gap-5">
                    <Card className="border border-border/60 bg-card/80 shadow-lg shadow-primary/10">
                        <Header />
                        <CardContent className="p-0">
                            <ProxyList />
                        </CardContent>
                    </Card>
                </div>
            </div>
        </div>
    );
});

export default ServerPage;
