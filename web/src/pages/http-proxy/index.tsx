import { Button, Dropdown, Table, Tag } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { IconMore } from '@douyinfe/semi-icons';
import { GetApiServiceList, ServiceStats, DeleteApiService, StartService, StopService, RestartService } from "../../services/ApiServiceService";
import CreateHttpProxy from "./features/CreateHttpProxy";
import UpdateHttpProxy from "./features/UpdateHttpProxy";

export default function HttpProxy() {

    const columns = [
        {
            title: '端口',
            dataIndex: 'listen',
            key: 'listen',
            render: (text: string) => {
                return <span style={{
                    fontWeight: '600',
                    color: 'var(--semi-color-primary)',
                    cursor: 'pointer',

                }}>{text}</span>
            }
        },
        {
            title: '启动',
            dataIndex: 'enable',
            key: 'enable',
            render: (text: boolean) => {
                return <span style={{
                    color: 'var(--semi-color-text-2)',
                    fontWeight: '600',
                }}>{text ? '是' : '否'}</span>
            }
        },
        {
            title: '状态',
            dataIndex: 'state',
            key: 'state',
            render: (_: any, item: any) => {
                // 判断是否有状态 stats
                if (stats[item.id]) {
                    return <Tag
                        // 绿色
                        color="green"
                        size='large'
                        shape='circle'
                        type='solid'
                    >在线</Tag>
                } else {
                    return <Tag
                        color="red"
                        size='large'
                        shape='circle'
                        type='solid'
                    >离线</Tag>
                }
            }
        },
        {
            title: '操作',
            dataIndex: 'action',
            key: 'action',
            render: (_: any, item: any) => {

                const isStats = stats[item.id];

                return <Dropdown
                    render={
                        <Dropdown.Menu>
                            <Dropdown.Item onClick={()=>{
                                setUpdateHttpProxyData(item);
                                setUpdateHttpProxyVisible(true);
                            }}>
                                编辑
                            </Dropdown.Item>
                            {
                                isStats ? <>
                                    <Dropdown.Item onClick={() => stopService(item.id)}>
                                        停止
                                    </Dropdown.Item>
                                    <Dropdown.Item onClick={() => restartService(item.id)}>
                                        重启
                                    </Dropdown.Item>
                                </> : <Dropdown.Item onClick={() => startService(item.id)}>
                                    启动
                                </Dropdown.Item>
                            }
                            <Dropdown.Item onClick={() => deleteService(item.id)}>
                                删除
                            </Dropdown.Item>
                        </Dropdown.Menu>
                    }
                ><IconMore />
                </Dropdown>;
            }
        },
    ]
    const [createHttpProxyVisible, setCreateHttpProxyVisible] = useState(false);
    const [updateHttpProxyVisible, setUpdateHttpProxyVisible] = useState(false);
    const [updateHttpProxyData, setUpdateHttpProxyData] = useState({} as any);
    const [data, setData] = useState([]);
    const [total, setTotal] = useState(0);
    const [loading, setLoading] = useState(false);
    const [stats, setStats] = useState({} as any);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });

    function LoadData() {
        setLoading(true);
        GetApiServiceList(input.page, input.pageSize).then((res) => {
            setData(res.data.items);
            setTotal(res.data.total);
            setLoading(false);
        });

    }

    useEffect(() => {
        loadStats();
    }, [data])

    function loadStats() {
        // 获取data的id
        const ids = data.map((item: any) => item.id);

        ServiceStats(ids).then((res) => {
            setStats(res.data)
        });
    }

    useEffect(() => {
        LoadData();
    }, [input]);

    function deleteService(id: string) {
        DeleteApiService(id)
            .then(() => {
                LoadData();
            });
    }

    function startService(id: string) {
        StartService(id).then(() => {
            LoadData();
        });
    }

    function stopService(id: string) {
        StopService(id).then(() => {
            LoadData();
        });
    }

    function restartService(id: string) {
        RestartService(id).then(() => {
            LoadData();
        })
    }



    return (
        <>
            <div style={{
                display: 'flex',
                justifyContent: 'space-between',
                alignItems: 'center',
                marginBottom: '20px',
                backgroundColor: 'var(--semi-color-bg-0)',
                padding: '5px',
                borderRadius: '10px',
            }}>
                <span style={{
                    color: 'var(--semi-color-text-0)',
                    fontSize: '24px',
                    fontWeight: '600'
                }}>共{total}个站点</span>
                <Button
                    onClick={() => setCreateHttpProxyVisible(true)}
                    style={{
                        color: 'var(--semi-color-primary)',
                        border: '1px solid var(--semi-color-primary)',
                        borderRadius: '5px',
                    }}
                >新增站点</Button>
            </div>
            <Table loading={loading} columns={columns} dataSource={data} pagination={
                {
                    total,
                    currentPage: input.page,
                    pageSize: input.pageSize,
                    onChange: (page, pageSize) => {
                        setInput({
                            ...input,
                            page,
                            pageSize,
                        });
                    }
                }
            } />
            <CreateHttpProxy visible={createHttpProxyVisible} onClose={() => setCreateHttpProxyVisible(false)} onOk={() => {
                setCreateHttpProxyVisible(false);
                LoadData();
            }} />
            <UpdateHttpProxy values={updateHttpProxyData} visible={updateHttpProxyVisible} onClose={()=> setUpdateHttpProxyVisible(false)}
                onOk={()=>{
                    setUpdateHttpProxyVisible(false);
                    LoadData();
                }}
                />
        </>
    );
}