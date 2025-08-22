import { Table } from 'antd';
import { cn } from '@/lib/utils';

interface ProtectConfigTableProps {
    columns: any[];
    dataSources: any[];
    onPaginationChange?: (page: number, pageSize: number) => void;
    total?: number;
    pageSize?: number;
    current?: number;
    loading?: boolean;
    className?: string;
}

function TableList(
    {
        columns,
        dataSources,
        onPaginationChange,
        total,
        pageSize,
        current,
        loading,
        className
    }: ProtectConfigTableProps
) {
    return (
        <div className={cn("overflow-hidden rounded-lg border bg-card shadow-sm", className)}>
            <Table
                dataSource={dataSources}
                loading={loading}
                columns={columns}
                pagination={{
                    total: total,
                    pageSize: pageSize,
                    current: current,
                    onChange: (page, pageSize) => {
                        if (onPaginationChange) {
                            onPaginationChange(page, pageSize);
                        }
                    },
                    showSizeChanger: true,
                    showQuickJumper: true,
                    showTotal: (total, range) => (
                        <span className="text-sm text-muted-foreground font-medium">
                            第 {range[0]}-{range[1]} 条/共 {total} 条
                        </span>
                    ),
                    className: "px-6 py-4",
                    size: "small"
                }}
                className="[&_.ant-table]:!bg-transparent [&_.ant-table-thead>tr>th]:!bg-muted/40 [&_.ant-table-thead>tr>th]:!border-border [&_.ant-table-thead>tr>th]:!font-semibold [&_.ant-table-tbody>tr>td]:!border-border [&_.ant-table-tbody>tr:hover>td]:!bg-muted/50 [&_.ant-table-tbody>tr>td]:!py-3"
                rowClassName="hover:bg-muted/30 transition-all duration-200"
                size="middle"
            />
        </div>
    );
}

export default TableList;