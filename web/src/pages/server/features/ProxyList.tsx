import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Badge } from "@/components/ui/badge";
import { memo, useEffect, useState, } from "react";
import { Button, message, Dropdown, Empty, theme } from 'antd';
import { Flexbox } from 'react-layout-kit';
import { useServerStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useNavigate } from "react-router-dom";
import UpdateServer from "./UpdateServer";

const ProxyList = memo(() => {
    const { servers, setServers, loadingServers } = useServerStore();
    const navigate = useNavigate();
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateServer, setUpdateServer] = useState<Server | null>(null);
    
    const {
        token: { colorBgContainer, colorBorder, colorText, colorTextSecondary },
    } = theme.useToken();

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
            <div className="relative rounded-xl overflow-hidden bg-card border border-border shadow-sm transition-all duration-300 cursor-pointer h-60 hover:shadow-lg hover:-translate-y-1 group">
                {/* 状态指示器 */}
                <div className={`absolute top-0 left-0 right-0 h-1 ${item.onLine ? 'bg-green-500' : 'bg-red-500'}`} />
                
                {/* 状态徽章 */}
                <div className="absolute top-4 right-4 flex items-center gap-2">
                    <div className={`w-2 h-2 rounded-full ${item.onLine ? 'bg-green-500' : 'bg-red-500'} ring-2 ${item.onLine ? 'ring-green-500/20' : 'ring-red-500/20'}`} />
                    <span className={`text-xs font-medium ${item.onLine ? 'text-green-500' : 'text-red-500'}`}>
                        {item.onLine ? '在线' : '离线'}
                    </span>
                </div>

                <Flexbox
                    key={item.id}
                    className="p-6 h-full relative"
                >
                    {/* 标题和操作按钮 */}
                    <div className="flex justify-between items-start mb-4 mt-2">
                        <h3 className="text-lg font-semibold text-foreground m-0 leading-tight break-words max-w-[70%]">
                            {item.name}
                        </h3>
                        <Dropdown
                            menu={{
                                items: [
                                    {
                                        key: 'edit',
                                        label: '编辑',
                                        onClick: () => {
                                            setUpdateServer(item);
                                            setUpdateVisible(true);
                                        }
                                    },
                                    {
                                        key: 'delete',
                                        label: '删除',
                                        style: {
                                            color: 'red'
                                        },
                                        onClick: () => {
                                            deleteServer(item.id).then(() => {
                                                loadServers();
                                            });
                                        }
                                    },
                                    {
                                        key: 'start',
                                        label: item.enable ? '禁用' : '启用',
                                        onClick: () => {
                                            enableServer(item.id).then(() => {
                                                loadServers();
                                            })
                                        }
                                    },
                                    {
                                        key: "server",
                                        label: item.onLine ? '关闭服务' : '启动服务',
                                        style: {
                                            color: item.onLine ? 'red' : 'green'
                                        },
                                        onClick: () => {
                                            onlineServer(item.id).then(() => {
                                                loadServers();
                                            });
                                        }
                                    },
                                    {
                                        key: 'reload',
                                        label: '刷新路由',
                                        onClick: () => {
                                            reloadServer(item.id)
                                                .then(() => {
                                                    message.success('刷新成功');
                                                });
                                        }
                                    }
                                ]
                            }}
                        >
                            <Button
                                type="text"
                                size="small"
                                style={{
                                    padding: '4px 8px',
                                    borderRadius: '6px',
                                    color: colorTextSecondary,
                                }}
                                icon={<AlignJustify size={16} />}
                            />
                        </Dropdown>
                    </div>

                    {/* 描述信息 */}
                    <div
                        onClick={() => {
                            navigate(`/server/${item.id}`);
                        }}
                        className="text-sm text-muted-foreground cursor-pointer leading-relaxed mb-5 min-h-[42px] line-clamp-2 select-none">
                        {item.description || '暂无描述'}
                    </div>

                    {/* 端口信息 */}
                    <div className="flex items-center mb-4 px-3 py-2 rounded-lg bg-muted/50 border border-border/20">
                        <span className="text-xs font-medium text-muted-foreground mr-2">
                            端口:
                        </span>
                        <Badge variant="secondary" className="font-semibold text-sm">
                            {item.listen}
                        </Badge>
                    </div>

                    {/* 功能标签 */}
                    <div className="flex flex-wrap gap-2 mt-auto">
                        {item.isHttps && (
                            <Badge variant="outline" className="text-xs border-green-200 text-green-700 hover:bg-green-50">
                                HTTPS
                            </Badge>
                        )}
                        {item.enableBlacklist && (
                            <Badge variant="outline" className="text-xs border-blue-200 text-blue-700 hover:bg-blue-50">
                                黑名单
                            </Badge>
                        )}
                        {item.enableWhitelist && (
                            <Badge variant="outline" className="text-xs border-indigo-200 text-indigo-700 hover:bg-indigo-50">
                                白名单
                            </Badge>
                        )}
                        {item.enableTunnel && (
                            <Badge variant="outline" className="text-xs border-orange-200 text-orange-700 hover:bg-orange-50">
                                隧道
                            </Badge>
                        )}
                        {item.redirectHttps && (
                            <Badge variant="outline" className="text-xs border-purple-200 text-purple-700 hover:bg-purple-50">
                                重定向HTTPS
                            </Badge>
                        )}
                        {item.staticCompress && (
                            <Badge variant="outline" className="text-xs border-cyan-200 text-cyan-700 hover:bg-cyan-50">
                                静态压缩
                            </Badge>
                        )}
                    </div>
                </Flexbox>
            </div>
        );
    }

    return (
        <div className="p-6 min-h-screen">
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 max-w-7xl mx-auto">
                {servers.map((item) => render(item))}
            </div>
            {servers.length === 0 && (
                <div className="flex justify-center items-center h-96">
                    <Empty description="暂无服务器数据" />
                </div>
            )}
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