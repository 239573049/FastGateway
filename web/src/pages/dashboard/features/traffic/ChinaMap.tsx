import { useEffect, useMemo, useState } from "react";
import { EChart } from "../../shared/EChart";
import { getChartTheme, SEQUENTIAL_BLUE } from "../../shared/chart-colors";
import { useIsDark } from "../../hooks/useIsDark";
import type { GeoItem } from "../../types";
import { ensureChinaMap, normalizeProvince } from "./map-utils";

export function ChinaMap({ items, mode }: { items: GeoItem[]; mode: "all" | "blocked" }) {
  const isDark = useIsDark();
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let live = true;
    ensureChinaMap().then(() => live && setReady(true));
    return () => {
      live = false;
    };
  }, []);

  const option = useMemo(() => {
    const theme = getChartTheme(isDark);
    const data = items.map((x) => ({
      name: normalizeProvince(x.name),
      value: mode === "blocked" ? x.blocked : x.count,
    }));
    const max = Math.max(...data.map((x) => x.value), 1);

    return {
      backgroundColor: "transparent",
      tooltip: {
        triggerOn: "mousemove",
        backgroundColor: theme.tooltipBg,
        borderColor: theme.border,
        textStyle: { color: theme.textStrong, fontSize: 12 },
        formatter: (e: any) => `${e.name}<br/>${mode === "blocked" ? "拦截" : "访问"}：${e.value || 0}`,
      },
      visualMap: {
        type: "continuous",
        min: 0,
        max,
        show: false,
        inRange: { color: SEQUENTIAL_BLUE },
      },
      series: [
        {
          type: "map",
          name: mode === "blocked" ? "拦截" : "访问量",
          map: "china",
          roam: true,
          zoom: 1.2,
          label: { show: false, color: theme.text },
          itemStyle: {
            borderWidth: 0.5,
            borderColor: theme.axis,
            areaColor: theme.mapBase,
          },
          emphasis: {
            label: { show: true, color: theme.textStrong },
            itemStyle: { areaColor: "#eda100" },
          },
          select: { disabled: true },
          data,
        },
      ],
    } as any;
  }, [items, mode, isDark]);

  if (!ready) return <div className="h-full w-full animate-pulse rounded-lg bg-muted/30" />;
  return <EChart option={option} themeKey={isDark ? "dark" : "light"} />;
}
