import { memo, useEffect, useState } from "react";
import Header from "../features/Header";
import { Button, message } from 'antd';
import TableList from "../features/Table";
import CreateBlacklistAndWhitelist from "../features/CreateBlacklistAndWhitelist";
import { DeleteBlacklist, GetBlacklist } from "@/services/BlacklistAndWhitelistService";

const BlackListPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [createVisible, setCreateVisible] = useState(false);
    const [data, setData] = useState([]);

    function loadData() {
        GetBlacklist(true, input.page, input.pageSize)
            .then((res) => {
                const result = res.data;
                setData(result.items);
                setInput({
                    ...input,
                    total: result.total
                });
            })
    }

    useEffect(() => {
        loadData();
    }, []);


    return (
        <>
            <Header
                action={<Button onClick={() => setCreateVisible(true)}>
                    新增黑名单
                </Button>}
                title="黑名单管理" />

            <TableList
                columns={[
                    {
                        title: '黑名单名称',
                        dataIndex: 'name',
                        key: 'name',
                    },
                    {
                        title: '是否启用',
                        dataIndex: 'enable',
                        key: 'enable',
                        render: (text: boolean) => {
                            return text ? '是' : '否';
                        }
                    },
                    {
                        title: '描述',
                        dataIndex: 'description',
                        key: 'description'
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
                        dataIndex: 'action',
                        key: 'action',
                        render: (_: any, itme: any) => {
                            return (
                                <div>
                                    <Button style={{
                                        marginRight: '10px',
                                    }}>编辑</Button>
                                    <Button danger onClick={() => {
                                        DeleteBlacklist(itme.id)
                                            .then(() => {
                                                message.success('删除成功');
                                                loadData();
                                            })
                                    }}>删除</Button>
                                </div>
                            );
                        }
                    }
                ]}
                dataSources={data}
                total={input.total}
                pageSize={input.pageSize}
                current={input.page}
                loading={false}
                onPaginationChange={(page, pageSize) => {
                    setInput({
                        ...input,
                        page,
                        pageSize
                    });
                }}
            />
            <CreateBlacklistAndWhitelist
                isBlacklist={true}
                visible={createVisible}
                onClose={() => setCreateVisible(false)}
                onOk={() => {
                    setCreateVisible(false);
                    loadData();
                }}
            />
        </>
    );
});

export default BlackListPage;