import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Server, Globe, Clock, Network, ExternalLink } from 'lucide-react';
import { format } from 'date-fns';
import { zhCN } from 'date-fns/locale';

interface TunnelDetailModalProps {
  tunnel: any | null;
  open: boolean;
  onClose: () => void;
}

export const TunnelDetailModal = ({ tunnel, open, onClose }: TunnelDetailModalProps) => {
  if (!tunnel) return null;

  const formatDate = (dateString?: string) => {
    if (!dateString) return '从未连接';
    return format(new Date(dateString), 'yyyy-MM-dd HH:mm:ss', { locale: zhCN });
  };

  return (
    <Dialog open={open} onOpenChange={onClose}>
      <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
        <DialogHeader>
          <DialogTitle className="text-lg font-semibold">
            节点详情 - {tunnel.name || '未命名节点'}
          </DialogTitle>
        </DialogHeader>
        
        <div className="space-y-4">
          {/* 基本信息 */}
          <div className="space-y-3">
            <div className="flex items-center justify-between">
              <h3 className="text-base font-medium">基本信息</h3>
              <Badge variant={tunnel.isOnline ? "default" : "secondary"}>
                {tunnel.isOnline ? '在线' : '离线'}
              </Badge>
            </div>
            
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3 text-sm">
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <Server className="h-4 w-4 text-muted-foreground" />
                  <span className="text-muted-foreground">服务器地址:</span>
                </div>
                <span className="font-mono text-xs bg-muted px-2 py-1 rounded">
                  {tunnel.serverUrl}
                </span>
              </div>
              
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <Globe className="h-4 w-4 text-muted-foreground" />
                  <span className="text-muted-foreground">代理数量:</span>
                </div>
                <Badge variant="outline">{tunnel.proxyCount}</Badge>
              </div>
            </div>
            
            {tunnel.description && (
              <div>
                <span className="text-sm text-muted-foreground">描述:</span>
                <p className="text-sm mt-1">{tunnel.description}</p>
              </div>
            )}
          </div>

          <Separator />

          {/* 连接配置 */}
          <div className="space-y-3">
            <h3 className="text-base font-medium">连接配置</h3>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4 text-sm">
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <Clock className="h-4 w-4 text-muted-foreground" />
                  <span className="text-muted-foreground">重连间隔:</span>
                </div>
                <span className="font-mono">{tunnel.reconnectInterval}ms</span>
              </div>
              
              <div className="space-y-2">
                <div className="flex items-center gap-2">
                  <Network className="h-4 w-4 text-muted-foreground" />
                  <span className="text-muted-foreground">心跳间隔:</span>
                </div>
                <span className="font-mono">{tunnel.heartbeatInterval}ms</span>
              </div>
            </div>
            
            <div className="space-y-2">
              <div className="flex items-center gap-2">
                <ExternalLink className="h-4 w-4 text-muted-foreground" />
                <span className="text-muted-foreground">最后连接:</span>
              </div>
              <span className="text-sm">{formatDate(tunnel.lastConnectTime)}</span>
            </div>
          </div>

          <Separator />

          {/* 代理列表 */}
          {tunnel.proxies && tunnel.proxies.length > 0 && (
            <div className="space-y-3">
              <h3 className="text-base font-medium">代理配置 ({tunnel.proxies.length})</h3>
              <div className="space-y-3 max-h-96 overflow-y-auto">
                {tunnel.proxies.map((proxy:any) => (
                  <div key={proxy.id} className="border rounded-lg p-3 space-y-2">
                    <div className="flex items-center justify-between">
                      <span className="font-medium text-sm">{proxy.route}</span>
                      <Badge variant={proxy.enabled ? "default" : "secondary"}>
                        {proxy.enabled ? '启用' : '禁用'}
                      </Badge>
                    </div>
                    
                    <div className="text-xs text-muted-foreground space-y-1">
                      {proxy.host && (
                        <div className="flex items-center gap-1">
                          <span>主机:</span>
                          <span className="font-mono bg-muted px-1 rounded">{proxy.host}</span>
                        </div>
                      )}
                      
                      <div className="flex items-center gap-1">
                        <span>本地服务:</span>
                        <span className="font-mono bg-muted px-1 rounded">{proxy.localRemote}</span>
                      </div>
                      
                      {proxy.description && (
                        <div>
                          <span>描述:</span>
                          <span className="ml-1">{proxy.description}</span>
                        </div>
                      )}
                      
                      {proxy.domains && proxy.domains.length > 0 && (
                        <div>
                          <span>域名:</span>
                          <div className="flex flex-wrap gap-1 mt-1">
                            {proxy.domains.map((domain:any, index:any) => (
                              <Badge key={index} variant="outline" className="text-xs">
                                {domain}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  </div>
                ))}
              </div>
            </div>
          )}
        </div>
      </DialogContent>
    </Dialog>
  );
};