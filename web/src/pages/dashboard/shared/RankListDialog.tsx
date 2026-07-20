import { useCallback, useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Skeleton } from "@/components/ui/skeleton";
import { getStatisticsRankingsPaged, type RankingType } from "@/services/StatisticsService";
import { formatCount, type RankingItem, type StatRange } from "../types";

const PAGE_SIZE = 20;

/**
 * “查看更多”对话框：分页加载完整排行。
 */
export function RankListDialog({
  open,
  onOpenChange,
  title,
  type,
  range,
  host,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  type: RankingType;
  range: StatRange;
  host?: string;
}) {
  const [items, setItems] = useState<RankingItem[]>([]);
  const [total, setTotal] = useState(0);
  const [page, setPage] = useState(1);
  const [loading, setLoading] = useState(false);

  const load = useCallback(
    async (nextPage: number) => {
      setLoading(true);
      try {
        const res: any = await getStatisticsRankingsPaged(range, type, nextPage, PAGE_SIZE, host);
        const data = res?.data;
        setItems(Array.isArray(data?.items) ? data.items : []);
        setTotal(Number(data?.total) || 0);
        setPage(nextPage);
      } catch (error) {
        console.error(error);
      } finally {
        setLoading(false);
      }
    },
    [range, type, host]
  );

  useEffect(() => {
    if (open) load(1);
  }, [open, load]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle className="text-sm">{title}</DialogTitle>
        </DialogHeader>
        <div className="max-h-[420px] space-y-1 overflow-y-auto pr-1">
          {loading ? (
            Array.from({ length: 8 }).map((_, i) => <Skeleton key={i} className="h-8 w-full" />)
          ) : items.length === 0 ? (
            <div className="py-10 text-center text-xs text-muted-foreground">暂无数据</div>
          ) : (
            items.map((item, i) => (
              <div
                key={item.key}
                className="flex items-center gap-3 rounded-md px-2 py-1.5 text-xs hover:bg-muted/40"
              >
                <span className="w-6 shrink-0 text-right tabular-nums text-muted-foreground">
                  {(page - 1) * PAGE_SIZE + i + 1}
                </span>
                <span className="min-w-0 flex-1 truncate" title={item.key}>
                  {item.key}
                </span>
                <span className="shrink-0 font-semibold tabular-nums">{formatCount(item.count)}</span>
                <span className="w-12 shrink-0 text-right tabular-nums text-muted-foreground">
                  {item.percent.toFixed(1)}%
                </span>
              </div>
            ))
          )}
        </div>
        <div className="flex items-center justify-between border-t border-border/60 pt-3 text-xs text-muted-foreground">
          <span>共 {total} 条</span>
          <div className="flex items-center gap-2">
            <Button variant="outline" size="sm" className="h-7" disabled={page <= 1 || loading} onClick={() => load(page - 1)}>
              上一页
            </Button>
            <span className="tabular-nums">
              {page}/{totalPages}
            </span>
            <Button
              variant="outline"
              size="sm"
              className="h-7"
              disabled={page >= totalPages || loading}
              onClick={() => load(page + 1)}
            >
              下一页
            </Button>
          </div>
        </div>
      </DialogContent>
    </Dialog>
  );
}
