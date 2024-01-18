import { TabPane, Tabs } from "@douyinfe/semi-ui";

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
                        <h3>快速起步</h3>
                        <pre
                            style={{
                                margin: '24px 0',
                                padding: '20px',
                                border: 'none',
                                whiteSpace: 'normal',
                                borderRadius: '6px',
                                color: 'var(--semi-color-text-1)',
                                backgroundColor: 'var(--semi-color-fill-0)',
                            }}
                        >
                            <code>
                                yarn add @douyinfe/semi-ui
                            </code>
                        </pre>
                    </div>
                </TabPane>
            </Tabs>
        </>
    )
}