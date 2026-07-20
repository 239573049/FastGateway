import { useState } from "react";
import { Globe2 } from "lucide-react";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import { getStatisticsGeo } from "@/services/StatisticsService";
import { usePolling } from "../../hooks/usePolling";
import { useDashboardStore } from "../../store";
import { formatCount, type GeoItem } from "../../types";
import { ChinaMap } from "./ChinaMap";
import { WorldMap2D } from "./WorldMap2D";
import { Globe3D } from "./Globe3D";

export function GeoCard() {
  const { range, host } = useDashboardStore();
  const [scope, setScope] = useState<"world" | "china">("world");
  const [mode, setMode] = useState<"all" | "blocked">("all");
  const [dimension, setDimension] = useState<"3d" | "2d">("3d");
  const [items, setItems] = useState<GeoItem[]>([]);
  const [loading, setLoading] = useState(true);

  usePolling(
    async (signal) => {
      const res: any = await getStatisticsGeo(range, scope, mode, host, 20, { signal });
      const data = res?.data;
      setItems(Array.isArray(data?.items) ? data.items : []);
      setLoading(false);
    },
    60000,
    [range, host, scope, mode]
  );

  const topItems = items.slice(0, 8);
  const valueOf = (x: GeoItem) => (mode === "blocked" ? x.blocked : x.count);
  const maxValue = Math.max(...topItems.map(valueOf), 1);

  return (
    <Card className="border-border/60 bg-card/80 shadow-sm">
      <CardHeader className="pb-2">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <CardTitle className="flex items-center gap-2 text-sm font-medium">
            <Globe2 className="h-4 w-4 text-blue-500" />
            地理位置
          </CardTitle>
          <div className="flex flex-wrap items-center gap-2">
            {scope === "world" && (
              <ToggleGroup
                type="single"
                size="sm"
                variant="outline"
                value={dimension}
                onValueChange={(v: string) => v && setDimension(v as "3d" | "2d")}
              >
                <ToggleGroupItem value="3d" className="h-7 px-2.5 text-xs">
                  3D
                </ToggleGroupItem>
                <ToggleGroupItem value="2d" className="h-7 px-2.5 text-xs">
                  2D
                </ToggleGroupItem>
              </ToggleGroup>
            )}
            <ToggleGroup
              type="single"
              size="sm"
              variant="outline"
              value={scope}
              onValueChange={(v: string) => v && setScope(v as "world" | "china")}
            >
              <ToggleGroupItem value="world" className="h-7 px-2.5 text-xs">
                世界
              </ToggleGroupItem>
              <ToggleGroupItem value="china" className="h-7 px-2.5 text-xs">
                中国
              </ToggleGroupItem>
            </ToggleGroup>
            <ToggleGroup
              type="single"
              size="sm"
              variant="outline"
              value={mode}
              onValueChange={(v: string) => v && setMode(v as "all" | "blocked")}
            >
              <ToggleGroupItem value="all" className="h-7 px-2.5 text-xs">
                访问
              </ToggleGroupItem>
              <ToggleGroupItem value="blocked" className="h-7 px-2.5 text-xs">
                仅拦截
              </ToggleGroupItem>
            </ToggleGroup>
          </div>
        </div>
      </CardHeader>
      <CardContent>
        <div className="relative">
          <div className="h-[340px] md:h-[420px]">
            {scope === "china" ? (
              <ChinaMap items={items} mode={mode} />
            ) : dimension === "3d" ? (
              <Globe3D items={items} mode={mode} />
            ) : (
              <WorldMap2D items={items} mode={mode} />
            )}
          </div>

          <div className="absolute right-0 top-0 w-44 rounded-lg border border-border/50 bg-card/75 p-3 backdrop-blur-sm md:w-52">
            {loading ? (
              <div className="space-y-2">
                {Array.from({ length: 5 }).map((_, i) => (
                  <div key={i} className="h-3.5 animate-pulse rounded bg-muted/50" />
                ))}
              </div>
            ) : topItems.length === 0 ? (
              <div className="py-4 text-center text-xs text-muted-foreground">暂无数据</div>
            ) : (
              <div className="space-y-2">
                {topItems.map((item) => (
                  <div key={item.name}>
                    <div className="flex items-baseline justify-between gap-2 text-xs">
                      <span className="min-w-0 truncate text-foreground/90">{item.name}</span>
                      <span className="shrink-0 font-semibold tabular-nums">{formatCount(valueOf(item))}</span>
                    </div>
                    <div className="mt-0.5 h-1 overflow-hidden rounded-full bg-muted/60">
                      <div
                        className="h-full rounded-full"
                        style={{
                          width: `${Math.max((valueOf(item) / maxValue) * 100, 2)}%`,
                          backgroundColor: mode === "blocked" ? "#eb6834" : "#2a78d6",
                        }}
                      />
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}
