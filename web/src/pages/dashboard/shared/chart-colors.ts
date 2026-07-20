/**
 * 图表配色（dataviz 参考调色板，亮/暗双模式已通过 CVD 校验）。
 * 分类色按固定顺序取用，不按图表循环重排。
 */
export const CATEGORICAL_LIGHT = [
  "#2a78d6", // blue
  "#008300", // green
  "#e87ba4", // magenta
  "#eda100", // yellow
  "#1baf7a", // aqua
  "#eb6834", // orange
  "#4a3aa7", // violet
  "#e34948", // red
];

export const CATEGORICAL_DARK = [
  "#3987e5",
  "#008300",
  "#d55181",
  "#c98500",
  "#199e70",
  "#d95926",
  "#9085e9",
  "#e66767",
];

/** 状态色（保留用途：状态码/健康度，不作为普通系列色） */
export const STATUS_COLORS: Record<string, string> = {
  "2xx": "#0ca30c",
  "3xx": "#2a78d6",
  "4xx": "#fab219",
  "5xx": "#d03b3b",
  other: "#898781",
};

/** 顺序色（地图 choropleth 用，单一蓝色 浅→深） */
export const SEQUENTIAL_BLUE = ["#cde2fb", "#9ec5f4", "#6da7ec", "#3987e5", "#256abf", "#184f95", "#0d366b"];

export function getCategorical(isDark: boolean): string[] {
  return isDark ? CATEGORICAL_DARK : CATEGORICAL_LIGHT;
}

export function statusColorOf(statusCode: string): string {
  const cls = `${statusCode.charAt(0)}xx`;
  return STATUS_COLORS[cls] ?? STATUS_COLORS.other;
}

/** ECharts 主题基色（画布不吃 CSS 变量，给显式 hex） */
export function getChartTheme(isDark: boolean) {
  return isDark
    ? {
        text: "#c3c2b7",
        textStrong: "#ffffff",
        axis: "#383835",
        grid: "#2c2c2a",
        tooltipBg: "#262624",
        border: "rgba(255,255,255,0.10)",
        mapBase: "#26262a",
        ocean: "#0b1220",
      }
    : {
        text: "#52514e",
        textStrong: "#0b0b0b",
        axis: "#c3c2b7",
        grid: "#e1e0d9",
        tooltipBg: "#ffffff",
        border: "rgba(11,11,11,0.10)",
        mapBase: "#ececec",
        ocean: "#e8eef7",
      };
}
