
import { Flexbox } from 'react-layout-kit';
import { Button, message } from 'antd';
import { Separator } from '@/components/ui/separator';
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
        <Flexbox className="flex-row items-center justify-between px-6 py-4" horizontal>
            <span className="text-xl font-semibold text-foreground">
                网关路由管理
            </span>
            <div className="flex gap-3">
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
                    className="bg-primary text-primary-foreground hover:bg-primary/90"
                >
                    刷新路由
                </Button>
                <Button
                    onClick={() => {
                        setCreateVisible(true);
                    }}
                    className="bg-secondary text-secondary-foreground hover:bg-secondary/80"
                >
                    新增路由
                </Button>
            </div>
        </Flexbox>
        <Separator className="mb-6" />
        <CreateDomain visible={createVisible} onClose={() => {
            setCreateVisible(false);
        }
        } onOk={() => {
            setCreateVisible(false);
        }} />

    </>)
}