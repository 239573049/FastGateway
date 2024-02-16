import { useEffect } from 'react';
import { displaydata } from '../../service/RequestSourceService'
import * as echarts from 'echarts';
import { Card, Toast } from '@douyinfe/semi-ui';

export default function RequestSource() {



    async function getDisplayData() {
        try {
            let option = {
                xAxis: {
                    type: 'category',
                    data: [] as any
                },
                yAxis: {
                    type: 'value'
                },
                series: [
                    {
                        data: [] as any,
                        type: 'bar'
                    }
                ]
            };

            const value = (await displaydata() )as any;
            // 遍历value
            for (let i = 0; i < value.length; i++) {
                option.xAxis.data.push(value[i].key as any);
                option.series[0].data.push(value[i].count);
            }

            const displayData = document.getElementById('display-data');
            const myChart = echarts.init(displayData as any);
            myChart.setOption(option);

        } catch (error) {
            Toast.error('获取数据失败');
            console.log(error);
        }
    }

    useEffect(() => {
        getDisplayData();
    }, [])

    return (<div>
        <Card style={{
            margin: '10px 0',
            width: '100%',
            height: '400px',
        }}>
            <div id='display-data' style={{
                height: '300px',
            }}>
            </div>
        </Card>
    </div>)
}