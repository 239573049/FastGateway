import { useEffect, useRef } from "react";
import * as echarts from "echarts";
import { cn } from "@/lib/utils";

/**
 * 通用 ECharts 容器：ref 挂载（支持多实例）、ResizeObserver 自适应、卸载销毁。
 */
export function EChart({
  option,
  className,
  onInit,
  themeKey,
}: {
  option: echarts.EChartsOption;
  className?: string;
  onInit?: (chart: echarts.ECharts) => void;
  /** 变化时重建实例（主题切换用） */
  themeKey?: string;
}) {
  const containerRef = useRef<HTMLDivElement>(null);
  const chartRef = useRef<echarts.ECharts | null>(null);
  const onInitRef = useRef(onInit);
  onInitRef.current = onInit;

  useEffect(() => {
    if (!containerRef.current) return;

    const chart = echarts.init(containerRef.current);
    chartRef.current = chart;
    onInitRef.current?.(chart);

    const observer = new ResizeObserver(() => chart.resize());
    observer.observe(containerRef.current);

    return () => {
      observer.disconnect();
      chart.dispose();
      chartRef.current = null;
    };
  }, [themeKey]);

  useEffect(() => {
    chartRef.current?.setOption(option, { notMerge: true });
  }, [option, themeKey]);

  return <div ref={containerRef} className={cn("h-full w-full", className)} />;
}
