import { useEffect } from 'react';
import { displaydata } from '../../service/RequestSourceService'
import * as echarts from 'echarts';
import { Card, Toast } from '@douyinfe/semi-ui';

export default function RequestSource() {



    async function getDisplayData() {
        try {
            let dayOptions = {
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

            let addressOptions = {
                tooltip: {
                  trigger: 'item'
                },
                legend: {
                  top: '5%',
                  left: 'center'
                },
                series: [
                  {
                    name: '七天内请求IP数量',
                    type: 'pie',
                    radius: ['40%', '70%'],
                    avoidLabelOverlap: false,
                    itemStyle: {
                      borderRadius: 10,
                      borderColor: '#fff',
                      borderWidth: 2
                    },
                    label: {
                      show: false,
                      position: 'center'
                    },
                    emphasis: {
                      label: {
                        show: true,
                        fontSize: 40,
                        fontWeight: 'bold'
                      }
                    },
                    labelLine: {
                      show: false
                    },
                    data: [
                    ] as any
                  }
                ]
              };

            const data = await displaydata() as any;

            const value = data.dayCountDtos as any;
            // 遍历value
            for (let i = 0; i < value.length; i++) {
                dayOptions.xAxis.data.push(value[i].day as any);
                dayOptions.series[0].data.push(value[i].count);
            }

            const displayDay = document.getElementById('display-day');
            const displayDayChat = echarts.init(displayDay as any);
            displayDayChat.setOption(dayOptions);


            const addressValue = data.addressDtos as any;

            for (let i = 0; i < addressValue.length; i++) {
                addressOptions.series[0].data.push({
                    value: addressValue[i].count,
                    name: addressValue[i].homeAddress
                }) 
            }

            const displayAddress = document.getElementById('display-address');
            const displayAddressChat = echarts.init(displayAddress as any);
            displayAddressChat.setOption(addressOptions);



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
            <div id='display-day' style={{
                height: '300px',
            }}>
            </div>
        </Card>
        <Card style={{
            margin: '10px 0',
            width: '100%',
            height: '400px',
        }}>
            <div id='display-address' style={{
                height: '300px',
            }}>
            </div>
        </Card>
    </div>)
}