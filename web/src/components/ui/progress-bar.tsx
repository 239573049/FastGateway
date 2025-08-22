"use client"

import { cn } from "@/lib/utils"
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from "./tooltip"

interface ProgressBarProps {
  value: number
  className?: string
  color?: "blue" | "green" | "red" | "yellow" | "purple" | "primary"
  tooltip?: string
  showValue?: boolean
  size?: "sm" | "md" | "lg"
}

const colorVariants = {
  blue: "bg-blue-500",
  green: "bg-green-500", 
  red: "bg-red-500",
  yellow: "bg-yellow-500",
  purple: "bg-purple-500",
  primary: "bg-[#42C4C1]",
}

const sizeVariants = {
  sm: "h-2",
  md: "h-3", 
  lg: "h-4",
}

export function ProgressBar({
  value,
  className,
  color = "primary",
  tooltip,
  showValue = false,
  size = "md",
}: ProgressBarProps) {
  const clampedValue = Math.min(Math.max(value, 0), 100)
  
  const progressBar = (
    <div className={cn("w-full", className)}>
      {showValue && (
        <div className="mb-1 flex justify-between text-xs text-muted-foreground">
          <span>Progress</span>
          <span>{clampedValue.toFixed(1)}%</span>
        </div>
      )}
      <div className={cn(
        "w-full rounded-full bg-muted overflow-hidden",
        sizeVariants[size]
      )}>
        <div
          className={cn(
            "h-full rounded-full transition-all duration-300 ease-in-out",
            colorVariants[color]
          )}
          style={{ width: `${clampedValue}%` }}
        />
      </div>
    </div>
  )

  if (tooltip) {
    return (
      <TooltipProvider>
        <Tooltip>
          <TooltipTrigger asChild>
            {progressBar}
          </TooltipTrigger>
          <TooltipContent>
            <p>{tooltip}</p>
          </TooltipContent>
        </Tooltip>
      </TooltipProvider>
    )
  }

  return progressBar
}