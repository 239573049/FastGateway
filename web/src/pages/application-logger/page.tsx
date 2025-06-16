import { getApplicationLogger, deleteOldLogs } from '@/services/ApplicationLoggerService';
import { Tooltip } from '@lobehub/ui';
import { Table, Button, message } from 'antd';
import { useEffect, useState } from 'react';

export default function ApplicationLoggerPage() {
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState<any[]>([]);
    const [deleteLoading, setDeleteLoading] = useState(false);

    function loadData() {
        setLoading(true);
        getApplicationLogger(input.page, input.pageSize)
            .then((res) => {
                const { items, total } = res.data;
                setData([...items]);
                setTotal(total);
            }).finally(() => {
                setLoading(false);
            });
    }

    const handleDeleteOldLogs = async () => {
        setDeleteLoading(true);
        try {
            const result = await deleteOldLogs();
            message.success(result.message || '删除成功');
            loadData(); // 重新加载数据
        } catch (error) {
            message.error('删除失败，请重试');
        } finally {
            setDeleteLoading(false);
        }
    };

    useEffect(() => {
        loadData();
    }, [input]);

    return (
        <>
            <div style={{ marginBottom: 16 }}>
                <Button 
                    type="primary" 
                    danger 
                    onClick={handleDeleteOldLogs}
                    loading={deleteLoading}
                >
                    删除一个月前的日志
                </Button>
            </div>
            <Table
                scroll={{ y: 65 * 9 }}
                loading={loading}
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
                        render: (text) => {
                            return text + 'ms';
                        }
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
                            return <>
                            <Tooltip title={text}>
                                <div style={{
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap',
                                }}>{text}</div>
                            </Tooltip>
                            </>
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