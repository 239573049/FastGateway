import { Button, Input, Pagination, Table, Toast, TimePicker, DatePicker } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { RequestLog } from '../../service/RequestLogService'


export default function Logger() {

    const [total, setTotal] = useState<number>(0);
    const [data, setData] = useState<any[]>([]);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 20,
        keyword: '',
        startTime: null as any,
        endTime: null as any,
    });

    const columns = [
        {
            title: '请求地址',
            dataIndex: 'path',
        },
        {
            title: '请求方式',
            dataIndex: 'method',
        },
        {
            title: 'IP',
            dataIndex: 'ip',
        },
        {
            title: '请求状态',
            dataIndex: 'statusCode',
        },
        {
            title: '请求耗时(ms)',
            dataIndex: 'executionDuration'
        },
        {
            title: '浏览器信息',
            dataIndex: 'browserInfo',
            // 超出隐藏
            ellipsis: true,

        },
        {
            title: '扩展字段',
            dataIndex: 'extraProperties',
        },
    ];

    useEffect(() => {
        loadingData();
    }, [input]);

    async function loadingData() {
        try {
            const data = await RequestLog(input) as any;
            setData(data.items);
            setTotal(data.total);
        } catch (error) {
            Toast.error('加载失败' + error);
        }
    }

    return (
        <div>
            <div style={{
                display: 'flex',
                marginBottom: '20px'

            }}>
                <Input
                    size='large'
                    placeholder="请输入搜索关键词"
                    value={input.keyword}
                    onChange={(e) => {
                        setInput({
                            ...input,
                            keyword: e
                        })
                    }}
                    style={{
                        width: '200px',
                        marginRight: '20px',
                    }}
                />
                <DatePicker
                    size='large'
                    placeholder="开始时间"
                    style={{
                        width: '200px',
                        marginRight: '20px',
                    }}
                    value={input.startTime}
                    onChange={(value) => {
                        setInput({
                            ...input,
                            startTime: value
                        })
                    }}
                />
                <DatePicker
                    size='large'
                    placeholder="结束时间"
                    style={{
                        width: '200px',
                        marginRight: '20px',
                    }}
                    value={input.endTime}
                    onChange={(value) => {
                        setInput({
                            ...input,
                            endTime: value
                        })
                    }}
                />
                <Button size='large' type="primary" block style={{ width: '100px' }}>搜索</Button>
            </div>

            <Table
                sticky={{ top: 0 }} style={{
                    height: 'calc(100vh - 300px)',
                    overflow: 'auto',
                    padding: '10px',
                }} columns={columns} dataSource={data} pagination={false} />
            <Pagination total={total} style={{ marginBottom: 12 }} pageSize={input.pageSize} currentPage={input.page} onPageChange={(page) => {
                setInput({
                    ...input,
                    page: page
                })
            }} onPageSizeChange={(pageSize) => {
                setInput({
                    ...input,
                    pageSize: pageSize
                })
            }}></Pagination>


        </div>
    )
}