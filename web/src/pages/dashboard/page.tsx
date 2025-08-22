

import React, { useEffect, useState, useCallback } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { getQpsData } from '@/services/QpsService';
import { Badge } from '@/components/ui/badge';
import {  TooltipProvider } from '@/components/ui/tooltip';
import { bytesToSize } from '@/utils/byte';
import { LineChart } from '@/components/ui/line-chart';
import { ProgressBar } from '@/components/ui/progress-bar';
import { BarChart } from '@/components/ui/bar-chart';
import { 
    Activity, 
    Server, 
    Zap, 
    TrendingUp, 
    TrendingDown, 
    AlertCircle, 
    CheckCircle, 
    Clock,
    Cpu,
    HardDrive,
    Wifi,
    Timer
} from 'lucide-react';

const Dashboard = () => {
    // 基础指标
    const [qps, setQps] = useState(0)
    const [peakQps, setPeakQps] = useState(0)
    const [avgQps, setAvgQps] = useState(0)
    const [_, setQpsHistory] = useState<number[]>([])
    

    // 网络数据
    const [netWork, setNetWork] = useState({
        upload: 0,
        download: 0
    })
    const [networkHistory, setNetworkHistory] = useState<Array<{
        time: string,
        upload: number,
        download: number,
        total: number
    }>>([])

    // 请求统计
    const [data, setData] = React.useState({
        total: 0,
        success: 0,
        fail: 0,
        todayTotal: 0,
        todaySuccess: 0,
        todayFail: 0,
    });

    // 响应时间统计
    const [responseTime, setResponseTime] = useState({
        avg: 0,
        p95: 0,
        p99: 0,
        min: 0,
        max: 0
    })

    // 系统资源
    const [memory, setMemory] = useState({
        totalMemory: 0,
        memoryUsage: 0,
        usedMemory: 0,
        freeMemory: 0
    })
    const [cpu, setCpu] = useState({
        usage: 0,
        cores: 0,
        temperature: 0
    })

    // 图表数据
    const [diskChart, setDiskChart] = useState<any>([])
    const [qpsChart, setQpsChart] = useState<Array<{time: string, QPS: number}>>(() => 
        Array.from({length: 30}, () => ({
            time: '0:00:00',
            QPS: 0
        }))
    )

    // 实时状态
    const [isOnline, setIsOnline] = useState(true)
    const [lastUpdate, setLastUpdate] = useState(new Date())
    const [windowWidth, setWindowWidth] = useState(typeof window !== 'undefined' ? window.innerWidth : 1200)
    const [isLoading, setIsLoading] = useState(true)

    // QPS计算函数
    const calculateQpsMetrics = useCallback((newQps: number) => {
        setQpsHistory(prev => {
            const updated = [...prev, newQps].slice(-60); // 保留最近60个数据点
            const sum = updated.reduce((a, b) => a + b, 0);
            const avg = updated.length > 0 ? Math.round(sum / updated.length) : 0;
            
            setAvgQps(avg);
            setPeakQps(Math.max(...updated, peakQps));
            
            return updated;
        });
    }, [peakQps]);

    useEffect(() => {
        async function loadData() {
            try {
                const data = await getQpsData();
                
                if (!data) return;
                
                const currentTime = data.now;
                const currentQps = data.qps;
                
                // 更新QPS图表
                setQpsChart(prev => {
                    const newData = [...prev];
                    newData.shift();
                    newData.push({
                        time: currentTime,
                        QPS: currentQps
                    });
                    return newData;
                });
                
                // 更新QPS指标
                setQps(currentQps);
                calculateQpsMetrics(currentQps);
                
                // 更新网络数据
                const networkData = {
                    upload: data.upload,
                    download: data.download
                };
                setNetWork(networkData);

                // 更新请求统计
                if (data.requests) {
                    setData(prev => ({
                        ...prev,
                        total: data.requests.total,
                        success: data.requests.success,
                        fail: data.requests.failed
                    }));
                }

                // 更新响应时间统计
                if (data.responseTime) {
                    setResponseTime({
                        avg: data.responseTime.avg,
                        p95: data.responseTime.p95,
                        p99: data.responseTime.p99,
                        min: data.responseTime.min,
                        max: data.responseTime.max
                    });
                }

                // 更新系统资源信息
                if (data.system) {
                    setCpu({
                        usage: data.system.cpu.usage,
                        cores: data.system.cpu.cores,
                        temperature: 0
                    });

                    setMemory({
                        totalMemory: data.system.memory.total,
                        memoryUsage: data.system.memory.usage,
                        usedMemory: data.system.memory.used,
                        freeMemory: data.system.memory.available
                    });

                    // 更新磁盘数据
                    if (data.system.disk) {
                        setDiskChart((prev: any) => {
                            const newData = [...prev, {
                                read: data.system.disk.readBytesPerSec,
                                write: data.system.disk.writeBytesPerSec,
                                date: currentTime
                            }].slice(-100);
                            return newData;
                        });
                    }
                }

                // 更新服务状态
                if (data.service) {
                    setIsOnline(data.service.isOnline);
                }
                
                // 更新网络历史数据
                setNetworkHistory(prev => {
                    const newData = [...prev, {
                        time: currentTime,
                        upload: networkData.upload,
                        download: networkData.download,
                        total: networkData.upload + networkData.download
                    }].slice(-30);
                    return newData;
                });
                
                // 更新最后更新时间
                setLastUpdate(new Date());
                setIsOnline(true);
                setIsLoading(false);
                
            } catch (error) {
                console.error('Failed to load dashboard data:', error);
                setIsOnline(false);
                setIsLoading(false);
            }
        }

        // 初始加载
        loadData();
        // 定时器 - 每3秒请求一次
        const timer = setInterval(() => {
            loadData();
        }, 3000);

        return () => {
            if (timer) clearInterval(timer);
        }
    }, [calculateQpsMetrics]);

    // 监听窗口尺寸变化
    useEffect(() => {
        const handleResize = () => {
            setWindowWidth(window.innerWidth);
        };

        window.addEventListener('resize', handleResize);
        return () => window.removeEventListener('resize', handleResize);
    }, []);

    const diskFormatter = (number: number) => {
        // 默认是btye需要将byte转换可视化字符串
        return bytesToSize(number);
    }


    return (
        <TooltipProvider>
            <div className="p-4 md:p-6 lg:p-8 min-h-screen overflow-x-hidden space-y-6">
                {/* Header Section */}
                <div className="flex items-center justify-between">
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight mb-2">实时监控</h1>
                        <p className="text-muted-foreground flex items-center gap-2">
                            <Activity className="w-4 h-4" />
                            FastGateway 性能与流量数据
                            {isLoading ? (
                                <Badge variant="secondary" className="bg-blue-100 text-blue-800 border-blue-200">
                                    <Activity className="w-3 h-3 mr-1 animate-spin" />
                                    加载中
                                </Badge>
                            ) : isOnline ? (
                                <Badge variant="secondary" className="bg-green-100 text-green-800 border-green-200">
                                    <CheckCircle className="w-3 h-3 mr-1" />
                                    在线
                                </Badge>
                            ) : (
                                <Badge variant="destructive" className="bg-red-100 text-red-800 border-red-200">
                                    <AlertCircle className="w-3 h-3 mr-1" />
                                    离线
                                </Badge>
                            )}
                        </p>
                    </div>
                    <div className="text-right text-sm text-muted-foreground">
                        <div className="flex items-center gap-1">
                            <Clock className="w-3 h-3" />
                            最后更新: {lastUpdate.toLocaleTimeString()}
                        </div>
                    </div>
                </div>

                {/* Real-time Metrics */}
                <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
                    {/* Current QPS */}
                    <Card className="hover:shadow-md transition-all duration-200">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground flex items-center justify-between">
                                <div className="flex items-center">
                                    <Zap className="w-4 h-4 mr-2 text-yellow-500" />
                                    实时 QPS
                                </div>
                                <Badge variant={qps > 100 ? "destructive" : qps > 50 ? "default" : "secondary"}>
                                    {qps > 100 ? "高负载" : qps > 50 ? "正常" : "空闲"}
                                </Badge>
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold text-yellow-600">{qps}</div>
                            <div className="flex items-center justify-between text-xs text-muted-foreground mt-2">
                                <span>平均: {avgQps}</span>
                                <span>峰值: {peakQps}</span>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Response Time */}
                    <Card className="hover:shadow-md transition-all duration-200">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                                <Timer className="w-4 h-4 mr-2 text-blue-500" />
                                响应时间
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold text-blue-600">{responseTime.avg}ms</div>
                            <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground mt-2">
                                <span>P95: {responseTime.p95}ms</span>
                                <span>P99: {responseTime.p99}ms</span>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Success Rate */}
                    <Card className="hover:shadow-md transition-all duration-200">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                                <CheckCircle className="w-4 h-4 mr-2 text-green-500" />
                                成功率
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold text-green-600">
                                {data.total > 0 ? ((data.success / data.total) * 100).toFixed(1) : 0}%
                            </div>
                            <div className="flex items-center justify-between text-xs text-muted-foreground mt-2">
                                <span className="text-green-600">成功: {data.success.toLocaleString()}</span>
                                <span className="text-red-600">失败: {data.fail.toLocaleString()}</span>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Network Speed */}
                    <Card className="hover:shadow-md transition-all duration-200">
                        <CardHeader className="pb-3">
                            <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                                <Wifi className="w-4 h-4 mr-2 text-purple-500" />
                                网络流量
                            </CardTitle>
                        </CardHeader>
                        <CardContent>
                            <div className="text-3xl font-bold text-purple-600">
                                {bytesToSize(netWork.upload + netWork.download)}/s
                            </div>
                            <div className="flex items-center justify-between text-xs text-muted-foreground mt-2">
                                <span className="flex items-center">
                                    <TrendingUp className="w-3 h-3 mr-1" />
                                    {bytesToSize(netWork.upload)}/s
                                </span>
                                <span className="flex items-center">
                                    <TrendingDown className="w-3 h-3 mr-1" />
                                    {bytesToSize(netWork.download)}/s
                                </span>
                            </div>
                        </CardContent>
                    </Card>
                </div>

                {/* Performance Charts */}
                <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                    {/* QPS Trend */}
                    <Card className="min-h-[350px] flex flex-col">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Activity className="w-5 h-5 text-yellow-500" />
                                QPS 趋势
                            </CardTitle>
                            <CardDescription>近30秒QPS变化趋势</CardDescription>
                        </CardHeader>
                        <CardContent className="pb-6 flex-1 flex flex-col">
                            <div className="w-full overflow-visible pt-2 pb-4 flex-1" style={{ width: '100%' }}>
                                <LineChart 
                                    key={`qps-chart-${windowWidth}`}
                                    data={qpsChart} 
                                    categories={["QPS"]}
                                    colors={["yellow"]}
                                    valueFormatter={(value: number) => `${value} req/s`}
                                    index="time"
                                    showXAxis={false}
                                    className="h-72"
                                />
                            </div>
                        </CardContent>
                    </Card>

                    {/* Network Traffic Trend */}
                    <Card className="min-h-[350px] flex flex-col">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Wifi className="w-5 h-5 text-purple-500" />
                                网络流量趋势
                            </CardTitle>
                            <CardDescription>近30秒网络上传下载速率</CardDescription>
                        </CardHeader>
                        <CardContent className="pb-6 flex-1 flex flex-col">
                            {networkHistory.length > 0 ? (
                                <div className="w-full overflow-visible pt-2 pb-4 flex-1" style={{ width: '100%' }}>
                                    <LineChart 
                                        key={`network-chart-${windowWidth}`}
                                        data={networkHistory} 
                                        categories={["upload", "download"]}
                                        colors={["blue", "green"]}
                                        valueFormatter={(value: number) => bytesToSize(value) + '/s'}
                                        className="h-72"
                                        index="time"
                                    />
                                </div>
                            ) : (
                                <div className="text-center text-muted-foreground text-sm h-64 flex items-center justify-center">
                                    暂无网络数据
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>

                {/* System Resources */}
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                    {/* CPU Usage */}
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Cpu className="w-5 h-5 text-orange-500" />
                                CPU 使用率
                            </CardTitle>
                            <CardDescription>{cpu.cores} 核心处理器</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="flex items-center justify-between mb-4">
                                <span className="text-2xl font-bold">{cpu.usage.toFixed(1)}%</span>
                                <Badge variant={cpu.usage > 80 ? "destructive" : cpu.usage > 60 ? "default" : "secondary"}>
                                    {cpu.usage > 80 ? "高负载" : cpu.usage > 60 ? "正常" : "空闲"}
                                </Badge>
                            </div>
                            <ProgressBar 
                                value={cpu.usage} 
                                className="h-3"
                                color={cpu.usage > 80 ? "red" : cpu.usage > 60 ? "yellow" : "green"}
                            />
                        </CardContent>
                    </Card>

                    {/* Memory Usage */}
                    <Card>
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <Server className="w-5 h-5 text-blue-500" />
                                内存使用率
                            </CardTitle>
                            <CardDescription>总内存: {bytesToSize(memory.totalMemory)}</CardDescription>
                        </CardHeader>
                        <CardContent>
                            <div className="flex items-center justify-between mb-4">
                                <span className="text-2xl font-bold">{memory.memoryUsage.toFixed(1)}%</span>
                                <Badge variant={memory.memoryUsage > 80 ? "destructive" : memory.memoryUsage > 60 ? "default" : "secondary"}>
                                    {memory.memoryUsage > 80 ? "高使用" : memory.memoryUsage > 60 ? "正常" : "充足"}
                                </Badge>
                            </div>
                            <ProgressBar 
                                value={memory.memoryUsage} 
                                className="h-3"
                                color={memory.memoryUsage > 80 ? "red" : memory.memoryUsage > 60 ? "yellow" : "green"}
                            />
                            <div className="flex justify-between text-xs text-muted-foreground mt-2">
                                <span>已用: {bytesToSize(memory.usedMemory)}</span>
                                <span>可用: {bytesToSize(memory.freeMemory)}</span>
                            </div>
                        </CardContent>
                    </Card>

                    {/* Disk I/O */}
                    <Card className="min-h-[300px] flex flex-col">
                        <CardHeader>
                            <CardTitle className="flex items-center gap-2">
                                <HardDrive className="w-5 h-5 text-gray-500" />
                                磁盘 I/O
                            </CardTitle>
                            <CardDescription>读写速率监控</CardDescription>
                        </CardHeader>
                        <CardContent className="pb-6 flex-1 flex flex-col">
                            {diskChart.length > 0 ? (
                                <>
                                    <div className="grid grid-cols-2 gap-4 mb-4">
                                        <div className="text-center">
                                            <div className="text-sm font-semibold text-blue-600 break-words">
                                                {diskFormatter(diskChart[diskChart.length - 1]?.read || 0)}/s
                                            </div>
                                            <div className="text-xs text-muted-foreground">读取</div>
                                        </div>
                                        <div className="text-center">
                                            <div className="text-sm font-semibold text-green-600 break-words">
                                                {diskFormatter(diskChart[diskChart.length - 1]?.write || 0)}/s
                                            </div>
                                            <div className="text-xs text-muted-foreground">写入</div>
                                        </div>
                                    </div>
                                    <div className="w-full overflow-visible pt-2 pb-4 flex-1" style={{ width: '100%' }}>
                                        <BarChart 
                                            key={`disk-chart-${windowWidth}`}
                                            data={diskChart.slice(-10)} 
                                            categories={["read", "write"]}
                                            colors={["blue", "green"]}
                                            valueFormatter={diskFormatter}
                                            index='disk'
                                            className="h-48"
                                        />
                                    </div>
                                </>
                            ) : (
                                <div className="text-center text-muted-foreground text-sm">
                                    暂无磁盘数据
                                </div>
                            )}
                        </CardContent>
                    </Card>
                </div>

                {/* System Info */}
                <Card>
                    <CardHeader>
                        <CardTitle className="flex items-center gap-2">
                            <Server className="w-5 h-5" />
                            系统状态概览
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
                            <div className="text-center">
                                <div className="text-2xl font-bold text-blue-600">{data.total.toLocaleString()}</div>
                                <div className="text-sm text-muted-foreground">总请求数</div>
                                <div className="text-xs text-green-600">今日: +{data.todayTotal.toLocaleString()}</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-green-600">{data.success.toLocaleString()}</div>
                                <div className="text-sm text-muted-foreground">成功请求</div>
                                <div className="text-xs text-green-600">今日: +{data.todaySuccess.toLocaleString()}</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-red-600">{data.fail.toLocaleString()}</div>
                                <div className="text-sm text-muted-foreground">失败请求</div>
                                <div className="text-xs text-red-600">今日: +{data.todayFail.toLocaleString()}</div>
                            </div>
                            <div className="text-center">
                                <div className="text-2xl font-bold text-purple-600">
                                    {data.total > 0 ? ((data.success / data.total) * 100).toFixed(1) : 0}%
                                </div>
                                <div className="text-sm text-muted-foreground">成功率</div>
                                <div className="text-xs text-muted-foreground">
                                    {data.fail > 0 ? "需要关注" : "运行良好"}
                                </div>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>
        </TooltipProvider>
    );
};

export default Dashboard;