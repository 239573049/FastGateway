import { Table } from 'antd';

interface ProtectConfigTableProps {
    columns: any[];
    dataSources: any[];
    onPaginationChange?: (page: number, pageSize: number) => void;
    total?: number;
    pageSize?: number;
    current?: number;
    loading?: boolean;

}

function TableList(
    {
        columns,
        dataSources,
        onPaginationChange,
        total,
        pageSize,
        current,
        loading
    }: ProtectConfigTableProps
) {
    return (
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
                }
            }}
        >
        </Table>
    );
}

export default TableList;