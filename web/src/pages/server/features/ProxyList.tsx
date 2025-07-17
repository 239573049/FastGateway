import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Tag } from "@lobehub/ui";
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
            <div style={{
                position: 'relative',
                borderRadius: '12px',
                overflow: 'hidden',
                backgroundColor: colorBgContainer,
                border: `1px solid ${colorBorder}`,
                boxShadow: '0 2px 8px rgba(0, 0, 0, 0.04)',
                transition: 'all 0.3s cubic-bezier(0.25, 0.46, 0.45, 0.94)',
                cursor: 'pointer',
                height: '240px',
            }}
            onMouseEnter={(e) => {
                e.currentTarget.style.boxShadow = '0 8px 32px rgba(0, 0, 0, 0.12)';
                e.currentTarget.style.transform = 'translateY(-2px)';
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.boxShadow = '0 2px 8px rgba(0, 0, 0, 0.04)';
                e.currentTarget.style.transform = 'translateY(0)';
            }}
            >
                {/* 状态指示器 */}
                <div style={{
                    position: 'absolute',
                    top: '0',
                    left: '0',
                    right: '0',
                    height: '4px',
                    backgroundColor: item.onLine ? '#52c41a' : '#ff4d4f',
                    background: item.onLine 
                        ? 'linear-gradient(90deg, #52c41a 0%, #73d13d 100%)'
                        : 'linear-gradient(90deg, #ff4d4f 0%, #ff7875 100%)',
                }} />
                
                {/* 状态徽章 */}
                <div style={{
                    position: 'absolute',
                    top: '16px',
                    right: '16px',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '8px',
                }}>
                    <div style={{
                        width: '8px',
                        height: '8px',
                        borderRadius: '50%',
                        backgroundColor: item.onLine ? '#52c41a' : '#ff4d4f',
                        boxShadow: `0 0 0 2px ${item.onLine ? 'rgba(82, 196, 26, 0.2)' : 'rgba(255, 77, 79, 0.2)'}`,
                    }} />
                    <span style={{
                        fontSize: '12px',
                        fontWeight: '500',
                        color: item.onLine ? '#52c41a' : '#ff4d4f',
                    }}>
                        {item.onLine ? '在线' : '离线'}
                    </span>
                </div>

                <Flexbox
                    key={item.id}
                    style={{
                        padding: '24px',
                        height: '100%',
                        position: 'relative',
                    }}
                >
                    {/* 标题和操作按钮 */}
                    <div style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'flex-start',
                        marginBottom: '16px',
                        marginTop: '8px',
                    }}>
                        <h3 style={{
                            fontSize: '18px',
                            fontWeight: '600',
                            color: colorText,
                            margin: '0',
                            lineHeight: '1.4',
                            wordBreak: 'break-word',
                            maxWidth: '70%',
                        }}>
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
                        style={{
                            fontSize: '14px',
                            color: colorTextSecondary,
                            cursor: 'pointer',
                            lineHeight: '1.5',
                            marginBottom: '20px',
                            minHeight: '42px',
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            userSelect: 'none',
                        }}>
                        {item.description || '暂无描述'}
                    </div>

                    {/* 端口信息 */}
                    <div style={{
                        display: 'flex',
                        alignItems: 'center',
                        marginBottom: '16px',
                        padding: '8px 12px',
                        borderRadius: '8px',
                        backgroundColor: colorBorder.replace('1)', '0.05)'),
                        border: `1px solid ${colorBorder.replace('1)', '0.1)')}`,
                    }}>
                        <span style={{
                            fontSize: '12px',
                            fontWeight: '500',
                            color: colorTextSecondary,
                            marginRight: '8px',
                        }}>
                            端口:
                        </span>
                        <Tag style={{
                            margin: '0',
                            fontWeight: '600',
                            fontSize: '13px',
                        }}>
                            {item.listen}
                        </Tag>
                    </div>

                    {/* 功能标签 */}
                    <div style={{
                        display: 'flex',
                        flexWrap: 'wrap',
                        gap: '6px',
                        marginTop: 'auto',
                    }}>
                        {item.isHttps && (
                            <Tag color='success' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                HTTPS
                            </Tag>
                        )}
                        {item.enableBlacklist && (
                            <Tag color='geekblue' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                黑名单
                            </Tag>
                        )}
                        {item.enableWhitelist && (
                            <Tag color='blue' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                白名单
                            </Tag>
                        )}
                        {item.enableTunnel && (
                            <Tag color='volcano' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                隧道
                            </Tag>
                        )}
                        {item.redirectHttps && (
                            <Tag color='magenta' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                重定向HTTPS
                            </Tag>
                        )}
                        {item.staticCompress && (
                            <Tag color='cyan' style={{
                                borderRadius: '4px',
                                fontSize: '11px',
                                padding: '2px 8px',
                                fontWeight: '500',
                            }}>
                                静态压缩
                            </Tag>
                        )}
                    </div>
                </Flexbox>
            </div>
        );
    }

    return (
        <div style={{
            padding: '24px',
            minHeight: '100vh',
        }}>
            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(360px, 1fr))',
                gap: '24px',
                maxWidth: '1400px',
                margin: '0 auto',
            }}>
                {servers.map((item) => render(item))}
            </div>
            {servers.length === 0 && (
                <div style={{
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    height: '400px',
                }}>
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