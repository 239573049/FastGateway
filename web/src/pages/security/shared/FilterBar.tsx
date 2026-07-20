import { useEffect, useState } from "react";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { getServers } from "@/services/ServerService";
import { getDomains } from "@/services/DomainNameService";
import { useDashboardStore } from "@/pages/dashboard/store";
import { RANGE_OPTIONS, type StatRange } from "@/pages/dashboard/types";

const ALL_HOSTS = "__all__";

interface FilterBarProps {
  showHost?: boolean;
}

/**
 * 安全中心统一的时间范围 / 应用筛选条，与统计报表共用 dashboard store，
 * 让「安全总览」「拦截日志」等页面的筛选状态保持一致。
 */
export function SecurityFilterBar({ showHost = true }: FilterBarProps) {
  const { range, host, setRange, setHost } = useDashboardStore();
  const [domains, setDomains] = useState<string[]>([]);

  useEffect(() => {
    if (!showHost) return;
    let live = true;
    (async () => {
      try {
        const res: any = await getServers();
        const servers = Array.isArray(res?.data) ? res.data : [];
        const results = await Promise.all(
          servers
            .filter((s: any) => typeof s.id === "string" && s.id)
            .map(async (s: any) => {
              const domainRes: any = await getDomains(s.id);
              const items = Array.isArray(domainRes?.data) ? domainRes.data : [];
              return items.flatMap((d: any) => (Array.isArray(d.domains) ? d.domains : []));
            })
        );
        if (!live) return;
        const unique = [...new Set(results.flat().filter((d: string) => d && d !== "*"))] as string[];
        setDomains(unique.sort());
      } catch (error) {
        console.error(error);
      }
    })();
    return () => {
      live = false;
    };
  }, [showHost]);

  return (
    <div className="flex flex-wrap items-center gap-2">
      {showHost && (
        <Select
          value={host ?? ALL_HOSTS}
          onValueChange={(value) => setHost(value === ALL_HOSTS ? undefined : value)}
        >
          <SelectTrigger className="h-8 w-[160px] text-xs">
            <SelectValue placeholder="全部应用" />
          </SelectTrigger>
          <SelectContent>
            <SelectItem value={ALL_HOSTS}>全部应用</SelectItem>
            {domains.map((domain) => (
              <SelectItem key={domain} value={domain}>
                {domain}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      )}

      <ToggleGroup
        type="single"
        size="sm"
        variant="outline"
        value={range}
        onValueChange={(value: string) => value && setRange(value as StatRange)}
        className="hidden sm:flex"
      >
        {RANGE_OPTIONS.map((option) => (
          <ToggleGroupItem key={option.value} value={option.value} className="h-8 px-3 text-xs">
            {option.label}
          </ToggleGroupItem>
        ))}
      </ToggleGroup>

      <Select value={range} onValueChange={(value) => setRange(value as StatRange)}>
        <SelectTrigger className="h-8 w-[120px] text-xs sm:hidden">
          <SelectValue />
        </SelectTrigger>
        <SelectContent>
          {RANGE_OPTIONS.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}

export default SecurityFilterBar;
