"use client"

import { BarChart as RechartsBarChart, Bar, XAxis, YAxis, CartesianGrid } from "recharts"
import { ChartContainer, ChartTooltip, ChartTooltipContent, chartConfig } from "./chart"
import { cn } from "@/lib/utils"

interface BarChartProps {
  data: Array<Record<string, any>>
  categories: string[]
  index: string
  className?: string
  valueFormatter?: (value: number) => string
  colors?: string[]
  showGrid?: boolean
  showXAxis?: boolean
  showYAxis?: boolean
  barRadius?: number
  barSize?: number
  barGap?: number | string
}

const defaultColors = [
  chartConfig.primary,
  "#68adee",
  "#1395ec", 
  "#0099ff",
  "#00497a"
]

export function BarChart({
  data,
  categories,
  index,
  className,
  valueFormatter = (value) => value.toString(),
  colors = defaultColors,
  showGrid = false,
  showXAxis = false,
  showYAxis = true,
  barRadius = 3,
  barSize,
  barGap = 4,
}: BarChartProps) {
  if (!data || data.length === 0) {
    return (
      <div className={cn("flex h-[200px] items-center justify-center text-muted-foreground", className)}>
        No data available
      </div>
    )
  }

  return (
    <div className={cn("w-full", className)}>
      <ChartContainer config={chartConfig} className="h-full w-full">
        <RechartsBarChart
          data={data}
          margin={{
            top: 5,
            right: 5,
            left: showYAxis ? 0 : 5,
            bottom: 5,
          }}
          barGap={barGap}
          barCategoryGap="10%"
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
              domain={[0, 'dataMax']}
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
            <Bar
              key={category}
              dataKey={category}
              fill={colors[index % colors.length]}
              radius={[barRadius, barRadius, barRadius, barRadius]}
              minPointSize={5}
              maxBarSize={barSize}
            />
          ))}
        </RechartsBarChart>
      </ChartContainer>
    </div>
  )
}