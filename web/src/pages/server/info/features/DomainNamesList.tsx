import { DomainName, ServiceType } from "@/types";
import { memo, useEffect, useState, } from "react";
import { useDomainStore, } from "@/store/server";
import { MoreVertical, Trash2, Edit3, Power } from 'lucide-react'
import { useParams } from "react-router-dom";
import { Separator } from "@/components/ui/separator";
import { deleteDomain, enableService, getDomains } from "@/services/DomainNameService";
import UpdateDomain from "./UpdateDomain";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { cn } from "@/lib/utils";

const DomainNamesList = memo(() => {
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateDomain, setUpdateDomain] = useState<DomainName | null>(null);
    const [tags, setTags] = useState<string[]>([]);
    const [selectedTags, setSelectedTags] = useState<string[]>([]);

    const { id } = useParams<{ id: string }>();
    const {
        domains,
        setDomains,
        loadingDomains
    } = useDomainStore()

    function loadDomainName() {
        if (id) {
            getDomains(id)
                .then((res) => {
                    setDomains(res.data);
                    tags.splice(0, tags.length);
                    res.data.forEach((item: any) => {
                        tags.push(item.domains);
                    });
                    // tags字符串数组去重
                    let newTags = Array.from(new Set(tags.flat()));

                    setTags([...newTags]);
                });
        }
    }
    useEffect(() => {
        loadDomainName();
    }, [loadingDomains, id]);


    const getServiceIcon = (type: ServiceType) => {
        switch (type) {
            case ServiceType.Service:
                return { icon: '🔵', label: '单一服务', color: 'text-blue-600 bg-blue-50' };
            case ServiceType.ServiceCluster:
                return { icon: '🟢', label: '服务集群', color: 'text-green-600 bg-green-50' };
            case ServiceType.StaticFile:
                return { icon: '🟠', label: '静态文件', color: 'text-orange-600 bg-orange-50' };
            default:
                return { icon: '⚪', label: '未知', color: 'text-gray-600 bg-gray-50' };
        }
    };

    const render = (item: DomainName) => {
        const serviceInfo = getServiceIcon(item.serviceType);
        
        return (
            <Card key={item.id} className={cn(
                "group relative overflow-hidden transition-all duration-300 hover:shadow-lg hover:-translate-y-1",
                !item.enable && "opacity-75"
            )}>
                {/* Status indicator */}
                <div className={cn(
                    "absolute top-0 left-0 w-1 h-full transition-all duration-300",
                    item.enable ? "bg-green-500" : "bg-gray-400"
                )} />
                
                {/* Header */}
                <CardHeader className="pb-3 pt-4 px-4">
                    <div className="flex items-start justify-between">
                        <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2 mb-1">
                                <span className="text-lg">{serviceInfo.icon}</span>
                                <span className={cn(
                                    "text-xs font-medium px-2 py-1 rounded-full",
                                    serviceInfo.color
                                )}>
                                    {serviceInfo.label}
                                </span>
                                <Badge 
                                    variant={item.enable ? "default" : "secondary"}
                                    className={cn(
                                        "text-xs",
                                        item.enable ? "bg-green-500 hover:bg-green-600" : ""
                                    )}
                                >
                                    {item.enable ? '运行中' : '已禁用'}
                                </Badge>
                            </div>
                            <h3 className="font-bold text-lg text-foreground truncate">
                                {item.path}
                            </h3>
                        </div>
                        <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                                <Button 
                                    variant="ghost" 
                                    size="sm" 
                                    className="h-8 w-8 p-0 opacity-0 group-hover:opacity-100 transition-opacity"
                                >
                                    <MoreVertical className="h-4 w-4" />
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end" className="w-48">
                                <DropdownMenuItem 
                                    onClick={() => {
                                        setUpdateDomain(item);
                                        setUpdateVisible(true);
                                    }}
                                    className="cursor-pointer"
                                >
                                    <Edit3 className="mr-2 h-4 w-4" />
                                    编辑配置
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        if (item.id) {
                                            enableService(item.id).then(() => {
                                                loadDomainName();
                                            })
                                        }
                                    }}
                                    className="cursor-pointer"
                                >
                                    <Power className="mr-2 h-4 w-4" />
                                    {item.enable ? '禁用服务' : '启用服务'}
                                </DropdownMenuItem>
                                <Separator className="my-1" />
                                <DropdownMenuItem 
                                    onClick={() => {
                                        if (item.id) {
                                            deleteDomain(item.id).then(() => {
                                                loadDomainName();
                                            });
                                        }
                                    }}
                                    className="text-destructive focus:text-destructive cursor-pointer"
                                >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    删除路由
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>
                </CardHeader>

                <CardContent className="pt-0 px-4 pb-4">
                    {/* Service details */}
                    <div className="space-y-3">
                        {item.serviceType === ServiceType.Service && (
                            <div className="flex items-center gap-2">
                                <span className="text-xs text-muted-foreground">代理:</span>
                                <code className="text-sm bg-muted px-2 py-1 rounded font-mono">
                                    {item.service}
                                </code>
                            </div>
                        )}
                        
                        {item.serviceType === ServiceType.StaticFile && (
                            <div className="flex items-center gap-2">
                                <span className="text-xs text-muted-foreground">目录:</span>
                                <code className="text-sm bg-muted px-2 py-1 rounded font-mono">
                                    {item.root}
                                </code>
                            </div>
                        )}
                        
                        {item.serviceType === ServiceType.ServiceCluster && (
                            <div>
                                <div className="text-xs text-muted-foreground mb-2">集群节点 ({item.upStreams.length})</div>
                                <div className="flex flex-wrap gap-1">
                                    {item.upStreams.slice(0, 3).map((x, i) => (
                                        <Badge key={i} variant="outline" className="text-xs">
                                            {x.service}
                                        </Badge>
                                    ))}
                                    {item.upStreams.length > 3 && (
                                        <Badge variant="secondary" className="text-xs">
                                            +{item.upStreams.length - 3}
                                        </Badge>
                                    )}
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Domains */}
                    <div className="mt-4 pt-3 border-t">
                        <div className="flex items-center justify-between mb-2">
                            <span className="text-xs text-muted-foreground">域名 ({item.domains.length})</span>
                            <div className="flex gap-1">
                                {item.domains.slice(0, 2).map((x, i) => (
                                    <Badge key={i} variant="outline" className="text-xs">
                                        {x}
                                    </Badge>
                                ))}
                                {item.domains.length > 2 && (
                                    <Badge variant="secondary" className="text-xs">
                                        +{item.domains.length - 2}
                                    </Badge>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Quick actions */}
                    <div className="mt-3 flex gap-2 opacity-0 group-hover:opacity-100 transition-opacity">
                        <Button 
                            size="sm" 
                            variant="ghost" 
                            className="h-7 px-2 text-xs"
                            onClick={() => {
                                setUpdateDomain(item);
                                setUpdateVisible(true);
                            }}
                        >
                            <Edit3 className="h-3 w-3 mr-1" />
                            编辑
                        </Button>
                        <Button 
                            size="sm" 
                            variant="ghost" 
                            className={cn(
                                "h-7 px-2 text-xs",
                                item.enable ? "text-orange-600" : "text-green-600"
                            )}
                            onClick={() => {
                                if (item.id) {
                                    enableService(item.id).then(() => {
                                        loadDomainName();
                                    })
                                }
                            }}
                        >
                            <Power className="h-3 w-3 mr-1" />
                            {item.enable ? '禁用' : '启用'}
                        </Button>
                    </div>
                </CardContent>
            </Card>
        );
    }

    return (
        <div className="container mx-auto py-6 px-4">
            {/* Header */}
            <div className="mb-8">
                <div className="flex items-center justify-between mb-6">
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight text-foreground">路由管理</h1>
                        <p className="text-muted-foreground mt-2">管理和配置网关路由规则</p>
                    </div>
                    <div className="text-sm text-muted-foreground">
                        共 {domains.length} 个路由
                    </div>
                </div>

                {/* Filter section */}
                {tags.length > 0 && (
                    <Card className="mb-6 border-0 shadow-sm">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-base font-semibold">域名筛选</CardTitle>
                        </CardHeader>
                        <CardContent className="pt-0">
                            <div className="flex flex-wrap gap-2">
                                <Button
                                    size="sm"
                                    variant={selectedTags.length === 0 ? "default" : "outline"}
                                    onClick={() => setSelectedTags([])}
                                    className="transition-all"
                                >
                                    全部
                                </Button>
                                {tags.map((x, i) => (
                                    <Button
                                        key={i}
                                        variant={selectedTags.includes(x) ? "default" : "outline"}
                                        size="sm"
                                        onClick={() => {
                                            if (selectedTags.includes(x)) {
                                                setSelectedTags(selectedTags.filter(y => y !== x));
                                            } else {
                                                setSelectedTags([...selectedTags, x]);
                                            }
                                        }}
                                        className="transition-all"
                                    >
                                        {x}
                                    </Button>
                                ))}
                            </div>
                        </CardContent>
                    </Card>
                )}
            </div>

            {/* Routes grid */}
            <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-3 2xl:grid-cols-4 gap-6">
                {domains
                    .filter(x => selectedTags.length === 0 || x.domains.some((y:any) => selectedTags.includes(y)))
                    .map((item) => render(item))
                }
            </div>
            
            {/* Empty state */}
            {domains.length === 0 && (
                <div className="flex flex-col items-center justify-center h-96">
                    <div className="text-center">
                        <div className="text-6xl mb-4">🚨</div>
                        <h3 className="text-lg font-semibold mb-2">暂无路由配置</h3>
                        <p className="text-muted-foreground mb-4">当前没有找到任何路由配置，请先创建一个路由。</p>
                        <Alert className="max-w-md">
                            <AlertDescription className="text-sm">
                                点击右上角的“创建路由”按钮开始配置。
                            </AlertDescription>
                        </Alert>
                    </div>
                </div>
            )}

            {/* Filtered empty state */}
            {domains.length > 0 && 
             domains.filter(x => selectedTags.length === 0 || x.domains.some((y:any) => selectedTags.includes(y))).length === 0 && (
                <div className="flex flex-col items-center justify-center h-96">
                    <div className="text-center">
                        <div className="text-6xl mb-4">🔍</div>
                        <h3 className="text-lg font-semibold mb-2">没有匹配的路由</h3>
                        <p className="text-muted-foreground">请更改筛选条件或清除筛选。</p>
                    </div>
                </div>
            )}
            
            <UpdateDomain visible={updateVisible} domainName={updateDomain} onClose={() => {
                setUpdateVisible(false);
            }} onOk={() => {
                setUpdateVisible(false);
                loadDomainName();
            }} />
        </div>
    );
});

export default DomainNamesList;