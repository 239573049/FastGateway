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

    const columns: ColumnDef<any>[] = [
        {
            accessorKey: 'name',
            header: '黑名单名称',
            cell: ({ row }) => (
                <div className="font-medium text-foreground">{row.getValue('name')}</div>
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
                        className={cn(
                            enabled ? "bg-green-500 hover:bg-green-600" : "",
                            "font-medium"
                        )}
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
                <div className="max-w-xs text-sm text-muted-foreground truncate">
                    {row.getValue('description') || '-'}
                </div>
            ),
        },
        {
            accessorKey: 'ips',
            header: 'IP数量',
            cell: ({ row }) => {
                const ips = row.getValue('ips') as string[]
                return (
                    <Badge variant="outline" className="font-mono text-xs">
                        {ips?.length || 0}
                    </Badge>
                )
            },
        },
        {
            accessorKey: 'createdAt',
            header: '创建时间',
            cell: ({ row }) => (
                <div className="text-sm text-muted-foreground font-mono">
                    {row.getValue('createdAt')}
                </div>
            ),
        },
        {
            id: 'actions',
            header: '操作',
            cell: ({ row }) => {
                const item = row.original
                return (
                    <div className="flex items-center gap-1">
                        <Button 
                            variant="ghost" 
                            size="icon"
                            className="h-8 w-8 hover:bg-primary/10"
                        >
                            <Edit className="h-4 w-4" />
                        </Button>
                        <Button 
                            variant="ghost" 
                            size="icon"
                            className="h-8 w-8 text-destructive hover:text-destructive hover:bg-destructive/10"
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
        <div className="container mx-auto py-8 px-4 sm:px-6 lg:px-8 max-w-7xl">
            <div className="mb-8">
                <h1 className="text-3xl font-bold tracking-tight text-foreground">黑名单管理</h1>
                <p className="text-muted-foreground mt-2">管理IP黑名单，阻止恶意访问</p>
            </div>

            <div className="flex justify-between items-center mb-6">
                <div className="flex items-center space-x-2">
                    <div className="h-8 w-1 bg-destructive rounded-full"></div>
                    <h2 className="text-xl font-semibold text-foreground">黑名单列表</h2>
                </div>
                <Button 
                    onClick={() => setCreateVisible(true)}
                    className="bg-destructive hover:bg-destructive/90"
                >
                    <Plus className="h-4 w-4 mr-2" />
                    新增黑名单
                </Button>
            </div>

            <Card className="border shadow-sm">
                <CardHeader className="border-b bg-muted/50">
                    <CardTitle className="text-lg font-semibold">黑名单规则</CardTitle>
                    <CardDescription className="text-sm text-muted-foreground">
                        配置和管理IP黑名单规则，保护系统安全
                    </CardDescription>
                </CardHeader>
                
                <CardContent className="p-0">
                    <div className="p-4">
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
                    </div>
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