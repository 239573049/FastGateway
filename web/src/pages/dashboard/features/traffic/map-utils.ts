import * as echarts from "echarts";

let chinaRegistered = false;
let worldRegistered = false;
let worldNameMap: Record<string, string> | null = null;

/** 懒加载 geojson（china 607KB / worlds 1MB，按需分包）并注册地图 */
export async function ensureChinaMap(): Promise<void> {
  if (chinaRegistered) return;
  const { default: china } = await import("@/assets/map/china.json");
  echarts.registerMap("china", china as any);
  chinaRegistered = true;
}

export async function ensureWorldMap(): Promise<Record<string, string>> {
  if (worldRegistered && worldNameMap) return worldNameMap;
  const [{ default: worlds }, { default: world }] = await Promise.all([
    import("@/assets/map/worlds.json"),
    import("@/assets/map/world.json"),
  ]);
  echarts.registerMap("world", worlds as any);
  worldNameMap = (world as any).namemap ?? {};
  worldRegistered = true;
  return worldNameMap!;
}

/** ip2region 省份名 → china.json 要素名（北京 → 北京市 等） */
const PROVINCE_ALIASES: Record<string, string> = {
  北京: "北京市",
  天津: "天津市",
  上海: "上海市",
  重庆: "重庆市",
  内蒙古: "内蒙古自治区",
  广西: "广西壮族自治区",
  西藏: "西藏自治区",
  宁夏: "宁夏回族自治区",
  新疆: "新疆维吾尔自治区",
  香港: "香港特别行政区",
  澳门: "澳门特别行政区",
  台湾: "台湾省",
};

export function normalizeProvince(name: string): string {
  if (!name) return name;
  if (PROVINCE_ALIASES[name]) return PROVINCE_ALIASES[name];
  if (/(省|市|自治区|特别行政区)$/.test(name)) return name;
  return `${name}省`;
}
