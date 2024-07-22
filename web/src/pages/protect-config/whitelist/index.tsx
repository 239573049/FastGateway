import { memo, useState } from "react";
import Header from "../features/Header";
import { Button } from 'antd';
import TableList from "../features/Table";

const WhiteListPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    return (
        <>
            <Header
                action={<Button>
                    新增白名单
                </Button>}
                title="白名单管理" />

            <TableList
                columns={[
                    {
                        title: '白名单名称',
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

export default WhiteListPage;