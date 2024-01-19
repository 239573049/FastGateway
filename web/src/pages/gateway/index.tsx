import { Button, TabPane, Tabs, Toast, Tooltip } from "@douyinfe/semi-ui";
import ClusterGateway from "../../components/Cluster-Gateway";
import RouterGateway from "../../components/Router-Gateway";
import { RefreshConfig } from "../../service/GatewayService";

export default function Gateway() {

    async function refreshConfig(){
        try{
            await RefreshConfig();
            Toast.success('刷新成功');
        }catch(error){
            Toast.error('刷新失败'+error);
        }
    }

    return (
        <>
            <Tabs tabBarExtraContent={
                <Tooltip
                    content={
                        <article>
                            更新代理配置，将从数据库中获取最新的代理配置
                        </article>
                    }
                    arrowPointAtCenter={false}
                    position={'right'}
                ><Button onClick={()=>refreshConfig()} theme='solid'>刷新缓存</Button>
                </Tooltip>} tabPosition="left" type='line'>
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
                        <RouterGateway />
                    </div>
                </TabPane>
            </Tabs>
        </>
    )
}