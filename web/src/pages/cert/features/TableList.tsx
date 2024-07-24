import { Table } from 'antd';

interface TableProps {
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
    }: TableProps
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