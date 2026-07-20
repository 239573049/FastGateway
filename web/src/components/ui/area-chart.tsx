"use client"

import {
  AreaChart as RechartsAreaChart,
  Area,
  XAxis,
  YAxis,
  CartesianGrid,
  ReferenceDot,
} from "recharts"
import { ChartContainer, ChartTooltip, ChartTooltipContent, type ChartConfig } from "./chart"
import { cn } from "@/lib/utils"

interface AreaChartProps {
  data: Array<Record<string, any>>
  categories: string[]
  index: string
  className?: string
  valueFormatter?: (value: number) => string
  colors?: string[]
  categoryLabels?: Record<string, string>
  showGrid?: boolean
  showXAxis?: boolean
  showYAxis?: boolean
  /** 在最大值处标注峰值点（单系列时生效） */
  markPeak?: boolean
}

const defaultColors = ["#2a78d6", "#eb6834", "#008300", "#eda100"]

export function AreaChart({
  data,
  categories,
  index,
  className,
  valueFormatter = (value) => value.toString(),
  colors = defaultColors,
  categoryLabels,
  showGrid = true,
  showXAxis = true,
  showYAxis = false,
  markPeak = false,
}: AreaChartProps) {
  const config = categories.reduce((acc, category, idx) => {
    acc[category] = {
      label: categoryLabels?.[category] ?? category,
      color: colors[idx % colors.length],
    }
    return acc
  }, {} as ChartConfig)

  const gradientId = (category: string) => `area-fill-${category}`

  let peak: { x: any; y: number } | null = null
  if (markPeak && categories.length === 1 && data.length > 0) {
    const category = categories[0]
    for (const row of data) {
      const value = Number(row[category]) || 0
      if (peak === null || value > peak.y) peak = { x: row[index], y: value }
    }
    if (peak && peak.y <= 0) peak = null
  }

  return (
    <ChartContainer config={config} className={cn("h-full w-full", className)}>
      <RechartsAreaChart data={data} margin={{ top: 8, right: 8, left: 8, bottom: 0 }}>
        <defs>
          {categories.map((category, idx) => (
            <linearGradient key={category} id={gradientId(category)} x1="0" y1="0" x2="0" y2="1">
              <stop offset="0%" stopColor={colors[idx % colors.length]} stopOpacity={0.32} />
              <stop offset="100%" stopColor={colors[idx % colors.length]} stopOpacity={0.02} />
            </linearGradient>
          ))}
        </defs>
        {showGrid && <CartesianGrid strokeDasharray="3 3" vertical={false} stroke="var(--border)" opacity={0.5} />}
        <XAxis
          dataKey={index}
          hide={!showXAxis}
          tickLine={false}
          axisLine={false}
          tick={{ fontSize: 10 }}
          minTickGap={32}
        />
        <YAxis hide={!showYAxis} tickLine={false} axisLine={false} tick={{ fontSize: 10 }} width={40} />
        <ChartTooltip
          content={
            <ChartTooltipContent
              formatter={(value, name) => [valueFormatter(Number(value)), config[String(name)]?.label ?? name]}
            />
          }
        />
        {categories.map((category, idx) => (
          <Area
            key={category}
            type="monotone"
            dataKey={category}
            stroke={colors[idx % colors.length]}
            strokeWidth={2}
            fill={`url(#${gradientId(category)})`}
            dot={false}
            activeDot={{ r: 3 }}
            isAnimationActive={false}
          />
        ))}
        {peak && (
          <ReferenceDot
            x={peak.x}
            y={peak.y}
            r={3}
            fill={colors[0]}
            stroke="var(--card)"
            strokeWidth={2}
          />
        )}
      </RechartsAreaChart>
    </ChartContainer>
  )
}
