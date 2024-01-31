import { useEffect, useState } from "react";
import { Card, Col, Row } from '@douyinfe/semi-ui';
import { stream } from '../../service/NetWorkService';
import * as echarts from 'echarts';

export default function Home() {
    const [totalRequestCount, setTotalRequestCount] = useState(0);
    const [totalErrorCount, setTotalErrorCount] = useState(0);
    const [currentRequestCount, setCurrentRequestCount] = useState(0);
    const [currentErrorCount, setCurrentErrorCount] = useState(0);
    const [readRate, setReadRate] = useState("");
    const [writeRate, setWriteRate] = useState("");
    const [totalRead, setTotalRead] = useState("");
    const [totalWrite, setTotalWrite] = useState("");

    useEffect(() => {
        var chartDom = document.getElementById('network')!;
        var myChart = echarts.init(chartDom);
        var option: any;

        option = {
            title: {
                text: '实时流量'
            },
            tooltip: {
                trigger: 'axis'
            },
            legend: {
                data: ['Email', 'Search Engine']
            },
            grid: {
                left: '3%',
                right: '4%',
                bottom: '3%',
                containLabel: true
            },
            toolbox: {
                feature: {
                    saveAsImage: {}
                }
            },
            xAxis: {
                type: 'category',
                boundaryGap: false,
                data: []
            },
            yAxis: {
                type: 'value'
            },
            series: [
                {
                    name: '下载宽带',
                    type: 'line',
                    stack: 'Total',
                    data: [],
                    label: {
                        show: true,
                        position: 'top',
                        textStyle: {
                            fontSize: 14
                        },
                        formatter: function (params: any) {
                            // 将字节转换KB MB GB
                            let value = params.value;

                            return formatBytes(value, 2);
                        }
                    }
                },
                {
                    name: '上传宽带',
                    type: 'line',
                    stack: 'Total',
                    data: [],
                    label: {
                        show: true,
                        position: 'top',
                        textStyle: {
                            fontSize: 14
                        },
                        formatter: function (params: any) {
                            // 将字节转换KB MB GB
                            let value = params.value;

                            return formatBytes(value, 2);
                        }
                    }
                }
            ]
        };

        // 定义获取数据的函数
        const fetchData = async () => {
            try {
                const response = await stream() as any;
                for await (const chunk of response) {
                    if (option.series[0].data.length > 20) {
                        option.series[0].data.shift();
                        option.series[1].data.shift();
                        option.xAxis.data.shift();
                    }

                    option.xAxis.data.push(chunk.Time);
                    option.series[0].data.push(chunk.Sent);
                    option.series[1].data.push(chunk.Received);

                    setCurrentErrorCount(chunk.CurrentErrorCount);
                    setCurrentRequestCount(chunk.CurrentRequestCount);
                    setTotalErrorCount(chunk.TotalErrorCount);
                    setTotalRequestCount(chunk.TotalRequestCount);

                    setReadRate(formatBytes(chunk.ReadRate));
                    setWriteRate(formatBytes(chunk.WriteRate));
                    setTotalRead(formatBytes(chunk.TotalRead));
                    setTotalWrite(formatBytes(chunk.TotalWrite));


                    option && myChart.setOption(option);

                    myChart.resize();

                }
            } catch (error) {
                console.error('获取数据失败:', error);
            }
        };

        fetchData();

        // 设置定时器，每隔一段时间调用获取数据的函数
        const intervalId = setInterval(fetchData, 10900); // 每5秒获取一次数据

        // 组件卸载时清除定时器
        return () => {
            clearInterval(intervalId);
        };
    }, []); // 空依赖数组意味着这个effect只会在组件挂载时执行一次

    function formatBytes(bytes: number, decimals = 2) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const dm = decimals < 0 ? 0 : decimals;
        const sizes = ['Bytes', 'KB', 'MB', 'GB', 'TB', 'PB', 'EB', 'ZB', 'YB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(dm)) + ' ' + sizes[i];
    }


    return (
        <div className="App" style={{
            width: '100%',
        }}>
            <h2 style={{
                textAlign: 'center',
                margin: '10px 0',
                fontSize: '20px',
                fontWeight: 'bold'
            }}>
                数据面板
            </h2>
            <Row>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        总请求数：{totalRequestCount}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        总错误数：{totalErrorCount}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        当天请求数：{currentRequestCount}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        当天错误数：{currentErrorCount}
                    </Card>
                </Col>
            </Row>
            <Row>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        入口流量总计：{totalRead}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        出口流量总计：{totalWrite}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        当天入口流量：{readRate}
                    </Card>
                </Col>
                <Col span={6}>
                    <Card style={{ maxWidth: 360, margin: 8 }} >
                        当天出口流量：{writeRate}
                    </Card>
                </Col>
            </Row>
            <Row>
                <div id='network' style={{
                    height: '300px',
                }}>
                </div>
            </Row>
        </div>
    );
}