import { Button, List } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { getClusters, addCluster, deleteCluster, updateCluster } from '../service/GatewayService'

export default function ClusterGateway() {
    const [data, setData] = useState<any[]>([]);

    async function loadingCluster() {
        const data = await getClusters() as any;
        debugger;
        setData(data.data);
    }

    useEffect(() => {
        loadingCluster();
    }, []);
    

    return (
        <>
            <div style={{

            }}>
                <Button theme='borderless' style={{
                    float: 'right',
                    marginTop: '10px',
                    marginRight: '10px',
                    
                }}>添加集群</Button>
            </div>

            <List
                size="large"
                header={<div>
                    集群管理
                </div>}
                bordered
                dataSource={data}
                renderItem={item => <List.Item>{item}</List.Item>}
            />
        </>
    )
}