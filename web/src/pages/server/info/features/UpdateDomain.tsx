import { useEffect, useMemo, useState } from "react";
import { useParams } from "react-router-dom";
import { Plus, X } from "lucide-react";
import { toast } from "sonner";

import { check, checkSrvcie, updateDomain } from "@/services/DomainNameService";
import { useDomainStore } from "@/store/server";
import { DomainName, ServiceType } from "@/types";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
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
import { MultiSelect } from "@/components/ui/multi-select";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

interface UpdateDomainProps {
    visible: boolean;
    domainName: DomainName | null;
    onClose: () => void;
    onOk: () => void;
}

export default function UpdateDomain({
    visible,
    onClose,
    domainName,
    onOk,
}: UpdateDomainProps) {
    const { id } = useParams<{ id: string }>();
    const { setLoadingDomains, loadingDomains } = useDomainStore();

    const fallbackValue = useMemo<DomainName>(
        () => ({
            domains: [],
            serverId: id ?? "",
            serviceType: ServiceType.Service,
            headers: [],
            tryFiles: [],
            enable: true,
            service: "",
            upStreams: [],
            enableHealthCheck: false,
            healthCheckPath: "/health",
            root: "",
            path: "/",
        }),
        [id]
    );

    const [tab, setTab] = useState<"basic" | "target" | "headers">("basic");
    const [isSaving, setIsSaving] = useState(false);
    const [value, setValue] = useState<DomainName>(fallbackValue);

    useEffect(() => {
        if (!visible) return;

        setTab("basic");
        setIsSaving(false);

        if (domainName) {
            setValue({ ...domainName, serverId: id ?? domainName.serverId });
        } else {
            setValue(fallbackValue);
        }
    }, [domainName, fallbackValue, id, visible]);

    useEffect(() => {
        if (!id) return;
        setValue((prev) => ({ ...prev, serverId: id }));
    }, [id]);

    const addUpstream = () => {
        setValue((prev) => ({
            ...prev,
            upStreams: [...prev.upStreams, { service: "", weight: 1 }],
        }));
    };

    const removeUpstream = (index: number) => {
        setValue((prev) => ({
            ...prev,
            upStreams: prev.upStreams.filter((_, i) => i !== index),
        }));
    };

    const updateUpstreamService = (index: number, service: string) => {
        setValue((prev) => ({
            ...prev,
            upStreams: prev.upStreams.map((item, i) =>
                i === index ? { ...item, service } : item
            ),
        }));
    };

    const addHeader = () => {
        setValue((prev) => ({
            ...prev,
            headers: [...prev.headers, { key: "", value: "" }],
        }));
    };

    const removeHeader = (index: number) => {
        setValue((prev) => ({
            ...prev,
            headers: prev.headers.filter((_, i) => i !== index),
        }));
    };

    const updateHeader = (index: number, patch: { key?: string; value?: string }) => {
        setValue((prev) => ({
            ...prev,
            headers: prev.headers.map((item, i) =>
                i === index ? { ...item, ...patch } : item
            ),
        }));
    };

    const save = async () => {
        if (!value.id) {
            toast.error("路由 ID 无效");
            return;
        }

        const path = value.path.trim();
        if (!path) {
            toast.error("匹配路由不能为空");
            setTab("basic");
            return;
        }

        if (!path.startsWith("/")) {
            toast.error("匹配路由需要以 / 开头");
            setTab("basic");
            return;
        }

        if (value.serviceType === ServiceType.Service && !value.service?.trim()) {
            toast.error("服务地址不能为空");
            setTab("target");
            return;
        }

        if (value.serviceType === ServiceType.StaticFile && !value.root?.trim()) {
            toast.error("根目录不能为空");
            setTab("target");
            return;
        }

        if (value.serviceType === ServiceType.ServiceCluster) {
            if (value.upStreams.length === 0) {
                toast.error("请至少添加一个代理节点");
                setTab("target");
                return;
            }
            if (value.upStreams.some((x) => !x.service?.trim())) {
                toast.error("代理节点服务地址不能为空");
                setTab("target");
                return;
            }
        }

        setIsSaving(true);
        try {
            await updateDomain(value.id, { ...value, path });
            toast.success("保存成功");
            onOk();
            setLoadingDomains(!loadingDomains);
        } catch {
            toast.error("保存失败");
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-3xl">
                <DialogHeader>
                    <DialogTitle>编辑路由</DialogTitle>
                    <DialogDescription>
                        修改会立即影响该节点的路由匹配与转发行为。
                    </DialogDescription>
                </DialogHeader>

                <Tabs
                    value={tab}
                    onValueChange={(next) => setTab(next as typeof tab)}
                    className="w-full"
                >
                    <TabsList className="grid w-full grid-cols-3">
                        <TabsTrigger value="basic">基本</TabsTrigger>
                        <TabsTrigger value="target">目标</TabsTrigger>
                        <TabsTrigger value="headers">Headers</TabsTrigger>
                    </TabsList>

                    <TabsContent value="basic" className="space-y-4">
                        <div className="space-y-2">
                            <Label className="text-sm font-medium">域名</Label>
                            <MultiSelect
                                value={value.domains}
                                onChange={(domains) =>
                                    setValue((prev) => ({ ...prev, domains }))
                                }
                                placeholder="请输入域名"
                                className="w-full"
                            />
                        </div>

                        <div className="grid gap-4 md:grid-cols-2">
                            <div className="space-y-2">
                                <Label
                                    htmlFor="update-domain-path"
                                    className="text-sm font-medium"
                                >
                                    匹配路由
                                </Label>
                                <Input
                                    id="update-domain-path"
                                    value={value.path}
                                    onChange={(e) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            path: e.target.value,
                                        }))
                                    }
                                    placeholder="例如：/api"
                                    className="font-mono"
                                />
                            </div>

                            <div className="space-y-2">
                                <Label className="text-sm font-medium">服务类型</Label>
                                <Select
                                    value={value.serviceType.toString()}
                                    onValueChange={(e) =>
                                        setValue((prev) => ({
                                            ...prev,
                                            serviceType: Number(e) as ServiceType,
                                        }))
                                    }
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="选择服务类型" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value={ServiceType.Service.toString()}>
                                            代理单个网络服务
                                        </SelectItem>
                                        <SelectItem
                                            value={ServiceType.ServiceCluster.toString()}
                                        >
                                            代理多个网络服务集群
                                        </SelectItem>
                                        <SelectItem value={ServiceType.StaticFile.toString()}>
                                            静态文件服务代理
                                        </SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="target" className="space-y-4">
                        {value.serviceType === ServiceType.Service && (
                            <Card>
                                <CardHeader>
                                    <CardTitle className="text-base">
                                        代理单个服务
                                    </CardTitle>
                                </CardHeader>
                                <CardContent className="space-y-2">
                                    <Label
                                        htmlFor="update-domain-service"
                                        className="text-sm font-medium"
                                    >
                                        服务地址
                                    </Label>
                                    <div className="flex flex-col gap-2 sm:flex-row">
                                        <Input
                                            id="update-domain-service"
                                            value={value.service ?? ""}
                                            onChange={(e) =>
                                                setValue((prev) => ({
                                                    ...prev,
                                                    service: e.target.value,
                                                }))
                                            }
                                            placeholder="例如：http://127.0.0.1:8080"
                                            className="font-mono"
                                        />
                                        <Button
                                            type="button"
                                            variant="outline"
                                            onClick={() => {
                                                checkSrvcie({ path: value.service }).then(
                                                    (res) => {
                                                        if (res.success) {
                                                            toast.success("服务访问正常");
                                                        } else {
                                                            toast.error(res.message);
                                                        }
                                                    }
                                                );
                                            }}
                                        >
                                            检测
                                        </Button>
                                    </div>
                                </CardContent>
                            </Card>
                        )}

                        {value.serviceType === ServiceType.StaticFile && (
                            <Card>
                                <CardHeader>
                                    <CardTitle className="text-base">
                                        静态文件服务
                                    </CardTitle>
                                </CardHeader>
                                <CardContent className="space-y-4">
                                    <div className="space-y-2">
                                        <Label
                                            htmlFor="update-domain-root"
                                            className="text-sm font-medium"
                                        >
                                            根目录
                                        </Label>
                                        <div className="flex flex-col gap-2 sm:flex-row">
                                            <Input
                                                id="update-domain-root"
                                                value={value.root ?? ""}
                                                onChange={(e) =>
                                                    setValue((prev) => ({
                                                        ...prev,
                                                        root: e.target.value,
                                                    }))
                                                }
                                                placeholder="例如：/var/www/html"
                                                className="font-mono"
                                            />
                                            <Button
                                                type="button"
                                                variant="outline"
                                                onClick={() => {
                                                    check({ path: value.root }).then((res) => {
                                                        if (res.data) {
                                                            toast.success("文件或目录存在");
                                                        } else {
                                                            toast.error("文件或目录不存在");
                                                        }
                                                    });
                                                }}
                                            >
                                                检测
                                            </Button>
                                        </div>
                                    </div>

                                    <div className="space-y-2">
                                        <Label className="text-sm font-medium">TryFiles</Label>
                                        <MultiSelect
                                            value={value.tryFiles}
                                            onChange={(tryFiles) =>
                                                setValue((prev) => ({
                                                    ...prev,
                                                    tryFiles,
                                                }))
                                            }
                                            placeholder="请输入异常时的文件列表"
                                            className="w-full"
                                        />
                                    </div>
                                </CardContent>
                            </Card>
                        )}

                        {value.serviceType === ServiceType.ServiceCluster && (
                            <Card>
                                <CardHeader>
                                    <CardTitle className="text-base">
                                        服务集群节点
                                    </CardTitle>
                                </CardHeader>
                                <CardContent className="space-y-3">
                                    {value.upStreams.map((item, index) => (
                                        <div
                                            key={index}
                                            className="flex flex-col gap-2 sm:flex-row sm:items-end"
                                        >
                                            <div className="flex-1 space-y-2">
                                                <Label
                                                    htmlFor={`update-domain-upstream-${index}`}
                                                    className="text-sm font-medium"
                                                >
                                                    服务地址
                                                </Label>
                                                <Input
                                                    id={`update-domain-upstream-${index}`}
                                                    value={item.service}
                                                    onChange={(e) =>
                                                        updateUpstreamService(
                                                            index,
                                                            e.target.value
                                                        )
                                                    }
                                                    placeholder="例如：http://127.0.0.1:8080"
                                                    className="font-mono"
                                                />
                                            </div>

                                            <div className="flex items-center gap-2">
                                                <Button
                                                    type="button"
                                                    variant="outline"
                                                    onClick={() => {
                                                        checkSrvcie({ path: item.service }).then(
                                                            (res) => {
                                                                if (res.success) {
                                                                    toast.success("服务访问正常");
                                                                } else {
                                                                    toast.error(res.message);
                                                                }
                                                            }
                                                        );
                                                    }}
                                                >
                                                    检测
                                                </Button>
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="icon"
                                                    aria-label="移除节点"
                                                    onClick={() => removeUpstream(index)}
                                                >
                                                    <X className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        </div>
                                    ))}

                                    <Button
                                        type="button"
                                        variant="outline"
                                        onClick={addUpstream}
                                    >
                                        <Plus className="mr-2 h-4 w-4" />
                                        添加代理节点
                                    </Button>
                                </CardContent>
                            </Card>
                        )}
                    </TabsContent>

                    <TabsContent value="headers" className="space-y-4">
                        <Card>
                            <CardHeader>
                                <CardTitle className="text-base">
                                    自定义 Headers
                                </CardTitle>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                {value.headers.length === 0 ? (
                                    <p className="text-sm text-muted-foreground">
                                        暂无自定义 Header。
                                    </p>
                                ) : (
                                    value.headers.map((item, index) => (
                                        <div
                                            key={index}
                                            className="grid gap-2 sm:grid-cols-[1fr_1fr_auto]"
                                        >
                                            <div className="space-y-2">
                                                <Label
                                                    htmlFor={`update-domain-header-key-${index}`}
                                                    className="text-sm font-medium"
                                                >
                                                    Key
                                                </Label>
                                                <Input
                                                    id={`update-domain-header-key-${index}`}
                                                    value={item.key}
                                                    onChange={(e) =>
                                                        updateHeader(index, {
                                                            key: e.target.value,
                                                        })
                                                    }
                                                    placeholder="例如：X-Request-ID"
                                                />
                                            </div>
                                            <div className="space-y-2">
                                                <Label
                                                    htmlFor={`update-domain-header-value-${index}`}
                                                    className="text-sm font-medium"
                                                >
                                                    Value
                                                </Label>
                                                <Input
                                                    id={`update-domain-header-value-${index}`}
                                                    value={item.value}
                                                    onChange={(e) =>
                                                        updateHeader(index, {
                                                            value: e.target.value,
                                                        })
                                                    }
                                                    placeholder="例如：123"
                                                />
                                            </div>
                                            <div className="flex items-end">
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="icon"
                                                    aria-label="移除 Header"
                                                    onClick={() => removeHeader(index)}
                                                >
                                                    <X className="h-4 w-4" />
                                                </Button>
                                            </div>
                                        </div>
                                    ))
                                )}

                                <Button
                                    type="button"
                                    variant="outline"
                                    onClick={addHeader}
                                >
                                    <Plus className="mr-2 h-4 w-4" />
                                    添加 Header
                                </Button>
                            </CardContent>
                        </Card>
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
