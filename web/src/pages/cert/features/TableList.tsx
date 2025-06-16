import { Table } from 'antd';
import { CSSProperties } from 'react';

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
        total,
        pageSize,
        current,
        loading,
        height = 500,
        maxHeight,
        scrollX
    }: TableProps
) {
    // 计算滚动配置
    const scrollConfig: any = {};
    
    // 设置垂直滚动
    if (height || maxHeight) {
        scrollConfig.y = height || maxHeight;
    }
    
    // 设置水平滚动
    if (scrollX) {
        scrollConfig.x = scrollX;
    }
    
    // 如果列数较多，自动启用水平滚动
    if (columns.length > 6 && !scrollX) {
        scrollConfig.x = 'max-content';
    }

    return (
        <div style={{ 
            border: '1px solid #f0f0f0', 
            borderRadius: '6px',
            overflow: 'hidden',
            boxShadow: '0 2px 8px rgba(0, 0, 0, 0.1)'
        }}>
            <Table
                dataSource={dataSources}
                loading={loading}
                columns={columns}
                scroll={Object.keys(scrollConfig).length > 0 ? scrollConfig : undefined}
                pagination={{
                    total: total,
                    pageSize: pageSize,
                    current: current,
                    showSizeChanger: true,
                    showQuickJumper: true,
                    showTotal: (total, range) => 
                        `第 ${range[0]}-${range[1]} 条，共 ${total} 条`,
                    pageSizeOptions: ['10', '20', '50', '100'],
                    onChange: (page, pageSize) => {
                        if (onPaginationChange) {
                            onPaginationChange(page, pageSize);
                        }
                    },
                    onShowSizeChange: (current, size) => {
                        if (onPaginationChange) {
                            onPaginationChange(current, size);
                        }
                    }
                }}
                style={{
                    backgroundColor: '#fff'
                }}
                tableLayout="fixed"
                size="middle"
            />
        </div>
    );
}

export default TableList;