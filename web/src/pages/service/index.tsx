import { Avatar, Button, ButtonGroup, List } from "@douyinfe/semi-ui";
import { getlist } from "../../service/PortManagementService";
import { useEffect, useState } from "react";
import { PortManagementEntity } from "../../index.d";
export default function Service() {
    const [page, setPage] = useState(1);
    const [pageSize, setPageSize] = useState(10);
    const [total, setTotal] = useState(0);
    const [list, setList] = useState([]);

    async function loading() {
        const result = (await getlist(page, pageSize)) as any;
        setList(result.items);
        setTotal(result.total);
    }

    useEffect(() => {
        loading()
    }, [page, pageSize])

    return (<div>
        <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            alignItems: 'center',
            padding: 12,
            borderBottom: '1px solid var(--semi-color-border)',
        }}>
            <span style={{ fontSize: 20, fontWeight: 500 }}>服务管理</span>
            <ButtonGroup style={{ float: 'right' }}>
                <Button theme='light'>新建</Button>
            </ButtonGroup>
        </div>
        <div style={{ padding: 12, border: '1px solid var(--semi-color-border)', margin: 12 }}>
            <List
                dataSource={list}
                renderItem={(item: PortManagementEntity) => (
                    <List.Item
                        header={<Avatar color="blue">SE</Avatar>}
                        main={
                            <div>
                                <span style={{ color: 'var(--semi-color-text-0)', fontWeight: 500 }}>示例标题</span>
                                {item.name}
                            </div>
                        }
                        extra={
                            <ButtonGroup theme="borderless">
                                <Button>启用/停止</Button>
                            </ButtonGroup>
                        }
                    />
                )}
            />
        </div>
    </div>)
}