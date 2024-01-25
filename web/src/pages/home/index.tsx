import { Panel } from '../../service/RequestLogService';
import { useEffect, useState } from "react";
import { VChart } from "@visactor/react-vchart";
import { Col, Row } from '@douyinfe/semi-ui';
import { stream } from '../../service/NetWorkService';
import { debounce } from '../../utils/utils'
import * as echarts from 'echarts';

export default function Home() {
    const [hours, setHours] = useState<number>(24);
    const [requestSize, setRequestSize] = useState<any>({
        spec: {

            type: 'area',
            data: {
                values: [
                ]
            },
            title: {
                visible: true,
                text: '最近24小时记录'
            },
            xField: 'time',
            yField: 'value'
        }
    });

    const [requestStatusCode, setRequestStatusCode] = useState<any>({
        spec: {
            type: 'bar',
            data: {
                values: [
                ]
            }
        }
    })
    const [requestPath, setRequestPath] = useState<any>({
        spec: {
            type: 'pie',
            data: [
                {
                    id: 'id0',
                    values: [
                    ]
                }
            ],
            outerRadius: 0.8,
            valueField: 'value',
            categoryField: 'type',
            title: {
                visible: true,
                text: '路由热度排名'
            },
            legends: {
                visible: true,
                orient: 'left'
            },
            label: {
                visible: true
            },
            tooltip: {
                mark: {
                    content: [
                        {
                            key: (datum: { [x: string]: any; }) => datum['type'],
                            value: (datum: { [x: string]: string; }) => datum['value'] + '%'
                        }
                    ]
                }
            }
        }
    })

    async function loadingPanel() {
        const response = await Panel(hours) as any;
        setRequestSize({
            spec: {
                type: 'area',
                data: {
                    values: response.data.requestSize
                },
                title: {
                    visible: true,
                    text: '最近24小时记录'
                },
                xField: 'time',
                yField: 'value'
            }
        });
        setRequestPath({
            spec: {
                type: 'pie',
                data: [
                    {
                        id: 'id0',
                        values: response.data.requestPath
                    }
                ],
                outerRadius: 0.8,
                valueField: 'value',
                categoryField: 'type',
                title: {
                    visible: true,
                    text: '路由热度排名'
                },
                legends: {
                    visible: true,
                    orient: 'left'
                },
                label: {
                    visible: true
                },
                tooltip: {
                    mark: {
                        content: [
                            {
                                key: (datum: { [x: string]: any; }) => datum['type'],
                                value: (datum: { [x: string]: string; }) => datum['value'] + '%'
                            }
                        ]
                    }
                }
            }
        });


        setRequestStatusCode({
            spec: {
                type: 'bar',
                data: {
                    values: response.data.requestStatusCode
                },
                xField: ['type', 'country'],
                yField: 'value',
                seriesField: 'country',
                animationAppear: {
                    duration: 1500,
                    easing: 'linear'
                },
                legends: [{ visible: true, position: 'middle', orient: 'bottom' }],
                // @ts-ignore
                animationAppear: {
                    duration: 500,
                    oneByOne: true
                },
                axes: [
                    {
                        orient: 'left',
                    }
                ]
            }
        });
    }


    useEffect(() => {
        const loadingPanelFunction = debounce(loadingPanel, 500, true);
        loadingPanelFunction()
    }, [hours]);

    useEffect(() => {
    }, []);

    useEffect(() => {
        var chartDom = document.getElementById('network')!;
        var myChart = echarts.init(chartDom);
        var option: any;

        option = {
            title: {
                text: '实时流量监控'
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
                }
            } catch (error) {
                console.error('获取数据失败:', error);
            }
        };

        fetchData();

        // 设置定时器，每隔一段时间调用获取数据的函数
        const intervalId = setInterval(fetchData, 6000); // 每5秒获取一次数据

        // 组件卸载时清除定时器
        return () => {
            clearInterval(intervalId);
        };
    }, []); // 空依赖数组意味着这个effect只会在组件挂载时执行一次


    return (
        <div className="App">

            <Row>
                <div id='network' style={{
                    width: '100%',
                    height: '300px',

                }}>
                </div>
            </Row>
            <Row>
                <Col span={12}><div className="col-content">
                    <VChart
                        spec={{
                            height: 350,
                            ...requestSize.spec,
                        }}
                    /></div></Col>
                <Col span={12}><div className="col-content">
                    <VChart
                        spec={{
                            height: 350,
                            ...requestPath.spec,
                        }} /></div></Col>
            </Row>

            <Row style={{
                marginTop: '20px',
            }}>
                <div style={{
                    fontSize: '20px',
                    fontWeight: 'bold',
                    marginBottom: '20px',
                }}>
                    7天请求状态码分布
                </div>
                <VChart
                    spec={{
                        ...requestStatusCode.spec,
                    }} />
            </Row>
        </div>
    );
}