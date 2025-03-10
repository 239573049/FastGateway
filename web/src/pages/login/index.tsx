import { useState } from 'react';
import { Form, Input, Button, Card, Typography, message, Spin } from 'antd';
import { LockOutlined } from '@ant-design/icons';
import { Auth } from '@/services/AuthorizationService';
import { useNavigate } from 'react-router-dom';
const { Title, Text } = Typography;

const LoginPage = () => {
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();
    
    const handleLogin = async (values: any) => {
        setLoading(true);
        try {
            const token = await Auth(values.password);
            if (token.success) {
                localStorage.setItem('token', token.data);
                navigate('/dashboard');
                message.success('登录成功！');
            } else {
                message.error(token.message);
            }
        } catch (e) {
            message.error('登录失败，请稍后重试');
        }
        setLoading(false);
    };

    return (
        <div style={styles.pageContainer}>
            <div style={styles.leftSection}>
                <div style={styles.brandingContainer}>
                    <Title level={1} style={styles.mainTitle}>FastGateway</Title>
                    <Title level={3} style={styles.subtitle}>网关管理系统</Title>
                    <Text style={styles.description}>
                        高效、安全、可靠的API网关管理平台，为您的服务提供强大的流量控制与安全保障
                    </Text>
                </div>
            </div>
            
            <div style={styles.rightSection}>
                <Card style={styles.card} bordered={false}>
                    <Spin spinning={loading} tip="登录中...">
                        <div style={styles.formContainer}>
                            <div style={styles.logoContainer}>
                                <div style={styles.logo}>FG</div>
                            </div>
                            
                            <Title level={2} style={styles.welcomeTitle}>欢迎回来</Title>
                            <Text style={styles.welcomeSubtitle}>请输入您的管理员密码以继续</Text>
                            
                            <Form
                                name="login"
                                onFinish={handleLogin}
                                style={styles.form}
                                size="large"
                            >
                                <Form.Item
                                    name="password"
                                    rules={[{ required: true, message: '请输入密码!' }]}
                                >
                                    <Input.Password 
                                        prefix={<LockOutlined style={styles.inputIcon} />}
                                        placeholder="管理员密码" 
                                        style={styles.input}
                                    />
                                </Form.Item>
                                
                                <Form.Item>
                                    <Button 
                                        type="primary" 
                                        htmlType="submit" 
                                        style={styles.button}
                                    >
                                        登录系统
                                    </Button>
                                </Form.Item>
                            </Form>
                        </div>
                    </Spin>
                </Card>
            </div>
        </div>
    );
};

const styles = {
    pageContainer: {
        display: 'flex',
        minHeight: '100vh',
        width: '100%',
        margin: 0,
        padding: 0,
        overflow: 'hidden',
    },
    leftSection: {
        flex: '1 1 50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        background: 'linear-gradient(135deg, #2a3eb1 0%, #1e4d8c 100%)',
        padding: '2rem',
    },
    brandingContainer: {
        maxWidth: '500px',
        color: 'white',
        padding: '0 2rem',
    },
    mainTitle: {
        color: 'white',
        margin: 0,
        fontSize: '3.5rem',
        fontWeight: 'bold',
    },
    subtitle: {
        color: 'rgba(255, 255, 255, 0.9)',
        marginTop: '0.5rem',
        fontWeight: 'normal',
    },
    description: {
        color: 'rgba(255, 255, 255, 0.8)',
        fontSize: '1.1rem',
        marginTop: '1.5rem',
        lineHeight: '1.6',
    },
    rightSection: {
        flex: '1 1 50%',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        padding: '2rem',
        background: '#f8fafc',
    },
    card: {
        width: '100%',
        maxWidth: '450px',
        borderRadius: '16px',
        boxShadow: '0 10px 25px rgba(0, 0, 0, 0.05)',
        background: 'white',
    },
    formContainer: {
        padding: '2rem',
        display: 'flex',
        flexDirection: 'column',
        alignItems: 'center',
    },
    logoContainer: {
        marginBottom: '1.5rem',
    },
    logo: {
        width: '64px',
        height: '64px',
        borderRadius: '16px',
        background: 'linear-gradient(135deg, #2a3eb1 0%, #1e4d8c 100%)',
        color: 'white',
        display: 'flex',
        alignItems: 'center',
        justifyContent: 'center',
        fontSize: '1.8rem',
        fontWeight: 'bold',
        boxShadow: '0 4px 10px rgba(42, 62, 177, 0.3)',
    },
    welcomeTitle: {
        marginBottom: '0.5rem',
        textAlign: 'center',
        color: '#1a202c',
    },
    welcomeSubtitle: {
        color: '#64748b',
        marginBottom: '2rem',
        textAlign: 'center',
    },
    form: {
        width: '100%',
    },
    input: {
        height: '50px',
        borderRadius: '10px',
    },
    inputIcon: {
        color: '#94a3b8',
    },
    button: {
        width: '100%',
        height: '50px',
        borderRadius: '10px',
        fontSize: '1rem',
        fontWeight: 'bold',
        background: 'linear-gradient(135deg, #2a3eb1 0%, #1e4d8c 100%)',
        border: 'none',
        boxShadow: '0 4px 10px rgba(42, 62, 177, 0.2)',
        marginTop: '0.5rem',
    },
} as const;

export default LoginPage;