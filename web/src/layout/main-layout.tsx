import React, { useEffect } from 'react';
import { Layout, Nav, Button, Avatar, Dropdown } from '@douyinfe/semi-ui';
import { IconGithubLogo, IconHome, IconServerStroked,IconSetting, } from '@douyinfe/semi-icons';
import { Outlet, useNavigate } from 'react-router-dom';

export default function MainLayout() {
    const [selectedKeys, setSelectedKeys] = React.useState(['Home']);

    const { Header, Footer, Sider, Content } = Layout;
    const navigate = useNavigate();

    useEffect(() => {
        localStorage.getItem('access_token') || navigate('/login');
        // 根据当前路由设置选中的菜单
        const path = window.location.pathname;
        if (path === '/') {
            setSelectedKeys(['Home']);
        } else if (path === '/Gateway') {
            setSelectedKeys(['Gateway']);
        } else if (path === '/Setting') {
            setSelectedKeys(['Setting']);
        }
    }, []);

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
                        } else if (key.itemKey === 'Setting') {
                            navigate('/Setting');
                        }
                    }}
                    style={{ maxWidth: 220, height: '100%' }}
                    items={[
                        { itemKey: 'Home', text: '首页', icon: <IconHome size="large" /> },
                        { itemKey: 'Gateway', text: '代理设置', icon: <IconServerStroked /> },
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
                                <Dropdown
                                    position='left'
                                    render={
                                        <Dropdown.Menu>
                                            <Dropdown.Item onClick={()=>{
                                                localStorage.removeItem('token');
                                                navigate('/login');
                                            }}>退出登录</Dropdown.Item>
                                        </Dropdown.Menu>
                                    }
                                >
                                    <Avatar src='./favicon.png' color="orange" size="small">
                                    </Avatar>
                                </Dropdown>
                            </>
                        }
                    ></Nav>
                </Header>
                <Content
                    style={{
                        padding: '24px',
                        backgroundColor: 'var(--semi-color-bg-0)',
                        height: 'max-content',
                        overflow: 'auto',
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