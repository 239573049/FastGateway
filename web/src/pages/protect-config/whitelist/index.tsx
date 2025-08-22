import { memo, useEffect, useState } from "react";
import { Button } from '@/components/ui/button';
import { message } from '@/utils/toast';
import CreateBlacklistAndWhitelist from "../features/CreateBlacklistAndWhitelist";
import { DeleteBlacklist, GetBlacklist } from "@/services/BlacklistAndWhitelistService";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { Trash2, Edit, Plus } from "lucide-react";
import { ColumnDef } from "@tanstack/react-table";
import { cn } from "@/lib/utils";

const WhiteListPage = memo(() => {
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
        GetBlacklist(false, input.page, input.pageSize)
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

    const columns: ColumnDef<any>[] = [
        {
            accessorKey: 'name',
            header: '白名单名称',
            cell: ({ row }) => (
                <div className="font-medium">{row.getValue('name')}</div>
            ),
        },
        {
            accessorKey: 'enable',
            header: '状态',
            cell: ({ row }) => {
                const enabled = row.getValue('enable') as boolean
                return (
                    <Badge 
                        variant={enabled ? "default" : "secondary"}
                        className={cn(enabled ? "bg-green-500 hover:bg-green-600" : "")}
                    >
                        {enabled ? '已启用' : '已禁用'}
                    </Badge>
                )
            },
        },
        {
            accessorKey: 'description',
            header: '描述',
            cell: ({ row }) => (
                <div className="max-w-xs truncate">{row.getValue('description')}</div>
            ),
        },
        {
            accessorKey: 'ips',
            header: 'IP数量',
            cell: ({ row }) => {
                const ips = row.getValue('ips') as string[]
                return (
                    <Badge variant="outline" className="border-green-200 text-green-700">
                        {ips?.length || 0} 个IP
                    </Badge>
                )
            },
        },
        {
            accessorKey: 'createdAt',
            header: '创建时间',
            cell: ({ row }) => (
                <div className="text-sm text-muted-foreground">{row.getValue('createdAt')}</div>
            ),
        },
        {
            id: 'actions',
            header: '操作',
            cell: ({ row }) => {
                const item = row.original
                return (
                    <div className="flex items-center gap-2">
                        <Button 
                            variant="ghost" 
                            size="sm"
                            className="hover:bg-green-100 text-green-600"
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
            },
        }
    ]

    return (
        <div className="container mx-auto py-6 space-y-6">
            <div className="flex items-center justify-between">
                <div>
                    <h1 className="text-3xl font-bold tracking-tight">白名单管理</h1>
                    <p className="text-muted-foreground">
                        管理IP白名单，允许可信访问
                    </p>
                </div>
                <Button 
                    onClick={() => setCreateVisible(true)}
                    size="sm"
                >
                    <Plus className="h-4 w-4 mr-2" />
                    新增白名单
                </Button>
            </div>

            <Card>
                <CardHeader>
                    <CardTitle>白名单列表</CardTitle>
                    <CardDescription>
                        查看和管理所有白名单规则
                    </CardDescription>
                </CardHeader>
                <CardContent>
                    <DataTable
                        columns={columns}
                        data={data}
                        loading={loading}
                        total={input.total}
                        pageSize={input.pageSize}
                        current={input.page}
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
                isBlacklist={false}
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

export default WhiteListPage;