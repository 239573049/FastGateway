import { useState } from 'react';
import { Form, Input, Button, Card, Typography, message } from 'antd';
import { GridShowcase, LogoThree } from '@lobehub/ui';
import { Auth } from '@/services/AuthorizationService';
import { useNavigate } from 'react-router-dom'
const { Title, Text } = Typography;

const LoginPage = () => {
    const [loading, setLoading] = useState(false);
    const location = useNavigate();
    const handleLogin = async (values: any) => {
        setLoading(true);
        try {
            const token = await Auth(values.password);
            if (token.success) {
                localStorage.setItem('token', token.data);
                location('/dashboard');
            } else {
                message.error(token.message);
            }
        } catch (e) {
        }
        setLoading(false);
    };

    return (
        <>
            <GridShowcase style={{ width: '100%' }}>
                <LogoThree size={180} style={{ marginTop: -64 }} />
                <div style={styles.container}>
                    <Card
                        hoverable
                        style={styles.card} bodyStyle={styles.cardBody}>
                        <div style={styles.content}>
                            <div style={styles.info}>
                                <Title level={2} style={styles.title}>FastGateway网关管理系统</Title>
                                <Text>欢迎使用FastGateway网关管理系统，请登录以继续。</Text>
                            </div>
                            <Form
                                name="login"
                                onFinish={handleLogin}
                                style={styles.form}
                            >
                                <Form.Item
                                    name="password"
                                    rules={[{ required: true, message: '请输入密码!' }]}
                                >
                                    <Input.Password placeholder="密码" />
                                </Form.Item>
                                <Form.Item>
                                    <Button type="primary" htmlType="submit" loading={loading} style={styles.button}>
                                        登录
                                    </Button>
                                </Form.Item>
                            </Form>
                        </div>
                    </Card>
                </div>
            </GridShowcase>
        </>
    );
};

const styles = {
    container: {
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        height: '50vh',
        width: '100%',
    },
    content: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
    },
    info: {
        flex: 1,
        marginRight: '20px',
    },
    card: {
        maxWidth: '800px',
        width: '100%',
        borderRadius: '10px',
        boxShadow: '0 4px 8px rgba(0, 0, 0, 0.1)',
    },
    cardBody: {
        display: 'flex',
        flexDirection: 'row',
        alignItems: 'center',
        // 背景色要蓝色
        background: 'rgba(0, 0, 255, 0.1)',
    },
    title: {
        marginBottom: '16px',
    },
    form: {
        flex: 1,
        width: '100%',
    },
    button: {
        width: '100%',
    },
} as const;

export default LoginPage;