

import * as echarts from 'echarts';


import React, { useEffect, useState } from 'react';
import { Row, Col, Card, Statistic } from 'antd';
import { getQpsChart } from '@/services/QpsService';
import { Tag, Tooltip } from '@lobehub/ui';
import { bytesToSize } from '@/utils/byte';
import { GetDashboard } from '@/services/DashboardService';
import Map from './features/map';
import MapList from './features/map.list';

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
        todayFail: 0
    });


    const [qps_chartData] = useState({
        tooltip: {
            trigger: 'axis',
            axisPointer: {
                type: "shadow"
            }
        },
        //图表离容器的距离
        grid: {
            left: 0,
            top: 0,
            right: 0,
            bottom: 0
        },
        //X轴
        xAxis: {
            type: "category",
            data: ['0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0',],
            axisLabel: {
                show: false
            },
            axisTick: {
                show: false //隐藏X轴刻度尺
            },
            axisLine: {
                show: false
            }
        },
        //y轴
        yAxis: {
            min: 0,
            max: 10,
            axisLabel: {
                show: true
            },
            splitLine: {
                show: false
            }
        },
        series: [{
            data: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,],
            name: 'QPS',
            type: 'bar',
            barMinHeight: 5,
            itemStyle: {
                barBorderRadius: [3, 3, 3, 3],
                color: "#42C4C1"
            }
        }]
    });

    let qps_chart: echarts.ECharts | null = null;
    useEffect(() => {
        setLocationData([])
        qps_chart = echarts.init(document.getElementById('qps_chart'));
        // 将qps_chartData.series[0].data中的数据全部替换为0
        qps_chartData.series[0].data = qps_chartData.series[0].data.map(() => 0);
        // 将qps_chartData.xAxis.data中的数据全部替换为0
        qps_chartData.xAxis.data = qps_chartData.xAxis.data.map(() => '0:00:00');

        // 等待DOM加载完成
        setTimeout(() => {
            qps_chart?.setOption(qps_chartData);
        }, 0);

        const resizeHandler = () => {
            qps_chart?.resize();
        };

        window.addEventListener('resize', resizeHandler);

        async function stream() {
            const response = await getQpsChart() as any;
            for await (const data of response) {

                qps_chartData.xAxis.data.shift();
                qps_chartData.xAxis.data.push(data.now);
                qps_chartData.series[0].data.shift();
                qps_chartData.series[0].data.push(data.qps);
                setQps(data.qps);
                setNetWork({
                    upload: data.upload,
                    download: data.download
                })
                qps_chart?.setOption(qps_chartData);
            }
        }

        stream();
        loadDashboard();

        // 定时器
        const timer = setInterval(() => {
            stream();
        }, 10000);

        return () => {
            clearInterval(timer);
            window.removeEventListener('resize', resizeHandler);
        }
    }, []);

    function loadDashboard() {
        GetDashboard()
            .then(res => {
                setData(res.data)
            })
    }

    return (
        <div style={{ padding: '20px' }}>
            <Card style={{
                float: 'right',
                width: '280px',
                height: '260px',
            }}>
                <div style={{
                    // 显示右边
                    display: 'flex',
                    justifyContent: 'flex-end'
                }}>
                    <span>
                        <Tooltip title='实时QPS'>
                            <Tag>
                                {qps}
                            </Tag>
                        </Tooltip>
                        <Tag>
                            {bytesToSize(netWork.upload)}/s
                        </Tag>
                        <Tag>
                            {bytesToSize(netWork.download)}/s
                        </Tag>
                    </span>
                </div>
                <div
                    id='qps_chart'
                    style={{
                        width: '230px',
                        height: '200px',
                    }}
                >
                </div>
            </Card>
            <Row
                style={{
                    width: 'calc(100% - 280px)',
                }}
                gutter={16}>
                <Col style={{
                    height: '120px',
                }} span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="访问总数" value={data.total} />
                    </Card>
                </Col>
                <Col span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="访问成功总数" value={data.success} />
                    </Card>
                </Col>
                <Col span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="访问失败总数" value={data.fail} />
                    </Card>
                </Col>
            </Row>
            <Row style={{
                marginTop: '20px',
                width: 'calc(100% - 280px)',
            }} gutter={16}>
                <Col span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="今日总数" value={data.todayTotal} />
                    </Card>
                </Col>
                <Col span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="今日成功总数" value={data.todaySuccess} />
                    </Card>
                </Col>
                <Col span={8}>
                    <Card style={{
                        height: '120px',
                    }} hoverable>
                        <Statistic title="今日失败总数" value={data.todayFail} />
                    </Card>
                </Col>
            </Row>
            <Row gutter={16} style={{ marginTop: '20px' }}>
                <Col span={24}>
                    <Card title="流量来源">
                        <div style={{
                            justifyContent: 'space-between',
                            lineHeight: '50px',
                            overflowX: 'hidden',
                            overflowY: 'auto',
                            userSelect: 'none',
                            scrollbarWidth: 'thin',
                            scrollbarColor: 'transparent transparent',
                            display: 'flex',
                            height: '500px',
                            overflow: 'auto',
                            padding: '10px'

                        }}>
                            <Map isGlobal={false} data={locationData} />
                            <MapList isGlobal={false} data={locationData} />
                        </div>
                    </Card>
                </Col>
            </Row>
        </div>
    );
};

export default Dashboard;