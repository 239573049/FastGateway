import { getApplicationLogger } from '@/services/ApplicationLoggerService';
import { Table } from 'antd';
import { useEffect, useState } from 'react';

export default function ApplicationLoggerPage() {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });
    const [total, setTotal] = useState(0);
    const [data, setData] = useState([]);

    function loadData() {
        getApplicationLogger(input.page, input.pageSize)
            .then((res) => {
                const { items, total } = res.data;
                setData(items);
                setTotal(total);
            })
    }

    useEffect(() => {
        loadData();
    }, [input]);

    return (
        <>
            <Table
                columns={[
                    {
                        title: '请求时间',
                        dataIndex: 'requestTime',
                        key: 'requestTime',
                    },
                    {
                        title: '请求路径',
                        dataIndex: 'path',
                        key: 'path',
                    },
                    {
                        title: '请求方法',
                        dataIndex: 'method',
                        key: 'method',
                    },
                    {
                        title: '请求状态',
                        dataIndex: 'statusCode',
                        key: 'statusCode',
                    },
                    {
                        title: '请求IP',
                        dataIndex: 'ip',
                        key: 'ip',
                    },
                    {
                        title: '请求耗时',
                        dataIndex: 'elapsed',
                        key: 'elapsed',
                    },
                    {
                        title: '请求域名',
                        dataIndex: 'domain',
                        key: 'domain',
                    },
                    {
                        title: 'User Agent',
                        dataIndex: 'userAgent',
                        key: 'userAgent',
                        render: (text) => {
                            return <div style={{
                                width: '200px',
                                overflow: 'hidden',
                                textOverflow: 'ellipsis',
                                whiteSpace: 'nowrap',
                            }}>{text}</div>
                        }
                    },
                    {
                        title: '是否成功',
                        dataIndex: 'success',
                        key: 'success',
                        render: (text) => {
                            return text ? '成功' : '失败';
                        }
                    },
                    {
                        title: '请求平台',
                        dataIndex: 'platform',
                        key: 'platform',
                    },
                    {
                        title: '请求国家',
                        dataIndex: 'country',
                        key: 'country',
                    },
                    {
                        title: '请求地区',
                        dataIndex: 'region',
                        key: 'region',
                    },
                ]}
                dataSource={data}
                pagination={{
                    total,
                    current: input.page,
                    pageSize: input.pageSize,
                    onChange: (page) => {
                        setInput({
                            ...input,
                            page,
                        });
                    },
                    onShowSizeChange: (current, pageSize) => {
                        setInput({
                            ...input,
                            page: current,
                            pageSize,
                        });
                    },
                }}

            />
        </>
    )
}