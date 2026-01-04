"use client"

import { LineChart as RechartsLineChart, Line, XAxis, YAxis, CartesianGrid } from "recharts"
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from "./chart"
import { cn } from "@/lib/utils"

interface LineChartProps {
  data: Array<Record<string, any>>
  categories: string[]
  index: string
  className?: string
  valueFormatter?: (value: number) => string
  colors?: string[]
  showGrid?: boolean
  showXAxis?: boolean
  showYAxis?: boolean
}

const defaultColors = [
  "hsl(var(--primary))",
  "#68adee",
  "#1395ec", 
  "#0099ff",
  "#00497a"
]

export function LineChart({
  data,
  categories,
  index,
  className,
  valueFormatter = (value) => value.toString(),
  colors = defaultColors,
  showGrid = true,
  showXAxis = true,
  showYAxis = true,
}: LineChartProps) {
  if (!data || data.length === 0) {
    return (
      <div className={cn("flex h-[300px] items-center justify-center text-muted-foreground", className)}>
        No data available
      </div>
    )
  }

  const config = categories.reduce((acc, category, idx) => {
    acc[category] = {
      label: category,
      color: colors[idx % colors.length],
    }
    return acc
  }, {} as ChartConfig)

  return (
    <div className={cn("w-full", className)}>
      <ChartContainer config={config} className="h-[300px]">
        <RechartsLineChart
          data={data}
          margin={{
            top: 5,
            right: 30,
            left: 20,
            bottom: 5,
          }}
        >
          {showGrid && (
            <CartesianGrid 
              strokeDasharray="3 3" 
              className="stroke-muted"
            />
          )}
          {showXAxis && (
            <XAxis
              dataKey={index}
              tickLine={false}
              axisLine={false}
              className="text-xs fill-muted-foreground"
            />
          )}
          {showYAxis && (
            <YAxis
              tickLine={false}
              axisLine={false}
              className="text-xs fill-muted-foreground"
              tickFormatter={valueFormatter}
            />
          )}
          <ChartTooltip
            content={
              <ChartTooltipContent
                formatter={(value, name) => [
                  valueFormatter(Number(value)),
                  name,
                ]}
                labelFormatter={(label) => `${index}: ${label}`}
              />
            }
          />
          {categories.map((category, index) => (
            <Line
              key={category}
              type="monotone"
              dataKey={category}
              stroke={colors[index % colors.length]}
              strokeWidth={2}
              dot={{
                fill: colors[index % colors.length],
                strokeWidth: 2,
                r: 4,
              }}
              activeDot={{
                r: 6,
                stroke: colors[index % colors.length],
                strokeWidth: 2,
              }}
            />
          ))}
        </RechartsLineChart>
      </ChartContainer>
    </div>
  )
}
