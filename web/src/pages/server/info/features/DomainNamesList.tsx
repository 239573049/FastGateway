import { DomainName, ServiceType } from "@/types";
import { SpotlightCard, Tag } from "@lobehub/ui";
import { memo, useEffect, useState, } from "react";
import { Button, Dropdown, Empty } from 'antd';
import { Flexbox } from 'react-layout-kit';
import { useDomainStore } from "@/store/server";
import { AlignJustify } from 'lucide-react'
import { useParams } from "react-router-dom";
import Divider from "@lobehub/ui/es/Form/components/FormDivider";
import { deleteDomain, enableService, getDomains } from "@/services/DomainNameService";
import UpdateDomain from "./UpdateDomain";

const DomainNamesList = memo(() => {
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateDomain, setUpdateDomain] = useState<DomainName | null>(null);

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
                });
        }
    }
    useEffect(() => {
        loadDomainName();
    }, [loadingDomains, id]);


    const render = (item: DomainName) => {
        return (
            <Flexbox
                key={item.id}
                style={{
                    padding: '10px',
                    borderRadius: '5px',
                    height: '100px',
                    marginBottom: '10px',
                    cursor: 'pointer',
                }}
            >
                <div style={{
                    fontSize: '18px',
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
                </div>
            </Flexbox>
        );
    }

    return (
        <>
            <br />
            <SpotlightCard items={domains} renderItem={render} />
            {
                domains.length === 0 && <Empty />
            }
            <UpdateDomain visible={updateVisible} domainName={updateDomain} onClose={()=>{
                setUpdateVisible(false);
            }} onOk={()=>{
                setUpdateVisible(false);
                loadDomainName();
            }}/>
        </>
    );
});

export default DomainNamesList;