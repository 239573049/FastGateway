import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Badge } from "@/components/ui/badge";
import { memo, useEffect, useState } from "react";
import { toast } from "sonner";
import { useServerStore } from "@/store/server";
import { 
    Server as ServerIcon, 
    Globe, 
    Shield, 
    Zap, 
    Settings, 
    Play, 
    Square, 
    MoreVertical,
    Trash2,
} from 'lucide-react';
import { useNavigate } from "react-router-dom";
import UpdateServer from "./UpdateServer";
import { Button } from "@/components/ui/button";
import { 
    DropdownMenu, 
    DropdownMenuContent, 
    DropdownMenuItem, 
    DropdownMenuSeparator,
    DropdownMenuTrigger 
} from "@/components/ui/dropdown-menu";
import { Card, CardContent, CardHeader, } from "@/components/ui/card";

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

    const renderServerCard = (server: Server) => {
        return (
            <Card className="group relative overflow-hidden transition-all duration-200 hover:shadow-md">
                {/* 状态指示器 */}
                <div className={`absolute top-0 left-0 right-0 h-1 ${server.onLine ? 'bg-green-500' : 'bg-muted'}`} />
                
                <CardHeader className="space-y-1 pb-4">
                    <div className="flex items-start justify-between gap-2">
                        <div className="flex items-start gap-3 flex-1 min-w-0">
                            <div className={`rounded-md p-2 ${server.onLine ? 'bg-green-100 text-green-600 dark:bg-green-900/20 dark:text-green-400' : 'bg-muted text-muted-foreground'}`}>
                                <ServerIcon className="h-4 w-4" />
                            </div>
                            <div className="flex-1 min-w-0 space-y-1">
                                <h3 className="font-semibold text-sm leading-none truncate">
                                    {server.name}
                                </h3>
                                <Badge 
                                    variant={server.onLine ? "default" : "secondary"}
                                    className="text-xs h-5"
                                >
                                    {server.onLine ? '在线' : '离线'}
                                </Badge>
                            </div>
                        </div>
                        
                        <DropdownMenu>
                            <DropdownMenuTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="icon"
                                    className="h-8 w-8 opacity-0 group-hover:opacity-100 transition-opacity"
                                >
                                    <MoreVertical className="h-4 w-4" />
                                    <span className="sr-only">打开菜单</span>
                                </Button>
                            </DropdownMenuTrigger>
                            <DropdownMenuContent align="end" className="w-40">
                                <DropdownMenuItem 
                                    onClick={() => {
                                        setUpdateServer(server);
                                        setUpdateVisible(true);
                                    }}
                                >
                                    <Settings className="mr-2 h-4 w-4" />
                                    编辑
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        enableServer(server.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                >
                                    <Shield className="mr-2 h-4 w-4" />
                                    {server.enable ? '禁用' : '启用'}
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem 
                                    onClick={() => {
                                        onlineServer(server.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                >
                                    {server.onLine ? (
                                        <>
                                            <Square className="mr-2 h-4 w-4" />
                                            停止服务
                                        </>
                                    ) : (
                                        <>
                                            <Play className="mr-2 h-4 w-4" />
                                            启动服务
                                        </>
                                    )}
                                </DropdownMenuItem>
                                <DropdownMenuItem 
                                    onClick={() => {
                                        reloadServer(server.id).then(() => {
                                            toast.success('刷新成功');
                                        });
                                    }}
                                >
                                    <Zap className="mr-2 h-4 w-4" />
                                    刷新路由
                                </DropdownMenuItem>
                                <DropdownMenuSeparator />
                                <DropdownMenuItem 
                                    onClick={() => {
                                        deleteServer(server.id).then(() => {
                                            loadServers();
                                        });
                                    }}
                                    className="text-destructive focus:text-destructive"
                                >
                                    <Trash2 className="mr-2 h-4 w-4" />
                                    删除
                                </DropdownMenuItem>
                            </DropdownMenuContent>
                        </DropdownMenu>
                    </div>
                </CardHeader>

                <CardContent className="space-y-4">
                    {/* 描述 */}
                    <div
                        onClick={() => navigate(`/server/${server.id}`)}
                        className="text-sm text-muted-foreground cursor-pointer hover:text-foreground transition-colors line-clamp-2 min-h-[40px]"
                    >
                        {server.description || '暂无描述'}
                    </div>

                    {/* 监听端口 */}
                    <div className="flex items-center justify-between rounded-md border bg-muted/50 px-3 py-2">
                        <div className="flex items-center gap-2 text-sm text-muted-foreground">
                            <Globe className="h-4 w-4" />
                            <span>监听端口</span>
                        </div>
                        <Badge variant="outline" className="font-mono">
                            :{server.listen}
                        </Badge>
                    </div>

                    {/* 功能标签 */}
                    {(server.isHttps || server.enableBlacklist || server.enableWhitelist || 
                      server.enableTunnel || server.redirectHttps || server.staticCompress) && (
                        <div className="flex flex-wrap gap-1.5">
                            {server.isHttps && (
                                <Badge variant="outline" className="text-xs">
                                    HTTPS
                                </Badge>
                            )}
                            {server.enableBlacklist && (
                                <Badge variant="outline" className="text-xs">
                                    黑名单
                                </Badge>
                            )}
                            {server.enableWhitelist && (
                                <Badge variant="outline" className="text-xs">
                                    白名单
                                </Badge>
                            )}
                            {server.enableTunnel && (
                                <Badge variant="outline" className="text-xs">
                                    隧道
                                </Badge>
                            )}
                            {server.redirectHttps && (
                                <Badge variant="outline" className="text-xs">
                                    HTTPS重定向
                                </Badge>
                            )}
                            {server.staticCompress && (
                                <Badge variant="outline" className="text-xs">
                                    静态压缩
                                </Badge>
                            )}
                        </div>
                    )}
                </CardContent>
            </Card>
        );
    };

    return (
        <div className="container mx-auto p-6 space-y-6">
            {servers.length === 0 ? (
                <div className="flex flex-col items-center justify-center min-h-[400px] text-center">
                    <div className="rounded-full bg-muted p-6 mb-4">
                        <ServerIcon className="h-10 w-10 text-muted-foreground" />
                    </div>
                    <h3 className="text-lg font-semibold mb-2">暂无服务器配置</h3>
                    <p className="text-sm text-muted-foreground mb-6 max-w-sm">
                        开始创建您的第一个代理服务器配置
                    </p>
                </div>
            ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
                    {servers.map((server) => (
                        <div key={server.id}>
                            {renderServerCard(server)}
                        </div>
                    ))}
                </div>
            )}
            
            <UpdateServer 
                visible={updateVisible} 
                server={updateServer} 
                onClose={() => setUpdateVisible(false)} 
                onOk={() => {
                    setUpdateVisible(false);
                    loadServers();
                }} 
            />
        </div>
    );
});

export default ProxyList;