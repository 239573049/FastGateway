import { Button, TabPane, Tabs, Toast, Tooltip } from "@douyinfe/semi-ui";
import ClusterGateway from "../../components/Cluster-Gateway";
import RouterGateway from "../../components/Router-Gateway";
import { RefreshConfig } from "../../service/GatewayService";
import CertificateGateway from "../../components/Certificate-Gateway";
import { useState } from "react";

export default function Gateway() {
    const [activeKey, setActiveKey] = useState<string>('1');

    async function refreshConfig() {
        try {
            await RefreshConfig();
            Toast.success('刷新成功');
        } catch (error) {
            Toast.error('刷新失败' + error);
        }
    }

    return (
        <>
            <Tabs activeKey={activeKey}
                onChange={(key) => setActiveKey(key)}
                tabBarExtraContent={
                    <Tooltip
                        content={
                            <article>
                                更新代理配置，将从数据库中获取最新的代理配置
                            </article>
                        }
                        arrowPointAtCenter={false}
                        position={'left'}
                    ><Button onClick={() => refreshConfig()} theme='solid'>刷新缓存</Button>
                    </Tooltip>} >
                <TabPane
                    tab={
                        <span>
                            集群
                        </span>
                    }
                    itemKey="1"
                >
                    <div style={{ padding: '0 24px' }}>
                        <ClusterGateway activeKey={activeKey}/>
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
                        <RouterGateway activeKey={activeKey}/>
                    </div>
                </TabPane>
                <TabPane
                    tab={
                        <span>
                            证书管理
                        </span>
                    }
                    itemKey="3"
                >
                    <div style={{ padding: '0 24px' }}>
                        <CertificateGateway activeKey={activeKey}/>
                    </div>
                </TabPane>
            </Tabs>
        </>
    )
}