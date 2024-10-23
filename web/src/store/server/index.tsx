
import { Server } from "@/types";
import { create } from "zustand";

interface ServerStore {
    loadingServers: boolean;
    setLoadingServers: (loading: boolean) => void;
    servers: Server[];
    setServers: (servers: Server[]) => void;
}

export const useServerStore = create<ServerStore>((set) => ({
    servers: [],
    setServers: (servers) => set({ servers }),
    setLoadingServers: (loadingServers) => set({ loadingServers }),
    loadingServers: false,
}));


interface DomainStore {
    loadingDomains: boolean;
    setLoadingDomains: (loading: boolean) => void;
    domains: any[];
    setDomains: (domains: any[]) => void;
}

export const useDomainStore = create<DomainStore>((set) => ({
    domains: [],
    setDomains: (domains) => set({ domains }),
    setLoadingDomains: (loadingDomains) => set({ loadingDomains }),
    loadingDomains: false,
}));

interface RouteStore {
    loading: boolean;
    setLoading: (loading: boolean) => void;
}

export const useRouteStore = create<RouteStore>((set) => ({
    setLoading: (loading) => set({ loading }),
    loading: false,
}));