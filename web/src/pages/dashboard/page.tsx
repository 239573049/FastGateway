

import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { getQpsChart } from '@/services/QpsService';
import { Badge } from '@/components/ui/badge';
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from '@/components/ui/tooltip';
import { bytesToSize } from '@/utils/byte';
import { GetDashboard, realtime } from '@/services/DashboardService';
import Map from './features/map';
import MapList from './features/map.list';
import { DonutChart } from '@/components/ui/donut-chart';
import { LineChart } from '@/components/ui/line-chart';
import { ProgressBar } from '@/components/ui/progress-bar';
import { BarChart } from '@/components/ui/bar-chart';

const Dashboard = () => {

    const [qps, setQps] = useState(0)
    const [locationData, setLocationData] = useState([] as any[]);

    const [netWork, setNetWork] = useState({
        upload: 0,
        download: 0
    })
    const [data, setData] = React.useState({
        total: 0,
        success: 0,
        fail: 0,
        todayTotal: 0,
        todaySuccess: 0,
        todayFail: 0,
    });
    const [memory, setMemory] = useState({
        totalMemory: 0,
        memoryUsage: 0,
    })
    const [diskChart, setDiskChart] = useState<any>([])
    const [cpuChart, setCpuChart] = useState<any>([])
    const [memoryChart, setMemoryChart] = useState<any>([])
    const [qpsChart, setQpsChart] = useState<Array<{time: string, QPS: number}>>(() => 
        Array.from({length: 30}, () => ({
            time: '0:00:00',
            QPS: 0
        }))
    );

    useEffect(() => {
        setLocationData([]);

        async function stream() {
            const response = await getQpsChart() as any;
            for await (const data of response) {
                setQpsChart(prev => {
                    const newData = [...prev];
                    newData.shift();
                    newData.push({
                        time: data.now,
                        QPS: data.qps
                    });
                    return newData;
                });
                setQps(data.qps);
                setNetWork({
                    upload: data.upload,
                    download: data.download
                })
            }
        }


        const diakChart = [] as any[]
        const cpuChart = [] as any[]
        async function loadRealtime() {
            const response = await realtime() as any;
            for await (const item of response) {
                const { cpu, memoryUsage, totalMemory, useMemory, diskRead, diskWrite } = item;
                // 如果cpuChart数量存在100个则删除第一个
                if (diakChart.length >= 100) {
                    diakChart.shift();
                    cpuChart.shift();
                }

                const memoryChart = [{
                    name: '剩余内存',
                    value: totalMemory - useMemory,
                }, {
                    name: '使用内存',
                    value: useMemory,
                }];

                setMemoryChart([...memoryChart])
                setMemory({
                    totalMemory,
                    memoryUsage
                })
                cpuChart.push({
                    CPU使用率: cpu,
                    name: 'CPU',
                    date: new Date().toLocaleTimeString('en-GB', { hour12: false })
                });
                setCpuChart([...cpuChart]);
                diakChart.push(
                    {
                        read: diskRead,
                        write: diskWrite,
                        date: new Date().toLocaleTimeString('en-GB', { hour12: false })
                    })
                setDiskChart([...diakChart]);

            }
        }

        stream();
        loadRealtime();
        loadDashboard();
        // 定时器
        const timer = setInterval(() => {
            stream();
            loadRealtime();
        }, 10000);


        return () => {
            timer && clearInterval(timer);
        }
    }, []);

    async function loadDashboard() {
        const result = await GetDashboard()
        setData(result.data)
    }

    const percentFormatter = (number: number) => {
        return new Intl.NumberFormat('us').format(number).toString() + "%";
    };

    const diskFormatter = (number: number) => {
        // 默认是btye需要将byte转换可视化字符串
        return bytesToSize(number);
    }


    return (
        <div className="p-4 md:p-6 lg:p-8 h-full overflow-auto">
            {/* Header Section */}
            <div className="mb-6">
                <h1 className="text-2xl font-bold tracking-tight mb-2">系统概览</h1>
                <p className="text-muted-foreground">实时监控网关性能和流量数据</p>
            </div>

            {/* Key Metrics Overview */}
            <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
                <Card className="hover:shadow-md transition-all duration-200">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                            <div className="w-2 h-2 bg-blue-500 rounded-full mr-2"></div>
                            总访问量
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.total.toLocaleString()}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            今日: +{data.todayTotal.toLocaleString()}
                        </p>
                    </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-all duration-200">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                            <div className="w-2 h-2 bg-green-500 rounded-full mr-2"></div>
                            成功请求
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.success.toLocaleString()}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            今日: +{data.todaySuccess.toLocaleString()}
                        </p>
                    </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-all duration-200">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                            <div className="w-2 h-2 bg-red-500 rounded-full mr-2"></div>
                            失败请求
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.fail.toLocaleString()}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            今日: +{data.todayFail.toLocaleString()}
                        </p>
                    </CardContent>
                </Card>

                <Card className="hover:shadow-md transition-all duration-200">
                    <CardHeader className="pb-3">
                        <CardTitle className="text-sm font-medium text-muted-foreground flex items-center">
                            <div className="w-2 h-2 bg-purple-500 rounded-full mr-2"></div>
                            实时 QPS
                        </CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{qps}</div>
                        <p className="text-xs text-muted-foreground mt-1">
                            ↑ {bytesToSize(netWork.upload)}/s ↓ {bytesToSize(netWork.download)}/s
                        </p>
                    </CardContent>
                </Card>
            </div>

            {/* Performance Dashboard */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-6">
                {/* Network Traffic */}
                <Card className="hover:shadow-md transition-all duration-200">
                    <CardHeader className="pb-3">
                        <div className="flex items-center justify-between">
                            <div>
                                <CardTitle className="text-base font-semibold">网络流量</CardTitle>
                                <CardDescription className="text-xs">实时上传下载速率</CardDescription>
                            </div>
                            <div className="text-right">
                                <div className="text-sm font-medium">
                                    ↑ {bytesToSize(netWork.upload)}/s
                                </div>
                                <div className="text-sm font-medium">
                                    ↓ {bytesToSize(netWork.download)}/s
                                </div>
                            </div>
                        </div>
                    </CardHeader>
                    <CardContent>
                        <div className="space-y-4">
                            <div className="flex items-center justify-between text-sm">
                                <span className="text-muted-foreground">上传速率</span>
                                <span className="font-medium">{bytesToSize(netWork.upload)}/s</span>
                            </div>
                            <div className="w-full bg-gray-200 rounded-full h-2">
                                <div className="bg-blue-500 h-2 rounded-full" style={{width: `${Math.min((netWork.upload / 1024 / 1024) * 100, 100)}%`}}></div>
                            </div>
                            <div className="flex items-center justify-between text-sm mt-2">
                                <span className="text-muted-foreground">下载速率</span>
                                <span className="font-medium">{bytesToSize(netWork.download)}/s</span>
                            </div>
                            <div className="w-full bg-gray-200 rounded-full h-2">
                                <div className="bg-green-500 h-2 rounded-full" style={{width: `${Math.min((netWork.download / 1024 / 1024) * 100, 100)}%`}}></div>
                            </div>
                        </div>
                    </CardContent>
                </Card>
            </div>

            {/* Geographic Analytics */}
            <Card className="hover:shadow-md transition-all duration-200">
                <CardHeader>
                    <CardTitle className="text-lg font-semibold">流量地理分布</CardTitle>
                    <CardDescription className="text-sm">按省份统计的访问来源</CardDescription>
                </CardHeader>
                <CardContent>
                    <div className="grid grid-cols-1 lg:grid-cols-5 gap-6">
                        <div className="lg:col-span-3">
                            <Map isGlobal={false} data={locationData} />
                        </div>
                        <div className="lg:col-span-2">
                            <MapList isGlobal={false} data={locationData} />
                        </div>
                    </div>
                </CardContent>
            </Card>
        </div>
    );
};

export default Dashboard;