import { memo, useEffect, useState } from "react";
import Header from "../features/Header";
import { Button } from '@/components/ui/button';
import { message } from '@/utils/toast';
import TableList from "../features/Table";
import { GetRateLimit } from "@/services/RateLimitService";
import CreateRateLimitPage from "./feautres/CreateRateLimit";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Clock, Plus, Edit, Trash2, Activity } from "lucide-react";

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

    return (
        <div className="space-y-6 p-6">
            <Card className="border-0 shadow-lg">
                <CardHeader className="border-b bg-gradient-to-r from-blue-50 to-background">
                    <div className="flex items-center justify-between">
                        <div>
                            <CardTitle className="text-2xl font-bold text-blue-600">
                                限流管理
                            </CardTitle>
                            <CardDescription className="text-muted-foreground">
                                管理请求限流策略，保护系统稳定性
                            </CardDescription>
                        </div>
                        <Button 
                            onClick={() => setCreateVisible(true)}
                            className="bg-blue-600 hover:bg-blue-700"
                        >
                            <Plus className="h-4 w-4 mr-2" />
                            新增限流
                        </Button>
                    </div>
                </CardHeader>
                
                <CardContent className="p-6">
                    <TableList
                        columns={[
                            {
                                title: '限流策略名称',
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
                                        className={text ? "bg-blue-500" : ""}
                                    >
                                        {text ? '已启用' : '已禁用'}
                                    </Badge>
                                )
                            },
                            {
                                title: '限流端点',
                                dataIndex: 'endpoint',
                                key: 'endpoint',
                                className: 'font-mono text-sm',
                            },
                            {
                                title: '限流周期',
                                dataIndex: 'period',
                                key: 'period',
                                render: (text: string) => (
                                    <div className="flex items-center gap-2">
                                        <Clock className="h-4 w-4 text-muted-foreground" />
                                        <span>{formatPeriod(text)}</span>
                                    </div>
                                )
                            },
                            {
                                title: '限流值',
                                dataIndex: 'limit',
                                key: 'limit',
                                render: (text: number) => (
                                    <div className="flex items-center gap-2">
                                        <Activity className="h-4 w-4 text-muted-foreground" />
                                        <Badge variant="outline">
                                            {text} 请求
                                        </Badge>
                                    </div>
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
                                render: () => {
                                    return (
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
                                    );
                                }
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