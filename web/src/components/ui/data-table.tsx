import { useEffect, useRef, useState } from "react"
import {
  type ColumnDef,
  type ColumnSizingState,
  flexRender,
  getCoreRowModel,
  getPaginationRowModel,
  useReactTable,
} from "@tanstack/react-table"

import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table"
import { Button } from "@/components/ui/button"

interface DataTableProps<TData, TValue> {
  columns: ColumnDef<TData, TValue>[]
  data: TData[]
  loading?: boolean
  pageSize?: number
  onPaginationChange?: (page: number, pageSize: number) => void
  total?: number
  current?: number
  className?: string
  tableContainerClassName?: string
  innerTableContainerClassName?: string
  footerClassName?: string
  resizable?: boolean
}

export function DataTable<TData, TValue>({
  columns,
  data,
  loading = false,
  pageSize = 10,
  onPaginationChange,
  total = 0,
  current = 1,
  className,
  tableContainerClassName,
  innerTableContainerClassName,
  footerClassName,
  resizable = false,
}: DataTableProps<TData, TValue>) {
  const [columnSizing, setColumnSizing] = useState<ColumnSizingState>({})
  const [containerWidth, setContainerWidth] = useState(0)
  const tableContainerRef = useRef<HTMLDivElement>(null)
  const topScrollRef = useRef<HTMLDivElement>(null)
  const hasManualSizing = Object.keys(columnSizing).length > 0
  const table = useReactTable({
    data,
    columns,
    getCoreRowModel: getCoreRowModel(),
    getPaginationRowModel: getPaginationRowModel(),
    manualPagination: true,
    enableColumnResizing: resizable,
    columnResizeMode: "onChange",
    defaultColumn: {
      minSize: 72,
      size: 160,
      maxSize: 640,
    },
    pageCount: Math.ceil(total / pageSize),
    onColumnSizingChange: setColumnSizing,
    state: {
      pagination: {
        pageIndex: current - 1,
        pageSize,
      },
      columnSizing,
    },
  })

  useEffect(() => {
    const element = tableContainerRef.current
    if (!resizable || !element) return
    const update = () => setContainerWidth(element.clientWidth)
    update()
    const observer = new ResizeObserver(update)
    observer.observe(element)
    return () => observer.disconnect()
  }, [resizable])

  const usesFitLayout = resizable && !hasManualSizing && (containerWidth === 0 || containerWidth >= 640)
  const showTopScroll = resizable && !usesFitLayout

  const syncTableFromTop = () => {
    if (!tableContainerRef.current || !topScrollRef.current) return
    tableContainerRef.current.scrollLeft = topScrollRef.current.scrollLeft
  }

  const syncTopFromTable = () => {
    if (!tableContainerRef.current || !topScrollRef.current) return
    topScrollRef.current.scrollLeft = tableContainerRef.current.scrollLeft
  }

  return (
    <div className={`space-y-4 ${className ?? ""}`}>
      <div className={`rounded-md border ${tableContainerClassName ?? ""}`}>
        {showTopScroll && (
          <div
            ref={topScrollRef}
            onScroll={syncTableFromTop}
            className="overflow-x-scroll overflow-y-hidden border-b bg-background"
            aria-label="表格横向滚动"
          >
            <div style={{ width: table.getTotalSize(), height: 1 }} />
          </div>
        )}
        <Table
          containerRef={tableContainerRef}
          containerClassName={innerTableContainerClassName}
          containerProps={{ onScroll: syncTopFromTable }}
          className={resizable ? "table-fixed" : undefined}
          style={resizable ? { width: usesFitLayout ? "100%" : table.getTotalSize(), minWidth: "100%" } : undefined}
        >
          <TableHeader>
            {table.getHeaderGroups().map((headerGroup) => (
              <TableRow key={headerGroup.id} className={resizable ? "bg-muted/50" : undefined}>
                {headerGroup.headers.map((header) => {
                  return (
                    <TableHead
                      key={header.id}
                      className={resizable ? "group/resize sticky top-0 z-10 overflow-hidden bg-muted/95 backdrop-blur-sm" : undefined}
                      style={resizable
                        ? usesFitLayout
                          ? { width: `${(header.getSize() / table.getTotalSize()) * 100}%` }
                          : { width: header.getSize() }
                        : undefined}
                    >
                      {header.isPlaceholder
                        ? null
                        : flexRender(
                            header.column.columnDef.header,
                            header.getContext()
                          )}
                      {resizable && header.column.getCanResize() && (
                        <button
                          type="button"
                          aria-label={`调整第 ${header.index + 1} 列宽度`}
                          title="拖动调整列宽，双击恢复默认宽度"
                          onMouseDown={header.getResizeHandler()}
                          onTouchStart={header.getResizeHandler()}
                          onDoubleClick={() => header.column.resetSize()}
                          className={`absolute inset-y-0 -right-1 z-10 w-2 cursor-col-resize touch-none select-none bg-transparent after:absolute after:inset-y-2 after:left-1/2 after:w-px after:-translate-x-1/2 after:bg-border hover:after:bg-primary ${header.column.getIsResizing() ? "after:bg-primary" : ""}`}
                        />
                      )}
                    </TableHead>
                  )
                })}
              </TableRow>
            ))}
          </TableHeader>
          <TableBody>
            {loading ? (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center"
                >
                  <div className="flex items-center justify-center">
                    <div className="h-4 w-4 animate-spin rounded-full border-2 border-primary border-t-transparent" />
                    <span className="ml-2 text-sm text-muted-foreground">加载中...</span>
                  </div>
                </TableCell>
              </TableRow>
            ) : table.getRowModel().rows?.length ? (
              table.getRowModel().rows.map((row) => (
                <TableRow
                  key={row.id}
                  data-state={row.getIsSelected() && "selected"}
                  className={resizable ? "bg-background" : undefined}
                >
                  {row.getVisibleCells().map((cell) => (
                    <TableCell key={cell.id} className={resizable ? "overflow-hidden bg-background" : undefined}>
                      {flexRender(cell.column.columnDef.cell, cell.getContext())}
                    </TableCell>
                  ))}
                </TableRow>
              ))
            ) : (
              <TableRow>
                <TableCell
                  colSpan={columns.length}
                  className="h-24 text-center text-muted-foreground"
                >
                  暂无数据
                </TableCell>
              </TableRow>
            )}
          </TableBody>
        </Table>
      </div>
      
      {total > 0 && (
        <div className={`flex items-center justify-between px-2 ${footerClassName ?? ""}`}>
          <div className="text-sm text-muted-foreground">
            第 {(current - 1) * pageSize + 1}-{Math.min(current * pageSize, total)} 条/共 {total} 条
          </div>
          <div className="flex items-center space-x-2">
            <Button
              variant="outline"
              size="sm"
              onClick={() => onPaginationChange?.(current - 1, pageSize)}
              disabled={current <= 1}
            >
              上一页
            </Button>
            <Button
              variant="outline"
              size="sm"
              onClick={() => onPaginationChange?.(current + 1, pageSize)}
              disabled={current >= Math.ceil(total / pageSize)}
            >
              下一页
            </Button>
          </div>
        </div>
      )}
    </div>
  )
}
