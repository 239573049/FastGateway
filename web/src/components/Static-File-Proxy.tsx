import { Button, Collapse, List, Tag, Toast } from "@douyinfe/semi-ui";
import { StaticFileProxyEntity } from "../index.d";
import { useEffect, useState } from "react";
import CreateStaticFileProxy from "./Create-StaticFileProxy";
import { getStaticFileProxyList, deleteStaticFileProxy } from "../service/StaticFileProxyService";
import './Static-File-Proxy.css';
import UpdateStaticFileProxy from "./Update-StaticFileProxy";

interface IProps {
    activeKey: string;
}


export default function StaticFileProxy({ activeKey }: IProps) {
    const [data, setData] = useState<any[]>();
    const [total, setTotal] = useState<number>(0);
    const [createStaticFileProxyVisible, setCreateStaticFileProxyVisible] = useState<boolean>(false)
    const [updateStaticFileProxyVisible, setUpdateStaticFileProxyVisible] = useState<boolean>(false)
    const [updateStaticFileProxy, setUpdateStaticFileProxy] = useState<StaticFileProxyEntity>({} as StaticFileProxyEntity)
    const [input, setInput] = useState({
        page: 1,
        pageSize: 10,
        keyword: '',
    })

    async function loadingStaticFileProxy() {
        try {
            const result = await getStaticFileProxyList(input) as any;

            setData(result.items);
            setTotal(result.total)

        } catch {
            Toast.error('加载静态文件代理列表失败');
        }
    }

    useEffect(() => {
        if (activeKey === '3') {
            loadingStaticFileProxy();
        }
    }, [input, activeKey])

    function renderResponse(responseHeaders: { [key: string]: string; }) {
        return Object.keys(responseHeaders).map((key) => {
            return <Tag style={{
                marginRight: '5px',
            }}>{key}:{responseHeaders[key]}</Tag>
        })
    }

    function onremove(id: string) {
        deleteStaticFileProxy(id)
            .then((value: any) => {
                if (value.code === 0) {
                    Toast.success('删除成功');
                    loadingStaticFileProxy();
                } else {
                    Toast.error(value.message);
                }
            })
    }

    return (<>

        <List
            size="large"
            header={
                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                    <div>
                        静态文件代理管理
                    </div>
                    <Button onClick={() => setCreateStaticFileProxyVisible(true)} theme='solid' style={{}}>
                        添加代理
                    </Button>
                </div>
            }
            bordered
            style={{
                height: 'calc(100vh - 280px)',
                overflow: 'auto',
                padding: '10px',
            }}
            dataSource={data}
            renderItem={(item: StaticFileProxyEntity, index) =>
                <Collapse key={index}>
                    <Collapse.Panel
                        header={item.name}
                        itemKey={item.id}>
                        <>
                            <Button theme='solid' onClick={() => {
                                setUpdateStaticFileProxyVisible(true);
                                setUpdateStaticFileProxy(item);
                            }} style={{
                                marginRight: '10px',
                                width: '60px',
                                marginBottom: '5px',
                            }}>编辑</Button>
                            <Button theme='solid' type='danger' onClick={() => onremove(item.id)} style={{
                                marginRight: '10px',
                                width: '60px',
                                marginBottom: '5px',
                            }}>删除</Button>
                            <div className="static-file-proxy-item">
                                <div>
                                    <span>代理名称：</span>
                                    <span>{item.name}</span>
                                </div>
                                <div>
                                    <span>描述：</span>
                                    <span>{item.description}</span>
                                </div>
                                <div>
                                    <span>匹配路由：</span>
                                    <span style={{
                                        fontSize: '12px',
                                        border: '1px solid #eee',
                                        padding: '5px',
                                        borderRadius: '5px',
                                    }}>{item.path}</span>
                                </div>
                                <div>
                                    <span>匹配域名：</span>
                                    <span >{item.hosts.map(x => {
                                        return <span style={{
                                            fontSize: '12px',
                                            border: '1px solid #eee',
                                            padding: '5px',
                                            borderRadius: '5px',
                                        }}>{x}</span>
                                    })}</span>
                                </div>
                                <div>
                                    <span>{item.gZip ? "启用GZIP压缩" : "未启用GZIP压缩"}</span>
                                </div>
                                <div>
                                    <span>响应头信息：</span>
                                    <span>
                                        {renderResponse(item.responseHeaders)}
                                    </span>
                                </div>
                                <div>
                                    <span>默认文件：</span>
                                    <span>{item.index}</span>
                                </div>
                                <div>
                                    <span>尝试的文件列表：</span>
                                    <span>{item.tryFiles?.map(x => {
                                        return <span style={{
                                            fontSize: '12px',
                                            border: '1px solid #eee',
                                            padding: '5px',
                                            borderRadius: '5px',
                                        }}>{x}</span>
                                    })}</span>
                                </div>
                            </div>
                        </>
                    </Collapse.Panel>
                </Collapse>}
        />
        <CreateStaticFileProxy visible={createStaticFileProxyVisible} onSusccess={() => {
            loadingStaticFileProxy();
            setCreateStaticFileProxyVisible(false);
        }} onCancel={() => setCreateStaticFileProxyVisible(false)} />
        <UpdateStaticFileProxy
            visible={updateStaticFileProxyVisible}
            entity={updateStaticFileProxy}
            onSusccess={() => {
                loadingStaticFileProxy();
                setUpdateStaticFileProxyVisible(false);
            }}
            onCancel={() => setUpdateStaticFileProxyVisible(false)} />
    </>)
}