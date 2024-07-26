import FHeader from "@/components/FHeader";
import { Button, message } from "antd";
import CreateCertPage from "./features/CreateCert";
import { useEffect, useState } from "react";
import TableList from "./features/TableList";
import { ApplyCert, DeleteCert, GetCert } from "@/services/CertService";


export default function CertPage() {
    const [createVisible, setCreateVisible] = useState(false);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [data, setData] = useState([]);

    function loadData() {
        GetCert(input.page, input.pageSize)
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
    }, [
        input.page,
        input.pageSize
    ]);


    return (
        <>
            <FHeader title="证书管理" action={<Button onClick={() => setCreateVisible(true)}>
                新增证书
            </Button>} />

            <CreateCertPage
                visible={createVisible}
                onClose={() => {
                    setCreateVisible(false);
                }}
                onOk={() => {
                    setCreateVisible(false);
                    loadData();
                }}
            />
            <TableList
                columns={[
                    {
                        title: '域名',
                        dataIndex: 'domain',
                        key: 'domain',
                    },
                    {
                        title: '邮箱',
                        dataIndex: 'email',
                        key: 'email',
                    },
                    {
                        title: '是否自动续期',
                        dataIndex: 'autoRenew',
                        key: 'autoRenew',
                        render: (text: boolean) => {
                            return text ? '是' : '否';
                        }
                    },
                    {
                        title: '续期状态',
                        dataIndex: 'renewStats',
                        key: 'renewStats',
                        render(text: number) {
                            switch (text) {
                                case 0:
                                    return '未续期';
                                case 1:
                                    return '续期成功';
                                case 2:
                                    return '续期失败';
                                default:
                                    return '未知';

                            }
                        }
                    },
                    {
                        title: '续期时间',
                        dataIndex: 'renewTime',
                        key: 'renewTime',
                    },
                    {
                        title: '证书过期时间',
                        dataIndex: 'notAfter',
                        key: 'notAfter',
                    },
                    {
                        title: '操作',
                        dataIndex: 'action',
                        key: 'action',
                        render: (_: any, item: any) => {
                            return (
                                <div>
                                    <Button onClick={() => {
                                        ApplyCert(item.id)
                                            .then((res) => {
                                                if (res.success) {
                                                    message.success('申请成功');
                                                    loadData();
                                                } else {
                                                    message.error(res.message);
                                                }
                                            });
                                    }} type="text">申请证书</Button>
                                    <Button onClick={() => {
                                        DeleteCert(item.id)
                                            .then(() => {
                                                message.success('删除成功');
                                                loadData();
                                            })
                                    }} danger type="text">删除</Button>
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
                onPaginationChange={(page: any, pageSize: any) => {
                    setInput({
                        ...input,
                        page,
                        pageSize
                    });
                }}
            />
        </>
    )
}