import { useEffect, useRef, useState } from "react";
import * as echarts from "echarts";
import { getChartTheme, SEQUENTIAL_BLUE } from "../../shared/chart-colors";
import { useIsDark } from "../../hooks/useIsDark";
import type { GeoItem } from "../../types";
import { ensureWorldMap } from "./map-utils";
import { WorldMap2D } from "./WorldMap2D";

let glLoaded = false;

async function loadGl(): Promise<boolean> {
  if (glLoaded) return true;
  const probe = document.createElement("canvas");
  const gl = probe.getContext("webgl") || probe.getContext("experimental-webgl");
  if (!gl) return false;
  try {
    await import("echarts-gl");
    glLoaded = true;
    return true;
  } catch (error) {
    console.warn("echarts-gl 加载失败，回落 2D 世界地图", error);
    return false;
  }
}

/** canvas 生成星空背景（避免外部纹理资源） */
function createStarfield(dark: boolean): HTMLCanvasElement {
  const canvas = document.createElement("canvas");
  canvas.width = 2048;
  canvas.height = 1024;
  const ctx = canvas.getContext("2d")!;
  const gradient = ctx.createRadialGradient(1024, 512, 100, 1024, 512, 1024);
  if (dark) {
    gradient.addColorStop(0, "#101624");
    gradient.addColorStop(1, "#05070d");
  } else {
    gradient.addColorStop(0, "#f2f6fc");
    gradient.addColorStop(1, "#dbe4f0");
  }
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, canvas.width, canvas.height);
  if (dark) {
    ctx.fillStyle = "rgba(255,255,255,0.8)";
    for (let i = 0; i < 400; i++) {
      const x = Math.random() * canvas.width;
      const y = Math.random() * canvas.height;
      const r = Math.random() * 1.1 + 0.2;
      ctx.globalAlpha = Math.random() * 0.8 + 0.2;
      ctx.beginPath();
      ctx.arc(x, y, r, 0, Math.PI * 2);
      ctx.fill();
    }
    ctx.globalAlpha = 1;
  }
  return canvas;
}

/**
 * 3D 地球：离屏 2D choropleth 作为球面贴图（echarts-gl 官方做法），
 * WebGL 不可用或 echarts-gl 与 echarts 版本不兼容时自动回落 WorldMap2D。
 */
export function Globe3D({ items, mode }: { items: GeoItem[]; mode: "all" | "blocked" }) {
  const isDark = useIsDark();
  const containerRef = useRef<HTMLDivElement>(null);
  const [fallback, setFallback] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let live = true;
    let chart: echarts.ECharts | null = null;
    let baseChart: echarts.ECharts | null = null;
    let observer: ResizeObserver | null = null;

    (async () => {
      const ok = await loadGl();
      if (!live) return;
      if (!ok) {
        setFallback(true);
        return;
      }

      const nameMap = await ensureWorldMap();
      if (!live || !containerRef.current) return;

      try {
        const theme = getChartTheme(isDark);
        const data = items.map((x) => ({ name: x.name, value: mode === "blocked" ? x.blocked : x.count }));
        const max = Math.max(...data.map((x) => x.value), 1);

        // 离屏世界地图 → 球面贴图
        const textureCanvas = document.createElement("canvas");
        textureCanvas.width = 2048;
        textureCanvas.height = 1024;
        baseChart = echarts.init(textureCanvas);
        baseChart.setOption({
          backgroundColor: isDark ? "#0b1220" : "#dbe7f5",
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
              map: "world",
              left: 0,
              top: 0,
              right: 0,
              bottom: 0,
              boundingCoords: [
                [-180, 90],
                [180, -90],
              ],
              itemStyle: {
                borderWidth: 0.4,
                borderColor: isDark ? "#3d4a5c" : "#8fa3ba",
                areaColor: isDark ? "#1e2836" : "#f2f4f7",
              },
              nameMap,
              data,
            },
          ],
        });

        chart = echarts.init(containerRef.current);
        chart.setOption({
          backgroundColor: "transparent",
          globe: {
            baseTexture: baseChart,
            environment: createStarfield(isDark),
            shading: "color",
            displacementScale: 0,
            atmosphere: { show: true, color: theme.text, glowPower: 4, innerGlowPower: 1 },
            light: { ambient: { intensity: 1 }, main: { intensity: 0.1 } },
            viewControl: {
              autoRotate: true,
              autoRotateSpeed: 6,
              autoRotateAfterStill: 3,
              distance: 180,
              minDistance: 120,
              maxDistance: 320,
            },
          },
          series: [],
        } as any);

        observer = new ResizeObserver(() => chart?.resize());
        observer.observe(containerRef.current);
        setReady(true);
      } catch (error) {
        console.warn("3D 地球初始化失败，回落 2D 世界地图", error);
        setFallback(true);
      }
    })();

    return () => {
      live = false;
      observer?.disconnect();
      chart?.dispose();
      baseChart?.dispose();
    };
  }, [items, mode, isDark]);

  if (fallback) return <WorldMap2D items={items} mode={mode} />;

  return (
    <div className="relative h-full w-full">
      <div ref={containerRef} className="h-full w-full" />
      {!ready && <div className="absolute inset-0 animate-pulse rounded-lg bg-muted/30" />}
    </div>
  );
}
