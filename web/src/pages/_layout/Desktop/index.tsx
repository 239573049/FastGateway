import { memo, useState } from "react";
import { Outlet } from "react-router-dom";
import './index.css'
import Icon, { DashboardOutlined, MenuUnfoldOutlined, MenuFoldOutlined } from '@ant-design/icons';
import { Button, Layout, Menu, theme } from 'antd';
import {
    Waypoints,
    ShieldCheck,
    ShieldPlus,
    ScrollText,
    Activity,
    Forward,
    Settings,
    Info
} from 'lucide-react'
import { useNavigate } from "react-router-dom";
const { Header, Content, Sider } = Layout;


const DestkopLayout = memo(() => {
    const {
        token: { colorBgContainer, borderRadiusLG },
    } = theme.useToken();
    const navigate = useNavigate();

    const [memuKey, setMemuKey] = useState('dashboard');

    const memus = [
        {
            key: 'dashboard',
            label: '仪表盘',
            icon: <DashboardOutlined />,
        },
        {
            key: 'server',
            label: '服务管理',
            icon: <Icon>
                <Waypoints size={15} />
            </Icon>,
        },
        {
            key: 'port-forward',
            label: '端口转发',
            icon: <Icon>
                <Forward size={15} />
            </Icon>,
        },
        {
            key: 'cert',
            label: '证书管理',
            icon: <Icon>
                <ShieldCheck size={15} />
            </Icon>,
        },
        {
            key: 'protect-config',
            label: '防护配置',
            icon: <Icon>
                <ShieldPlus size={15} />
            </Icon>,
            children: [
                {
                    key: 'blacklist',
                    label: '全局黑名单',
                    icon: <Icon>
                        <ShieldPlus size={15} />
                    </Icon>,
                },
                {
                    key: 'whitelist',
                    label: '全局白名单',
                    icon: <Icon>
                        <ShieldCheck size={15} />
                    </Icon>,
                },
                // 限流
                {
                    key: 'rate-limit',
                    label: '限流',
                    icon: <Icon>
                        <Activity size={15} />
                    </Icon>,
                },
            ]
        },
        {
            key: 'log',
            label: '日志',
            icon: <Icon>
                <ScrollText size={15} />
            </Icon>,
        },
        {
            key: 'setting',
            label: '系统设置',
            icon: <Icon>
                <Settings size={15} />
            </Icon>,
        },
        {
            key: 'info',
            label: '关于',
            icon: <Icon>
                <Info size={15} />
            </Icon>,
        },
    ]

    const [collapsed, setCollapsed] = useState(false);

    function updateMemu(key: string) {
        setMemuKey(key);
        navigate(key);
    }

    return (
        <Layout className="desktop-layout">
            <Layout>
                <Sider collapsed={collapsed} width={200} style={{ background: colorBgContainer }}>
                    {
                        !collapsed &&
                        <div style={{
                            display: 'flex',
                            justifyContent: 'center',
                            alignItems: 'center',
                            height: '64px',
                            background: colorBgContainer,
                            fontSize: '24px',
                            color: 'white',
                            // 好看的样式，需要会动字体
                            fontFamily: 'cursive',
                        }}>
                            FastGateway
                        </div>
                    }
                    <Menu
                        onClick={({ key }) => updateMemu(key)}
                        mode="inline"
                        selectedKeys={[memuKey]}

                        style={{ height: '100%', borderRight: 0 }}
                        items={memus}
                    />
                </Sider>
                <Layout>
                    <Header style={{ padding: 0, background: colorBgContainer }}>
                        <Button
                            type="text"
                            icon={collapsed ? <MenuUnfoldOutlined /> : <MenuFoldOutlined />}
                            onClick={() => setCollapsed(!collapsed)}
                            style={{
                                fontSize: '16px',
                                width: 64,
                                height: 64,
                            }}
                        />
                    </Header>
                    <Content
                        style={{
                            margin: '24px 16px',
                            padding: 24,
                            minHeight: 280,
                            background: colorBgContainer,
                            borderRadius: borderRadiusLG,
                        }}
                    >
                        <Outlet />
                    </Content>
                </Layout>
            </Layout>
        </Layout>
    );
});

export default DestkopLayout;