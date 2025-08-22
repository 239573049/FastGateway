import FCheckbox from "@/components/FCheckbox";
import FInput from "@/components/FInput";
import { CreateCert } from "@/services/CertService";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { message } from '@/utils/toast';
import { useState } from "react";

interface CreateCertProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

export default function CreateCertPage({
    visible,
    onClose,
    onOk
}: CreateCertProps) {
    const [value, setValue] = useState({
        domain: '',
        autoRenew: true,
        email: '',
    });

    function handleOk() {
        // 判断是否输入了域名
        if (!value.domain) {
            message.error('请输入域名');
            return;
        }
        // 判断是否输入了邮箱
        if (!value.email) {
            message.error('请输入邮箱');
            return;
        }

        // 判断邮箱格式是否正确
        if (!/^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$/.test(value.email)) {
            message.error('邮箱格式不正确');
            return;
        }

        CreateCert(value)
            .then(() => {
                message.success('新增成功');
                onOk();
            }).catch(() => {
                message.error('新增失败');
            });
        
        onOk();
    }

    const handleChange = (field: string) => (e: { target: any; }) => {
        const { target } = e;
        const newValue = target.type === 'checkbox' ? target.checked : target.value;
        setValue({ ...value, [field]: newValue });
    };



    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-md">
                <DialogHeader>
                    <DialogTitle className="text-lg font-semibold">新增证书</DialogTitle>
                </DialogHeader>
                <div className="space-y-4 py-4">
                    <FInput 
                        value={value.domain} 
                        onChange={handleChange("domain")} 
                        label='证书域名' 
                        placeholder='请输入域名，如 example.com'
                        className="w-full"
                    />
                    <FInput
                        value={value.email} 
                        onChange={handleChange("email")} 
                        label='邮箱' 
                        placeholder='请输入邮箱地址'
                        className="w-full"
                    />
                    <FCheckbox
                        checked={value.autoRenew}
                        onCheckedChange={(checked) => setValue({ ...value, autoRenew: checked })}
                        label='自动续期' 
                        className="mt-2"
                    />
                </div>
                <DialogFooter className="gap-2">
                    <Button variant="outline" onClick={onClose} className="flex-1">
                        取消
                    </Button>
                    <Button onClick={handleOk} className="flex-1">
                        确定
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}