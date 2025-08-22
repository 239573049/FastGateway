import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Badge } from "@/components/ui/badge";
import { memo, useEffect, useState, } from "react";
import { toast } from "sonner";
import { useServerStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useNavigate } from "react-router-dom";
import UpdateServer from "./UpdateServer";

import { Button } from "@/components/ui/button";
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from "@/components/ui/dropdown-menu";
import { Card, CardContent } from "@/components/ui/card";

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
            <Card className="relative overflow-hidden h-60">
                
                {/* 状态徽章 */}
                <div className="absolute top-4 right-4 z-10">
                    <Badge 
                        variant={item.onLine ? "secondary" : "destructive"}
                        className={`text-xs font-medium ${item.onLine ? 'bg-emerald-50 text-emerald-700 border-emerald-200' : 'bg-rose-50 text-rose-700 border-rose-200'}`}
                    >
                        <div className={`w-1.5 h-1.5 rounded-full mr-1.5 ${item.onLine ? 'bg-emerald-500' : 'bg-rose-500'}`} />
                        {item.onLine ? '在线' : '离线'}
                    </Badge>
                </div>

                <CardContent className="p-6 h-full flex flex-col relative">
                    {/* 标题和操作按钮 */}
                    <div className="flex justify-between items-start mb-4">
                        <h3 className="text-lg font-semibold text-foreground leading-tight break-words max-w-[calc(100%-40px)]">
                            {item.name}
                        </h3>
                        <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="sm"
                                    className="h-8 w-8 p-0"
                                >
                                    <AlignJustify className="h-4 w-4" />
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end" className="w-48">
                                <DropdownMenuItem onClick={() => {
                                    setUpdateServer(item);
                                    setUpdateVisible(true);
                                }}>
                                    编辑
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        enableServer(item.id).then(() => {
                                            loadServers();
                                        })
                                    }}
                                >
                                    {item.enable ? '禁用' : '启用'}
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        onlineServer(item.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                    className={item.onLine ? 'text-rose-600 focus:text-rose-600' : 'text-emerald-600 focus:text-emerald-600'}
                                >
                                    {item.onLine ? '关闭服务' : '启动服务'}
                                </DropdownMenuItem>
                                <DropdownMenuItem onClick={() => {
                                    reloadServer(item.id)
                                        .then(() => {
                                            toast.success('刷新成功');
                                        });
                                }}>
                                    刷新路由
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        deleteServer(item.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                    className="text-rose-600 focus:text-rose-600"
                                >
                                    删除
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>

                    {/* 描述信息 */}
                    <div
                        onClick={() => {
                            navigate(`/server/${item.id}`);
                        }}
                        className="text-sm text-muted-foreground cursor-pointer leading-relaxed mb-4 min-h-[40px] line-clamp-2">
                        {item.description || '暂无描述'}
                    </div>

                    {/* 端口信息 */}
                    <div className="flex items-center justify-between mb-4 p-3 rounded-lg bg-muted/30 border border-border/30">
                        <span className="text-xs font-medium text-muted-foreground">
                            监听端口
                        </span>
                        <Badge variant="secondary" className="font-mono font-semibold">
                            {item.listen}
                        </Badge>
                    </div>

                    {/* 功能标签 */}
                    <div className="flex flex-wrap gap-1.5 mt-auto">
                        {item.isHttps && (
                            <Badge variant="outline" className="text-xs bg-emerald-50 border-emerald-200 text-emerald-700">
                                HTTPS
                            </Badge>
                        )}
                        {item.enableBlacklist && (
                            <Badge variant="outline" className="text-xs bg-blue-50 border-blue-200 text-blue-700">
                                黑名单
                            </Badge>
                        )}
                        {item.enableWhitelist && (
                            <Badge variant="outline" className="text-xs bg-indigo-50 border-indigo-200 text-indigo-700">
                                白名单
                            </Badge>
                        )}
                        {item.enableTunnel && (
                            <Badge variant="outline" className="text-xs bg-orange-50 border-orange-200 text-orange-700">
                                隧道
                            </Badge>
                        )}
                        {item.redirectHttps && (
                            <Badge variant="outline" className="text-xs bg-purple-50 border-purple-200 text-purple-700">
                                重定向HTTPS
                            </Badge>
                        )}
                        {item.staticCompress && (
                            <Badge variant="outline" className="text-xs bg-cyan-50 border-cyan-200 text-cyan-700">
                                静态压缩
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
                    <Card className="border-2 border-dashed border-muted-foreground/20 h-96">
                        <CardContent className="h-full flex flex-col justify-center items-center text-muted-foreground">
                            <div className="text-center space-y-3">
                                <div className="w-16 h-16 mx-auto rounded-full bg-muted/50 flex items-center justify-center mb-4">
                                    <AlignJustify className="w-8 h-8" />
                                </div>
                                <p className="text-lg font-medium text-foreground">暂无服务器数据</p>
                                <p className="text-sm">请先创建一个服务器开始使用</p>
                            </div>
                        </CardContent>
                    </Card>
                ) : (
                    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 2xl:grid-cols-5 gap-6">
                        {servers.map((item) => render(item))}
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