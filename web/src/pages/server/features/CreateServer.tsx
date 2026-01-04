import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";

import { createServer } from "@/services/ServerService";
import { Server } from "@/types";

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
import { Switch } from "@/components/ui/switch";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Textarea } from "@/components/ui/textarea";
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "@/components/ui/tooltip";


interface CreateServerProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

export default function CreateServer({
    visible,
    onClose,
    onOk
}: CreateServerProps) {
    const initialValue = useMemo<Server>(
        () => ({
            name: "",
            listen: 80,
            description: "",
            enable: true,
            staticCompress: false,
            enableBlacklist: true,
            enableWhitelist: false,
            enableTunnel: false,
            redirectHttps: false,
            id: null,
            isHttps: false,
            onLine: false,
            copyRequestHost: true,
            maxRequestBodySize: null,
            timeout: 900,
        }),
        []
    );

    const [tab, setTab] = useState<"basic" | "limits" | "features">("basic");
    const [isSaving, setIsSaving] = useState(false);
    const [value, setValue] = useState<Server>(initialValue);

    useEffect(() => {
        if (!visible) return;
        setTab("basic");
        setIsSaving(false);
        setValue(initialValue);
    }, [initialValue, visible]);

    const save = async () => {
        const name = value.name.trim();
        if (!name) {
            toast.error("服务名称不能为空");
            setTab("basic");
            return;
        }

        if (!value.listen || value.listen < 0 || value.listen > 65535) {
            toast.error("端口范围为0-65535");
            setTab("basic");
            return;
        }

        if (value.timeout <= 0) {
            toast.error("超时时间必须大于 0");
            setTab("limits");
            return;
        }

        if (value.redirectHttps && !value.isHttps) {
            toast.error("启用 HTTPS 重定向时必须同时启用 HTTPS");
            setTab("features");
            return;
        }

        setIsSaving(true);
        try {
            await createServer({ ...value, name });
            toast.success("创建成功");
            onOk();
        } catch {
            toast.error("创建失败");
        } finally {
            setIsSaving(false);
        }
    };

    const FieldLabel = ({
        htmlFor,
        label,
        tooltip,
    }: {
        htmlFor: string;
        label: string;
        tooltip?: string;
    }) => {
        if (!tooltip) {
            return (
                <Label htmlFor={htmlFor} className="text-sm font-medium">
                    {label}
                </Label>
            );
        }

        return (
            <Tooltip>
                <TooltipTrigger asChild>
                    <Label
                        htmlFor={htmlFor}
                        className="cursor-help text-sm font-medium underline decoration-dotted underline-offset-4"
                    >
                        {label}
                    </Label>
                </TooltipTrigger>
                <TooltipContent>
                    <p className="max-w-[320px] leading-relaxed">{tooltip}</p>
                </TooltipContent>
            </Tooltip>
        );
    };

    const SettingSwitch = ({
        id,
        label,
        description,
        checked,
        onCheckedChange,
    }: {
        id: string;
        label: string;
        description: string;
        checked: boolean;
        onCheckedChange: (next: boolean) => void;
    }) => (
        <div className="flex items-start justify-between gap-4 rounded-lg border bg-muted/30 p-3">
            <div className="space-y-0.5">
                <Label htmlFor={id} className="text-sm font-medium">
                    {label}
                </Label>
                <p className="text-xs text-muted-foreground">{description}</p>
            </div>
            <Switch id={id} checked={checked} onCheckedChange={onCheckedChange} />
        </div>
    );

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-2xl">
                <TooltipProvider>
                    <DialogHeader>
                        <DialogTitle>创建服务</DialogTitle>
                        <DialogDescription>
                            新建一个代理服务节点，并配置基础参数与转发能力。
                        </DialogDescription>
                    </DialogHeader>

                    <Tabs
                        value={tab}
                        onValueChange={(next) => setTab(next as typeof tab)}
                        className="w-full"
                    >
                        <TabsList className="grid w-full grid-cols-3">
                            <TabsTrigger value="basic">基础</TabsTrigger>
                            <TabsTrigger value="limits">限制</TabsTrigger>
                            <TabsTrigger value="features">功能</TabsTrigger>
                        </TabsList>

                        <TabsContent value="basic" className="space-y-4">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <FieldLabel
                                        htmlFor="create-server-listen"
                                        label="服务端口"
                                        tooltip="设置代理服务监听的端口。"
                                    />
                                    <Input
                                        id="create-server-listen"
                                        type="number"
                                        value={value.listen}
                                        onChange={(e) =>
                                            setValue((prev) => ({
                                                ...prev,
                                                listen: Number(e.target.value),
                                            }))
                                        }
                                        placeholder="例如：80"
                                    />
                                </div>

                                <div className="space-y-2">
                                    <FieldLabel
                                        htmlFor="create-server-name"
                                        label="服务名称"
                                        tooltip="用于区分不同服务节点的展示名称。"
                                    />
                                    <Input
                                        id="create-server-name"
                                        value={value.name}
                                        onChange={(e) =>
                                            setValue((prev) => ({
                                                ...prev,
                                                name: e.target.value,
                                            }))
                                        }
                                        placeholder="例如：API 网关"
                                    />
                                </div>
                            </div>

                            <div className="space-y-2">
                                <FieldLabel
                                    htmlFor="create-server-desc"
                                    label="服务描述"
                                    tooltip="用于备注用途与上下文，便于团队协作。"
                                />
                                <Textarea
                                    id="create-server-desc"
                                    value={value.description ?? ""}
                                    onChange={(e) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            description: e.target.value,
                                        }))
                                    }
                                    placeholder="例如：对外提供 /api 转发，支持 HTTPS 重定向"
                                    className="min-h-20"
                                />
                            </div>
                        </TabsContent>

                        <TabsContent value="limits" className="space-y-4">
                            <div className="grid gap-4 md:grid-cols-2">
                                <div className="space-y-2">
                                    <FieldLabel
                                        htmlFor="create-server-max-body"
                                        label="最大请求体大小(字节)"
                                        tooltip="最大请求体大小限制（字节）。留空表示不限制。"
                                    />
                                    <Input
                                        id="create-server-max-body"
                                        type="number"
                                        value={value.maxRequestBodySize ?? ""}
                                        onChange={(e) => {
                                            const next =
                                                e.target.value.trim() === ""
                                                    ? null
                                                    : Number(e.target.value);
                                            setValue((prev) => ({
                                                ...prev,
                                                maxRequestBodySize: next,
                                            }));
                                        }}
                                        placeholder="留空则不限制"
                                    />
                                </div>

                                <div className="space-y-2">
                                    <FieldLabel
                                        htmlFor="create-server-timeout"
                                        label="超时时间(秒)"
                                        tooltip="请求超时时间（秒）。默认 900 秒（15 分钟）。"
                                    />
                                    <Input
                                        id="create-server-timeout"
                                        type="number"
                                        value={value.timeout}
                                        onChange={(e) =>
                                            setValue((prev) => ({
                                                ...prev,
                                                timeout: Number(e.target.value),
                                            }))
                                        }
                                        placeholder="例如：900"
                                    />
                                </div>
                            </div>
                        </TabsContent>

                        <TabsContent value="features" className="space-y-4">
                            <div className="grid gap-3 md:grid-cols-2">
                                <SettingSwitch
                                    id="create-server-enable"
                                    label="启动服务"
                                    description="是否立即启用当前创建的服务，或跟随网关启动。"
                                    checked={value.enable}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({ ...prev, enable: next }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-static-compress"
                                    label="静态压缩"
                                    description="对静态代理内容启用压缩以降低带宽消耗。"
                                    checked={value.staticCompress}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            staticCompress: next,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-blacklist"
                                    label="启用黑名单"
                                    description="使用全局黑名单策略进行拦截。"
                                    checked={value.enableBlacklist}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            enableBlacklist: next,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-whitelist"
                                    label="启用白名单"
                                    description="使用全局白名单策略进行过滤。"
                                    checked={value.enableWhitelist}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            enableWhitelist: next,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-tunnel"
                                    label="启用隧道"
                                    description="启用内网穿透隧道能力。"
                                    checked={value.enableTunnel}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            enableTunnel: next,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-copy-host"
                                    label="复制请求 Host"
                                    description="将访问域名复制到下游请求 Host，用于保持原始域名。"
                                    checked={value.copyRequestHost}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            copyRequestHost: next,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-https"
                                    label="启用 HTTPS"
                                    description="为当前服务启用 HTTPS 监听（与证书配置相关）。"
                                    checked={value.isHttps}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            isHttps: next,
                                            redirectHttps: next ? prev.redirectHttps : false,
                                        }))
                                    }
                                />
                                <SettingSwitch
                                    id="create-server-redirect"
                                    label="重定向到 HTTPS"
                                    description="当服务为 HTTP 时，自动将请求重定向到 443 的 HTTPS。"
                                    checked={value.redirectHttps}
                                    onCheckedChange={(next) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            redirectHttps: next,
                                            isHttps: next ? true : prev.isHttps,
                                        }))
                                    }
                                />
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
                </TooltipProvider>
            </DialogContent>
        </Dialog>
    );
}
