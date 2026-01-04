import * as React from "react"
import { Link, useLocation } from "react-router-dom"
import type { LucideIcon } from "lucide-react"
import {
  AlertTriangle,
  Clock,
  FileText,
  Globe,
  Info,
  LayoutDashboard,
  Lock,
  Network,
  ShieldCheck,
  ShieldPlus,
} from "lucide-react"

import { NavUser } from "@/components/nav-user"
import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarMenu,
  SidebarMenuBadge,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarMenuSub,
  SidebarMenuSubButton,
  SidebarMenuSubItem,
} from "@/components/ui/sidebar"

type NavItem = {
  key: string
  title: string
  to: string
  icon: LucideIcon
  badge?: string
  children?: NavItem[]
}

const NAV_GROUPS: { label: string; items: NavItem[] }[] = [
  {
    label: "概览",
    items: [
      { key: "dashboard", title: "仪表盘", to: "/dashboard", icon: LayoutDashboard },
    ],
  },
  {
    label: "核心",
    items: [
      { key: "server", title: "服务管理", to: "/server", icon: Globe },
      { key: "tunnel", title: "节点管理", to: "/tunnel", icon: Network },
    ],
  },
  {
    label: "安全",
    items: [
      { key: "cert", title: "证书管理", to: "/cert", icon: ShieldCheck, badge: "SSL" },
      {
        key: "protect-config",
        title: "安全防护",
        to: "/protect-config/blacklist",
        icon: ShieldPlus,
        children: [
          { key: "blacklist", title: "IP黑名单", to: "/protect-config/blacklist", icon: Lock },
          { key: "whitelist", title: "IP白名单", to: "/protect-config/whitelist", icon: ShieldCheck },
          { key: "rate-limit", title: "限流配置", to: "/protect-config/rate-limit", icon: Clock },
          { key: "abnormal-ip", title: "异常IP", to: "/protect-config/abnormal-ip", icon: AlertTriangle },
        ],
      },
    ],
  },
  {
    label: "工具",
    items: [
      { key: "filestorage", title: "文件管理", to: "/filestorage", icon: FileText },
    ],
  },
  {
    label: "系统",
    items: [
      { key: "about", title: "系统信息", to: "/about", icon: Info },
    ],
  },
]

function normalizePath(path: string) {
  if (!path) return "/"
  const normalized = path.replace(/\/+$/, "")
  return normalized.length ? normalized : "/"
}

function isActivePath(pathname: string, to: string) {
  const current = normalizePath(pathname)
  const target = normalizePath(to)

  if (target === "/dashboard") {
    return current === "/" || current === "/dashboard" || current.startsWith("/dashboard/")
  }

  return current === target || current.startsWith(`${target}/`)
}

export function AppSidebar({
  onLogout,
  ...props
}: React.ComponentProps<typeof Sidebar> & { onLogout?: () => void }) {
  const location = useLocation()
  const pathname = location.pathname

  return (
    <Sidebar collapsible="icon" {...props}>
      <SidebarHeader>
        <SidebarMenu>
          <SidebarMenuItem>
            <SidebarMenuButton asChild size="lg">
              <Link to="/dashboard">
                <ShieldCheck />
                <span className="text-base font-semibold">FastGateway</span>
              </Link>
            </SidebarMenuButton>
          </SidebarMenuItem>
        </SidebarMenu>
      </SidebarHeader>

      <SidebarContent>
        {NAV_GROUPS.map((group) => (
          <SidebarGroup key={group.label}>
            <SidebarGroupLabel>{group.label}</SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {group.items.map((item) => (
                  <SidebarMenuItem key={item.key}>
                    <SidebarMenuButton
                      asChild
                      isActive={
                        item.children
                          ? item.children.some((child) => isActivePath(pathname, child.to))
                          : isActivePath(pathname, item.to)
                      }
                      tooltip={item.title}
                    >
                      <Link to={item.to}>
                        <item.icon />
                        <span>{item.title}</span>
                      </Link>
                    </SidebarMenuButton>
                    {item.badge ? <SidebarMenuBadge>{item.badge}</SidebarMenuBadge> : null}
                    {item.children ? (
                      <SidebarMenuSub>
                        {item.children.map((child) => (
                          <SidebarMenuSubItem key={child.key}>
                            <SidebarMenuSubButton
                              asChild
                              isActive={isActivePath(pathname, child.to)}
                            >
                              <Link to={child.to}>
                                <child.icon />
                                <span>{child.title}</span>
                              </Link>
                            </SidebarMenuSubButton>
                          </SidebarMenuSubItem>
                        ))}
                      </SidebarMenuSub>
                    ) : null}
                  </SidebarMenuItem>
                ))}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        ))}
      </SidebarContent>

      <SidebarFooter>
        <NavUser user={{ name: "管理员", email: "admin", avatar: "" }} onLogout={onLogout} />
      </SidebarFooter>
    </Sidebar>
  )
}
