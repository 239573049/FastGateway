import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Badge } from "@/components/ui/badge";
import { memo, useEffect, useState, } from "react";
import { toast } from "sonner";
import { useServerStore } from "@/store/server";
import { AlignJustify, Server as ServerIcon, Globe, Shield, Zap, Settings, Play, Square, MoreVertical } from 'lucide-react'
import { useNavigate } from "react-router-dom";
import UpdateServer from "./UpdateServer";

import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Card, CardContent, CardHeader } from "@/components/ui/card";

const ProxyList = memo(() => {
    const { servers, setServers, loadingServers } = useServerStore();
    const navigate = useNavigate();
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateServer, setUpdateServer] = useState<Server | null>(null);

    useEffect(() => {
        loadServers();
    }, [loadingServers]);

    function loadServers() {
        getServers().then((res) => {
            setServers(res.data);
        });
    }

    const render = (item: Server) => {
        return (
            <Card className="group relative overflow-hidden transition-all duration-300 hover:shadow-xl hover:shadow-primary/10 border-border/50 hover:border-primary/30 bg-gradient-to-br from-card to-card/80 backdrop-blur-sm hover:bg-card/90 hover:-translate-y-1">
                
                {/* 状态指示器 */}
                <div className="absolute top-0 left-0 w-full h-1 bg-gradient-to-r from-transparent via-primary/20 to-transparent" />
                <div className={`absolute top-3 right-3 w-3 h-3 rounded-full ${item.onLine ? 'bg-emerald-500 shadow-emerald-500/50' : 'bg-rose-500 shadow-rose-500/50'} shadow-lg`}>
                    {item.onLine && (
                        <div className="absolute inset-0 w-3 h-3 rounded-full bg-emerald-500 animate-ping opacity-75" />
                    )}
                </div>

                <CardHeader className="pb-3">
                    <div className="flex items-start justify-between">
                        <div className="flex items-center gap-3">
                            <div className={`p-2 rounded-lg ${item.onLine ? 'bg-emerald-100 text-emerald-600' : 'bg-muted text-muted-foreground'} transition-colors`}>
                                <ServerIcon className="h-4 w-4" />
                            </div>
                            <div>
                                <h3 className="font-semibold text-foreground leading-tight truncate max-w-[180px]">
                                    {item.name}
                                </h3>
                                <Badge 
                                    variant={item.onLine ? "secondary" : "destructive"}
                                    className={`text-xs mt-1 font-medium transition-colors ${
                                        item.onLine 
                                            ? 'bg-emerald-50 text-emerald-700 border-emerald-200 hover:bg-emerald-100 dark:bg-emerald-900 dark:text-emerald-100' 
                                            : 'bg-rose-50 text-rose-700 border-rose-200 hover:bg-rose-100 dark:bg-rose-900 dark:text-rose-100'
                                    }`}
                                >
                                    {item.onLine ? '在线' : '离线'}
                                </Badge>
                            </div>
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
                                <DropdownMenuItem onClick={() => {
                                    setUpdateServer(item);
                                    setUpdateVisible(true);
                                }} className="flex items-center gap-2">
                                    <Settings className="h-4 w-4" />
                                    编辑
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        enableServer(item.id).then(() => {
                                            loadServers();
                                        })
                                    }}
                                    className="flex items-center gap-2"
                                >
                                    <Shield className="h-4 w-4" />
                                    {item.enable ? '禁用' : '启用'}
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        onlineServer(item.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                    className={`flex items-center gap-2 ${item.onLine ? 'text-rose-600 focus:text-rose-600' : 'text-emerald-600 focus:text-emerald-600'}`}
                                >
                                    {item.onLine ? <Square className="h-4 w-4" /> : <Play className="h-4 w-4" />}
                                    {item.onLine ? '关闭服务' : '启动服务'}
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={() => {
                                    reloadServer(item.id)
                                        .then(() => {
                                            toast.success('刷新成功');
                                        });
                                }} className="flex items-center gap-2">
                                    <Zap className="h-4 w-4" />
                                    刷新路由
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        deleteServer(item.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                    className="text-rose-600 focus:text-rose-600 flex items-center gap-2"
                                >
                                    <AlignJustify className="h-4 w-4" />
                                    删除
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>
                </CardHeader>

                <CardContent className="pt-0 space-y-4">
                    {/* 描述信息 */}
                    <div
                        onClick={() => {
                            navigate(`/server/${item.id}`);
                        }}
                        className="text-sm text-muted-foreground cursor-pointer leading-relaxed hover:text-foreground transition-colors min-h-[40px] line-clamp-2">
                        {item.description || '暂无描述信息'}
                    </div>

                    {/* 端口信息 */}
                    <div className="flex items-center justify-between p-3 rounded-lg bg-gradient-to-r from-muted/50 to-muted/30 border border-border/50">
                        <div className="flex items-center gap-2">
                            <Globe className="h-4 w-4 text-muted-foreground" />
                            <span className="text-sm font-medium text-muted-foreground">
                                监听端口
                            </span>
                        </div>
                        <Badge variant="secondary" className="font-mono font-semibold bg-primary/10 text-primary border-primary/20">
                            :{item.listen}
                        </Badge>
                    </div>

                    {/* 功能标签 */}
                    <div className="flex flex-wrap gap-2.5 mt-1">
                        {item.isHttps && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-emerald-50 border-emerald-200 text-emerald-700 hover:bg-emerald-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                🔒 HTTPS
                            </Badge>
                        )}
                        {item.enableBlacklist && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-red-50 border-red-200 text-red-700 hover:bg-red-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                🚫 黑名单
                            </Badge>
                        )}
                        {item.enableWhitelist && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-blue-50 border-blue-200 text-blue-700 hover:bg-blue-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                ✅ 白名单
                            </Badge>
                        )}
                        {item.enableTunnel && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-orange-50 border-orange-200 text-orange-700 hover:bg-orange-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                🚇 隧道
                            </Badge>
                        )}
                        {item.redirectHttps && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-purple-50 border-purple-200 text-purple-700 hover:bg-purple-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                ↗️ HTTPS重定向
                            </Badge>
                        )}
                        {item.staticCompress && (
                            <Badge variant="outline" className="text-xs px-2.5 py-1 bg-cyan-50 border-cyan-200 text-cyan-700 hover:bg-cyan-100 transition-all duration-200 hover:scale-105 hover:shadow-sm">
                                📦 静态压缩
                            </Badge>
                        )}
                    </div>
                </CardContent>
            </Card>
        );
    }

    return (
        <div className="p-6 bg-background min-h-[calc(100vh-200px)]">
            <div className="max-w-7xl mx-auto">
                {servers.length === 0 ? (
                    <div className="flex flex-col items-center justify-center py-16 px-4 min-h-[400px]">
                        <div className="w-24 h-24 rounded-full bg-gradient-to-br from-muted/40 to-muted/20 flex items-center justify-center mb-6 shadow-lg">
                            <ServerIcon className="h-12 w-12 text-muted-foreground/60" />
                        </div>
                        <h3 className="text-xl font-semibold text-foreground mb-3">暂无服务器配置</h3>
                        <p className="text-muted-foreground text-center mb-8 max-w-md leading-relaxed">
                            开始创建您的第一个代理服务器配置，轻松管理网络流量转发和路由规则
                        </p>
                        <div className="flex flex-col sm:flex-row gap-3">
                            <Button className="gap-2 px-6 py-2.5">
                                <ServerIcon className="h-4 w-4" />
                                创建第一个服务器
                            </Button>
                            <Button variant="outline" className="gap-2 px-6 py-2.5">
                                <Globe className="h-4 w-4" />
                                查看文档
                            </Button>
                        </div>
                    </div>
                ) : (
                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5 gap-6">
                        {servers.map((item) => (
                            <div key={item.id} className="min-w-0">
                                {render(item)}
                            </div>
                        ))}
                    </div>
                )}
            </div>
            <UpdateServer visible={updateVisible} server={updateServer} onClose={() => {
                setUpdateVisible(false);
            }} onOk={() => {
                setUpdateVisible(false);
                loadServers();
            }} />
        </div>
    );
});

export default ProxyList;