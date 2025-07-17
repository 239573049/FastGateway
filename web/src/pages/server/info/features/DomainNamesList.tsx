import { DomainName, ServiceType } from "@/types";
import { memo, useEffect, useState, } from "react";
import { Button, Dropdown, Empty, theme } from 'antd';
import { Flexbox } from 'react-layout-kit';
import { useDomainStore, useRouteStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useParams } from "react-router-dom";
import Divider from "@lobehub/ui/es/Form/components/FormDivider";
import { deleteDomain, enableService, getDomains } from "@/services/DomainNameService";
import UpdateDomain from "./UpdateDomain";

import { Tag } from 'antd';

const DomainNamesList = memo(() => {
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateDomain, setUpdateDomain] = useState<DomainName | null>(null);
    const [loading] = useRouteStore((state) => [state.loading]);
    const [tags, setTags] = useState<string[]>([]);
    const [selectedTags, setSelectedTags] = useState<string[]>([]);

    const {
        token: { colorBgContainer, colorBorder, colorText, colorTextSecondary },
    } = theme.useToken();

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


    const render = (item: DomainName) => {
        return (                <Flexbox
                    key={item.id}
                    style={{
                        padding: '16px',
                        borderRadius: '8px',
                        height: '180px',
                        marginBottom: '10px',
                        cursor: 'pointer',
                        backgroundColor: colorBgContainer,
                        border: `1px solid ${colorBorder}`,
                        boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)',
                        transition: 'box-shadow 0.2s ease',
                    }}
                    onMouseEnter={(e) => {
                        e.currentTarget.style.boxShadow = '0 4px 8px rgba(0, 0, 0, 0.15)';
                    }}
                    onMouseLeave={(e) => {
                        e.currentTarget.style.boxShadow = '0 2px 4px rgba(0, 0, 0, 0.1)';
                    }}
                >
                <div style={{
                    fontSize: '18px',
                    color: colorText,
                }}>
                    路由：
                    {item.path}
                    <Dropdown
                        menu={{
                            items: [
                                {
                                    key: 'edit',
                                    label: '编辑',
                                    onClick: () => {
                                        setUpdateDomain(item);
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
                                        if (item.id) {
                                            deleteDomain(item.id).then(() => {
                                                loadDomainName();
                                            });
                                        }
                                    }
                                },
                                {
                                    key: 'start',
                                    label: item.enable ? '禁用' : '启用',
                                    onClick: () => {
                                        if (item.id) {
                                            enableService(item.id).then(() => {
                                                loadDomainName();
                                            })
                                        }
                                    }
                                }
                            ]
                        }}
                    >
                        <Button
                            loading={loading}
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
                    style={{
                        fontSize: '14px',
                        width: '100%',
                        padding: '10px',
                        flex: 1,
                        userSelect: 'none',
                        overflow: 'auto',
                        color: colorTextSecondary,
                    }}>
                    {
                        item.serviceType === ServiceType.Service && ('代理服务: ' + item.service)
                    }
                    {
                        item.serviceType === ServiceType.StaticFile && ('静态文件代理目录: ' + item.root)
                    }
                    {
                        item.serviceType === ServiceType.ServiceCluster && ('服务集群: ')
                    }
                    {
                        item.serviceType === ServiceType.ServiceCluster && item.upStreams.map((x, i) => {
                            return (<Tag key={i}>{x.service}</Tag>)
                        })
                    }
                    <Divider>
                        域名
                    </Divider>
                    {
                        item.domains.map((x, i) => {
                            return (<Tag key={i}>{x}</Tag>)
                        })
                    }
                </div>
            </Flexbox>
        );
    }

    return (
        <>
            <div style={{
                padding: '10px',
            }}>
                <span style={{
                    fontSize: '18px',
                    fontWeight: 'bold',
                    marginTop: '10px',
                    marginBottom: '10px',
                    marginRight: '10px',
                    flex: 1,
                }}>
                    域名过滤
                </span>
                {
                    tags.map((x, i) => {
                        return (<Tag.CheckableTag
                            onChange={(checked) => {
                                if (checked) {
                                    setSelectedTags([...selectedTags, x]);
                                } else {
                                    setSelectedTags(selectedTags.filter(y => y !== x));
                                }
                            }}
                            style={{
                                background: selectedTags.includes(x) ? '#1890ff' : 'transparent',
                            }}
                            checked={selectedTags.includes(x)}
                            key={i}>{x}</Tag.CheckableTag>)
                    })
                }
            </div>

            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fill, minmax(400px, 1fr))',
                gap: '16px',
                padding: '16px'
            }}>
                {domains.filter(x => selectedTags.length === 0 || x.domains.some((y:any) => selectedTags.includes(y))).map((item) => render(item))}
            </div>
            {
                domains.length === 0 && <Empty />
            }
            <UpdateDomain visible={updateVisible} domainName={updateDomain} onClose={() => {
                setUpdateVisible(false);
            }} onOk={() => {
                setUpdateVisible(false);
                loadDomainName();
            }} />
        </>
    );
});

export default DomainNamesList;