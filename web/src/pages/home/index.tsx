import { useEffect, useState } from "react";
import { Col, Row } from '@douyinfe/semi-ui';
import { stream } from '../../service/NetWorkService';
import * as echarts from 'echarts';

export default function Home() {
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
                            let unit = 'B';
                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'KB';
                            }

                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'MB';
                            }

                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'GB';
                            }

                            return value.toFixed(2) + unit;
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
                            let unit = 'B';
                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'KB';
                            }

                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'MB';
                            }

                            if (value > 1024) {
                                value = value / 1024;
                                unit = 'GB';
                            }

                            return value.toFixed(2) + unit;
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
                    option && myChart.setOption(option);

                    myChart.resize();

                }
            } catch (error) {
                console.error('获取数据失败:', error);
            }
        };

        fetchData();

        // 设置定时器，每隔一段时间调用获取数据的函数
        const intervalId = setInterval(fetchData, 5900); // 每5秒获取一次数据

        // 组件卸载时清除定时器
        return () => {
            clearInterval(intervalId);
        };
    }, []); // 空依赖数组意味着这个effect只会在组件挂载时执行一次


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
                <div id='network' style={{
                    height: '300px',
                }}>
                </div>
            </Row>
        </div>
    );
}