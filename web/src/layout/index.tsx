import { memo } from "react";
import { Outlet } from "react-router-dom";
import { Toaster } from "@/components/ui/sonner"
import { ThemeProvider } from "@/components/theme-provider";

const Layout = memo(() => {
    return (
        <ThemeProvider defaultTheme="system" storageKey="vite-ui-theme">
            <Outlet />
            <Toaster />
        </ThemeProvider>
    );
});

export default Layout;