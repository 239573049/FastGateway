import { Skeleton } from "@/components/ui/skeleton";
import { formatCount } from "../types";

/**
 * Top-N 横条排行（纯 HTML，条宽相对第一名）。
 */
export function RankList({
  items,
  loading,
  emptyText = "暂无数据",
  color = "#2a78d6",
  maxRows,
}: {
  items: Array<{ key: string; count: number }>;
  loading?: boolean;
  emptyText?: string;
  color?: string;
  maxRows?: number;
}) {
  if (loading) {
    return (
      <div className="space-y-3 py-1">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="space-y-1.5">
            <Skeleton className="h-3.5 w-3/4" />
            <Skeleton className="h-1.5 w-full" />
          </div>
        ))}
      </div>
    );
  }

  const rows = maxRows ? items.slice(0, maxRows) : items;
  if (rows.length === 0)
    return <div className="py-8 text-center text-xs text-muted-foreground">{emptyText}</div>;

  const max = Math.max(...rows.map((x) => x.count), 1);

  return (
    <div className="space-y-2.5">
      {rows.map((item) => (
        <div key={item.key} className="group">
          <div className="flex items-baseline justify-between gap-3">
            <span className="min-w-0 truncate text-xs text-foreground/90" title={item.key}>
              {item.key}
            </span>
            <span className="shrink-0 text-xs font-semibold tabular-nums text-foreground">
              {formatCount(item.count)}
            </span>
          </div>
          <div className="mt-1 h-1.5 overflow-hidden rounded-full bg-muted/60">
            <div
              className="h-full rounded-full transition-[width] duration-300"
              style={{ width: `${Math.max((item.count / max) * 100, 2)}%`, backgroundColor: color }}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
