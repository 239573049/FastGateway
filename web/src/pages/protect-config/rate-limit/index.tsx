import { memo, useState } from "react";
import Header from "../features/Header";
import { Button } from 'antd';
import TableList from "../features/Table";

const RateLimitPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });

    return (
        <>
            <Header
                action={<Button>
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
                        title: '错误类型',
                        dataIndex: 'rateLimitContentType',
                        key: 'rateLimitContentType',
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
                dataSources={[]}
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
        </>
    );
});

export default RateLimitPage;