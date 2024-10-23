
import { Flexbox } from 'react-layout-kit';
import { Button, message } from 'antd';
import Divider from '@lobehub/ui/es/Form/components/FormDivider';
import { useState } from 'react';
import CreateDomain from './CreateDomain';
import { reloadServer } from '@/services/ServerService';
import { useParams } from "react-router-dom";
import { useRouteStore } from '@/store/server';

export default function Header() {
    const [createVisible, setCreateVisible] = useState(false);
    const { id } = useParams<{ id: string }>();
    const [loading, setLoading] = useRouteStore((state) => [state.loading, state.setLoading]);

    return (<>
        <Flexbox style={{
        }} horizontal>
            <span style={{
                fontSize: '18px',
                fontWeight: 'bold',
                marginLeft: '10px',
                marginTop: '10px',
                marginBottom: '10px',
                flex: 1,

            }}>
                网关路由管理
            </span>
            <div style={{
                marginRight: '20px',
                marginBottom: '20px'
            }}>
                <Button
                    type='primary'
                    loading={loading}
                    onClick={() => {
                        setLoading(true);
                        reloadServer(id)
                            .then(() => {
                                message.success('刷新成功');
                            }).finally(() => {
                                setLoading(false);
                            });
                    }}
                    style={{
                        marginRight: '10px',
                    }}>
                    刷新路由
                </Button>
                <Button
                    onClick={() => {
                        setCreateVisible(true);
                    }}
                >
                    新增路由
                </Button>
            </div>
        </Flexbox>
        <Divider />
        <CreateDomain visible={createVisible} onClose={() => {
            setCreateVisible(false);
        }
        } onOk={() => {
            setCreateVisible(false);
        }} />

    </>)
}