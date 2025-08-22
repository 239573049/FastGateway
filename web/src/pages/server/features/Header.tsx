import { Separator } from "@/components/ui/separator";
import { memo, useState } from "react";
import { Button } from '@/components/ui/button';
import CreateServer from "./CreateServer";
import { useServerStore } from "@/store/server";

const Header = memo(() => {
    const [createVisible, setCreateVisible] = useState(false);
    const { setLoadingServers, loadingServers } = useServerStore();

    return (
        <>
            <div className="flex justify-end mb-6 px-6">
                <Button 
                    onClick={() => {
                        setCreateVisible(true);
                    }}
                    className="bg-primary text-primary-foreground hover:bg-primary/90"
                >
                    新增服务
                </Button>
            </div>
            <Separator className="mb-6" />
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
        </>
    );
});

export default Header;