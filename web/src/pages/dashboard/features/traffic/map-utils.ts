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

/** 英文国名 → 中文展示名（兼容历史统计数据） */
const COUNTRY_ALIASES: Record<string, string> = {
  China: "中国",
  "United States": "美国",
  "United States of America": "美国",
  USA: "美国",
  US: "美国",
  Japan: "日本",
  "United Kingdom": "英国",
  UK: "英国",
  Singapore: "新加坡",
  Netherlands: "荷兰",
  Germany: "德国",
  France: "法国",
  Russia: "俄罗斯",
  "Russian Federation": "俄罗斯",
  Canada: "加拿大",
  Australia: "澳大利亚",
  India: "印度",
  "South Korea": "韩国",
  Korea: "韩国",
  "Republic of Korea": "韩国",
  "Hong Kong": "中国香港",
  Taiwan: "中国台湾",
  Macao: "中国澳门",
  Macau: "中国澳门",
  Brazil: "巴西",
  Italy: "意大利",
  Spain: "西班牙",
  Mexico: "墨西哥",
  Indonesia: "印度尼西亚",
  Turkey: "土耳其",
  "Saudi Arabia": "沙特阿拉伯",
  "United Arab Emirates": "阿联酋",
  UAE: "阿联酋",
  Vietnam: "越南",
  "Viet Nam": "越南",
  Thailand: "泰国",
  Malaysia: "马来西亚",
  Philippines: "菲律宾",
  Poland: "波兰",
  Sweden: "瑞典",
  Switzerland: "瑞士",
  Belgium: "比利时",
  Ireland: "爱尔兰",
  Norway: "挪威",
  Denmark: "丹麦",
  Finland: "芬兰",
  Austria: "奥地利",
  "New Zealand": "新西兰",
  "South Africa": "南非",
  Argentina: "阿根廷",
  Chile: "智利",
  Israel: "以色列",
  Ukraine: "乌克兰",
  Portugal: "葡萄牙",
  Greece: "希腊",
  "Czech Republic": "捷克",
  Czechia: "捷克",
  Romania: "罗马尼亚",
  Hungary: "匈牙利",
  Egypt: "埃及",
  Pakistan: "巴基斯坦",
  Bangladesh: "孟加拉国",
  Nigeria: "尼日利亚",
  Colombia: "哥伦比亚",
  Peru: "秘鲁",
  "Sri Lanka": "斯里兰卡",
  Cambodia: "柬埔寨",
  Myanmar: "缅甸",
  Laos: "老挝",
  Mongolia: "蒙古",
  Kazakhstan: "哈萨克斯坦",
  Uzbekistan: "乌兹别克斯坦",
  Iran: "伊朗",
  Iraq: "伊拉克",
  Qatar: "卡塔尔",
  Kuwait: "科威特",
  Oman: "阿曼",
  Bahrain: "巴林",
  Jordan: "约旦",
  Lebanon: "黎巴嫩",
  Syria: "叙利亚",
  Georgia: "格鲁吉亚",
};

export function normalizeCountry(name: string, nameMap?: Record<string, string> | null): string {
  if (!name) return name;
  if (COUNTRY_ALIASES[name]) return COUNTRY_ALIASES[name];
  if (nameMap?.[name]) return nameMap[name];
  // world.json namemap 是 英文→中文；也兼容大小写差异
  if (nameMap) {
    const hit = Object.entries(nameMap).find(([en]) => en.toLowerCase() === name.toLowerCase());
    if (hit) return hit[1];
  }
  return name;
}

