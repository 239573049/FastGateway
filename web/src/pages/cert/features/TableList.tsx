import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { Button } from "@/components/ui/button";
import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";

interface TableProps {
    columns: any[];
    dataSources: any[];
    onPaginationChange?: (page: number, pageSize: number) => void;
    total?: number;
    pageSize?: number;
    current?: number;
    loading?: boolean;
    height?: number | string;
    maxHeight?: number | string;
    scrollX?: number | string;
}

function TableList(
    {
        columns,
        dataSources,
        onPaginationChange,
        total = 0,
        pageSize = 10,
        current = 1,
        loading,
        height = 500
    }: TableProps
) {
    const totalPages = Math.ceil(total / pageSize);
    const startItem = (current - 1) * pageSize + 1;
    const endItem = Math.min(current * pageSize, total);

    if (loading) {
        return (
            <div className="border rounded-lg p-8 bg-card shadow-sm">
                <div className="flex items-center justify-center space-x-3">
                    <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
                    <span className="text-muted-foreground font-medium">加载中...</span>
                </div>
            </div>
        );
    }

    return (
        <div className="border rounded-lg bg-card shadow-sm overflow-hidden">
            <div className="overflow-auto" style={{ maxHeight: height }}>
                <Table>
                    <TableHeader>
                        <TableRow className="bg-muted/40 hover:bg-muted/40">
                            {columns.map((column, index) => (
                                <TableHead key={column.key || index} className="font-semibold text-foreground">
                                    {column.title}
                                </TableHead>
                            ))}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {dataSources.length === 0 ? (
                            <TableRow className="hover:bg-transparent">
                                <TableCell colSpan={columns.length} className="text-center py-12 text-muted-foreground">
                                    <div className="flex flex-col items-center space-y-2">
                                        <div className="w-12 h-12 rounded-full bg-muted/50 flex items-center justify-center">
                                            <span className="text-xl">📋</span>
                                        </div>
                                        <p className="font-medium">暂无数据</p>
                                        <p className="text-sm">还没有任何记录</p>
                                    </div>
                                </TableCell>
                            </TableRow>
                        ) : (
                            dataSources.map((row, rowIndex) => (
                                <TableRow key={row.id || rowIndex} className="hover:bg-muted/50 transition-colors duration-200">
                                    {columns.map((column, colIndex) => (
                                        <TableCell key={column.key || colIndex} className="py-3">
                                            {column.render ? column.render(row[column.dataIndex], row, rowIndex) : row[column.dataIndex]}
                                        </TableCell>
                                    ))}
                                </TableRow>
                            ))
                        )}
                    </TableBody>
                </Table>
            </div>
            
            {/* 分页 */}
            {total > 0 && (
                <div className="flex items-center justify-between px-6 py-4 border-t bg-muted/20">
                    <div className="text-sm text-muted-foreground font-medium">
                        第 {startItem}-{endItem} 条，共 {total} 条
                    </div>
                    
                    <div className="flex items-center space-x-2">
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onPaginationChange?.(current - 1, pageSize)}
                            disabled={current <= 1}
                            className="hover:bg-muted/50"
                        >
                            <ChevronLeftIcon className="h-4 w-4" />
                        </Button>
                        
                        <div className="flex items-center space-x-1">
                            {Array.from({ length: Math.min(5, totalPages) }, (_, i) => {
                                let pageNum;
                                if (totalPages <= 5) {
                                    pageNum = i + 1;
                                } else if (current <= 3) {
                                    pageNum = i + 1;
                                } else if (current >= totalPages - 2) {
                                    pageNum = totalPages - 4 + i;
                                } else {
                                    pageNum = current - 2 + i;
                                }
                                
                                return (
                                    <Button
                                        key={pageNum}
                                        variant={current === pageNum ? "default" : "outline"}
                                        size="sm"
                                        onClick={() => onPaginationChange?.(pageNum, pageSize)}
                                        className={current === pageNum ? "shadow-sm" : "hover:bg-muted/50"}
                                    >
                                        {pageNum}
                                    </Button>
                                );
                            })}
                        </div>
                        
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onPaginationChange?.(current + 1, pageSize)}
                            disabled={current >= totalPages}
                            className="hover:bg-muted/50"
                        >
                            <ChevronRightIcon className="h-4 w-4" />
                        </Button>
                    </div>
                </div>
            )}
        </div>
    );
}

export default TableList;