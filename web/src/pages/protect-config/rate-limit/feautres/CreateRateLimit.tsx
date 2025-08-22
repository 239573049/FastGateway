import { RateLimit } from "@/types";
import { useState } from "react";
import { message,  Select } from 'antd';
import { CreateRateLimit } from "@/services/RateLimitService";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { cn } from "@/lib/utils";

interface CreateRateLimitProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

const CreateRateLimitPage: React.FC<CreateRateLimitProps> = ({
    visible,
    onClose,
    onOk
}: CreateRateLimitProps) => {
    const [value, setValue] = useState<RateLimit>({
        name: '',
        enable: true,
        endpoint: '',
        period: '',
        limit: 1,
        endpointWhitelist: [],
        ipWhitelist: [],
        id: null,
    });

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

        if (!value.endpoint) {
            message.error('请输入限流的端点');
            return;
        }

        if (!value.limit) {
            message.error('请输入限流值');
            return;
        }

        if (!value.period) {
            message.error('请输入限流周期');
            return;
        }

        if(value.limit < 1) {
            message.error('限流值必须大于0');
            return;
        }

        CreateRateLimit(value)
            .then(() => {
                message.success('新增成功');
                onOk();
            }).catch(() => {
                message.error('新增失败');
            });
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-lg max-h-[80vh] overflow-y-auto">
                <DialogHeader className="border-b pb-4">
                    <DialogTitle className="text-xl font-bold">新增限流</DialogTitle>
                </DialogHeader>
                
                <div className="space-y-6 py-6">
                    <div className="space-y-2">
                        <Label htmlFor="name" className="text-sm font-medium">名称</Label>
                        <Input 
                            id="name"
                            value={value.name} 
                            onChange={(e) => setValue({ ...value, name: e.target.value })} 
                            placeholder='请输入名称' 
                            className="w-full"
                        />
                    </div>
                    
                    <div className="flex items-center space-x-2">
                        <Switch
                            id="enable"
                            checked={value.enable} 
                            onCheckedChange={(checked) => setValue({ ...value, enable: checked })} 
                        />
                        <Label htmlFor="enable" className="text-sm font-medium cursor-pointer">启用当前限流策略</Label>
                    </div>
                    
                    <div className="space-y-2">
                        <Label htmlFor="endpoint" className="text-sm font-medium">限流端点</Label>
                        <Input 
                            id="endpoint"
                            value={value.endpoint} 
                            onChange={(e) => setValue({ ...value, endpoint: e.target.value })} 
                            placeholder='请输入限流端点' 
                            className="w-full font-mono text-sm"
                        />
                    </div>
                    
                    <div className="grid grid-cols-2 gap-4">
                        <div className="space-y-2">
                            <Label htmlFor="limit" className="text-sm font-medium">限流值</Label>
                            <Input 
                                id="limit"
                                type="number"
                                value={value.limit} 
                                onChange={(e) => setValue({ ...value, limit: parseInt(e.target.value) || 1 })} 
                                placeholder='请输入限流值' 
                                className="w-full"
                                min="1"
                            />
                        </div>
                        
                        <div className="space-y-2">
                            <Label htmlFor="period" className="text-sm font-medium">限流周期</Label>
                            <Input 
                                id="period"
                                value={value.period} 
                                onChange={(e) => setValue({ ...value, period: e.target.value })} 
                                placeholder='s|m|h|d' 
                                className="w-full font-mono text-sm"
                            />
                        </div>
                    </div>

                    <div className="space-y-2">
                        <Label htmlFor="endpointWhitelist" className="text-sm font-medium">端点白名单</Label>
                        <Select
                            id="endpointWhitelist"
                            value={value.endpointWhitelist}
                            mode="tags"
                            onChange={(e) => {
                                setValue({ ...value, endpointWhitelist: e })
                            }}
                            placeholder='请输入端点白名单的端点'
                            className="w-full"
                        />
                    </div>
                    
                    <div className="space-y-2">
                        <Label htmlFor="ipWhitelist" className="text-sm font-medium">IP白名单</Label>
                        <Select
                            id="ipWhitelist"
                            value={value.ipWhitelist}
                            mode="tags"
                            onChange={(e) => {
                                setValue({ ...value, ipWhitelist: e })
                            }}
                            placeholder='请输入IP白名单的ip'
                            className="w-full"
                        />
                    </div>
                </div>
                <DialogFooter className="border-t pt-4">
                    <ShadcnButton variant="outline" onClick={onClose}>
                        取消
                    </ShadcnButton>
                    <ShadcnButton onClick={save} className="bg-primary hover:bg-primary/90">
                        保存
                    </ShadcnButton>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}

export default CreateRateLimitPage;