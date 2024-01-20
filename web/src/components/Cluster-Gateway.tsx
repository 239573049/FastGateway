import { Button, Collapse, List, Tag, Toast } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { getClusters, addCluster, deleteCluster, updateCluster } from '../service/GatewayService'
import { ClusterEntity, DestinationsEntity } from "../index.d";
import CreateCluster from "./Create-Cluster";
import UpdateCluster from "./Update-Cluster";

interface IProps {
    activeKey: string;
}

export default function ClusterGateway({ activeKey }: IProps) {
    const [data, setData] = useState<any[]>([]);
    const [createClusterVisible, setCreateClusterVisible] = useState<boolean>(false);
    const [updateClusterVisible, setUpdateClusterVisible] = useState<boolean>(false);
    const [updateCluster, setUpdateCluster] = useState<ClusterEntity>({} as ClusterEntity);

    async function loadingCluster() {
        const data = await getClusters() as any;
        setData(data.data);
    }

    useEffect(() => {
        loadingCluster();
    }, []);


    /**
     * 删除集群
     * @param id 
     */
    function deleteClusterById(id: string) {
        deleteCluster(id).then(() => {
            loadingCluster();
        }).catch(() => {
            Toast.error('删除失败');
        })
    }


    return (
        <>
            <List
                size="large"
                header={
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <div>
                            集群管理
                        </div>
                        <Button onClick={() => setCreateClusterVisible(true)} theme='solid' style={{}}>
                            添加集群
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
                renderItem={(item: ClusterEntity, index) =>
                    <Collapse key={index}>
                        <Collapse.Panel
                            header={item.clusterName}
                            itemKey={item.clusterId}>
                            <>

                                <div style={{
                                    float: 'left',
                                    marginBottom: '20px',
                                }}>
                                    <div>
                                        <span>集群名称：</span>
                                        <span>{item.clusterName}</span>
                                    </div>
                                    <div>
                                        <span>集群描述：</span>
                                        <span>{item.description}</span>
                                    </div>
                                    <div>
                                        <span>集群地址：</span>
                                        <span>{item.destinationsEntities.map((x: DestinationsEntity) => {
                                            return (<Tag style={{
                                                marginRight: '5px',
                                            }} size="small" shape='circle' color='amber'>{x.address}</Tag>)
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
                                        setUpdateCluster(item);
                                        setUpdateClusterVisible(true);
                                    }} style={{
                                        marginRight: '10px',
                                        width: '60px',
                                        marginBottom: '5px',
                                    }}>编辑</Button>
                                    <Button theme='solid' type='danger' onClick={() => deleteClusterById(item.clusterId)} style={{
                                        marginRight: '10px',
                                        width: '60px',
                                    }}>删除</Button>
                                </div>
                            </>
                        </Collapse.Panel>
                    </Collapse>}
            />
            <CreateCluster visible={createClusterVisible} onSusccess={() => {
                loadingCluster()
                setCreateClusterVisible(false)
            }} onCancel={() => setCreateClusterVisible(false)} />
            <UpdateCluster cluster={updateCluster} visible={updateClusterVisible} onSusccess={() => {
                loadingCluster()
                setUpdateClusterVisible(false)
            }} onCancel={() => setUpdateClusterVisible(false)} />
        </>
    )
}