import { Panel } from '../../service/RequestLogService';
import { useEffect, useState } from "react";
import { VChart } from "@visactor/react-vchart";
import { Col, Row } from '@douyinfe/semi-ui';
import { stream } from '../../service/NetWorkService';

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
        loadingPanel();
        onstream();
    }, [hours]);

    async function onstream(){
        const response = await stream();

        for await(let item of response){
            console.log(item);
        }
    }

    return (
        <div className="App">

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