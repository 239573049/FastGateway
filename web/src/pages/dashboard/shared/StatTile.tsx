import type { ReactNode } from "react";
import { cn } from "@/lib/utils";
import { Skeleton } from "@/components/ui/skeleton";

export function StatTile({
  label,
  value,
  sub,
  tone,
  loading,
  icon,
}: {
  label: string;
  value: string;
  sub?: string;
  tone?: "default" | "danger" | "warning";
  loading?: boolean;
  icon?: ReactNode;
}) {
  return (
    <div className="min-w-0 px-4 py-3">
      <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
        {icon}
        <span className="truncate">{label}</span>
      </div>
      {loading ? (
        <Skeleton className="mt-1.5 h-7 w-16" />
      ) : (
        <div
          className={cn(
            "mt-1 truncate text-2xl font-semibold tabular-nums",
            tone === "danger" && "text-red-500",
            tone === "warning" && "text-amber-500"
          )}
        >
          {value}
        </div>
      )}
      {sub && !loading && <div className="mt-0.5 truncate text-[11px] text-muted-foreground">{sub}</div>}
    </div>
  );
}
