import { useState } from 'react';
import {
    Form,
    Input,
    Button,
    Typography,
    message,
    Spin,
    Layout,
    Row,
    Col,
    Space
} from 'antd';
import { LockOutlined, SafetyCertificateOutlined } from '@ant-design/icons';
import { Auth } from '@/services/AuthorizationService';
import { useNavigate } from 'react-router-dom';

const { Title, Text } = Typography;
const { Content } = Layout;

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
        <>
            <Layout style={{ minHeight: '100vh', width: '100%', background: '#f5f7fa' }}>
                <Content style={{
                    display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '24px',
                    width: '100%'
                }}>
                    <Row style={{ width: '100%', maxWidth: '1200px', minHeight: '600px' }} gutter={0}>
                        {/* 左侧品牌区域 */}
                        <Col
                            xs={0}
                            sm={0}
                            md={12}
                            lg={14}
                            xl={15}
                            style={{
                                background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                borderTopLeftRadius: '12px',
                                borderBottomLeftRadius: '12px',
                                position: 'relative',
                                overflow: 'hidden'
                            }}
                        >
                            <div style={{
                                position: 'absolute',
                                top: 0,
                                left: 0,
                                right: 0,
                                bottom: 0,
                                background: 'url("data:image/svg+xml,%3Csvg width="60" height="60" viewBox="0 0 60 60" xmlns="http://www.w3.org/2000/svg"%3E%3Cg fill="none" fill-rule="evenodd"%3E%3Cg fill="%23ffffff" fill-opacity="0.05"%3E%3Cpath d="m36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z"/%3E%3C/g%3E%3C/g%3E%3C/svg%3E") repeat',
                                opacity: 0.3
                            }} />

                            <div style={{
                                padding: '40px',
                                textAlign: 'center',
                                color: 'white',
                                position: 'relative',
                                zIndex: 1
                            }}>
                                <div style={{
                                    width: '80px',
                                    height: '80px',
                                    background: 'rgba(255,255,255,0.15)',
                                    borderRadius: '20px',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    margin: '0 auto 24px',
                                    boxShadow: '0 8px 32px rgba(0,0,0,0.1)',
                                    backdropFilter: 'blur(10px)'
                                }}>
                                    <SafetyCertificateOutlined style={{ fontSize: '36px', color: 'white' }} />
                                </div>

                                <Title level={1} style={{
                                    color: 'white',
                                    marginBottom: '16px',
                                    fontSize: 'clamp(28px, 4vw, 48px)',
                                    fontWeight: '600'
                                }}>
                                    FastGateway
                                </Title>

                                <Title level={4} style={{
                                    color: 'rgba(255,255,255,0.9)',
                                    marginBottom: '24px',
                                    fontWeight: '400'
                                }}>
                                    网关管理系统
                                </Title>

                                <Text style={{
                                    color: 'rgba(255,255,255,0.8)',
                                    fontSize: '16px',
                                    lineHeight: '1.6',
                                    display: 'block',
                                    maxWidth: '320px',
                                    margin: '0 auto'
                                }}>
                                    高效、安全、可靠的API网关管理平台
                                </Text>
                            </div>
                        </Col>

                        {/* 右侧登录区域 */}
                        <Col
                            xs={24}
                            sm={24}
                            md={12}
                            lg={10}
                            xl={9}
                            style={{
                                background: 'white',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                borderTopRightRadius: '12px',
                                borderBottomRightRadius: '12px',
                                boxShadow: '0 4px 24px rgba(0,0,0,0.08)',
                                padding: '40px 32px'
                            }}
                            className="login-form-col"
                        >
                            <div style={{ width: '100%', maxWidth: '360px' }}>
                                <Spin spinning={loading} tip="登录中...">
                                    <Space direction="vertical" size="large" style={{ width: '100%', textAlign: 'center' }}>
                                        <div className="mobile-logo" style={{ display: 'none' }}>
                                            <div style={{
                                                width: '64px',
                                                height: '64px',
                                                background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                                                borderRadius: '16px',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center',
                                                margin: '0 auto 16px',
                                                boxShadow: '0 4px 16px rgba(102,126,234,0.3)'
                                            }}>
                                                <SafetyCertificateOutlined style={{ fontSize: '28px', color: 'white' }} />
                                            </div>
                                            <Title level={3} style={{ marginBottom: '8px', color: '#1f2937' }}>
                                                FastGateway
                                            </Title>
                                        </div>

                                        <div>
                                            <Title level={2} style={{
                                                marginBottom: '8px',
                                                color: '#1f2937',
                                                fontSize: '28px',
                                                fontWeight: '600'
                                            }}>
                                                欢迎回来
                                            </Title>
                                            <Text style={{
                                                fontSize: '15px'
                                            }}>
                                                请输入管理员密码以继续访问系统
                                            </Text>
                                        </div>

                                        <Form
                                            name="login"
                                            onFinish={handleLogin}
                                            size="large"
                                            style={{ width: '100%' }}
                                            autoComplete="off"
                                        >
                                            <Form.Item
                                                name="password"
                                                rules={[
                                                    { required: true, message: '请输入管理员密码' },
                                                    { min: 1, message: '密码不能为空' }
                                                ]}
                                            >
                                                <Input.Password
                                                    prefix={<LockOutlined style={{ color: '#9ca3af' }} />}
                                                    placeholder="请输入管理员密码"
                                                    style={{
                                                        height: '48px',
                                                        borderRadius: '8px',
                                                        border: '1px solid #e5e7eb',
                                                        fontSize: '14px'
                                                    }}
                                                    onPressEnter={() => {
                                                        const form = document.querySelector('form[name="login"]') as HTMLFormElement;
                                                        if (form) {
                                                            const event = new Event('submit', { bubbles: true });
                                                            form.dispatchEvent(event);
                                                        }
                                                    }}
                                                />
                                            </Form.Item>

                                            <Form.Item style={{ marginBottom: 0 }}>
                                                <Button
                                                    type="primary"
                                                    htmlType="submit"
                                                    loading={loading}
                                                    style={{
                                                        width: '100%',
                                                        height: '48px',
                                                        borderRadius: '8px',
                                                        fontSize: '15px',
                                                        fontWeight: '500',
                                                        background: 'linear-gradient(135deg, #667eea 0%, #764ba2 100%)',
                                                        border: 'none',
                                                        boxShadow: '0 2px 8px rgba(102,126,234,0.3)',
                                                        transition: 'all 0.3s ease'
                                                    }}
                                                >
                                                    {loading ? '登录中...' : '登录系统'}
                                                </Button>
                                            </Form.Item>
                                        </Form>
                                    </Space>
                                </Spin>
                            </div>
                        </Col>
                    </Row>
                </Content>
            </Layout>

            {/* 响应式样式 */}
            <style>{`
                @media (max-width: 768px) {
                    .mobile-logo {
                        display: block !important;
                    }
                    
                    .login-form-col {
                        border-top-left-radius: 12px !important;
                        border-bottom-left-radius: 12px !important;
                    }
                }
                
                @media (max-width: 576px) {
                    .ant-layout-content {
                        padding: 16px !important;
                    }
                    
                    .login-form-col {
                        padding: 24px 20px !important;
                    }
                }
                
                .login-form-col:hover {
                    box-shadow: 0 8px 32px rgba(0,0,0,0.12) !important;
                    transition: box-shadow 0.3s ease;
                }
                
                .ant-btn-primary:hover {
                    transform: translateY(-1px);
                    box-shadow: 0 4px 16px rgba(102,126,234,0.4) !important;
                }
                
                .ant-input-affix-wrapper:focus,
                .ant-input-affix-wrapper-focused {
                    border-color: #667eea !important;
                    box-shadow: 0 0 0 2px rgba(102,126,234,0.1) !important;
                }
            `}</style>
        </>
    );
};

export default LoginPage;