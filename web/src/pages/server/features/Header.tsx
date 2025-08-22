import { memo, useState } from "react";
import { Button } from '@/components/ui/button';
import CreateServer from "./CreateServer";
import { useServerStore } from "@/store/server";

const Header = memo(() => {
    const [createVisible, setCreateVisible] = useState(false);
    const { setLoadingServers, loadingServers } = useServerStore();

    return (
        <div className="border-b bg-background/95 backdrop-blur supports-[backdrop-filter]:bg-background/60">
            <div className="max-w-7xl mx-auto px-6 py-4">
                <div className="flex items-center justify-between">
                    <div className="flex items-center space-x-4">
                        <h1 className="text-2xl font-bold text-foreground">服务管理</h1>
                        <div className="text-sm text-muted-foreground">
                            管理和监控您的代理服务器
                        </div>
                    </div>
                    <div className="flex items-center space-x-3">
                        <Button 
                            onClick={() => {
                                setCreateVisible(true);
                            }}
                            className="bg-primary text-primary-foreground hover:bg-primary/90 shadow-sm"
                            size="sm"
                        >
                            <span className="mr-2">+</span>
                            新增服务
                        </Button>
                    </div>
                </div>
            </div>
            <CreateServer 
                visible={createVisible} 
                onClose={() => {
                    setCreateVisible(false);
                }} 
                onOk={() => {
                    setCreateVisible(false);
                    setLoadingServers(!loadingServers);
                }} 
            />
        </div>
    );
});

export default Header;