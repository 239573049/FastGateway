import { memo, useEffect, useMemo, useRef, useState, useCallback } from "react";
import { useLocation, useNavigate } from "react-router-dom";
import { AnimatePresence, motion } from "motion/react";
import { AppSidebar } from "@/components/app-sidebar";
import { EASE, PageTransition, StatusIndicator } from "@/components/motion";
import { useTheme } from "@/components/theme-provider";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { ThemeSwitch } from "@/components/ui/theme-switch";
import { CommandPalette } from "@/components/CommandPalette";
import { DefaultPasswordBanner } from "@/components/DefaultPasswordBanner";
import { useUserStore } from "@/store/user";
import type { ThemeMode } from "antd-style";
import { Search } from "lucide-react";

const PAGE_TITLES: Record<string, string> = {
    dashboard: "统计报表",
    server: "服务管理",
    "stream-forward": "端口转发",
    tunnel: "节点管理",
    cluster: "集群管理",
    cert: "证书管理",
    "security/overview": "安全总览",
    "security/access": "访问控制",
    "security/rate-limit": "限流策略",
    "security/threats": "威胁检测",
    "security/risk": "登录风控",
    "security/logs": "拦截日志",
    filestorage: "文件管理",
    setting: "系统设置",
    about: "系统信息",
};

function getPageTitle(pathname: string) {
    const key = pathname.replace(/^\/+/, "");
    if (!key || key === "dashboard") return PAGE_TITLES.dashboard;

    if (key.startsWith("server/")) return PAGE_TITLES.server;
    if (key.startsWith("security/")) {
        const parts = key.split("/").slice(0, 2).join("/");
        return PAGE_TITLES[parts] || "安全中心";
    }

    const base = key.split("/")[0];
    return PAGE_TITLES[key] || PAGE_TITLES[base] || "控制台";
}

const DesktopLayout = memo(() => {
    const location = useLocation();
    const navigate = useNavigate();
    const userStore = useUserStore();
    const mainRef = useRef<HTMLElement>(null);
    const [commandOpen, setCommandOpen] = useState(false);

    const { theme, setTheme: setCtxTheme } = useTheme();

    useEffect(() => {
        const t = userStore.theme;
        setCtxTheme(t === "auto" ? "system" : t);
    }, [userStore.theme, setCtxTheme]);

    useEffect(() => {
        const token = localStorage.getItem("token");
        if (!token) {
            navigate("/login");
        }
    }, [navigate]);

    // 监听 ⌘K / Ctrl+K 快捷键
    useEffect(() => {
        const handleKeyDown = (e: KeyboardEvent) => {
            if ((e.metaKey || e.ctrlKey) && e.key.toLowerCase() === "k") {
                e.preventDefault();
                setCommandOpen((prev) => !prev);
            }
        };

        window.addEventListener("keydown", handleKeyDown);
        return () => window.removeEventListener("keydown", handleKeyDown);
    }, []);

    const handleLogout = useCallback(() => {
        localStorage.removeItem("token");
        navigate("/login");
    }, [navigate]);

    const pageTitle = useMemo(() => getPageTitle(location.pathname), [location.pathname]);

    return (
        <SidebarProvider defaultOpen>
            <AppSidebar onLogout={handleLogout} />
            <SidebarInset>
                <header className="sticky top-0 z-30 flex h-13 shrink-0 items-center justify-between gap-2 border-b border-border/60 bg-background/80 px-4 backdrop-blur-md">
                    <div className="flex items-center gap-2">
                        <SidebarTrigger className="-ml-1 text-muted-foreground hover:text-foreground" />
                        <Separator orientation="vertical" className="mx-1 h-4" />
                        
                        <AnimatePresence mode="wait" initial={false}>
                            <motion.h1
                                key={pageTitle}
                                initial={{ opacity: 0, y: 3 }}
                                animate={{ opacity: 1, y: 0 }}
                                exit={{ opacity: 0, y: -3 }}
                                transition={{ duration: 0.14, ease: EASE.out }}
                                className="text-sm font-semibold tracking-tight text-foreground"
                            >
                                {pageTitle}
                            </motion.h1>
                        </AnimatePresence>
                    </div>

                    {/* 中间全局快速指令搜索条 */}
                    <div className="flex flex-1 max-w-sm mx-4">
                        <button
                            type="button"
                            onClick={() => setCommandOpen(true)}
                            className="group flex w-full items-center justify-between rounded-lg border border-border/70 bg-muted/40 px-3 py-1.5 text-xs text-muted-foreground transition-all hover:border-primary/40 hover:bg-muted/70 hover:text-foreground focus:outline-none focus:ring-1 focus:ring-primary/40"
                        >
                            <span className="flex items-center gap-2">
                                <Search className="h-3.5 w-3.5 opacity-70 group-hover:opacity-100" />
                                <span>搜索页面或指令...</span>
                            </span>
                            <kbd className="pointer-events-none inline-flex h-4.5 select-none items-center gap-0.5 rounded border border-border/60 bg-background px-1.5 font-mono text-[10px] font-medium text-muted-foreground opacity-80">
                                <span>⌘</span>K
                            </kbd>
                        </button>
                    </div>

                    {/* 右侧 HUD 状态与主题切换 */}
                    <div className="flex items-center gap-3">
                        <div className="hidden sm:flex items-center gap-2 rounded-full border border-border/50 bg-background/50 px-2.5 py-1 shadow-2xs backdrop-blur-xs">
                            <StatusIndicator status="online" label="网关运行中" size="sm" />
                        </div>

                        <ThemeSwitch
                            onThemeSwitch={(v) => {
                                userStore.setTheme(v);
                                setCtxTheme(v === "auto" ? "system" : v);
                            }}
                            themeMode={theme === "system" ? "auto" : (theme as ThemeMode)}
                        />
                    </div>
                </header>

                <main
                    ref={mainRef}
                    className="flex-1 overflow-auto bg-muted/15 p-4 md:p-6 [scrollbar-gutter:stable]"
                >
                    <DefaultPasswordBanner />
                    <PageTransition scrollRef={mainRef} />
                </main>

                <CommandPalette
                    open={commandOpen}
                    onOpenChange={setCommandOpen}
                    onLogout={handleLogout}
                />
            </SidebarInset>
        </SidebarProvider>
    );
});

export default DesktopLayout;
