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
        <div className={cn("overflow-hidden rounded-lg border bg-card", className)}>
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
                        <span className="text-sm text-muted-foreground">
                            第 {range[0]}-{range[1]} 条/共 {total} 条
                        </span>
                    ),
                    className: "p-4"
                }}
                className="[&_.ant-table]:!bg-transparent [&_.ant-table-thead>tr>th]:!bg-muted/50 [&_.ant-table-thead>tr>th]:!border-b [&_.ant-table-tbody>tr>td]:!border-b [&_.ant-table-tbody>tr:hover>td]:!bg-muted/30"
                rowClassName="hover:bg-muted/20 transition-colors"
            />
        </div>
    );
}

export default TableList;