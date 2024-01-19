import { Panel } from '../../service/RequestLogService';
import { useEffect, useState } from "react";
import { VChart } from "@visactor/react-vchart";
import { Col, Row } from '@douyinfe/semi-ui';

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
    }

    useEffect(() => {
        loadingPanel();
    }, [hours]);

    return (
        <div className="App">

            <Row>
                <Col span={12}><div className="col-content">
                    <VChart
                        spec={{
                            height: 200,
                            ...requestSize.spec,
                        }}
                    /></div></Col>
                <Col span={12}><div className="col-content">
                    <VChart
                        spec={{
                            height: 200,
                            ...requestPath.spec,
                        }} /></div></Col>
            </Row>
        </div>
    );
}