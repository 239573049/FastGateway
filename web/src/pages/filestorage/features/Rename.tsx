import { Input } from "@/components/ui/input";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { useEffect, useState } from "react";
import { message } from "@/utils/toast";
import { renameFile } from "@/services/FileStorageService";

interface RenameProps {
    visible: boolean;
    id: string;
    name: string;
    drives: string;
    path: string;
    isFile: boolean;
    onClose: () => void;
    onOk: (name: string) => void;
}


export default function Rename(props: RenameProps) {
    const { visible,  name, isFile, onClose, onOk, path, drives } = props;
    const [inputValue, setInputValue] = useState(name);
    const [loading, setLoading] = useState(false);

    const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setInputValue(e.target.value);
    }

    const onOkClick = async () => {
        if (loading) {
            return;
        }
        setLoading(true);

        renameFile(path, drives, inputValue)
            .then(() => {
                message.success('重命名成功');
                onOk(inputValue);
            })
            .catch(e => {
                console.error(e);
            })
            .finally(() => {
                setLoading(false);
            });
    }

    useEffect(() => {
        setInputValue(name);
    }, [name]);


    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{`重命名${isFile ? '文件' : '文件夹'}`}</DialogTitle>
                </DialogHeader>
                <div className="space-y-4">
                    <Input value={inputValue} onChange={onInputChange} />
                </div>
                <DialogFooter>
                    <ShadcnButton variant="outline" onClick={onClose}>
                        取消
                    </ShadcnButton>
                    <ShadcnButton onClick={onOkClick} disabled={loading}>
                        {loading ? '重命名中...' : '重命名'}
                    </ShadcnButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}