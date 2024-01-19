import React from 'react';
import { Layout, Nav, Button, Avatar } from '@douyinfe/semi-ui';
import { IconGithubLogo, IconHome, IconServerStroked, IconLive, IconSetting, } from '@douyinfe/semi-icons';
import { Outlet,useNavigate } from 'react-router-dom';

export default function MainLayout() {
    const [selectedKeys, setSelectedKeys] = React.useState(['Home']);

    const { Header, Footer, Sider, Content } = Layout;
    const navigate = useNavigate();

    return (
        <Layout style={{ border: '1px solid var(--semi-color-border)', height: '100vh' }}>
            <Sider style={{ backgroundColor: 'var(--semi-color-bg-1)' }}>
                <Nav
                    defaultSelectedKeys={selectedKeys}
                    onSelect={key => {
                        setSelectedKeys([key.itemKey.toString()]);
                        if (key.itemKey === 'Home') {
                            navigate('/');
                        } else if (key.itemKey === 'Gateway') {
                            navigate('/Gateway');
                        } else if (key.itemKey === 'Logger') {
                            navigate('/Logger');
                        } else if (key.itemKey === 'Setting') {
                            navigate('/Setting');
                        }
                    }}
                    style={{ maxWidth: 220, height: '100%' }}
                    items={[
                        { itemKey: 'Home', text: '首页', icon: <IconHome size="large" /> },
                        { itemKey: 'Gateway', text: '代理设置', icon: <IconServerStroked /> },
                        { itemKey: 'Logger', text: '日志', icon: <IconLive size="large" /> },
                        { itemKey: 'Setting', text: '设置', icon: <IconSetting size="large" /> },
                    ]}
                    header={{
                        // logo: <IconSemiLogo style={{ fontSize: 36 }} />,
                        text: 'Gateway',
                    }}
                    footer={{
                        collapseButton: true,
                    }}
                />
            </Sider>
            <Layout>
                <Header style={{ backgroundColor: 'var(--semi-color-bg-1)' }}>
                    <Nav
                        mode="horizontal"
                        footer={
                            <>

                                <Avatar color="orange" size="small">
                                    Gateway
                                </Avatar>
                            </>
                        }
                    ></Nav>
                </Header>
                <Content
                    style={{
                        padding: '24px',
                        backgroundColor: 'var(--semi-color-bg-0)',
                    }}
                >
                    <Outlet />
                </Content>
                <Footer
                    style={{
                        display: 'flex',
                        justifyContent: 'space-between',
                        padding: '20px',
                        color: 'var(--semi-color-text-2)',
                        backgroundColor: 'rgba(var(--semi-grey-0), 1)',
                    }}
                >
                    <span
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                        }}
                    >
                        <span>Token © 2023 </span>
                    </span>
                    <span>
                        <span>
                            <Button onClick={() => {
                                window.open('http://token-ai.cn:1001/token/Gateway', '_blank');
                            }} theme='borderless' type='primary' icon={<IconGithubLogo />}></Button>
                        </span>
                    </span>
                </Footer>
            </Layout>
        </Layout>
    );
}