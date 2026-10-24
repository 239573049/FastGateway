import { useCallback } from "react";
import { useNavigate } from "react-router-dom";
import {
  LayoutDashboard,
  Globe,
  Waypoints,
  Network,
  Boxes,
  Activity,
  Lock,
  Gauge,
  Radar,
  ShieldAlert,
  ScrollText,
  ShieldCheck,
  FileText,
  Settings,
  Info,
  Sun,
  Moon,
  Laptop,
  LogOut,
} from "lucide-react";
import {
  CommandDialog,
  CommandEmpty,
  CommandGroup,
  CommandInput,
  CommandItem,
  CommandList,
  CommandSeparator,
  CommandShortcut,
} from "@/components/ui/command";
import { useTheme } from "@/components/theme-provider";
import { useUserStore } from "@/store/user";

interface CommandPaletteProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  onLogout?: () => void;
}

export function CommandPalette({ open, onOpenChange, onLogout }: CommandPaletteProps) {
  const navigate = useNavigate();
  const { setTheme: setCtxTheme } = useTheme();
  const userStore = useUserStore();

  const runCommand = useCallback(
    (command: () => void) => {
      onOpenChange(false);
      command();
    },
    [onOpenChange]
  );

  return (
    <CommandDialog
      open={open}
      onOpenChange={onOpenChange}
      title="全局快捷指令"
      description="快速检索页面、执行系统操作或切换主题"
    >
      <CommandInput placeholder="输入指令或搜索页面..." />
      <CommandList>
        <CommandEmpty>未找到相关指令或页面</CommandEmpty>

        <CommandGroup heading="导航直达">
          <CommandItem
            onSelect={() => runCommand(() => navigate("/dashboard"))}
          >
            <LayoutDashboard className="mr-2 h-4 w-4" />
            <span>仪表盘 / 流量报表</span>
            <CommandShortcut>G D</CommandShortcut>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/server"))}
          >
            <Globe className="mr-2 h-4 w-4" />
            <span>服务管理 / 路由配置</span>
            <CommandShortcut>G S</CommandShortcut>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/stream-forward"))}
          >
            <Waypoints className="mr-2 h-4 w-4" />
            <span>端口转发 (四层 Stream)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/tunnel"))}
          >
            <Network className="mr-2 h-4 w-4" />
            <span>节点管理 / 隧道穿透</span>
            <CommandShortcut>G T</CommandShortcut>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/cluster"))}
          >
            <Boxes className="mr-2 h-4 w-4" />
            <span>集群管理 (分布式组网)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/cert"))}
          >
            <ShieldCheck className="mr-2 h-4 w-4" />
            <span>证书管理 (SSL/TLS & ACME)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/filestorage"))}
          >
            <FileText className="mr-2 h-4 w-4" />
            <span>文件管理 (静态托管与存储)</span>
          </CommandItem>
        </CommandGroup>

        <CommandSeparator />

        <CommandGroup heading="安全中心">
          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/overview"))}
          >
            <Activity className="mr-2 h-4 w-4" />
            <span>安全总览态势</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/access"))}
          >
            <Lock className="mr-2 h-4 w-4" />
            <span>访问控制 (IP 黑白名单)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/rate-limit"))}
          >
            <Gauge className="mr-2 h-4 w-4" />
            <span>限流策略 (Rate Limiting)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/threats"))}
          >
            <Radar className="mr-2 h-4 w-4" />
            <span>威胁检测 (异常 IP 追踪)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/risk"))}
          >
            <ShieldAlert className="mr-2 h-4 w-4" />
            <span>登录风控 (暴力破解防护)</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/security/logs"))}
          >
            <ScrollText className="mr-2 h-4 w-4" />
            <span>拦截日志记录</span>
          </CommandItem>
        </CommandGroup>

        <CommandSeparator />

        <CommandGroup heading="外观与偏好">
          <CommandItem
            onSelect={() =>
              runCommand(() => {
                userStore.setTheme("light");
                setCtxTheme("light");
              })
            }
          >
            <Sun className="mr-2 h-4 w-4" />
            <span>浅色模式</span>
          </CommandItem>

          <CommandItem
            onSelect={() =>
              runCommand(() => {
                userStore.setTheme("dark");
                setCtxTheme("dark");
              })
            }
          >
            <Moon className="mr-2 h-4 w-4" />
            <span>深色模式</span>
          </CommandItem>

          <CommandItem
            onSelect={() =>
              runCommand(() => {
                userStore.setTheme("auto");
                setCtxTheme("system");
              })
            }
          >
            <Laptop className="mr-2 h-4 w-4" />
            <span>跟随系统</span>
          </CommandItem>
        </CommandGroup>

        <CommandSeparator />

        <CommandGroup heading="系统">
          <CommandItem
            onSelect={() => runCommand(() => navigate("/setting"))}
          >
            <Settings className="mr-2 h-4 w-4" />
            <span>系统设置</span>
          </CommandItem>

          <CommandItem
            onSelect={() => runCommand(() => navigate("/about"))}
          >
            <Info className="mr-2 h-4 w-4" />
            <span>系统信息</span>
          </CommandItem>

          {onLogout && (
            <CommandItem
              onSelect={() => runCommand(onLogout)}
              className="text-destructive focus:text-destructive"
            >
              <LogOut className="mr-2 h-4 w-4" />
              <span>退出登录</span>
            </CommandItem>
          )}
        </CommandGroup>
      </CommandList>
    </CommandDialog>
  );
}
