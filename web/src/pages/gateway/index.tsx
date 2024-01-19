import { TabPane, Tabs } from "@douyinfe/semi-ui";
import ClusterGateway from "../../components/Cluster-Gateway";

export default function Gateway() {
    
    return (
        <>

            <Tabs tabPosition="left" type='line'>
                <TabPane
                    tab={
                        <span>
                            集群
                        </span>
                    }
                    itemKey="1"
                >
                    <div style={{ padding: '0 24px' }}>
                        <ClusterGateway />
                    </div>
                </TabPane>
                <TabPane
                    tab={
                        <span>
                            路由
                        </span>
                    }
                    itemKey="2"
                >
                    <div style={{ padding: '0 24px' }}>
                    </div>
                </TabPane>
            </Tabs>
        </>
    )
}