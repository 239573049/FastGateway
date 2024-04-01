import { Button, Dropdown, Notification, Table, Tag } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { Cert } from "../../module";
import CreateCertPage from "./features/CreateCert";
import { IconMore } from '@douyinfe/semi-icons';
import { Apply, DeleteCert, GetCertList } from "../../services/CertService";

export default function CertPage() {
    const [loading, setLoading] = useState(false);
    const [data, setData] = useState([]);
    const [total, setTotal] = useState(0);
    const [createCertVisible, setCreateCertVisible] = useState(false);
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
    });

    const columns = [
        {
            title: '类型',
            dataIndex: 'autoRenew',
            key: 'autoRenew',
            render: (text: any, record: any) => (
                <div>
                    {record.autoRenew ? '自动续期' : '上传证书'}
                </div>
            )
        },
        {
            title: '域名',
            dataIndex: 'domains',
            key: 'domains',
            render: (text: any, record: any) => (
                <div>
                    {record.domains.join(',')}
                </div>
            )
        },
        {
            title: '颁发机构',
            dataIndex: 'issuer',
            key: 'issuer'
        },
        {
            title: '证书状态',
            dataIndex: 'renewStats',
            key: 'renewStats',
            render: (text: any, record: Cert) => (
                <Tag
                    color='blue'
                    size='large'
                    shape='circle'
                >
                    {record.renewStats === 1 ? '成功' : record.renewStats === 2 ? '失败' : '未续期'}
                </Tag>
            )
        },
        {
            title: '最近续期时间',
            dataIndex: 'renewTime',
            key: 'renewTime',
            render: (text: any, record: any) => (
                <div>
                    {record.renewTime}
                </div>
            )
        },
        {
            title: '有效期',
            dataIndex: 'notAfter',
            key: 'notAfter',
            render: (text: any, record: any) => (
                <div>
                    {record.notAfter}
                </div>
            )
        },
        {
            title: '操作',
            key: 'action',
            render: (text: any, record: any) => (
                <Dropdown
                    render={
                        <Dropdown.Menu>
                            <Dropdown.Item>
                                编辑
                            </Dropdown.Item>
                            <Dropdown.Item onClick={() => applyCert(record.id)}>
                                申请/续期
                            </Dropdown.Item>
                            <Dropdown.Item onClick={() => del(record.id)}>
                                删除
                            </Dropdown.Item>
                        </Dropdown.Menu>
                    }
                ><IconMore />
                </Dropdown>
            )
        }
    ]

    async function applyCert(id: string) {
        try {
            setLoading(true)
            await Apply(id);
            loadData();
            Notification.success({
                title: '申请成功'
            });
        } catch (e) {
            Notification.error({
                title: '申请失败'
            });
        }
    }

    function del(id: string) {
        DeleteCert(id).then(() => {
            Notification.success({
                title: '删除成功'
            });
            loadData();
        }).catch(() => {
            Notification.error({
                title: '删除失败'
            });
        });
    }

    function loadData() {
        setLoading(true);
        GetCertList(input).then((res) => {
            setData(res.data.items);
            setTotal(res.data.total);
        }).finally(() => {
            setLoading(false);
        });
    }

    useEffect(() => {
        loadData()
    }, [input]);

    return <>

        <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            marginBottom: '20px',
            backgroundColor: 'var(--semi-color-bg-0)',
            padding: '5px',
            borderRadius: '10px',
        }}>
            <Button
                onClick={() => setCreateCertVisible(true)}
                style={{
                    color: 'var(--semi-color-primary)',
                    border: '1px solid var(--semi-color-primary)',
                    borderRadius: '5px',
                }}
            >新增证书</Button>
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

        <CreateCertPage visible={createCertVisible} onCancel={() => {
            setCreateCertVisible(false);
        }} onOk={() => {
            loadData();
            setCreateCertVisible(false);
        }} />
    </>
}