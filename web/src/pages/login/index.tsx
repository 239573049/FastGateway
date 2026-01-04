import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { ThemeSwitch } from '@/components/ui/theme-switch';
import { message } from '@/utils/toast';
import { Activity, Eye, EyeOff, KeyRound, Lock, ShieldCheck } from 'lucide-react';
import { Auth } from '@/services/AuthorizationService';
import { useNavigate } from 'react-router-dom';

const LoginPage = () => {
    const [loading, setLoading] = useState(false);
    const navigate = useNavigate();

    const [password, setPassword] = useState('');
    const [showPassword, setShowPassword] = useState(false);
    const isDev = import.meta.env.DEV;

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
        <div className="relative min-h-screen w-full bg-slate-50 dark:bg-slate-950 flex items-center justify-center p-4">
            <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top,_rgba(59,130,246,0.14),transparent_46%)] dark:bg-[radial-gradient(circle_at_top,_rgba(59,130,246,0.22),transparent_46%)]" />

            <div className="absolute right-4 top-4 z-20 flex items-center gap-2">
                {isDev ? (
                    <Badge
                        variant="secondary"
                        className="rounded-full bg-amber-500/10 text-amber-700 dark:bg-amber-500/10 dark:text-amber-300"
                    >
                        DEV
                    </Badge>
                ) : null}
                <ThemeSwitch />
            </div>

            <div className="relative w-full max-w-5xl overflow-hidden rounded-2xl border border-slate-200/70 bg-white/70 shadow-2xl backdrop-blur-xl dark:border-slate-800 dark:bg-slate-900/30">
                <div className="grid lg:grid-cols-2">
                    {/* 左侧品牌区域 */}
                    <div className="relative hidden lg:flex flex-col justify-between bg-gradient-to-br from-slate-950 via-slate-950 to-blue-950 px-10 py-12 text-slate-50">
                        <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(circle_at_top,_rgba(59,130,246,0.32),transparent_55%)]" />

                        <div className="relative space-y-10">
                            <div className="flex items-center gap-3">
                                <div className="flex h-11 w-11 items-center justify-center rounded-xl bg-blue-600 text-white shadow-sm">
                                    <ShieldCheck className="h-6 w-6" />
                                </div>
                                <div className="leading-tight">
                                    <div className="text-lg font-semibold tracking-tight">FastGateway</div>
                                    <div className="text-xs text-slate-300">Enterprise Gateway Console</div>
                                </div>
                            </div>

                            <div className="space-y-3">
                                <h1 className="text-3xl font-semibold tracking-tight">企业级网关管理平台</h1>
                                <p className="text-slate-300 leading-relaxed">
                                    统一管理路由、鉴权与限流，为生产环境提供稳定的流量治理与安全防护。
                                </p>
                            </div>

                            <div className="space-y-4">
                                <div className="flex items-start gap-3">
                                    <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-lg bg-white/10">
                                        <ShieldCheck className="h-4 w-4 text-blue-300" />
                                    </div>
                                    <div>
                                        <div className="text-sm font-medium">安全合规</div>
                                        <div className="text-sm text-slate-300">统一鉴权、密钥管理与访问控制。</div>
                                    </div>
                                </div>
                                <div className="flex items-start gap-3">
                                    <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-lg bg-white/10">
                                        <Activity className="h-4 w-4 text-blue-300" />
                                    </div>
                                    <div>
                                        <div className="text-sm font-medium">流量治理</div>
                                        <div className="text-sm text-slate-300">限流熔断、黑白名单与路由策略。</div>
                                    </div>
                                </div>
                                <div className="flex items-start gap-3">
                                    <div className="mt-0.5 flex h-9 w-9 items-center justify-center rounded-lg bg-white/10">
                                        <KeyRound className="h-4 w-4 text-blue-300" />
                                    </div>
                                    <div>
                                        <div className="text-sm font-medium">权限管控</div>
                                        <div className="text-sm text-slate-300">细粒度权限与可审计的管理流程。</div>
                                    </div>
                                </div>
                            </div>
                        </div>

                        <div className="relative text-xs text-slate-400">仅限授权管理员使用，所有操作将被记录。</div>
                    </div>

                    {/* 右侧登录区域 */}
                    <div className="flex items-center justify-center bg-white/60 px-6 py-10 dark:bg-slate-950/40 lg:px-10">
                        <div className="w-full max-w-md">
                            <div className="mb-8 lg:hidden">
                                <div className="flex items-center gap-3">
                                    <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-blue-600 text-white shadow-sm">
                                        <ShieldCheck className="h-5 w-5" />
                                    </div>
                                    <div className="leading-tight">
                                        <div className="text-lg font-semibold tracking-tight">FastGateway</div>
                                        <div className="text-xs text-muted-foreground">Enterprise Gateway Console</div>
                                    </div>
                                </div>
                            </div>

                            <div className="space-y-2">
                                <h2 className="text-2xl font-semibold tracking-tight">管理员登录</h2>
                                <p className="text-sm text-muted-foreground">请输入管理员密码以进入控制台</p>
                            </div>

                            <form onSubmit={handleLogin} className="mt-6 space-y-5">
                                <div className="space-y-2">
                                    <Label htmlFor="password" className="text-sm font-medium">
                                        <div className="flex items-center gap-2">
                                            <KeyRound className="h-4 w-4" />
                                            <span>管理员密码</span>
                                        </div>
                                    </Label>

                                    <div className="relative">
                                        <Lock className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
                                        <Input
                                            id="password"
                                            type={showPassword ? 'text' : 'password'}
                                            placeholder="请输入管理员密码"
                                            value={password}
                                            onChange={(e) => setPassword(e.target.value)}
                                            className="h-11 pl-10 pr-10 bg-white/70 border-slate-200/70 focus-visible:ring-blue-600/25 focus-visible:ring-offset-0 dark:bg-white/5 dark:border-white/10 dark:focus-visible:ring-blue-500/25"
                                            autoComplete="current-password"
                                            autoFocus
                                            disabled={loading}
                                        />
                                        <Button
                                            type="button"
                                            variant="ghost"
                                            size="icon"
                                            onClick={() => setShowPassword((v) => !v)}
                                            className="absolute right-1 top-1/2 h-9 w-9 -translate-y-1/2"
                                            aria-label={showPassword ? '隐藏密码' : '显示密码'}
                                            disabled={loading}
                                        >
                                            {showPassword ? (
                                                <EyeOff className="h-4 w-4 text-muted-foreground" />
                                            ) : (
                                                <Eye className="h-4 w-4 text-muted-foreground" />
                                            )}
                                        </Button>
                                    </div>
                                </div>

                                <Button
                                    type="submit"
                                    disabled={loading}
                                    className="h-11 w-full bg-blue-600 text-white hover:bg-blue-700 dark:bg-blue-600 dark:hover:bg-blue-500"
                                >
                                    {loading ? (
                                        <div className="flex items-center justify-center gap-2">
                                            <div className="h-4 w-4 animate-spin rounded-full border-b-2 border-current" />
                                            <span>正在登录</span>
                                        </div>
                                    ) : (
                                        '登录'
                                    )}
                                </Button>

                                <div className="text-xs leading-relaxed text-muted-foreground">
                                    仅限授权管理员使用，所有操作将被记录。如需重置密码，请联系系统管理员。
                                </div>
                            </form>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default LoginPage;
