import { memo, useEffect, useState } from "react";
import { Button } from '@/components/ui/button';
import { message } from '@/utils/toast';
import { GetRateLimit } from "@/services/RateLimitService";
import CreateRateLimitPage from "./feautres/CreateRateLimit";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { DataTable } from "@/components/ui/data-table";
import { Clock, Plus, Edit, Trash2, Activity } from "lucide-react";
import { ColumnDef } from "@tanstack/react-table";
import { cn } from "@/lib/utils";

const RateLimitPage = memo(() => {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        total: 0,
    });
    const [data, setData] = useState([]);
    const [createVisible, setCreateVisible] = useState(false);
    const [loading, setLoading] = useState(false);

    function loadData() {
        setLoading(true);
        GetRateLimit(input.page, input.pageSize)
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

    const formatPeriod = (period: string) => {
        const periodMap: Record<string, string> = {
            '1s': '1秒',
            '1m': '1分钟',
            '1h': '1小时',
            '1d': '1天',
            '1w': '1周',
            '1M': '1个月',
        };
        return periodMap[period] || period;
    };

    const columns: ColumnDef<any>[] = [
        {
            accessorKey: 'name',
            header: '限流策略名称',
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
                        className={cn(enabled ? "bg-blue-500 hover:bg-blue-600" : "")}
                    >
                        {enabled ? '已启用' : '已禁用'}
                    </Badge>
                )
            },
        },
        {
            accessorKey: 'endpoint',
            header: '限流端点',
            cell: ({ row }) => (
                <div className="font-mono text-sm">{row.getValue('endpoint')}</div>
            ),
        },
        {
            accessorKey: 'period',
            header: '限流周期',
            cell: ({ row }) => (
                <div className="flex items-center gap-2">
                    <Clock className="h-4 w-4 text-muted-foreground" />
                    <span>{formatPeriod(row.getValue('period'))}</span>
                </div>
            ),
        },
        {
            accessorKey: 'limit',
            header: '限流值',
            cell: ({ row }) => {
                const limit = row.getValue('limit') as number
                return (
                    <div className="flex items-center gap-2">
                        <Activity className="h-4 w-4 text-muted-foreground" />
                        <Badge variant="outline">
                            {limit} 请求
                        </Badge>
                    </div>
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
            cell: ({ row }) => (
                <div className="flex items-center gap-2">
                    <Button 
                        variant="ghost" 
                        size="sm"
                        className="hover:bg-blue-100 text-blue-600"
                    >
                        <Edit className="h-4 w-4" />
                    </Button>
                    <Button 
                        variant="ghost" 
                        size="sm"
                        className="text-destructive hover:text-destructive hover:bg-destructive/10"
                    >
                        <Trash2 className="h-4 w-4" />
                    </Button>
                </div>
            ),
        }
    ]

    return (
        <div className="container mx-auto py-8 px-4 md:px-6 lg:px-8">
            <div className="max-w-7xl mx-auto">
                {/* Header Section */}
                <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4 mb-8">
                    <div className="space-y-1">
                        <h1 className="text-3xl font-bold tracking-tight bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                            限流管理
                        </h1>
                        <p className="text-muted-foreground text-sm">
                            管理请求限流策略，保护系统稳定性
                        </p>
                    </div>
                    <Button 
                        onClick={() => setCreateVisible(true)}
                        className="w-full sm:w-auto"
                    >
                        <Plus className="h-4 w-4 mr-2" />
                        创建限流策略
                    </Button>
                </div>

                {/* Stats Cards */}
                <div className="grid gap-4 md:grid-cols-3 mb-6">
                    <Card className="border-l-4 border-l-blue-500">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground">总策略数</CardTitle>
                            <div className="text-2xl font-bold">{input.total}</div>
                        </CardHeader>
                    </Card>
                    <Card className="border-l-4 border-l-green-500">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground">已启用</CardTitle>
                            <div className="text-2xl font-bold">{data.filter(item => item.enable).length}</div>
                        </CardHeader>
                    </Card>
                    <Card className="border-l-4 border-l-orange-500">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground">已禁用</CardTitle>
                            <div className="text-2xl font-bold">{data.filter(item => !item.enable).length}</div>
                        </CardHeader>
                    </Card>
                </div>

                {/* Main Content Card */}
                <Card className="overflow-hidden">
                    <CardHeader className="border-b bg-muted/50">
                        <div className="flex items-center justify-between">
                            <div>
                                <CardTitle className="text-lg">限流策略列表</CardTitle>
                                <CardDescription className="text-sm">
                                    查看和管理所有限流规则
                                </CardDescription>
                            </div>
                        </div>
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
            </div>
            
            <CreateRateLimitPage 
                visible={createVisible} 
                onClose={() => setCreateVisible(false)} 
                onOk={() => {
                    loadData();
                    setCreateVisible(false);
                }} 
            />
        </div>
    );
});

export default RateLimitPage;