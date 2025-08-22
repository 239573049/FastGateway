"use client"

import { PieChart, Pie, Cell, Legend } from "recharts"
import { ChartContainer, ChartTooltip, ChartTooltipContent, chartConfig } from "./chart"
import { cn } from "@/lib/utils"

interface DonutChartProps {
  data: Array<{ name: string; value: number }>
  className?: string
  label?: string
  noDataText?: string
  onValueChange?: (value: any) => void
  valueFormatter?: (value: number) => string
  variant?: "donut" | "pie"
  colors?: string[]
}

const defaultColors = [
  chartConfig.primary,
  "#68adee",
  "#1395ec", 
  "#0099ff",
  "#00497a"
]

export function DonutChart({
  data,
  className,
  label,
  noDataText = "No data available",
  onValueChange,
  valueFormatter = (value) => value.toString(),
  variant = "donut",
  colors = defaultColors,
}: DonutChartProps) {
  if (!data || data.length === 0) {
    return (
      <div className={cn("flex h-[200px] items-center justify-center text-muted-foreground", className)}>
        {noDataText}
      </div>
    )
  }

  const handleClick = (data: any) => {
    onValueChange?.(data)
  }

  return (
    <div className={cn("w-full", className)}>
      {label && (
        <div className="mb-2 text-center text-sm font-medium text-foreground">
          {label}
        </div>
      )}
      <ChartContainer config={chartConfig} className="h-[200px]">
        <PieChart>
          <ChartTooltip
            content={
              <ChartTooltipContent
                formatter={(value, name) => [
                  valueFormatter(Number(value)),
                  name,
                ]}
              />
            }
          />
          <Pie
            data={data}
            cx="50%"
            cy="50%"
            outerRadius={variant === "donut" ? 80 : 80}
            innerRadius={variant === "donut" ? 40 : 0}
            paddingAngle={2}
            dataKey="value"
            onClick={handleClick}
          >
            {data.map((_, index) => (
              <Cell 
                key={`cell-${index}`} 
                fill={colors[index % colors.length]}
                className="cursor-pointer hover:opacity-80 transition-opacity"
              />
            ))}
          </Pie>
          <Legend 
            formatter={(value) => (
              <span className="text-xs text-muted-foreground">{value}</span>
            )}
          />
        </PieChart>
      </ChartContainer>
    </div>
  )
}