

import React, { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
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
        <div className="p-5 overflow-auto h-full">
            <Card className="float-right w-[280px] h-[260px]">
                <CardContent className="p-4">
                    <div className="flex justify-end">
                    <TooltipProvider>
                        <span className="space-x-2">
                            <Tooltip>
                                <TooltipTrigger asChild>
                                    <Badge variant="secondary">
                                        {qps}
                                    </Badge>
                                </TooltipTrigger>
                                <TooltipContent>
                                    <p>实时QPS</p>
                                </TooltipContent>
                            </Tooltip>
                            <Badge variant="secondary">
                                {bytesToSize(netWork.upload)}/s
                            </Badge>
                            <Badge variant="secondary">
                                {bytesToSize(netWork.download)}/s
                            </Badge>
                        </span>
                    </TooltipProvider>
                    </div>
                    <div className="w-[230px] h-[200px]">
                        <BarChart
                            data={qpsChart}
                            categories={['QPS']}
                            index="time"
                            showXAxis={false}
                            showGrid={false}
                        />
                    </div>
                </CardContent>
            </Card>
            <div className="grid grid-cols-3 gap-4 w-[calc(100%-280px)]">
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">访问总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.total.toLocaleString()}</div>
                    </CardContent>
                </Card>
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">访问成功总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.success.toLocaleString()}</div>
                    </CardContent>
                </Card>
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">访问失败总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.fail.toLocaleString()}</div>
                    </CardContent>
                </Card>
            </div>
            <div className="grid grid-cols-3 gap-4 mt-5 w-[calc(100%-280px)]">
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">今日总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.todayTotal.toLocaleString()}</div>
                    </CardContent>
                </Card>
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">今日成功总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.todaySuccess.toLocaleString()}</div>
                    </CardContent>
                </Card>
                <Card className="h-[120px] hover:shadow-lg transition-shadow cursor-pointer">
                    <CardHeader className="pb-2">
                        <CardTitle className="text-sm font-medium text-muted-foreground">今日失败总数</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="text-2xl font-bold">{data.todayFail.toLocaleString()}</div>
                    </CardContent>
                </Card>
            </div>
            <div className="grid grid-cols-3 gap-4 mt-5">
                <Card className="h-[420px] hover:shadow-lg transition-shadow">
                    <CardHeader>
                        <CardTitle>CPU</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <LineChart
                            categories={['CPU使用率']}
                            data={cpuChart}
                            index="date"
                            valueFormatter={percentFormatter}
                        />
                    </CardContent>
                </Card>
                <Card className="h-[420px] hover:shadow-lg transition-shadow">
                    <CardHeader>
                        <CardTitle>内存</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <DonutChart
                            label={"总内存" + bytesToSize(memory.totalMemory, 0)}
                            data={memoryChart}
                            noDataText="暂无数据"
                            onValueChange={(v) => console.log(v)}
                            valueFormatter={diskFormatter}
                            variant="donut"
                        />
                        <ProgressBar 
                            className="mt-10"
                            color={'blue'} 
                            tooltip={`内存使用率${memory.memoryUsage}%`} 
                            value={memory.memoryUsage} 
                        />
                    </CardContent>
                </Card>
                <Card className="h-[420px] hover:shadow-lg transition-shadow">
                    <CardHeader>
                        <CardTitle>硬盘读/写</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <LineChart
                            categories={['read', 'write']}
                            data={diskChart}
                            index="date"
                            valueFormatter={diskFormatter}
                        />
                    </CardContent>
                </Card>
            </div>
            <div className="mt-5">
                <Card>
                    <CardHeader>
                        <CardTitle>流量来源</CardTitle>
                    </CardHeader>
                    <CardContent>
                        <div className="flex justify-between leading-[50px] overflow-hidden select-none h-[500px] overflow-auto p-2.5">
                            <Map isGlobal={false} data={locationData} />
                            <MapList isGlobal={false} data={locationData} />
                        </div>
                    </CardContent>
                </Card>
            </div>
        </div>
    );
};

export default Dashboard;