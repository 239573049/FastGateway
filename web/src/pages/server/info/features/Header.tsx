
import { Flexbox } from 'react-layout-kit';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import { useState } from 'react';
import CreateDomain from './CreateDomain';
import { reloadServer } from '@/services/ServerService';
import { useParams } from "react-router-dom";
import { useRouteStore } from '@/store/server';
import { toast } from 'sonner';

export default function Header() {
    const [createVisible, setCreateVisible] = useState(false);
    const { id } = useParams<{ id: string }>();
    const [loading, setLoading] = useRouteStore((state) => [state.loading, state.setLoading]);

    return (
        <div className="space-y-4">
            <Flexbox className="flex-row items-center justify-between px-6 py-4" horizontal>
                <h1 className="text-2xl font-semibold">
                    网关路由管理
                </h1>
                <div className="flex gap-3">
                    <Button
                        variant="outline"
                        disabled={loading}
                        onClick={() => {
                            setLoading(true);
                            reloadServer(id)
                                .then(() => {
                                    toast.success('刷新成功');
                                }).finally(() => {
                                    setLoading(false);
                                });
                        }}
                    >
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
            <Separator className="mb-6" />
            <CreateDomain visible={createVisible} onClose={() => {
                setCreateVisible(false);
            }} onOk={() => {
                setCreateVisible(false);
            }} />
        </div>
    );
}