import { useEffect, useState } from 'react';
import { displaydata } from '../../service/RequestSourceService'
import * as echarts from 'echarts';
import { Card, Pagination, Table, Toast } from '@douyinfe/semi-ui';

export default function RequestSource() {

    const columns = [
        {
            title: 'ip',
            dataIndex: 'ip'
        },
        {
            title: '请求域名',
            dataIndex: 'host',
        },
        {
            title: '归属地',
            dataIndex: 'homeAddress',
        },
        {
            title: '请求数量',
            dataIndex: 'requestCount',
        },
        {
            title: '请求平台',
            dataIndex: 'platform',
        },
        {
            title: '创建时间',
            dataIndex: 'createdTime',
            render: (text: any) => {
                // 转换 yyyy-MM-dd HH:mm:ss
                const date = new Date(text);
                const year = date.getFullYear();
                const month = date.getMonth() + 1;
                const day = date.getDate();
                return `${year}-${month}-${day}`;
            }
        },
    ];
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);
    const [data, setData] = useState([] as any);
    const [total, setTotal] = useState(0);

    async function getDisplayData() {
        try {
            let dayOptions = {
                xAxis: {
                    type: 'category',
                    data: [] as any
                },
                yAxis: {
                    type: 'value',
                },
                tooltip: {
                    trigger: 'axis',
                    formatter: '请求IP数量{c0}'
                },
                series: [
                    {
                        name: 'ip数量',
                        stack: 'Total',
                        data: [] as any,
                        type: 'line'
                    }
                ]
            };


            const data = await displaydata(page,pageSize) as any;

            const value = data.dayCountDtos as any;
            // 遍历value
            for (let i = 0; i < value.length; i++) {
                dayOptions.xAxis.data.push(value[i].day as any);
                dayOptions.series[0].data.push(value[i].count);
            }

            const displayDay = document.getElementById('display-day');
            const displayDayChat = echarts.init(displayDay as any);
            displayDayChat.setOption(dayOptions);

            setData(data.items);
            setTotal(data.total);
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
            height: '350px',
        }}>
            <div id='display-day' style={{
                height: '300px',
            }}>
            </div>
        </Card>
        
        <Table columns={columns} dataSource={data} pagination={false} />
        <Pagination onPageChange={(currentPage)=>{
            setPage(currentPage);
            getDisplayData();
        }} total={
            Math.ceil(total / pageSize)
        } style={{ marginBottom: 12 }}></Pagination>
    </div>)
}