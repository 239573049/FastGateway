// 使用zustand提供主题切换功能
import create from 'zustand'

export type Theme = 'light' | 'dark' | 'auto'

interface UserStore {
    theme: Theme
    setTheme: (theme: Theme) => void
    hideSettingsMoveGuide: boolean
    setHideSettingsMoveGuide: (hideSettingsMoveGuide: boolean) => void
}

export const useUserStore = create<UserStore>((set) => ({
    theme: (localStorage.getItem('theme') as Theme) || 'auto',
    setTheme: (theme) => {
        localStorage.setItem('theme', theme)
        set({ theme })
    },
    hideSettingsMoveGuide: false,
    setHideSettingsMoveGuide: (hideSettingsMoveGuide) => set({ hideSettingsMoveGuide })
}))