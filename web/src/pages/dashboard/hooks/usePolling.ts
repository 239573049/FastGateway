import { useEffect, useRef } from "react";

/**
 * 周期轮询：页面不可见时暂停，恢复可见立即补一次；
 * 每轮携带 AbortSignal，组件卸载/依赖变化时中止在途请求。
 */
export function usePolling(
  fn: (signal: AbortSignal) => Promise<void> | void,
  intervalMs: number,
  deps: unknown[] = []
) {
  const fnRef = useRef(fn);
  fnRef.current = fn;

  useEffect(() => {
    let timer: ReturnType<typeof setInterval> | null = null;
    let controller: AbortController | null = null;
    let disposed = false;

    const tick = () => {
      if (document.visibilityState === "hidden") return;
      controller?.abort();
      controller = new AbortController();
      Promise.resolve(fnRef.current(controller.signal)).catch((error) => {
        if ((error as Error)?.name !== "AbortError" && !disposed) console.error(error);
      });
    };

    const handleVisibility = () => {
      if (document.visibilityState === "visible") tick();
    };

    tick();
    timer = setInterval(tick, intervalMs);
    document.addEventListener("visibilitychange", handleVisibility);

    return () => {
      disposed = true;
      if (timer) clearInterval(timer);
      controller?.abort();
      document.removeEventListener("visibilitychange", handleVisibility);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intervalMs, ...deps]);
}
