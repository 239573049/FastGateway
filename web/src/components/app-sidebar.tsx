import * as React from "react"
import { Link, useLocation } from "react-router-dom"
import type { LucideIcon } from "lucide-react"
import {
  Activity,
  FileText,
  Gauge,
  Globe,
  Info,
  LayoutDashboard,
  Lock,
  Network,
  Radar,
  ScrollText,
  ShieldCheck,
  Waypoints,
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
      { key: "stream-forward", title: "端口转发", to: "/stream-forward", icon: Waypoints },
      { key: "tunnel", title: "节点管理", to: "/tunnel", icon: Network },
    ],
  },
  {
    label: "安全中心",
    items: [
      { key: "security-overview", title: "安全总览", to: "/security/overview", icon: Activity },
      { key: "security-access", title: "访问控制", to: "/security/access", icon: Lock },
      { key: "security-rate-limit", title: "限流策略", to: "/security/rate-limit", icon: Gauge },
      { key: "security-threats", title: "威胁检测", to: "/security/threats", icon: Radar },
      { key: "security-logs", title: "拦截日志", to: "/security/logs", icon: ScrollText },
      { key: "cert", title: "证书管理", to: "/cert", icon: ShieldCheck, badge: "SSL" },
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
