import  { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { message } from '@/utils/toast';
import { getTunnelList,  deleteTunnel } from '@/services/TunnelService';
import { Trash2, RefreshCw, Network, Globe, Server, Eye } from 'lucide-react';
import { TunnelDetailModal } from './components/TunnelDetailModal';

interface TunnelProxy {
    id: string;
    host?: string;
    route: string;
    localRemote: string;
    description: string;
    domains: string[];
    enabled: boolean;
}

interface TunnelNode {
    name: string;
    description: string;
    token: string;
    serverUrl: string;
    reconnectInterval: number;
    heartbeatInterval: number;
    proxyCount: number;
    proxies: TunnelProxy[];
    isOnline: boolean;
    lastConnectTime?: string;
}

const TunnelPage = () => {
    const [tunnels, setTunnels] = useState<TunnelNode[]>([]);
    const [loading, setLoading] = useState(false);
    const [selectedTunnel, setSelectedTunnel] = useState<TunnelNode | null>(null);
    const [detailModalOpen, setDetailModalOpen] = useState(false);

    const loadTunnels = async () => {
        setLoading(true);
        try {
            const response = await getTunnelList();
            setTunnels(response.data || []);
        } catch (error: any) {
            message.error(`加载节点列表失败: ${error.message}`);
        } finally {
            setLoading(false);
        }
    };

    const handleDelete = async (name: string) => {
        try {
            await deleteTunnel(name);
            message.success('删除节点成功');
            loadTunnels();
        } catch (error: any) {
            message.error(`删除节点失败: ${error.message}`);
        }
    };

    const handleShowDetails = (tunnel: TunnelNode) => {
        setSelectedTunnel(tunnel);
        setDetailModalOpen(true);
    };

    useEffect(() => {
        loadTunnels();
    }, []);

    return (
        <div className="p-6 space-y-6">
            {/* 页面头部 */}
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-2xl font-semibold text-foreground">节点管理</h1>
                    <p className="text-muted-foreground mt-1">管理隧道节点和代理配置</p>
                </div>
                <Button
                    onClick={loadTunnels}
                    disabled={loading}
                    className="flex items-center gap-2 bg-primary text-primary-foreground hover:bg-primary/90"
                >
                    <RefreshCw className={`h-4 w-4 ${loading ? 'animate-spin' : ''}`} />
                    刷新
                </Button>
            </div>

            <Separator />

            {/* 节点列表 */}
            {loading ? (
                <div className="flex justify-center items-center h-64">
                    <div className="text-muted-foreground">加载中...</div>
                </div>
            ) : tunnels.length === 0 ? (
                <div className="flex flex-col items-center justify-center h-64 text-muted-foreground">
                    <Network className="h-12 w-12 mb-4 opacity-50" />
                    <p>暂无节点数据</p>
                </div>
            ) : (
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
                    {tunnels.map((tunnel) => (
                        <Card key={tunnel.name} className="hover:shadow-lg transition-shadow bg-card border-border">
                            <CardHeader className="pb-3">
                                <div className="flex items-center justify-between">
                                    <CardTitle className="text-lg font-semibold text-foreground">
                                        {tunnel.name || '未命名节点'}
                                    </CardTitle>
                                    <div className="flex items-center gap-2">
                                        <Button
                                            variant="ghost"
                                            size="sm"
                                            onClick={() => handleDelete(tunnel.name)}
                                            className="text-destructive hover:text-destructive/80 hover:bg-destructive/10"
                                        >
                                            <Trash2 className="h-4 w-4" />
                                        </Button>
                                    </div>
                                </div>
                                <p className="text-sm text-muted-foreground">
                                    {tunnel.description || '暂无描述'}
                                </p>
                            </CardHeader>
                            
                            <CardContent className="pt-0">
                                {/* 基本信息 */}
                                <div className="space-y-2 mb-4">
                                    <div className="flex items-center gap-2 text-sm">
                                        <Server className="h-4 w-4 text-muted-foreground" />
                                        <span className="text-muted-foreground">服务器:</span>
                                        <span className="font-mono text-xs bg-muted px-2 py-1 rounded text-foreground truncate">
                                            {tunnel.serverUrl}
                                        </span>
                                    </div>
                                    <div className="flex items-center gap-2 text-sm">
                                        <Globe className="h-4 w-4 text-muted-foreground" />
                                        <span className="text-muted-foreground">代理数量:</span>
                                        <Badge variant="outline" className="border-border text-foreground">{tunnel.proxyCount}</Badge>
                                    </div>
                                </div>

                                {/* 状态和操作 */}
                                <div className="flex items-center justify-between">
                                    <Badge 
                                        variant={tunnel.isOnline ? "default" : "secondary"}
                                        className={tunnel.isOnline ? "bg-green-500 text-green-50" : "bg-muted text-muted-foreground"}
                                    >
                                        {tunnel.isOnline ? '在线' : '离线'}
                                    </Badge>
                                    <Button
                                        variant="ghost"
                                        size="sm"
                                        onClick={() => handleShowDetails(tunnel)}
                                        className="text-primary hover:text-primary/80"
                                    >
                                        <Eye className="h-4 w-4 mr-1" />
                                        查看详情
                                    </Button>
                                </div>
                            </CardContent>
                        </Card>
                    ))}
                </div>
            )}

            <TunnelDetailModal 
                tunnel={selectedTunnel} 
                open={detailModalOpen} 
                onClose={() => {
                    setDetailModalOpen(false);
                    setSelectedTunnel(null);
                }} 
            />
        </div>
    );
};

export default TunnelPage;