import { deleteServer, enableServer, getServers, onlineServer, reloadServer } from "@/services/ServerService";
import { Server } from "@/types";
import { SpotlightCard, Tag } from "@lobehub/ui";
import { memo, useEffect, useState, } from "react";
import { Button, Badge, message, Dropdown, Empty } from 'antd';
import { Flexbox } from 'react-layout-kit';
import { useServerStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useNavigate } from "react-router-dom";
import Divider from "@lobehub/ui/es/Form/components/FormDivider";
import UpdateServer from "./UpdateServer";

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
            <Badge.Ribbon style={{
                backgroundColor: item.onLine ? 'green' : 'red',
            }} text={item.enable ? ('●') : ('●')}>
                <Flexbox
                    key={item.id}
                    style={{
                        padding: '10px',
                        borderRadius: '5px',
                        height: '200px',
                        marginBottom: '10px',
                        cursor: 'pointer',
                    }}
                >
                    <div style={{
                        fontSize: '18px',
                    }}>
                        {item.name}
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
                                style={{
                                    float: 'right',
                                    marginTop: '-5px'
                                }}
                                icon={<AlignJustify size={20} />}
                            />
                        </Dropdown>
                    </div>
                    <Divider />
                    <div
                        onClick={() => {
                            navigate(`/server/${item.id}`);
                        }}
                        style={{
                            fontSize: '14px',
                            color: 'gray',
                            cursor: 'pointer',
                            width: '100%',
                            flex: 1,
                            userSelect: 'none',
                        }}>
                        {item.description}
                    </div>
                    <Divider />
                    <div style={{
                        fontSize: '14px',
                        color: 'gray',
                        width: '100%',
                        userSelect: 'none',
                        marginTop: '10px'
                    }}>
                        端口：
                        <Tag>
                            {item.listen}
                        </Tag>
                        {
                            item.isHttps && (
                                <Tag color='lime'>
                                    HTTPS
                                </Tag>
                            )
                        }
                        {
                            item.enableBlacklist && (<Tag color='geekblue'>黑名单</Tag>)
                        }
                        {
                            item.enableWhitelist && (<Tag color='blue-inverse'>白名单</Tag>)
                        }
                        {
                            item.enableTunnel && (<Tag color='volcano-inverse'>隧道</Tag>)
                        }
                        {
                            item.redirectHttps && (<Tag color='pink-inverse'>重定向HTTPS</Tag>)
                        }
                        {
                            item.staticCompress && (<Tag color='lime'>静态压缩</Tag>)
                        }
                    </div>
                </Flexbox>
            </Badge.Ribbon>
        );
    }

    return (
        <>
            <br />
            <SpotlightCard items={servers} renderItem={render} />
            {
                servers.length === 0 && <Empty />
            }
            <UpdateServer visible={updateVisible} server={updateServer} onClose={() => {
                setUpdateVisible(false);
            }} onOk={() => {
                setUpdateVisible(false);
                loadServers();
            }} />
        </>
    );
});

export default ProxyList;