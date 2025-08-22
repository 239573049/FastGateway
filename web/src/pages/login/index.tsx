import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { message } from '@/utils/toast';
import { LockIcon, ShieldCheckIcon } from 'lucide-react';
import { Auth } from '@/services/AuthorizationService';
import { useNavigate } from 'react-router-dom';

const LoginPage = () => {
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const [password, setPassword] = useState('');

    const handleLogin = async (e: React.FormEvent) => {
        e.preventDefault();
        if (!password) {
            message.error('请输入管理员密码');
            return;
        }

        setLoading(true);
        try {
            const token = await Auth(password);
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
        <div className="min-h-screen w-full bg-gray-50 flex items-center justify-center p-6">
            <div className="w-full max-w-6xl min-h-[600px] bg-white rounded-xl shadow-xl overflow-hidden">
                <div className="grid md:grid-cols-2 h-full">
                    {/* 左侧品牌区域 */}
                    <div className="hidden md:flex bg-gradient-to-br from-blue-500 to-purple-600 p-10 items-center justify-center relative overflow-hidden">
                        {/* 背景图案 */}
                        <div className="absolute inset-0 opacity-10">
                            <div className="w-full h-full" style={{
                                backgroundImage: 'url("data:image/svg+xml,%3Csvg width="60" height="60" viewBox="0 0 60 60" xmlns="http://www.w3.org/2000/svg"%3E%3Cg fill="none" fill-rule="evenodd"%3E%3Cg fill="%23ffffff" fill-opacity="0.3"%3E%3Cpath d="m36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z"/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")',
                                backgroundRepeat: 'repeat'
                            }} />
                        </div>

                        <div className="text-center text-white relative z-10">
                            <div className="w-20 h-20 bg-white/20 backdrop-blur-sm rounded-2xl flex items-center justify-center mx-auto mb-6 shadow-2xl">
                                <ShieldCheckIcon className="w-9 h-9 text-white" />
                            </div>
                            
                            <h1 className="text-4xl lg:text-5xl font-bold mb-4">
                                FastGateway
                            </h1>
                            
                            <h2 className="text-xl text-white/90 mb-6 font-light">
                                网关管理系统
                            </h2>
                            
                            <p className="text-white/80 max-w-sm mx-auto leading-relaxed">
                                高效、安全、可靠的API网关管理平台
                            </p>
                        </div>
                    </div>

                    {/* 右侧登录区域 */}
                    <div className="flex items-center justify-center p-8">
                        <div className="w-full max-w-sm">
                            {/* 移动端Logo */}
                            <div className="md:hidden text-center mb-8">
                                <div className="w-16 h-16 bg-gradient-to-br from-blue-500 to-purple-600 rounded-2xl flex items-center justify-center mx-auto mb-4 shadow-lg">
                                    <ShieldCheckIcon className="w-8 h-8 text-white" />
                                </div>
                                <h1 className="text-2xl font-bold text-gray-900">FastGateway</h1>
                            </div>

                            <div className="text-center mb-8">
                                <h2 className="text-3xl font-bold text-gray-900 mb-2">
                                    欢迎回来
                                </h2>
                                <p className="text-gray-600">
                                    请输入管理员密码以继续访问系统
                                </p>
                            </div>

                            {loading && (
                                <div className="absolute inset-0 bg-white/80 flex items-center justify-center z-50">
                                    <div className="text-center">
                                        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-blue-600 mx-auto mb-2"></div>
                                        <p className="text-gray-600">登录中...</p>
                                    </div>
                                </div>
                            )}

                            <form onSubmit={handleLogin} className="space-y-6">
                                <div className="space-y-2">
                                    <Label htmlFor="password">管理员密码</Label>
                                    <div className="relative">
                                        <LockIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 h-5 w-5 text-gray-400" />
                                        <Input
                                            id="password"
                                            type="password"
                                            placeholder="请输入管理员密码"
                                            value={password}
                                            onChange={(e) => setPassword(e.target.value)}
                                            className="pl-10 h-12"
                                            disabled={loading}
                                        />
                                    </div>
                                </div>

                                <Button
                                    type="submit"
                                    disabled={loading}
                                    className="w-full h-12 bg-gradient-to-r from-blue-500 to-purple-600 hover:from-blue-600 hover:to-purple-700 text-white font-medium rounded-lg transition-all duration-300 transform hover:-translate-y-0.5 hover:shadow-lg"
                                >
                                    {loading ? '登录中...' : '登录系统'}
                                </Button>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LoginPage;