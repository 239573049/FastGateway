import { Button, Dropdown, Notification, Table, Tag } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { ProtectionType } from "../../module";
import CreateBlacklistAntWhitelist from "./features/CreateBlacklistAntWhitelist";
import { DeleteBlacklistAndWhitelist, EnableBlacklistAndWhitelist, GetBlacklistAndWhitelist } from "../../services/ProtectionService";
import { IconMore } from '@douyinfe/semi-icons';

export default function Protection() {
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState([]);
    const [total, setTotal] = useState(0);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });
    const [createVisible, setCreateVisible] = useState(false);
    const columns = [
        {
            title: '名称',
            dataIndex: 'name',
            key: 'name',
        },
        {
            title: '类型',
            dataIndex: 'type',
            key: 'type',
            render: (text: ProtectionType) => {
                switch (text) {
                    case ProtectionType.Blacklist:
                        return '黑名单';
                    case ProtectionType.Whitelist:
                        return '白名单';
                    default:
                        return '未知';
                }
            }
        },
        {
            title: '是否启用',
            dataIndex: 'enable',
            key: 'enable',
            render: (text: boolean) => {
                return text ? <Tag color='green'>启用</Tag> : <Tag color="red">禁用</Tag>;
            }
        },
        {
            title: 'IP列表',
            dataIndex: 'ips',
            key: 'ips',
            render: (text: string[]) => {
                return text.join(',');
            }
        },
        {
            title: '操作',
            key: 'action',
            render: (_text: any, item: any) => (
                <Dropdown
                    render={
                        <Dropdown.Menu>
                            <Dropdown.Item onClick={() => {

                            }}>
                                编辑
                            </Dropdown.Item>
                            <Dropdown.Item onClick={() => {
                                EnableBlacklistAndWhitelist(item.id, !item.enable)
                                    .then(() => {
                                        Notification.success({
                                            title: '操作成功',
                                        });
                                        fetchData();
                                    }).catch(() => {
                                        Notification.error({
                                            title: '操作失败',
                                        });
                                    });
                            }}>
                                {item.enable ? '禁用' : '启用'}
                            </Dropdown.Item>
                            <Dropdown.Item onClick={() => remove(item.id)}>
                                删除
                            </Dropdown.Item>
                        </Dropdown.Menu>
                    }
                ><IconMore />
                </Dropdown>
            ),
        },
    ];

    useEffect(() => {
        fetchData();
    }, [input]);

    function remove(id: string) {
        DeleteBlacklistAndWhitelist(id)
            .then(() => {
                Notification.success({
                    title: '删除成功',
                });
                fetchData();
            });

    }

    function fetchData() {
        setLoading(true);
        GetBlacklistAndWhitelist(input.page, input.pageSize)
            .then((res: any) => {
                setData(res.data.items);
                setTotal(res.data.total);
            })
            .finally(() => {
                setLoading(false);
            });

    }

    return (
        <>
            <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '20px',
                backgroundColor: 'var(--semi-color-bg-0)',
                padding: '5px',
                borderRadius: '10px',
            }}>
                <span style={{
                    color: 'var(--semi-color-text-0)',
                    fontSize: '24px',
                    fontWeight: '600'
                }}>
                    黑白名单防护配置
                </span>
                <Button
                    onClick={() => setCreateVisible(true)}
                    style={{
                        color: 'var(--semi-color-primary)',
                        border: '1px solid var(--semi-color-primary)',
                        borderRadius: '5px',
                    }}
                >新增黑白名单</Button>
            </div>
            <Table loading={loading} columns={columns} dataSource={data} pagination={
                {
                    total,
                    currentPage: input.page,
                    pageSize: input.pageSize,
                    onChange: (page, pageSize) => {
                        setInput({
                            ...input,
                            page,
                            pageSize,
                        });
                    }
                }
            } />
            <CreateBlacklistAntWhitelist visible={createVisible}
                onClose={() => setCreateVisible(false)} onOk={() => {
                    setInput({
                        ...input,
                        page: 1,
                    });
                    setCreateVisible(false);
                }} />
        </>
    );
}