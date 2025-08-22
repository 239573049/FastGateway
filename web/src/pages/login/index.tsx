import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { message } from '@/utils/toast';
import { LockIcon, ShieldCheckIcon, KeyRoundIcon } from 'lucide-react';
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
        <div className="min-h-screen w-full bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 flex items-center justify-center p-4">
            <div className="w-full max-w-5xl">
                <div className="grid lg:grid-cols-2 gap-8 items-center">
                    {/* 左侧品牌区域 */}
                    <div className="hidden lg:flex flex-col items-center justify-center space-y-8 p-8">
                        <div className="relative">
                            <div className="w-32 h-32 bg-gradient-to-br from-blue-500 via-purple-500 to-pink-500 rounded-3xl flex items-center justify-center shadow-2xl shadow-blue-500/20 animate-pulse">
                                <ShieldCheckIcon className="w-16 h-16 text-white" />
                            </div>
                            <div className="absolute -inset-4 bg-gradient-to-r from-blue-500 to-purple-500 rounded-full blur-3xl opacity-20 animate-pulse" />
                        </div>
                        
                        <div className="text-center space-y-4">
                            <h1 className="text-5xl font-bold bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
                                FastGateway
                            </h1>
                            
                            <h2 className="text-xl text-slate-600 dark:text-slate-300 font-light">
                                网关管理系统
                            </h2>
                            
                            <p className="text-slate-500 dark:text-slate-400 max-w-md leading-relaxed">
                                高效、安全、可靠的API网关管理平台，为您的微服务架构提供强大的流量管理和安全防护。
                            </p>
                        </div>

                        <div className="flex space-x-8 text-sm text-slate-500 dark:text-slate-400">
                            <div className="flex items-center space-x-2">
                                <div className="w-2 h-2 bg-green-500 rounded-full" />
                                <span>高可用</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-2 h-2 bg-blue-500 rounded-full" />
                                <span>高性能</span>
                            </div>
                            <div className="flex items-center space-x-2">
                                <div className="w-2 h-2 bg-purple-500 rounded-full" />
                                <span>易管理</span>
                            </div>
                        </div>
                    </div>

                    {/* 右侧登录区域 */}
                    <div className="flex items-center justify-center p-4">
                        <Card className="w-full max-w-md border-0 shadow-2xl shadow-slate-200/50 dark:shadow-slate-900/50">
                            <CardHeader className="space-y-2 text-center pb-8 pt-8">
                                <div className="lg:hidden mb-4">
                                    <div className="w-16 h-16 bg-gradient-to-br from-blue-500 via-purple-500 to-pink-500 rounded-2xl flex items-center justify-center mx-auto mb-2 shadow-lg">
                                        <ShieldCheckIcon className="w-8 h-8 text-white" />
                                    </div>
                                    <h1 className="text-2xl font-bold bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">FastGateway</h1>
                                </div>
                                
                                <CardTitle className="text-3xl font-bold">
                                    欢迎回来
                                </CardTitle>
                                <CardDescription className="text-base">
                                    请输入管理员密码以继续访问系统
                                </CardDescription>
                            </CardHeader>
                            
                            <CardContent className="pb-8 px-8">
                                <form onSubmit={handleLogin} className="space-y-6">
                                    <div className="space-y-2">
                                        <Label htmlFor="password" className="text-sm font-medium">
                                            <div className="flex items-center space-x-2">
                                                <KeyRoundIcon className="w-4 h-4" />
                                                <span>管理员密码</span>
                                            </div>
                                        </Label>
                                        <div className="relative">
                                            <LockIcon className="absolute left-3 top-1/2 transform -translate-y-1/2 h-5 w-5 text-muted-foreground" />
                                            <Input
                                                id="password"
                                                type="password"
                                                placeholder="请输入管理员密码"
                                                value={password}
                                                onChange={(e) => setPassword(e.target.value)}
                                                className="pl-10 h-12 bg-muted/50 border-0 focus:ring-2 focus:ring-blue-500"
                                                disabled={loading}
                                            />
                                        </div>
                                    </div>

                                    <Button
                                        type="submit"
                                        disabled={loading}
                                        className="w-full h-12 bg-gradient-to-r from-blue-600 to-purple-600 hover:from-blue-700 hover:to-purple-700 text-white font-medium transition-all duration-300 transform hover:-translate-y-0.5 hover:shadow-lg disabled:opacity-50"
                                    >
                                        {loading ? (
                                            <div className="flex items-center justify-center space-x-2">
                                                <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white" />
                                                <span>登录中...</span>
                                            </div>
                                        ) : '登录系统'}
                                    </Button>
                                </form>
                            </CardContent>
                        </Card>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LoginPage;