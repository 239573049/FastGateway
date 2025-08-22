import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { message } from '@/utils/toast';
import { useState } from "react";
import { CreateBlacklist } from "@/services/BlacklistAndWhitelistService";
import { Badge } from "@/components/ui/badge";
import { X, Shield, ShieldCheck } from "lucide-react";
import { Textarea } from "@/components/ui/textarea";
import { Switch } from "@/components/ui/switch";

interface CreateBlacklistAndWhitelistProps {
    isBlacklist: boolean;
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

const CreateBlacklistAndWhitelist: React.FC<CreateBlacklistAndWhitelistProps> = ({
    visible,
    onClose,
    onOk,
    isBlacklist
}: CreateBlacklistAndWhitelistProps) => {
    const [value, setValue] = useState({
        ips: [] as string[],
        name: '',
        description: '',
        enable: true,
        isBlacklist: isBlacklist,
    });
    const [ipInput, setIpInput] = useState('');

    const handleChange = (field: string) => (e: { target: any; }) => {
        const { target } = e;
        const newValue = target.type === 'checkbox' ? target.checked : target.value;
        setValue({ ...value, [field]: newValue });
    };

    function save() {
        if (!value.name) {
            message.error('请输入名称');
            return;
        }
        if (!value.ips.length) {
            message.error('请输入IP');
            return
        }

        CreateBlacklist(value)
            .then(() => {
                message.success('新增成功');
                onOk();
            }).catch(() => {
                message.error('新增失败');
            });

    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="sm:max-w-[600px] max-h-[90vh] overflow-y-auto">
                <DialogHeader className="border-b pb-4">
                    <div className="flex items-center gap-2">
                        {isBlacklist ? 
                            <Shield className="h-5 w-5 text-destructive" /> : 
                            <ShieldCheck className="h-5 w-5 text-green-600" />
                        }
                        <DialogTitle className="text-xl font-semibold">
                            {isBlacklist ? '新增黑名单' : '新增白名单'}
                        </DialogTitle>
                    </div>
                </DialogHeader>
                
                <div className="space-y-6 py-6">
                    <div className="space-y-2">
                        <Label htmlFor="name" className="text-sm font-medium">
                            名称
                            <span className="text-destructive ml-1">*</span>
                        </Label>
                        <Input 
                            id="name"
                            value={value.name} 
                            onChange={handleChange("name")} 
                            placeholder={isBlacklist ? '如：恶意IP黑名单' : '如：可信IP白名单'}
                            className="w-full"
                        />
                    </div>
                    
                    <div className="space-y-2">
                        <Label htmlFor="description" className="text-sm font-medium">
                            描述
                        </Label>
                        <Textarea 
                            id="description"
                            value={value.description} 
                            onChange={handleChange("description")} 
                            placeholder={isBlacklist ? '请输入黑名单的描述信息' : '请输入白名单的描述信息'}
                            className="w-full min-h-[80px] resize-none"
                        />
                    </div>

                    <div className="space-y-4">
                        <Label className="text-sm font-medium">
                            IP地址列表
                            <span className="text-destructive ml-1">*</span>
                        </Label>
                        
                        <div className="border rounded-lg p-4 space-y-4">
                            <div className="flex flex-wrap gap-2">
                                {value.ips.map((ip, index) => (
                                    <Badge 
                                        key={index} 
                                        variant="secondary" 
                                        className="flex items-center gap-1.5 px-2.5 py-1 text-sm"
                                    >
                                        {ip}
                                        <button
                                            onClick={() => {
                                                const newIps = value.ips.filter((_, i) => i !== index);
                                                setValue({ ...value, ips: newIps });
                                            }}
                                            className="ml-1 hover:bg-destructive/10 rounded-full p-0.5 transition-colors"
                                        >
                                            <X className="h-3 w-3" />
                                        </button>
                                    </Badge>
                                ))}
                                {value.ips.length === 0 && (
                                    <div className="text-sm text-muted-foreground">
                                        暂无IP地址，请添加
                                    </div>
                                )}
                            </div>
                            
                            <div className="flex gap-2">
                                <Input
                                    value={ipInput}
                                    onChange={(e) => setIpInput(e.target.value)}
                                    placeholder="请输入IP地址 (如: 192.168.1.1)"
                                    onKeyPress={(e) => {
                                        if (e.key === 'Enter') {
                                            e.preventDefault();
                                            const reg = /^(\d{1,3}\.){3}\d{1,3}$/;
                                            if (!reg.test(ipInput.trim())) {
                                                message.error('请输入正确的IP格式');
                                                return;
                                            }
                                            if (!value.ips.includes(ipInput.trim())) {
                                                setValue({ ...value, ips: [...value.ips, ipInput.trim()] });
                                            }
                                            setIpInput('');
                                        }
                                    }}
                                    className="flex-1"
                                />
                                <ShadcnButton
                                    type="button"
                                    variant="outline"
                                    size="sm"
                                    onClick={() => {
                                        const reg = /^(\d{1,3}\.){3}\d{1,3}$/;
                                        if (!reg.test(ipInput.trim())) {
                                            message.error('请输入正确的IP格式');
                                            return;
                                        }
                                        if (!value.ips.includes(ipInput.trim())) {
                                            setValue({ ...value, ips: [...value.ips, ipInput.trim()] });
                                        }
                                        setIpInput('');
                                    }}
                                >
                                    添加
                                </ShadcnButton>
                            </div>
                        </div>
                    </div>

                    <div className="flex items-center justify-between rounded-lg border p-4">
                        <div className="space-y-0.5">
                            <Label htmlFor="enable" className="text-sm font-medium cursor-pointer">
                                启用{isBlacklist ? '黑名单' : '白名单'}
                            </Label>
                            <p className="text-sm text-muted-foreground">
                                {isBlacklist ? '启用后将阻止列表中的IP访问' : '启用后将允许列表中的IP访问'}
                            </p>
                        </div>
                        <Switch
                            id="enable"
                            checked={value.enable} 
                            onCheckedChange={(checked: boolean) => setValue({ ...value, enable: checked })} 
                        />
                    </div>
                </div>
                
                <DialogFooter className="border-t pt-4">
                    <ShadcnButton variant="outline" onClick={onClose}>
                        取消
                    </ShadcnButton>
                    <ShadcnButton onClick={() => save()}>
                        保存
                    </ShadcnButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

export default CreateBlacklistAndWhitelist;