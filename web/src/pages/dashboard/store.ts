import create from "zustand";
import type { StatRange } from "./types";

interface DashboardStore {
  range: StatRange;
  host?: string;
  setRange: (range: StatRange) => void;
  setHost: (host?: string) => void;
}

export const useDashboardStore = create<DashboardStore>((set) => ({
  range: "24h",
  host: undefined,
  setRange: (range) => set({ range }),
  setHost: (host) => set({ host }),
}));
