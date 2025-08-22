import { createDirectory } from "@/services/FileStorageService";
import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { message } from "@/utils/toast";
import { useState } from "react";

interface CreateDirectoryProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
    drives: string;
    path: string;
}

export default function CreateDirectory(props: CreateDirectoryProps) {
    const [name, setName] = useState('');
    function save() {
        createDirectory(props.path, props.drives, name)
            .then(() => {
                props.onOk();
                message.success('创建成功');
            })
    }

    return (
        <Dialog open={props.visible} onOpenChange={(open) => !open && props.onClose()}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>新建目录</DialogTitle>
                </DialogHeader>
                <div className="space-y-4">
                    <Input 
                        value={name} 
                        onChange={(e) => setName(e.target.value)} 
                        placeholder="请输入文件夹名称" 
                    />
                </div>
                <DialogFooter>
                    <ShadcnButton variant="outline" onClick={props.onClose}>
                        取消
                    </ShadcnButton>
                    <ShadcnButton onClick={() => save()}>
                        创建
                    </ShadcnButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}