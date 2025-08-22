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
      <ChartContainer config={chartConfig} className="h-[200px]">
        <RechartsBarChart
          data={data}
          margin={{
            top: 0,
            right: 0,
            left: 0,
            bottom: 0,
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
            />
          ))}
        </RechartsBarChart>
      </ChartContainer>
    </div>
  )
}