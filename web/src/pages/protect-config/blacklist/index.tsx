import { memo, useEffect, useState } from "react";
import Header from "../features/Header";
import { Button } from '@/components/ui/button';
import { message } from '@/utils/toast';
import TableList from "../features/Table";
import CreateBlacklistAndWhitelist from "../features/CreateBlacklistAndWhitelist";
import { DeleteBlacklist, GetBlacklist } from "@/services/BlacklistAndWhitelistService";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Switch } from "@/components/ui/switch";
import { Trash2, Edit, Plus } from "lucide-react";

const BlackListPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [createVisible, setCreateVisible] = useState(false);
    const [data, setData] = useState([]);
    const [loading, setLoading] = useState(false);

    function loadData() {
        setLoading(true);
        GetBlacklist(true, input.page, input.pageSize)
            .then((res) => {
                const result = res.data;
                setData(result.items);
                setInput({
                    ...input,
                    total: result.total
                });
            })
            .finally(() => setLoading(false));
    }

    useEffect(() => {
        loadData();
    }, [input.page, input.pageSize]);

    const handleDelete = (id: string) => {
        DeleteBlacklist(id)
            .then(() => {
                message.success('删除成功');
                loadData();
            })
            .catch(() => {
                message.error('删除失败');
            });
    };

    return (
        <div className="space-y-6 p-6">
            <Card className="border-0 shadow-lg">
                <CardHeader className="border-b bg-gradient-to-r from-destructive/5 to-background">
                    <div className="flex items-center justify-between">
                        <div>
                            <CardTitle className="text-2xl font-bold text-destructive">
                                黑名单管理
                            </CardTitle>
                            <CardDescription className="text-muted-foreground">
                                管理IP黑名单，阻止恶意访问
                            </CardDescription>
                        </div>
                        <Button 
                            onClick={() => setCreateVisible(true)}
                            className="bg-destructive hover:bg-destructive/90"
                        >
                            <Plus className="h-4 w-4 mr-2" />
                            新增黑名单
                        </Button>
                    </div>
                </CardHeader>
                
                <CardContent className="p-6">
                    <TableList
                        columns={[
                            {
                                title: '黑名单名称',
                                dataIndex: 'name',
                                key: 'name',
                                className: 'font-medium',
                            },
                            {
                                title: '状态',
                                dataIndex: 'enable',
                                key: 'enable',
                                render: (text: boolean) => (
                                    <Badge 
                                        variant={text ? "default" : "secondary"}
                                        className={text ? "bg-green-500" : ""}
                                    >
                                        {text ? '已启用' : '已禁用'}
                                    </Badge>
                                )
                            },
                            {
                                title: '描述',
                                dataIndex: 'description',
                                key: 'description',
                                className: 'max-w-xs truncate',
                            },
                            {
                                title: 'IP数量',
                                dataIndex: 'ips',
                                key: 'ips',
                                render: (text: string[]) => (
                                    <Badge variant="outline">
                                        {text?.length || 0} 个IP
                                    </Badge>
                                )
                            },
                            {
                                title: '创建时间',
                                dataIndex: 'createdAt',
                                key: 'createdAt',
                                className: 'text-sm text-muted-foreground',
                            },
                            {
                                title: '操作',
                                dataIndex: 'action',
                                key: 'action',
                                render: (_: any, item: any) => (
                                    <div className="flex items-center gap-2">
                                        <Button 
                                            variant="ghost" 
                                            size="sm"
                                            className="hover:bg-primary/10"
                                        >
                                            <Edit className="h-4 w-4" />
                                        </Button>
                                        <Button 
                                            variant="ghost" 
                                            size="sm"
                                            className="text-destructive hover:text-destructive hover:bg-destructive/10"
                                            onClick={() => handleDelete(item.id)}
                                        >
                                            <Trash2 className="h-4 w-4" />
                                        </Button>
                                    </div>
                                )
                            }
                        ]}
                        dataSources={data}
                        total={input.total}
                        pageSize={input.pageSize}
                        current={input.page}
                        loading={loading}
                        onPaginationChange={(page, pageSize) => {
                            setInput(prev => ({
                                ...prev,
                                page,
                                pageSize
                            }));
                        }}
                    />
                </CardContent>
            </Card>
            
            <CreateBlacklistAndWhitelist
                isBlacklist={true}
                visible={createVisible}
                onClose={() => setCreateVisible(false)}
                onOk={() => {
                    setCreateVisible(false);
                    loadData();
                }}
            />
        </div>
    );
});

export default BlackListPage;