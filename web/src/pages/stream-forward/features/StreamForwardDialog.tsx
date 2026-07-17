import { useEffect, useMemo, useState } from "react";
import { Plus, Trash2 } from "lucide-react";
import { toast } from "sonner";

import { createStreamForward, updateStreamForward } from "@/services/StreamForwardService";
import { StreamForward, StreamLoadBalancing, StreamProtocol, StreamUpStream } from "@/types";

import { Button } from "@/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogDescription,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";

interface StreamForwardDialogProps {
    visible: boolean;
    streamForward?: StreamForward | null;
    onClose: () => void;
    onOk: () => void;
}

const createDefault = (): StreamForward => ({
    id: null,
    name: "",
    description: "",
    enable: true,
    protocol: StreamProtocol.Tcp,
    listenPort: 9000,
    listenAddress: "0.0.0.0",
    upStreams: [{ host: "", port: 0, weight: 1 }],
    loadBalancing: StreamLoadBalancing.RoundRobin,
    connectTimeoutMs: 5000,
    idleTimeoutSeconds: 300,
    enableBlacklist: true,
    enableWhitelist: false,
    onLine: false,
    activeConnections: 0,
    udpSessions: 0,
});

export default function StreamForwardDialog({
    visible,
    streamForward,
    onClose,
    onOk,
}: StreamForwardDialogProps) {
    const isEdit = Boolean(streamForward);
    const [tab, setTab] = useState<"basic" | "upstream" | "advanced">("basic");
    const [isSaving, setIsSaving] = useState(false);
    const [value, setValue] = useState<StreamForward>(createDefault());

    const initialValue = useMemo(
        () => (streamForward ? { ...streamForward } : createDefault()),
        [streamForward]
    );

    useEffect(() => {
        if (!visible) return;
        setTab("basic");
        setIsSaving(false);
        setValue({
            ...initialValue,
            upStreams:
                initialValue.upStreams && initialValue.upStreams.length > 0
                    ? initialValue.upStreams.map((u) => ({ ...u }))
                    : [{ host: "", port: 0, weight: 1 }],
        });
    }, [initialValue, visible]);

    const updateUpstream = (index: number, patch: Partial<StreamUpStream>) => {
        setValue((prev) => ({
            ...prev,
            upStreams: prev.upStreams.map((u, i) => (i === index ? { ...u, ...patch } : u)),
        }));
    };

    const addUpstream = () => {
        setValue((prev) => ({
            ...prev,
            upStreams: [...prev.upStreams, { host: "", port: 0, weight: 1 }],
        }));
    };

    const removeUpstream = (index: number) => {
        setValue((prev) => ({
            ...prev,
            upStreams: prev.upStreams.filter((_, i) => i !== index),
        }));
    };

    const save = async () => {
        const name = value.name.trim();
        if (!name) {
            toast.error("规则名称不能为空");
            setTab("basic");
            return;
        }

        if (!value.listenPort || value.listenPort < 1 || value.listenPort > 65535) {
            toast.error("监听端口范围为 1-65535");
            setTab("basic");
            return;
        }

        const upStreams = value.upStreams
            .map((u) => ({ ...u, host: u.host.trim() }))
            .filter((u) => u.host !== "");

        if (upStreams.length === 0) {
            toast.error("至少配置一个上游目标");
            setTab("upstream");
            return;
        }

        for (const u of upStreams) {
            if (!u.port || u.port < 1 || u.port > 65535) {
                toast.error("上游端口范围为 1-65535");
                setTab("upstream");
                return;
            }
        }

        setIsSaving(true);
        try {
            const payload = { ...value, name, upStreams };
            if (isEdit && value.id) {
                await updateStreamForward(value.id, payload);
                toast.success("更新成功");
            } else {
                await createStreamForward(payload);
                toast.success("创建成功");
            }
            onOk();
        } catch {
            toast.error(isEdit ? "更新失败" : "创建失败");
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                <DialogHeader>
                    <DialogTitle>{isEdit ? "编辑端口转发" : "创建端口转发"}</DialogTitle>
                    <DialogDescription>
                        配置 TCP/UDP 端口转发规则，将监听端口的流量裸转发到上游目标。
                    </DialogDescription>
                </DialogHeader>

                <Tabs value={tab} onValueChange={(next) => setTab(next as typeof tab)} className="w-full">
                    <TabsList className="grid w-full grid-cols-3">
                        <TabsTrigger value="basic">基础</TabsTrigger>
                        <TabsTrigger value="upstream">上游</TabsTrigger>
                        <TabsTrigger value="advanced">高级</TabsTrigger>
                    </TabsList>

                    <TabsContent value="basic" className="space-y-4">
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="sf-name">规则名称</Label>
                                <Input
                                    id="sf-name"
                                    value={value.name}
                                    onChange={(e) => setValue((p) => ({ ...p, name: e.target.value }))}
                                    placeholder="例如：SSH 转发"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="sf-protocol">协议</Label>
                                <Select
                                    value={String(value.protocol)}
                                    onValueChange={(v) =>
                                        setValue((p) => ({ ...p, protocol: Number(v) as StreamProtocol }))
                                    }
                                >
                                    <SelectTrigger id="sf-protocol">
                                        <SelectValue />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value={String(StreamProtocol.Tcp)}>TCP</SelectItem>
                                        <SelectItem value={String(StreamProtocol.Udp)}>UDP</SelectItem>
                                        <SelectItem value={String(StreamProtocol.Both)}>TCP + UDP</SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>

                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="sf-listen-address">监听地址</Label>
                                <Input
                                    id="sf-listen-address"
                                    value={value.listenAddress}
                                    onChange={(e) =>
                                        setValue((p) => ({ ...p, listenAddress: e.target.value }))
                                    }
                                    placeholder="0.0.0.0"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="sf-listen-port">监听端口</Label>
                                <Input
                                    id="sf-listen-port"
                                    type="number"
                                    value={value.listenPort}
                                    onChange={(e) =>
                                        setValue((p) => ({ ...p, listenPort: Number(e.target.value) }))
                                    }
                                    placeholder="例如：9000"
                                />
                            </div>
                        </div>

                        <div className="space-y-2">
                            <Label htmlFor="sf-desc">描述</Label>
                            <Textarea
                                id="sf-desc"
                                value={value.description ?? ""}
                                onChange={(e) => setValue((p) => ({ ...p, description: e.target.value }))}
                                placeholder="例如：将 9000 转发到内网数据库"
                                className="min-h-20"
                            />
                        </div>
                    </TabsContent>

                    <TabsContent value="upstream" className="space-y-4">
                        <div className="space-y-2">
                            <Label htmlFor="sf-lb">负载均衡策略</Label>
                            <Select
                                value={String(value.loadBalancing)}
                                onValueChange={(v) =>
                                    setValue((p) => ({
                                        ...p,
                                        loadBalancing: Number(v) as StreamLoadBalancing,
                                    }))
                                }
                            >
                                <SelectTrigger id="sf-lb">
                                    <SelectValue />
                                </SelectTrigger>
                                <SelectContent>
                                    <SelectItem value={String(StreamLoadBalancing.RoundRobin)}>轮询</SelectItem>
                                    <SelectItem value={String(StreamLoadBalancing.LeastConnections)}>
                                        最少连接
                                    </SelectItem>
                                    <SelectItem value={String(StreamLoadBalancing.Random)}>随机</SelectItem>
                                </SelectContent>
                            </Select>
                        </div>

                        <div className="space-y-2">
                            <div className="flex items-center justify-between">
                                <Label>上游目标</Label>
                                <Button type="button" variant="outline" size="sm" onClick={addUpstream}>
                                    <Plus className="mr-2 h-4 w-4" />
                                    添加
                                </Button>
                            </div>

                            <div className="space-y-2">
                                {value.upStreams.map((u, index) => (
                                    <div key={index} className="flex items-center gap-2">
                                        <Input
                                            value={u.host}
                                            onChange={(e) => updateUpstream(index, { host: e.target.value })}
                                            placeholder="地址（IP 或域名）"
                                            className="flex-1"
                                        />
                                        <Input
                                            type="number"
                                            value={u.port || ""}
                                            onChange={(e) =>
                                                updateUpstream(index, { port: Number(e.target.value) })
                                            }
                                            placeholder="端口"
                                            className="w-24"
                                        />
                                        <Input
                                            type="number"
                                            value={u.weight}
                                            onChange={(e) =>
                                                updateUpstream(index, { weight: Number(e.target.value) })
                                            }
                                            placeholder="权重"
                                            className="w-20"
                                        />
                                        <Button
                                            type="button"
                                            variant="ghost"
                                            size="icon"
                                            className="h-9 w-9 shrink-0"
                                            onClick={() => removeUpstream(index)}
                                            disabled={value.upStreams.length <= 1}
                                        >
                                            <Trash2 className="h-4 w-4" />
                                        </Button>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="advanced" className="space-y-4">
                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2">
                                <Label htmlFor="sf-connect-timeout">连接超时(ms)</Label>
                                <Input
                                    id="sf-connect-timeout"
                                    type="number"
                                    value={value.connectTimeoutMs}
                                    onChange={(e) =>
                                        setValue((p) => ({ ...p, connectTimeoutMs: Number(e.target.value) }))
                                    }
                                    placeholder="例如：5000"
                                />
                            </div>
                            <div className="space-y-2">
                                <Label htmlFor="sf-idle-timeout">空闲超时(秒)</Label>
                                <Input
                                    id="sf-idle-timeout"
                                    type="number"
                                    value={value.idleTimeoutSeconds}
                                    onChange={(e) =>
                                        setValue((p) => ({
                                            ...p,
                                            idleTimeoutSeconds: Number(e.target.value),
                                        }))
                                    }
                                    placeholder="例如：300"
                                />
                            </div>
                        </div>

                        <div className="grid gap-3 md:grid-cols-2">
                            <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-3">
                                <div className="space-y-0.5">
                                    <Label htmlFor="sf-enable" className="text-sm font-medium">
                                        启用规则
                                    </Label>
                                    <p className="text-xs text-muted-foreground">随网关启动自动监听。</p>
                                </div>
                                <Switch
                                    id="sf-enable"
                                    checked={value.enable}
                                    onCheckedChange={(next) => setValue((p) => ({ ...p, enable: next }))}
                                />
                            </div>
                            <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-3">
                                <div className="space-y-0.5">
                                    <Label htmlFor="sf-blacklist" className="text-sm font-medium">
                                        启用黑名单
                                    </Label>
                                    <p className="text-xs text-muted-foreground">按全局黑名单拦截来源 IP。</p>
                                </div>
                                <Switch
                                    id="sf-blacklist"
                                    checked={value.enableBlacklist}
                                    onCheckedChange={(next) =>
                                        setValue((p) => ({ ...p, enableBlacklist: next }))
                                    }
                                />
                            </div>
                            <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-3">
                                <div className="space-y-0.5">
                                    <Label htmlFor="sf-whitelist" className="text-sm font-medium">
                                        启用白名单
                                    </Label>
                                    <p className="text-xs text-muted-foreground">
                                        白名单优先，仅放行白名单来源 IP。
                                    </p>
                                </div>
                                <Switch
                                    id="sf-whitelist"
                                    checked={value.enableWhitelist}
                                    onCheckedChange={(next) =>
                                        setValue((p) => ({ ...p, enableWhitelist: next }))
                                    }
                                />
                            </div>
                        </div>
                    </TabsContent>
                </Tabs>

                <DialogFooter>
                    <Button variant="outline" onClick={onClose} disabled={isSaving}>
                        取消
                    </Button>
                    <Button onClick={save} disabled={isSaving}>
                        {isSaving ? "保存中…" : "保存"}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
