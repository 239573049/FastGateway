import { memo, useState } from "react";
import { Plus } from "lucide-react";

import { Button } from "@/components/ui/button";

import StreamForwardDialog from "./StreamForwardDialog";

interface HeaderProps {
    onCreated: () => void;
}

const Header = memo(({ onCreated }: HeaderProps) => {
    const [createVisible, setCreateVisible] = useState(false);

    return (
        <div className="flex flex-col gap-4 md:flex-row md:items-end md:justify-between">
            <div className="space-y-1">
                <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">FastGateway</p>
                <h2 className="text-3xl font-semibold tracking-tight">端口转发</h2>
                <p className="text-sm text-muted-foreground">配置 TCP/UDP 四层端口转发，将流量裸转发到上游目标</p>
            </div>

            <div className="flex w-full items-center gap-2 md:w-auto">
                <Button size="sm" onClick={() => setCreateVisible(true)} className="shrink-0">
                    <Plus className="mr-2 h-4 w-4" />
                    新增转发
                </Button>
            </div>

            <StreamForwardDialog
                visible={createVisible}
                onClose={() => setCreateVisible(false)}
                onOk={() => {
                    setCreateVisible(false);
                    onCreated();
                }}
            />
        </div>
    );
});

export default Header;
