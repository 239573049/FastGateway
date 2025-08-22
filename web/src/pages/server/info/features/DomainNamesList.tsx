import { DomainName, ServiceType } from "@/types";
import { memo, useEffect, useState, } from "react";
import { Button, Dropdown, Empty, theme } from 'antd';
import { Flexbox } from 'react-layout-kit';
import { useDomainStore, useRouteStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useParams } from "react-router-dom";
import { Separator } from "@/components/ui/separator";
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
                    className="p-4 rounded-lg h-44 mb-3 cursor-pointer bg-card border border-border shadow-sm transition-shadow hover:shadow-md"
                >
                <div className="text-lg font-semibold text-foreground mb-2">
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
                            className="float-right -mt-1"
                            icon={<AlignJustify size={20} />}
                        />
                    </Dropdown>
                </div>
                <Separator />
                <div className="text-sm w-full p-2 flex-1 select-none overflow-auto text-muted-foreground">
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
                    <div className="flex items-center gap-2 my-4">
                        <Separator className="flex-1" />
                        <span className="text-sm text-muted-foreground px-2">域名</span>
                        <Separator className="flex-1" />
                    </div>
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
            <div className="p-4">
                <span className="text-lg font-semibold text-foreground mr-3">
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
                            className={selectedTags.includes(x) ? "bg-primary text-primary-foreground" : "bg-transparent'}
                            checked={selectedTags.includes(x)}
                            key={i}>{x}</Tag.CheckableTag>)
                    })
                }
            </div>

            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4 p-4">
                {domains.filter(x => selectedTags.length === 0 || x.domains.some((y:any) => selectedTags.includes(y))).map((item) => render(item))}
            </div>
            {
                domains.length === 0 && <div className="flex justify-center items-center h-64"><Empty /></div>
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