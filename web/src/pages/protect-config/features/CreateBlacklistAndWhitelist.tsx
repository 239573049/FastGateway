import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Checkbox } from "@/components/ui/checkbox";
import { Label } from "@/components/ui/label";
import { message } from '@/utils/toast';
import { useState } from "react";
import { CreateBlacklist } from "@/services/BlacklistAndWhitelistService";
import { Badge } from "@/components/ui/badge";
import { X, Shield, ShieldCheck } from "lucide-react";
import { Card, CardContent } from "@/components/ui/card";
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
                        {isBlacklist ? <Shield className="h-5 w-5 text-destructive" /> : <ShieldCheck className="h-5 w-5 text-green-600" />}
                        <DialogTitle className="text-xl font-bold">
                            {isBlacklist ? '新增黑名单' : '新增白名单'}
                        </DialogTitle>
                    </div>
                </DialogHeader>
                
                <div className="space-y-6 py-6">
                    <div className="space-y-2">
                        <Label htmlFor="name" className="text-sm font-medium">名称</Label>
                        <Input 
                            id="name"
                            value={value.name} 
                            onChange={handleChange("name")} 
                            placeholder='请输入名称' 
                            className="w-full"
                        />
                    </div>
                    
                    <div className="space-y-2">
                        <Label htmlFor="description" className="text-sm font-medium">描述</Label>
                        <Textarea 
                            id="description"
                            value={value.description} 
                            onChange={handleChange("description")} 
                            placeholder='请输入描述' 
                            className="w-full min-h-[80px]"
                        />
                    </div>

                    <div className="space-y-4">
                        <Label className="text-sm font-medium">IP地址列表</Label>
                        
                        <Card className="border-dashed">
                            <CardContent className="p-4">
                                <div className="flex flex-wrap gap-2 mb-4">
                                    {value.ips.map((ip, index) => (
                                        <Badge 
                                            key={index} 
                                            variant="secondary" 
                                            className="flex items-center gap-1.5 px-3 py-1.5 bg-primary/10 text-primary hover:bg-primary/20 transition-colors"
                                        >
                                            {ip}
                                            <button
                                                onClick={() => {
                                                    const newIps = value.ips.filter((_, i) => i !== index);
                                                    setValue({ ...value, ips: newIps });
                                                }}
                                                className="ml-1 hover:bg-destructive/20 rounded-full p-0.5 transition-colors"
                                            >
                                                <X className="h-3 w-3" />
                                            </button>
                                        </Badge>
                                    ))}
                                </div>
                                
                                <div className="flex gap-2">
                                    <Input
                                        value={ipInput}
                                        onChange={(e) => setIpInput(e.target.value)}
                                        placeholder="请输入IP地址 (xxx.xxx.xxx.xxx)"
                                        onKeyPress={(e) => {
                                            if (e.key === 'Enter') {
                                                e.preventDefault();
                                                const reg = /^('d+)'.('d+)'.('d+)'.('d+)$/;
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
                                        onClick={() => {
                                            const reg = /^('d+)'.('d+)'.('d+)'.('d+)$/;
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
                            </CardContent>
                        </Card>
                    </div>

                    <div className="flex items-center space-x-2">
                        <Switch
                            id="enable"
                            checked={value.enable} 
                            onCheckedChange={(checked: boolean) => setValue({ ...value, enable: checked })} 
                        />
                        <Label htmlFor="enable" className="text-sm font-medium cursor-pointer">
                            启用{isBlacklist ? '黑名单' : '白名单'}
                        </Label>
                    </div>
                </div>
                
                <DialogFooter className="border-t pt-4">
                    <ShadcnButton variant="outline" onClick={onClose}>
                        取消
                    </ShadcnButton>
                    <ShadcnButton onClick={() => save()} className="bg-primary hover:bg-primary/90">
                        保存
                    </ShadcnButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

export default CreateBlacklistAndWhitelist;