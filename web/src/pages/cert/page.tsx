import { Button } from "@/components/ui/button";
import { message } from "@/utils/toast";
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
        <div className="p-6 space-y-6">
            <div className="flex items-center justify-between">
                <h1 className="text-2xl font-semibold text-foreground">证书管理</h1>
                <Button 
                    onClick={() => setCreateVisible(true)}
                    className="bg-primary text-primary-foreground hover:bg-primary/90"
                >
                    新增证书
                </Button>
            </div>

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
            
            <div className="bg-card rounded-lg shadow-sm border">
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
                            title: '自动续期',
                            dataIndex: 'autoRenew',
                            key: 'autoRenew',
                            render: (text: boolean) => (
                                <span className={text ? 'text-green-600' : 'text-muted-foreground'}>
                                    {text ? '是' : '否'}
                                </span>
                            )
                        },
                        {
                            title: '续期状态',
                            dataIndex: 'renewStats',
                            key: 'renewStats',
                            render: (text: number) => {
                                const statusMap = {
                                    0: { text: '未续期', class: 'text-muted-foreground' },
                                    1: { text: '续期成功', class: 'text-green-600' },
                                    2: { text: '续期失败', class: 'text-red-600' }
                                };
                                const status = statusMap[text as keyof typeof statusMap] || { text: '未知', class: 'text-muted-foreground' };
                                return <span className={status.class}>{status.text}</span>;
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
                                    <div className="flex gap-2">
                                        <Button 
                                            variant="ghost" 
                                            size="sm"
                                            className="text-primary hover:text-primary/80"
                                            onClick={() => {
                                                ApplyCert(item.id)
                                                    .then((res) => {
                                                        if (res.success) {
                                                            message.success('申请成功');
                                                            loadData();
                                                        } else {
                                                            message.error(res.message);
                                                        }
                                                    });
                                            }}
                                        >
                                            申请证书
                                        </Button>
                                        <Button 
                                            variant="ghost" 
                                            size="sm"
                                            className="text-destructive hover:text-destructive/80"
                                            onClick={() => {
                                                DeleteCert(item.id)
                                                    .then(() => {
                                                        message.success('删除成功');
                                                        loadData();
                                                    })
                                            }}
                                        >
                                            删除
                                        </Button>
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
            </div>
        </div>
    )
}