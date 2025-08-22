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
            <div className="border rounded-lg p-8 bg-card">
                <div className="flex items-center justify-center">
                    <div className="animate-spin rounded-full h-6 w-6 border-b-2 border-primary"></div>
                    <span className="ml-2 text-muted-foreground">加载中...</span>
                </div>
            </div>
        );
    }

    return (
        <div className="border rounded-lg bg-card shadow-sm">
            <div className="overflow-auto" style={{ maxHeight: height }}>
                <Table>
                    <TableHeader>
                        <TableRow>
                            {columns.map((column, index) => (
                                <TableHead key={column.key || index}>
                                    {column.title}
                                </TableHead>
                            ))}
                        </TableRow>
                    </TableHeader>
                    <TableBody>
                        {dataSources.length === 0 ? (
                            <TableRow>
                                <TableCell colSpan={columns.length} className="text-center py-8 text-muted-foreground">
                                    暂无数据
                                </TableCell>
                            </TableRow>
                        ) : (
                            dataSources.map((row, rowIndex) => (
                                <TableRow key={row.id || rowIndex}>
                                    {columns.map((column, colIndex) => (
                                        <TableCell key={column.key || colIndex}>
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
                <div className="flex items-center justify-between px-4 py-3 border-t">
                    <div className="text-sm text-muted-foreground">
                        第 {startItem}-{endItem} 条，共 {total} 条
                    </div>
                    
                    <div className="flex items-center space-x-2">
                        <Button
                            variant="outline"
                            size="sm"
                            onClick={() => onPaginationChange?.(current - 1, pageSize)}
                            disabled={current <= 1}
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