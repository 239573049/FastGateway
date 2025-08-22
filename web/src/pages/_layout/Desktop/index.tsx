import { memo, useEffect, useState } from "react";
import { Outlet } from "react-router-dom";
import {
    ShieldCheck,
    ShieldPlus,
    Info,
    LayoutDashboard,
    ChevronLeft,
    ChevronRight,
    LogOut,
    User,
    Network,
    Globe,
    Lock,
    Clock,
    FileText
} from 'lucide-react'
import { useNavigate, useLocation } from "react-router-dom";
import { ThemeSwitch } from "@/components/ui/theme-switch";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";
import { useUserStore } from "@/store/user";
import {
    Tooltip,
    TooltipContent,
    TooltipProvider,
    TooltipTrigger,
} from "@/components/ui/tooltip";
import { Badge } from "@/components/ui/badge";
import { useTheme } from "@/components/theme-provider";
import type { ThemeMode } from "antd-style";

const DesktopLayout = memo(() => {
    const location = useLocation();
    const navigate = useNavigate();
    const userStore = useUserStore();

    const { theme, setTheme: setCtxTheme } = useTheme();

    const [menuKey, setMenuKey] = useState('dashboard');
    const [collapsed, setCollapsed] = useState(false);
    const [openSections, setOpenSections] = useState<Set<string>>(new Set());

    useEffect(() => {
        const t = userStore.theme;
        setCtxTheme(t === 'auto' ? 'system' : t);
    }, [userStore.theme, setCtxTheme]);

    const menus = [
        {
            key: 'dashboard',
            label: '仪表盘',
            icon: LayoutDashboard,
            description: '系统概览和实时监控',
            category: '概览'
        },
        {
            key: 'server',
            label: '服务管理',
            icon: Globe,
            description: '代理服务和路由配置',
            category: '核心'
        },
        {
            key: 'tunnel',
            label: '节点管理',
            icon: Network,
            description: '隧道节点和代理配置管理',
            category: '核心'
        },
        {
            key: 'cert',
            label: '证书管理',
            icon: ShieldCheck,
            description: 'SSL证书和域名配置',
            badge: 'SSL',
            category: '安全'
        },
        {
            key: 'protect-config',
            label: '安全防护',
            icon: ShieldPlus,
            description: '安全策略和访问控制',
            category: '安全',
            children: [
                {
                    key: 'protect-config/blacklist',
                    label: 'IP黑名单',
                    icon: Lock,
                    description: 'IP地址黑名单管理',
                },
                {
                    key: 'protect-config/whitelist',
                    label: 'IP白名单',
                    icon: ShieldCheck,
                    description: 'IP地址白名单管理',
                },
                {
                    key: 'protect-config/rate-limit',
                    label: '限流配置',
                    icon: Clock,
                    description: '请求频率限制设置',
                },
            ]
        },
        {
            key: 'filestorage',
            label: '文件管理',
            icon: FileText,
            description: '静态文件和存储管理',
            category: '工具'
        },
        {
            key: 'about',
            label: '系统信息',
            icon: Info,
            description: '版本信息和系统状态',
            category: '系统'
        },
    ]

    function updateMenu(key: string) {
        setMenuKey(key);
        navigate(key);
    }

    function toggleSection(key: string) {
        const newOpenSections = new Set(openSections);
        if (newOpenSections.has(key)) {
            newOpenSections.delete(key);
        } else {
            newOpenSections.add(key);
        }
        setOpenSections(newOpenSections);
    }

    useEffect(() => {
        const key = location.pathname.substring(1);
        setMenuKey(key);
        
        // Auto-open parent section if child is active
        const parentKey = key.split('/')[0];
        if (parentKey !== key) {
            setOpenSections(prev => new Set(prev).add(parentKey));
        }
    }, [location]);

    useEffect(() => {
        const token = localStorage.getItem('token');
        if (!token) {
            navigate('/login');
        }
    }, [navigate]);

    const handleLogout = () => {
        localStorage.removeItem('token');
        navigate('/login');
    };

    const MenuItem = ({ item, isChild = false }: { item: any; isChild?: boolean }) => {
        const isActive = menuKey === item.key;
        const hasChildren = item.children && item.children.length > 0;
        const isSectionOpen = openSections.has(item.key);

        const content = (
            <div className={cn(
                "flex items-center gap-3 px-3 py-2.5 text-sm font-medium rounded-lg transition-all duration-200",
                isActive 
                    ? "bg-primary/10 text-primary border-r-2 border-primary" 
                    : "text-muted-foreground hover:text-foreground hover:bg-accent/50",
                isChild && "ml-8 text-xs",
                collapsed && !isChild && "justify-center px-1.5"
            )}>
                <div className={cn(
                    "p-1.5 rounded-md transition-colors",
                    isActive ? "bg-primary/20" : "bg-transparent"
                )}>
                    <item.icon className={cn(
                        "h-4 w-4 flex-shrink-0",
                        isActive && "text-primary"
                    )} />
                </div>
                {!collapsed && (
                    <>
                        <span className="flex-1 truncate">{item.label}</span>
                        {item.badge && (
                            <Badge 
                                variant={isActive ? "default" : "secondary"} 
                                className="text-xs px-1.5 py-0"
                            >
                                {item.badge}
                            </Badge>
                        )}
                        {hasChildren && (
                            <ChevronRight className={cn(
                                "h-4 w-4 transition-transform duration-200",
                                isSectionOpen && "rotate-90"
                            )} />
                        )}
                    </>
                )}
            </div>
        );

        if (collapsed && !isChild) {
            return (
                <Tooltip key={item.key}>
                    <TooltipTrigger asChild>
                        <div className="w-full">{content}</div>
                    </TooltipTrigger>
                    <TooltipContent side="right" className="flex items-center gap-2 shadow-lg">
                        <item.icon className="h-4 w-4" />
                        <span className="font-medium">{item.label}</span>
                        {item.badge && (
                            <Badge variant="secondary" className="text-xs">
                                {item.badge}
                            </Badge>
                        )}
                    </TooltipContent>
                </Tooltip>
            );
        }

        return (
            <div key={item.key} className="w-full">
                <button
                    onClick={() => {
                        if (hasChildren && !collapsed) {
                            toggleSection(item.key);
                        } else {
                            updateMenu(item.key);
                        }
                    }}
                    className="w-full text-left transition-all duration-200"
                    title={item.description}
                >
                    {content}
                </button>
                {hasChildren && !collapsed && isSectionOpen && (
                    <div className="mt-1 space-y-1 animate-in slide-in-from-top-1 duration-200">
                        {item.children.map((child: any) => (
                            <MenuItem key={child.key} item={child} isChild />
                        ))}
                    </div>
                )}
            </div>
        );
    };

    return (
        <TooltipProvider>
            <div className="flex h-screen bg-gradient-to-br from-background via-background to-muted/20">
                {/* Sidebar */}
                <aside className={cn(
                    "bg-sidebar text-sidebar-foreground border-r border-sidebar transition-all duration-300 ease-in-out flex flex-col",
                    collapsed ? "w-16" : "w-64"
                )}>
                    {/* Header */}
                    <div className={cn(
                        "flex items-center h-14 transition-all duration-300 border-b border-sidebar",
                        collapsed ? "justify-center px-2" : "justify-between px-3"
                    )}>
                        {!collapsed && (
                            <div className="flex items-center gap-2">
                                <div className="w-7 h-7 bg-sidebar-accent rounded-lg flex items-center justify-center">
                                    <ShieldCheck className="h-5 w-5 text-sidebar-accent-foreground" />
                                </div>
                                <div>
                                    <h1 className="text-lg font-bold">FastGateway</h1>
                                    <p className="text-xs text-muted-foreground">网关管理系统</p>
                                </div>
                            </div>
                        )}
                        <Button
                            variant="ghost"
                            size="sm"
                            onClick={() => setCollapsed(!collapsed)}
                            className="h-8 w-8 p-0"
                        >
                            {collapsed ? (
                                <ChevronRight className="h-4 w-4" />
                            ) : (
                                <ChevronLeft className="h-4 w-4" />
                            )}
                        </Button>
                    </div>

                    {/* Navigation */}
                    <nav className="flex-1 p-2.5 space-y-0.5 overflow-y-auto">
                        {menus.map(item => (
                            <MenuItem key={item.key} item={item} />
                        ))}
                    </nav>

                    {/* Footer */}
                    <div className={cn(
                        "border-t border-sidebar p-2.5 space-y-1.5",
                        collapsed && "p-1.5"
                    )}>
                        {!collapsed && (
                            <div className="flex items-center gap-2 px-2.5 py-2">
                                <div className="w-7 h-7 bg-sidebar-accent rounded-full flex items-center justify-center">
                                    <User className="h-4 w-4 text-sidebar-accent-foreground" />
                                </div>
                                <div className="flex-1 min-w-0">
                                    <p className="text-sm font-medium truncate">管理员</p>
                                    <p className="text-xs text-muted-foreground">在线</p>
                                </div>
                            </div>
                        )}
                        
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <Button
                                    variant="ghost"
                                    size="sm"
                                    onClick={handleLogout}
                                    className={cn(
                                        "w-full justify-start",
                                        collapsed && "justify-center px-2"
                                    )}
                                >
                                    <LogOut className="h-4 w-4" />
                                    {!collapsed && <span className="ml-2">退出登录</span>}
                                </Button>
                            </TooltipTrigger>
                            {collapsed && <TooltipContent side="right">退出登录</TooltipContent>}
                        </Tooltip>
                    </div>
                </aside>

                {/* Main Content */}
                <div className="flex-1 flex flex-col overflow-hidden">
                    {/* Header */}
                    <header className="h-14 bg-card/50 backdrop-blur-sm flex items-center px-4 justify-between">
                        <div className="flex items-center gap-3">
                            <div className="text-sm text-muted-foreground">
                                {menus.find(m => menuKey.startsWith(m.key))?.label || '仪表盘'}
                            </div>
                        </div>
                        
                        <div className="flex items-center gap-2">
                            <ThemeSwitch 
                                onThemeSwitch={(v) => {
                                    userStore.setTheme(v);
                                    setCtxTheme(v === 'auto' ? 'system' : v);
                                }} 
                                themeMode={theme === 'system' ? 'auto' : (theme as ThemeMode)} 
                            />
                        </div>
                    </header>

                    {/* Content */}
                    <main className="flex-1 overflow-auto p-4 bg-muted/30">
                        <div className="bg-card rounded-xl border shadow-sm">
                            <Outlet />
                        </div>
                    </main>
                </div>
            </div>
        </TooltipProvider>
    );
});

export default DesktopLayout;