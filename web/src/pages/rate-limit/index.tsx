import { Button, Dropdown, Notification, Table, Tag } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { DeleteRateLimit, GetRateLimit } from "../../services/RateLimitService";
import CreateRateLimitPage from "./features/CreateRateLimit";
import UpdateRateLimitPage from "./features/UpdateRateLimit";


export default function RateLimit(){
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState([]);
    const [total, setTotal] = useState(0);
    const [updateVisible, setUpdateVisible] = useState(false);
    const [updateData, setUpdateData] = useState({} as any);
    const [createVisible, setCreateVisible] = useState(false);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });

    const columns = [
        {
            title: '名称',
            dataIndex: 'name',
            key: 'name',
        },
        {
            title: '是否启用',
            dataIndex: 'enable',
            key: 'enable',
            render: (text: boolean) => {
                return text ? <Tag color='green'>启用</Tag> : <Tag color="red">禁用</Tag>;
            }
        },
        {
            title: '操作',
            key: 'action',
            render: (_text: any, item: any) => (
                <Dropdown
                    render={
                        <Dropdown.Menu>
                            <Dropdown.Item>启用/禁用</Dropdown.Item>
                            <Dropdown.Item onClick={()=>{
                                del(item.name);
                            }}>删除</Dropdown.Item>
                            <Dropdown.Item onClick={()=>{
                                setUpdateData(item);
                                setUpdateVisible(true);
                            }}>编辑</Dropdown.Item>
                        </Dropdown.Menu>
                    }
                >
                    <Button>操作</Button>
                </Dropdown>
            )
        }
    ]

    function del(name: string){
        DeleteRateLimit(name).then(() => {
            setInput({
                ...input,
                page: 1,
            });
            Notification.success({
                title: '删除成功',
            });
        });
    }
    
    function load(){
        GetRateLimit(input.page, input.pageSize).then(res => {
            setData(res.items);
            setTotal(res.total);
        }).finally(() => {
            setLoading(false);
        });
    }

    useEffect(() => {
        load();
    }, [input]);

    return(
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
                }}>
                   限流管理
                </span>
                <Button
                    onClick={() => setCreateVisible(true)}
                    style={{
                        color: 'var(--semi-color-primary)',
                        border: '1px solid var(--semi-color-primary)',
                        borderRadius: '5px',
                    }}
                >新增限流策略</Button>
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
            <CreateRateLimitPage visible={createVisible} onClose={() => setCreateVisible(false)} onOk={() => {
                setInput({
                    ...input,
                    page: 1,
                });
                setCreateVisible(false);
            }} />
            <UpdateRateLimitPage value={updateData} visible={updateVisible} onClose={() => setUpdateVisible(false)} onOk={() => {
                setInput({
                    ...input,
                    page: 1,
                });
                setUpdateVisible(false);
            }} />
        </>)
}