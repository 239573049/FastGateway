import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { ArrowLeft, Plus, RefreshCw } from "lucide-react";
import { toast } from "sonner";

import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
    Breadcrumb,
    BreadcrumbItem,
    BreadcrumbLink,
    BreadcrumbList,
    BreadcrumbPage,
    BreadcrumbSeparator,
} from "@/components/ui/breadcrumb";
import { reloadServer } from "@/services/ServerService";
import { useDomainStore, useRouteStore } from "@/store/server";

import CreateDomain from "./CreateDomain";

export default function Header() {
    const navigate = useNavigate();
    const [createVisible, setCreateVisible] = useState(false);
    const { id } = useParams<{ id: string }>();

    const [loading, setLoading] = useRouteStore((state) => [
        state.loading,
        state.setLoading,
    ]);
    const { domains, loadingDomains, setLoadingDomains } = useDomainStore();

    const handleRefreshRoute = () => {
        if (!id) {
            toast.error("服务 ID 无效");
            return;
        }

        setLoading(true);
        reloadServer(id)
            .then(() => {
                toast.success("刷新成功");
                setLoadingDomains(!loadingDomains);
            })
            .catch(() => {
                toast.error("刷新失败");
            })
            .finally(() => {
                setLoading(false);
            });
    };

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
                <div className="space-y-2">
                    <Breadcrumb>
                        <BreadcrumbList>
                            <BreadcrumbItem>
                                <BreadcrumbLink asChild>
                                    <Link to="/server">服务管理</Link>
                                </BreadcrumbLink>
                            </BreadcrumbItem>
                            <BreadcrumbSeparator />
                            <BreadcrumbItem>
                                <BreadcrumbPage>路由管理</BreadcrumbPage>
                            </BreadcrumbItem>
                        </BreadcrumbList>
                    </Breadcrumb>

                    <div className="flex flex-wrap items-center gap-2">
                        <h2 className="text-3xl font-semibold tracking-tight">
                            网关路由
                        </h2>
                        {id && (
                            <Badge variant="outline" className="font-mono">
                                {id}
                            </Badge>
                        )}
                        <Badge variant="secondary" className="font-normal">
                            {domains.length} 条
                        </Badge>
                    </div>

                    <p className="text-sm text-muted-foreground">
                        为该节点配置域名、路径与转发策略
                    </p>
                </div>

                <div className="flex w-full items-center gap-2 overflow-x-auto pb-1 md:w-auto md:overflow-visible">
                    <Button
                        variant="outline"
                        size="sm"
                        onClick={() => navigate("/server")}
                        aria-label="返回"
                        className="shrink-0"
                    >
                        <ArrowLeft className="h-4 w-4 sm:mr-2" />
                        <span className="hidden sm:inline">返回</span>
                    </Button>
                    <Button
                        variant="outline"
                        size="sm"
                        disabled={loading || !id}
                        onClick={handleRefreshRoute}
                        aria-label="刷新路由"
                        className="shrink-0"
                    >
                        <RefreshCw className="h-4 w-4 sm:mr-2" />
                        <span className="hidden sm:inline">刷新路由</span>
                    </Button>
                    <Button
                        size="sm"
                        onClick={() => setCreateVisible(true)}
                        disabled={!id}
                        className="shrink-0"
                    >
                        <Plus className="mr-2 h-4 w-4" />
                        新增路由
                    </Button>
                </div>
            </div>

            <CreateDomain
                visible={createVisible}
                onClose={() => {
                    setCreateVisible(false);
                }}
                onOk={() => {
                    setCreateVisible(false);
                }}
            />
        </div>
    );
}
