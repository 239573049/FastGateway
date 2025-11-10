import  { useCallback, useEffect, useState } from 'react';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { ProgressBar } from '@/components/ui/progress-bar';
import { LineChart } from '@/components/ui/line-chart';
import { BarChart } from '@/components/ui/bar-chart';
import { TooltipProvider } from '@/components/ui/tooltip';
import { bytesToSize } from '@/utils/byte';
import { getQpsData } from '@/services/QpsService';
import {
  Activity,
  AlertCircle,
  CheckCircle,
  Clock,
  Cpu,
  HardDrive,
  Server,
  Timer,
  TrendingDown,
  TrendingUp,
  Wifi,
  Zap,
} from 'lucide-react';

const Dashboard = () => {
  const [qps, setQps] = useState(0);
  const [peakQps, setPeakQps] = useState(0);
  const [avgQps, setAvgQps] = useState(0);
  const [, setQpsHistory] = useState<number[]>([]);
  const [network, setNetwork] = useState({ upload: 0, download: 0 });
  const [networkHistory, setNetworkHistory] = useState<
    Array<{ time: string; upload: number; download: number; total: number }>
  >([]);
  const [data, setData] = useState({
    total: 0,
    success: 0,
    fail: 0,
    todayTotal: 0,
    todaySuccess: 0,
    todayFail: 0,
  });
  const [responseTime, setResponseTime] = useState({
    avg: 0,
    p95: 0,
    p99: 0,
    min: 0,
    max: 0,
  });
  const [memory, setMemory] = useState({
    totalMemory: 0,
    memoryUsage: 0,
    usedMemory: 0,
    freeMemory: 0,
  });
  const [cpu, setCpu] = useState({
    usage: 0,
    cores: 0,
    temperature: 0,
  });
  const [diskChart, setDiskChart] = useState<Array<{ date: string; read: number; write: number }>>([]);
  const [qpsChart, setQpsChart] = useState<Array<{ time: string; QPS: number }>>(() =>
    Array.from({ length: 30 }, () => ({
      time: '00:00',
      QPS: 0,
    }))
  );
  const [isOnline, setIsOnline] = useState(true);
  const [lastUpdate, setLastUpdate] = useState(new Date());
  const [windowWidth, setWindowWidth] = useState(typeof window !== 'undefined' ? window.innerWidth : 1200);
  const [isLoading, setIsLoading] = useState(true);

  const calculateQpsMetrics = useCallback(
    (newQps: number) => {
      setQpsHistory(prev => {
        const updated = [...prev, newQps].slice(-60);
        const sum = updated.reduce((acc, value) => acc + value, 0);
        const avg = updated.length ? Math.round(sum / updated.length) : 0;

        setAvgQps(avg);
        setPeakQps(currentPeak => Math.max(...updated, currentPeak));
        return updated;
      });
    },
    [setAvgQps, setPeakQps]
  );

  useEffect(() => {
    const loadData = async () => {
      try {
        const fetchedData = await getQpsData();

        if (!fetchedData) {
          return;
        }

        const currentTime = fetchedData.now;
        const currentQps = fetchedData.qps;

        setQpsChart(prev => {
          const newData = [...prev];
          newData.shift();
          newData.push({
            time: currentTime,
            QPS: currentQps,
          });
          return newData;
        });

        setQps(currentQps);
        calculateQpsMetrics(currentQps);

        const newNetwork = {
          upload: fetchedData.upload,
          download: fetchedData.download,
        };
        setNetwork(newNetwork);

        setNetworkHistory(prev => {
          const newData = [
            ...prev,
            {
              time: currentTime,
              upload: newNetwork.upload,
              download: newNetwork.download,
              total: newNetwork.upload + newNetwork.download,
            },
          ].slice(-30);
          return newData;
        });

        if (fetchedData.requests) {
          setData(prev => ({
            ...prev,
            total: fetchedData.requests.total,
            success: fetchedData.requests.success,
            fail: fetchedData.requests.failed,
          }));
        }

        if (fetchedData.responseTime) {
          setResponseTime({
            avg: fetchedData.responseTime.avg,
            p95: fetchedData.responseTime.p95,
            p99: fetchedData.responseTime.p99,
            min: fetchedData.responseTime.min,
            max: fetchedData.responseTime.max,
          });
        }

        if (fetchedData.system) {
          setCpu({
            usage: fetchedData.system.cpu.usage,
            cores: fetchedData.system.cpu.cores,
            temperature: fetchedData.system.cpu.temperature ?? 0,
          });
          setMemory({
            totalMemory: fetchedData.system.memory.total,
            memoryUsage: fetchedData.system.memory.usage,
            usedMemory: fetchedData.system.memory.used,
            freeMemory: fetchedData.system.memory.available,
          });

          if (fetchedData.system.disk) {
            setDiskChart(prev => {
              const newData = [
                ...prev,
                {
                  date: currentTime,
                  read: fetchedData.system.disk.readBytesPerSec,
                  write: fetchedData.system.disk.writeBytesPerSec,
                },
              ].slice(-80);
              return newData;
            });
          }
        }

        if (fetchedData.service) {
          setIsOnline(fetchedData.service.isOnline);
        }

        setLastUpdate(new Date());
        setIsLoading(false);
      } catch (error) {
        console.error('Failed to load dashboard data:', error);
        setIsOnline(false);
        setIsLoading(false);
      }
    };

    loadData();
    const timer = setInterval(loadData, 3000);
    return () => {
      clearInterval(timer);
    };
  }, [calculateQpsMetrics]);

  useEffect(() => {
    const handleResize = () => {
      setWindowWidth(window.innerWidth);
    };

    window.addEventListener('resize', handleResize);
    return () => window.removeEventListener('resize', handleResize);
  }, []);

  const successRate = data.total ? (data.success / data.total) * 100 : 0;

  const qpsBadgeVariant = qps > 100 ? 'destructive' : qps > 50 ? 'default' : 'secondary';
  const qpsBadgeLabel = qps > 100 ? '高峰' : qps > 50 ? '活跃' : '平稳';

  const getStatusIcon = () => {
    if (isLoading) {
      return <Activity className="animate-spin text-muted-foreground" />;
    }
    if (isOnline) {
      return <CheckCircle className="text-green-500" />;
    }
    return <AlertCircle className="text-red-500" />;
  };

  const latencyMetrics = [
    { label: 'P95 响应', value: responseTime.p95, trend: responseTime.max, icon: Timer },
    { label: 'P99 响应', value: responseTime.p99, trend: responseTime.max, icon: Timer },
    { label: '最小响应', value: responseTime.min, trend: responseTime.avg, icon: Clock },
    { label: '最大响应', value: responseTime.max, trend: responseTime.avg, icon: Clock },
  ];

  const requestStats = [
    {
      label: '累计请求',
      value: data.total,
      change: data.todayTotal,
      icon: Server,
      tone: 'text-blue-500',
    },
    {
      label: '今日成功',
      value: data.todaySuccess,
      icon: CheckCircle,
      tone: 'text-emerald-500',
    },
    {
      label: '今日失败',
      value: data.todayFail,
      icon: AlertCircle,
      tone: 'text-red-500',
    },
    {
      label: '成功率',
      value: Number(successRate.toFixed(1)),
      change: data.fail > 0 ? -1 : 1,
      icon: TrendingUp,
      tone: 'text-purple-500',
    },
  ];

  return (
    <TooltipProvider>
      <div className="min-h-screen space-y-8 px-4 py-6 md:px-6 lg:px-10">
        <header className="space-y-2">
          <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">FastGateway 控制台</p>
          <div className="flex flex-wrap items-center justify-between gap-4">
            <div className="space-y-1">
              <h1 className="text-3xl font-semibold tracking-tight">实时概览</h1>
              <p className="text-sm text-muted-foreground">统一了解请求、网络与系统资源状态</p>
            </div>
            <div className="flex items-center gap-3 rounded-full border border-border px-4 py-2 text-sm text-muted-foreground">
              <Clock className="h-4 w-4 text-muted-foreground" />
              上次同步 {lastUpdate.toLocaleTimeString()}
            </div>
          </div>
          <div className="flex items-center gap-2 text-sm text-muted-foreground">
            {getStatusIcon()}
            {isLoading ? '数据同步中…' : isOnline ? '节点健康' : '无法连接服务'}
          </div>
        </header>

        <section className="grid gap-4 md:grid-cols-2 xl:grid-cols-4">
          <Card className="space-y-2 border border-border">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center justify-between text-sm font-medium">
                <span className="flex items-center gap-2">
                  <Zap className="h-4 w-4 text-yellow-500" />
                  实时 QPS
                </span>
                <Badge variant={qpsBadgeVariant}>{qpsBadgeLabel}</Badge>
              </CardTitle>
              <CardDescription className="text-[0.85rem] text-muted-foreground">最近 30s 滑动窗口</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-3xl font-semibold text-yellow-600">{qps.toLocaleString()}</p>
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span>平均 {avgQps.toLocaleString()}</span>
                <span>峰值 {peakQps.toLocaleString()}</span>
              </div>
            </CardContent>
          </Card>

          <Card className="space-y-2 border border-border">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Timer className="h-4 w-4 text-emerald-500" />
                响应延迟
              </CardTitle>
              <CardDescription className="text-[0.85rem] text-muted-foreground">实时追踪 P95 / P99</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-3xl font-semibold text-emerald-600">{responseTime.avg.toFixed(1)} ms</p>
              <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground">
                <div className="space-y-0.5">
                  <p className="text-muted-foreground">P95</p>
                  <p className="text-sm font-semibold text-foreground">{responseTime.p95.toFixed(1)} ms</p>
                </div>
                <div className="space-y-0.5">
                  <p className="text-muted-foreground">P99</p>
                  <p className="text-sm font-semibold text-foreground">{responseTime.p99.toFixed(1)} ms</p>
                </div>
              </div>
            </CardContent>
          </Card>

          <Card className="space-y-3 border border-border">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <CheckCircle className="h-4 w-4 text-blue-500" />
                成功率
              </CardTitle>
              <CardDescription className="text-[0.85rem] text-muted-foreground">请求成功/失败的比例</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <div className="flex items-baseline gap-2">
                <p className="text-3xl font-semibold text-blue-600">{successRate.toFixed(1)}%</p>
                <p className="text-xs text-muted-foreground">成功 {data.success.toLocaleString()} / 失败 {data.fail.toLocaleString()}</p>
              </div>
              <ProgressBar
                value={successRate}
                className="h-3"
                color={successRate > 90 ? 'green' : successRate > 70 ? 'yellow' : 'red'}
              />
            </CardContent>
          </Card>

          <Card className="space-y-2 border border-border">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-sm font-medium">
                <Wifi className="h-4 w-4 text-purple-500" />
                网络吞吐
              </CardTitle>
              <CardDescription className="text-[0.85rem] text-muted-foreground">上行/下行带宽</CardDescription>
            </CardHeader>
            <CardContent className="space-y-3">
              <p className="text-3xl font-semibold text-purple-600">
                {bytesToSize(network.upload + network.download)}/s
              </p>
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span className="flex items-center gap-1">
                  <TrendingUp className="h-3 w-3" />
                  {bytesToSize(network.upload)}/s
                </span>
                <span className="flex items-center gap-1">
                  <TrendingDown className="h-3 w-3" />
                  {bytesToSize(network.download)}/s
                </span>
              </div>
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-6 lg:grid-cols-2">
          <Card className="min-h-[360px] border border-border">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg font-semibold">
                <Activity className="h-5 w-5 text-yellow-500" />
                QPS 趋势
              </CardTitle>
              <CardDescription>滚动的 30 次请求曲线</CardDescription>
            </CardHeader>
            <CardContent className="h-[280px]">
              <LineChart
                key={`qps-chart-${windowWidth}`}
                data={qpsChart}
                categories={['QPS']}
                colors={['yellow']}
                valueFormatter={(value: number) => `${value} req/s`}
                index="time"
                showXAxis={false}
                className="h-full w-full"
              />
            </CardContent>
          </Card>

          <Card className="min-h-[360px] border border-border">
            <CardHeader>
              <CardTitle className="flex items-center gap-2 text-lg font-semibold">
                <Wifi className="h-5 w-5 text-purple-500" />
                网络流量
              </CardTitle>
              <CardDescription>上/下行速度</CardDescription>
            </CardHeader>
            <CardContent className="h-[280px]">
              {networkHistory.length ? (
                <LineChart
                  key={`network-chart-${windowWidth}`}
                  data={networkHistory}
                  categories={['upload', 'download']}
                  colors={['blue', 'green']}
                  valueFormatter={(value: number) => `${bytesToSize(value)}/s`}
                  index="time"
                  className="h-full w-full"
                />
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  等待网络数据…
                </div>
              )}
            </CardContent>
          </Card>
        </section>

        <section className="grid gap-6 xl:grid-cols-3">
          <Card className="space-y-4 border border-border">
            <CardHeader>
              <CardTitle className="text-lg font-semibold">系统资源</CardTitle>
              <CardDescription>CPU、内存、磁盘概览</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm font-medium">
                  <span className="flex items-center gap-2">
                    <Cpu className="h-4 w-4 text-orange-500" />
                    CPU 负载
                  </span>
                  <span className="text-xs text-muted-foreground">{cpu.cores} 核</span>
                </div>
                <div className="flex items-center justify-between text-lg font-semibold text-foreground">
                  {cpu.usage.toFixed(1)}%
                </div>
                <ProgressBar value={cpu.usage} className="h-2" color={cpu.usage > 80 ? 'red' : cpu.usage > 60 ? 'yellow' : 'green'} />
              </div>

              <div className="space-y-2">
                <div className="flex items-center justify-between text-sm font-medium">
                  <span className="flex items-center gap-2">
                    <Server className="h-4 w-4 text-blue-500" />
                    内存
                  </span>
                  <span className="text-xs text-muted-foreground">{bytesToSize(memory.totalMemory)} 总计</span>
                </div>
                <div className="flex items-center justify-between text-lg font-semibold text-foreground">
                  {memory.memoryUsage.toFixed(1)}%
                </div>
                <ProgressBar
                  value={memory.memoryUsage}
                  className="h-2"
                  color={memory.memoryUsage > 80 ? 'red' : memory.memoryUsage > 60 ? 'yellow' : 'green'}
                />
                <p className="text-xs text-muted-foreground">
                  已用 {bytesToSize(memory.usedMemory)} · 剩余 {bytesToSize(memory.freeMemory)}
                </p>
              </div>
            </CardContent>
          </Card>

          <Card className="space-y-4 border border-border">
            <CardHeader>
              <CardTitle className="text-lg font-semibold flex items-center gap-2">
                <HardDrive className="h-4 w-4 text-gray-500" />
                磁盘 I/O
              </CardTitle>
              <CardDescription>读取 / 写入 每秒</CardDescription>
            </CardHeader>
            <CardContent className="h-[260px]">
              {diskChart.length ? (
                <BarChart
                  data={diskChart}
                  categories={['read', 'write']}
                  colors={['cyan', 'indigo']}
                  index="date"
                  valueFormatter={(value: number) => bytesToSize(value)}
                  className="h-full"
                />
              ) : (
                <div className="flex h-full items-center justify-center text-sm text-muted-foreground">
                  等待磁盘指标…
                </div>
              )}
            </CardContent>
          </Card>

          <Card className="space-y-3 border border-border">
            <CardHeader>
              <CardTitle className="text-lg font-semibold flex items-center gap-2">
                <Server className="h-4 w-4 text-blue-500" />
                请求概况
              </CardTitle>
              <CardDescription>全量请求与今日增量</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="grid grid-cols-2 gap-4 md:grid-cols-4">
                {requestStats.map(stat => (
                  <div key={stat.label} className="space-y-1 text-center">
                    <div className={`mx-auto flex h-8 w-8 items-center justify-center rounded-full bg-muted text-muted-foreground`}>
                      <stat.icon className="h-4 w-4" />
                    </div>
                    <p className="text-sm font-medium text-muted-foreground">{stat.label}</p>
                    <p className={`text-lg font-semibold ${stat.tone}`}>{stat.label === '成功率' ? `${stat.value}%` : stat.value.toLocaleString()}</p>
                    {stat.change !== undefined && stat.label !== '成功率' && (
                      <p className="text-xs text-muted-foreground">+{stat.change.toLocaleString()} 今日</p>
                    )}
                  </div>
                ))}
              </div>

              <div className="grid gap-3 text-sm text-muted-foreground md:grid-cols-2">
                {latencyMetrics.map(metric => (
                  <div key={metric.label} className="rounded-lg border border-border p-3">
                    <div className="flex items-center gap-2 text-xs uppercase tracking-widest">
                      <metric.icon className="h-3 w-3 text-muted-foreground" />
                      <span>{metric.label}</span>
                    </div>
                    <p className="text-lg font-semibold text-foreground">{metric.value.toFixed(1)} ms</p>
                    <p className="text-xs text-muted-foreground">参考 {metric.trend.toFixed(1)} ms</p>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        </section>
      </div>
    </TooltipProvider>
  );
};

export default Dashboard;
