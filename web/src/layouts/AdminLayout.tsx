import { Layout, Nav, Avatar, Switch } from '@douyinfe/semi-ui';
import { IconSemiLogo, IconMoon, IconSun, IconKey, IconGlobeStroke, IconShield, IconHistogram, IconSetting } from '@douyinfe/semi-icons';
import { Outlet, useNavigate } from 'react-router-dom';
import { useEffect, useState } from 'react';

const body = document.body;

export default function AdminLayout() {
    const { Header, Footer, Sider, Content } = Layout;
    const [key, setKey] = useState('DataStatistics');
    const navigate = useNavigate();
    const [theme, setTheme] = useState(!body.hasAttribute('theme-mode') ? true : false);

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
        } else if (path === '/protection') {
            setKey('protection');
        } else if (path === '/rate-limit') {
            setKey('rate-limit');
        }

    }, []);

    function onTheme(theme: boolean) {
        setTheme(theme);
        if (theme) {
            document.documentElement.style.setProperty('--x', 0 + 'px')
            document.documentElement.style.setProperty('--y', 0 + 'px')
        } else {
            // 获取屏幕右下角坐标
            const x = window.innerWidth;
            const y = window.innerHeight;
            document.documentElement.style.setProperty('--x', x + 'px')
            document.documentElement.style.setProperty('--y', y + 'px')
        }
        document.startViewTransition(() => {
            if (!theme) {
                body.setAttribute('theme-mode', 'dark');
                localStorage.setItem('theme', 'dark');
            } else {
                body.setAttribute('theme-mode', 'light');
                localStorage.setItem('theme', 'light');
            }
        });
    }



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
                            <Switch onChange={(v) => onTheme(v)} checked={theme} style={{
                                marginRight: '20px',
                            }} checkedText={<IconMoon />} uncheckedText={<IconSun />} size="large" />
                            <Avatar color="orange" size="small">
                                FG
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
                                itemKey: 'protection',
                                text: '防护配置',
                                icon: <IconShield size="large" />,
                                onClick: () => {
                                    selectKey('protection', '/protection');
                                }
                            },
                            {
                                itemKey: 'rate-limit',
                                text: '限流管理',
                                icon: <IconShield size="large" />,
                                onClick: () => {
                                    selectKey('rate-limit', '/rate-limit');
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