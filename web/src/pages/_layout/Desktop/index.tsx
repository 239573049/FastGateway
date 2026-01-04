import { memo, useEffect, useMemo } from "react";
import { Outlet, useLocation, useNavigate } from "react-router-dom";
import { AppSidebar } from "@/components/app-sidebar";
import { useTheme } from "@/components/theme-provider";
import { Separator } from "@/components/ui/separator";
import { SidebarInset, SidebarProvider, SidebarTrigger } from "@/components/ui/sidebar";
import { ThemeSwitch } from "@/components/ui/theme-switch";
import { useUserStore } from "@/store/user";
import type { ThemeMode } from "antd-style";

const PAGE_TITLES: Record<string, string> = {
    dashboard: "仪表盘",
    server: "服务管理",
    tunnel: "节点管理",
    cert: "证书管理",
    "protect-config/blacklist": "IP黑名单",
    "protect-config/whitelist": "IP白名单",
    "protect-config/rate-limit": "限流配置",
    "protect-config/abnormal-ip": "异常IP",
    filestorage: "文件管理",
    about: "系统信息",
};

function getPageTitle(pathname: string) {
    const key = pathname.replace(/^\/+/, "");
    if (!key || key === "dashboard") return PAGE_TITLES.dashboard;

    if (key.startsWith("server/")) return PAGE_TITLES.server;
    if (key.startsWith("protect-config/")) {
        const parts = key.split("/").slice(0, 2).join("/");
        return PAGE_TITLES[parts] || "安全防护";
    }

    const base = key.split("/")[0];
    return PAGE_TITLES[key] || PAGE_TITLES[base] || "控制台";
}

const DesktopLayout = memo(() => {
    const location = useLocation();
    const navigate = useNavigate();
    const userStore = useUserStore();

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

    const handleLogout = () => {
        localStorage.removeItem("token");
        navigate("/login");
    };

    const pageTitle = useMemo(() => getPageTitle(location.pathname), [location.pathname]);

    return (
        <SidebarProvider defaultOpen>
            <AppSidebar onLogout={handleLogout} />
            <SidebarInset>
                <header className="flex h-12 shrink-0 items-center gap-2 border-b bg-background/60 backdrop-blur">
                    <div className="flex flex-1 items-center gap-2 px-4">
                        <SidebarTrigger className="-ml-1" />
                        <Separator orientation="vertical" className="mx-2 data-[orientation=vertical]:h-4" />
                        <h1 className="text-sm font-medium">{pageTitle}</h1>
                    </div>

                    <div className="flex items-center gap-2 px-4">
                        <ThemeSwitch
                            onThemeSwitch={(v) => {
                                userStore.setTheme(v);
                                setCtxTheme(v === "auto" ? "system" : v);
                            }}
                            themeMode={theme === "system" ? "auto" : (theme as ThemeMode)}
                        />
                    </div>
                </header>

                <main className="flex-1 overflow-auto bg-muted/20 p-4">
                    <Outlet />
                </main>
            </SidebarInset>
        </SidebarProvider>
    );
});

export default DesktopLayout;
