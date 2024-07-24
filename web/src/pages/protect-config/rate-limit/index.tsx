import { memo, useEffect, useState } from "react";
import Header from "../features/Header";
import { Button } from 'antd';
import TableList from "../features/Table";
import { GetRateLimit } from "@/services/RateLimitService";
import CreateRateLimitPage from "./feautres/CreateRateLimit";

const RateLimitPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [data, setData] = useState([]);
    const [createVisible, setCreateVisible] = useState(false);

    function loadData() {
        GetRateLimit(input.page, input.pageSize)
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
                action={<Button onClick={() => {
                    setCreateVisible(true);
                }}>
                    新增限流
                </Button>}
                title="限流管理" />
            <TableList
                columns={[
                    {
                        title: '限流策略名称',
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
                        title: '限流端点',
                        dataIndex: 'endpoint',
                        key: 'endpoint',
                    },
                    {
                        title: '限流周期',
                        dataIndex: 'period',
                        key: 'period',
                    },
                    {
                        title: '限流值',
                        dataIndex: 'limit',
                        key: 'limit',
                    },
                    {
                        title: '操作',
                        dataIndex: 'action',
                        key: 'action',
                        render: () => {
                            return (
                                <div>
                                    <Button type="link">编辑</Button>
                                    <Button type="link">删除</Button>
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
            <CreateRateLimitPage visible={createVisible} onClose={() => setCreateVisible(false)} onOk={() => {
                loadData();
                setCreateVisible(false);
            }} />
        </>
    );
});

export default RateLimitPage;