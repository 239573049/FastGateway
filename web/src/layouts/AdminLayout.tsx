import { Layout, Nav, Button, Avatar } from '@douyinfe/semi-ui';
import { IconSemiLogo, IconBell, IconHelpCircle, IconKey, IconGlobeStroke, IconHistogram, IconSetting } from '@douyinfe/semi-icons';
import { Outlet, useNavigate } from 'react-router-dom';
import { useEffect, useState } from 'react';

export default function AdminLayout() {
    const { Header, Footer, Sider, Content } = Layout;
    const [key, setKey] = useState('DataStatistics');
    const navigate = useNavigate();

    function selectKey(key: string, path: string) {
        setKey(key);
        navigate(path);
    }


    useEffect(() => {
        // 获取路由
        const path = window.location.pathname;
        if (path === '/http-proxy') {
            setKey('HTTPProxt');
        } else if (path === '/setting') {
            setKey('Setting');
        } else if (path === '/cert') {
            setKey('cert');
        }

    }, []);

    return (
        <Layout style={{ border: '1px solid var(--semi-color-border)', height: "100%" }}>
            <Header style={{ backgroundColor: 'var(--semi-color-bg-1)' }}>
                <div>
                    <Nav mode="horizontal" defaultSelectedKeys={['Home']}>
                        <Nav.Header>
                            <IconSemiLogo style={{ height: '36px', fontSize: 36 }} />
                        </Nav.Header>
                        <span
                            style={{
                                color: 'var(--semi-color-text-2)',
                            }}
                        >
                            <span
                                style={{
                                    marginRight: '24px',
                                    color: 'var(--semi-color-text-0)',
                                    fontWeight: '600',
                                }}
                            >
                                FastGateway
                            </span>
                        </span>
                        <Nav.Footer>
                            <Button
                                theme="borderless"
                                icon={<IconBell size="large" />}
                                style={{
                                    color: 'var(--semi-color-text-2)',
                                    marginRight: '12px',
                                }}
                            />
                            <Button
                                theme="borderless"
                                icon={<IconHelpCircle size="large" />}
                                style={{
                                    color: 'var(--semi-color-text-2)',
                                    marginRight: '12px',
                                }}
                            />
                            <Avatar color="orange" size="small">
                                Gateway
                            </Avatar>
                        </Nav.Footer>
                    </Nav>
                </div>
            </Header>
            <Layout>
                <Sider style={{ backgroundColor: 'var(--semi-color-bg-1)' }}>
                    <Nav
                        style={{ maxWidth: 220, height: '100%' }}
                        defaultSelectedKeys={[key]}
                        items={[
                            {
                                itemKey: 'DataStatistics',
                                text: '数据统计',
                                icon: <IconGlobeStroke size="large" />,
                                onClick: () => {
                                    selectKey('DataStatistics', '/');
                                }
                            },
                            {
                                itemKey: 'HTTPProxt',
                                text: 'HTTP代理',
                                icon: <IconHistogram size="large" />,
                                onClick: () => {
                                    selectKey('HTTPProxt', '/http-proxy');
                                }
                            },
                            {
                                itemKey: 'cert',
                                text: '证书管理',
                                icon: <IconKey size="large" />,
                                onClick: () => {
                                    selectKey('cert', '/cert');
                                }
                            },
                            {
                                itemKey: 'Setting',
                                text: '设置',
                                icon: <IconSetting size="large" />,
                                onClick: () => {
                                    selectKey('Setting', '/setting');
                                }
                            },
                        ]}
                        onSelect={key => {
                            setKey(key.itemKey as string);
                        }}
                        footer={{
                            collapseButton: true,
                        }}
                    />
                </Sider>
                <Content
                    style={{
                        margin: '20px',
                        backgroundColor: 'var(--semi-color-bg-0)',
                        borderRadius: '10px',
                        padding: '20px',
                        overflow: 'auto',
                        // 高度自适应
                        height: 'calc(100vh - 160px)',
                    }}
                >
                    <Outlet />
                </Content>
            </Layout>
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
                    <span>
                        © 2021 Token Team. All rights reserved.
                    </span>
                </span>
                <span>
                    <span>反馈建议</span>
                </span>
            </Footer>
        </Layout>
    );
}