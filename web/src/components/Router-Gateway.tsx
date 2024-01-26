import { List, Button, Collapse, Tag } from "@douyinfe/semi-ui"
import { ClusterEntity, DestinationsEntity, RouteEntity } from "../index.d"
import { useEffect, useState } from "react";
import CreateRouter from "./Create-Router";
import { getRoutes, deleteRoute } from "../service/GatewayService";
import UpdateRoute from "./Update-Route";

interface IProps {
    activeKey: string;
}


export default function RouterGateway({ activeKey }: IProps) {

    const [updateRouterVisible, setUpdateRouterVisible] = useState<boolean>(false);
    const [createRouterVisible, setCreateRouterVisible] = useState<boolean>(false);
    const [data, setData] = useState<any[]>([]);
    const [updateRoute, setUpdateRoute] = useState<RouteEntity>({} as RouteEntity);

    async function deleteRouterById(id: string) {
        await deleteRoute(id);
        loadingRoute();
    }

    async function loadingRoute() {
        const routers = await getRoutes() as any;
        setData(routers.data);
    }

    // 如果activeKey不是2，不加载数据
    useEffect(() => {
        if (activeKey === '2') {
            loadingRoute();
        }
    }, [
        activeKey
    ]);
    
    return (
        <>
            <List
                size="large"
                header={
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <div>
                            路由管理
                        </div>
                        <Button onClick={() => setCreateRouterVisible(true)} theme='solid' style={{}}>
                            添加路由
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
                renderItem={(item: RouteEntity, index) =>
                    <Collapse key={index}>
                        <Collapse.Panel
                            header={item.routeName}
                            itemKey={item.routeId}>
                            <>

                                <div style={{
                                    float: 'left',
                                    marginBottom: '20px',
                                }}>
                                    <div>
                                        <span>集群名称：</span>
                                        <span>{item.routeName}</span>
                                    </div>
                                    <div style={{
                                        marginTop: '5px',
                                    }}>
                                        <span>集群描述：</span>
                                        <span>{item.description}</span>
                                    </div>
                                    <div style={{
                                        marginTop: '5px',
                                    }}>
                                        <span>路由：</span>
                                        <Tag style={{
                                            marginRight: '5px',
                                        }} size="small" shape='circle' color='amber'>{item.path}</Tag>
                                        {item?.hosts && item.hosts.map((x: string) => {
                                            return (<Tag size="small" shape='circle' color='amber'>
                                                {x}
                                            </Tag>)
                                        })}
                                    </div>
                                    <div style={{
                                        marginTop: '5px',
                                    }}>
                                        <span>绑定集群：</span>
                                        <span>{item.clusterEntity?.clusterName}</span>
                                        <span>{item.clusterEntity?.description}</span>
                                        <span style={{
                                            marginLeft: '5px',
                                        }}>{item.clusterEntity?.destinationsEntities.map(x => {
                                            return (<Tag>
                                                {x.address}
                                            </Tag>)
                                        })}</span>
                                    </div>
                                </div>
                                <div style={{
                                    float: 'right',
                                    display: 'flex',
                                    flexDirection: 'column',
                                    justifyContent: 'center',

                                }}>
                                    <Button theme='solid' onClick={() => {
                                        setUpdateRouterVisible(true);
                                        setUpdateRoute(item);
                                    }} style={{
                                        marginRight: '10px',
                                        width: '60px',
                                        marginBottom: '5px',
                                    }}>编辑</Button>
                                    <Button theme='solid' type='danger' onClick={() => deleteRouterById(item.routeId)} style={{
                                        marginRight: '10px',
                                        width: '60px',
                                    }}>删除</Button>
                                </div>
                            </>
                        </Collapse.Panel>
                    </Collapse>}
            />
            <CreateRouter visible={createRouterVisible} onCancel={() => setCreateRouterVisible(false)} onSusccess={() => {
                setCreateRouterVisible(false);
                loadingRoute();
            }} />

            <UpdateRoute router={updateRoute} visible={updateRouterVisible} onCancel={() => {
                setUpdateRouterVisible(false);
                loadingRoute();
            }} onSusccess={() => {
                setUpdateRouterVisible(false);
                loadingRoute();
            }} />
        </>
    )
}