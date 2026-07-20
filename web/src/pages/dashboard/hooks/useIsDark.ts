import { useEffect, useState } from "react";
import { useTheme } from "@/components/theme-provider";

/** 解析当前实际主题（theme=system 时跟随系统并响应变化） */
export function useIsDark(): boolean {
  const { theme } = useTheme();
  const [systemDark, setSystemDark] = useState(
    () => typeof window !== "undefined" && window.matchMedia("(prefers-color-scheme: dark)").matches
  );

  useEffect(() => {
    const media = window.matchMedia("(prefers-color-scheme: dark)");
    const handler = (e: MediaQueryListEvent) => setSystemDark(e.matches);
    media.addEventListener("change", handler);
    return () => media.removeEventListener("change", handler);
  }, []);

  if (theme === "dark") return true;
  if (theme === "light") return false;
  return systemDark;
}
