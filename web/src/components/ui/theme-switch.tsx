import * as React from "react"
import { Moon, Sun, Laptop } from "lucide-react"
import { Button } from "@/components/ui/button"
import { useTheme } from "@/components/theme-provider"
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu"

type ThemeOption = "light" | "dark" | "auto"

interface ThemeSwitchProps extends React.HTMLAttributes<HTMLDivElement> {
  onThemeSwitch?: (v: ThemeOption) => void
  themeMode?: ThemeOption
}

const ThemeSwitch = React.forwardRef<HTMLDivElement, ThemeSwitchProps>(
  ({ className, onThemeSwitch, themeMode, ...props }, ref) => {
    const { theme, setTheme } = useTheme()

    return (
      <div ref={ref} className={className} {...props}>
        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Toggle theme">
              <Sun className="h-[1.2rem] w-[1.2rem] rotate-0 scale-100 transition-all dark:-rotate-90 dark:scale-0" />
              <Moon className="absolute h-[1.2rem] w-[1.2rem] rotate-90 scale-0 transition-all dark:rotate-0 dark:scale-100" />
              <span className="sr-only">切换主题</span>
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => { setTheme("light"); onThemeSwitch?.("light") }}>
              <Sun className="mr-2 h-4 w-4" />
              <span>浅色</span>
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => { setTheme("dark"); onThemeSwitch?.("dark") }}>
              <Moon className="mr-2 h-4 w-4" />
              <span>深色</span>
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => { setTheme("system"); onThemeSwitch?.("auto") }}>
              <Laptop className="mr-2 h-4 w-4" />
              <span>跟随系统</span>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>
    )
  }
)
ThemeSwitch.displayName = "ThemeSwitch"

export { ThemeSwitch }