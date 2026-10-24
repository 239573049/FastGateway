/**
 * 路由 chunk 加载器的单一来源。
 *
 * App.tsx 用它建 lazy 组件，侧边栏在 hover/focus 时用它提前把 chunk 拉下来 ——
 * 页面切换是 AnimatePresence mode="wait"，旧页卸载后新页还在下载的那段空窗
 * 会露出 Suspense fallback。鼠标移到菜单项到点击之间通常有 200ms 以上，
 * 足够加载完。
 *
 * 每个页面只在这里被 import() 一次，避免 bundler 切出重复 chunk。
 * dynamic import() 的结果本身带缓存，重复调用是免费的。
 */
export const PAGE_LOADERS = {
  "/dashboard": () => import("@/pages/dashboard/page"),
  "/server": () => import("@/pages/server/page"),
  "/server/:id": () => import("@/pages/server/info/page"),
  "/stream-forward": () => import("@/pages/stream-forward/page"),
  "/tunnel": () => import("@/pages/tunnel/page"),
  "/cluster": () => import("@/pages/cluster/page"),
  "/security/overview": () => import("@/pages/security/overview"),
  "/security/access": () => import("@/pages/security/access"),
  "/security/rate-limit": () => import("@/pages/protect-config/rate-limit"),
  "/security/threats": () => import("@/pages/security/threats"),
  "/security/risk": () => import("@/pages/security/risk"),
  "/security/logs": () => import("@/pages/security/logs"),
  "/cert": () => import("@/pages/cert/page"),
  "/filestorage": () => import("@/pages/filestorage/page"),
  "/setting": () => import("@/pages/system-setting/page"),
  "/about": () => import("@/pages/about/page"),
};

const triggered = new Set<string>();

/**
 * 预取某条路由的 chunk。未知路径直接忽略；同一路径只触发一次。
 * 失败在这里咽掉 —— 真正导航时还会再走一遍，届时由 Suspense 正常处理。
 */
export function preloadRoute(path: string) {
  if (triggered.has(path)) return;
  const loader = (PAGE_LOADERS as Record<string, (() => Promise<unknown>) | undefined>)[path];
  if (!loader) return;
  triggered.add(path);
  loader().catch(() => triggered.delete(path));
}
