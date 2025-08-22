import { RateLimit } from "@/types";
import { useState } from "react";
import { message } from '@/utils/toast';
import { CreateRateLimit } from "@/services/RateLimitService";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Switch } from "@/components/ui/switch";
import { Textarea } from "@/components/ui/textarea";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
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

    const periodOptions = [
        { value: '1s', label: '1秒' },
        { value: '1m', label: '1分钟' },
        { value: '1h', label: '1小时' },
        { value: '1d', label: '1天' },
        { value: '1w', label: '1周' },
        { value: '1M', label: '1个月' },
    ];

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-2xl max-h-[90vh] overflow-y-auto p-0">
                <div className="bg-gradient-to-r from-blue-600 to-purple-600 p-6 text-white">
                    <DialogHeader className="p-0">
                        <DialogTitle className="text-2xl font-bold">创建限流策略</DialogTitle>
                        <p className="text-blue-100 mt-2">设置请求限制规则以保护服务稳定性</p>
                    </DialogHeader>
                </div>
                
                <div className="p-6 space-y-6">
                    {/* Basic Settings */}
                    <div className="space-y-4">
                        <h3 className="text-lg font-semibold flex items-center gap-2">
                            <div className="w-2 h-2 bg-blue-500 rounded-full" />
                            基本设置
                        </h3>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="name" className="text-sm font-medium flex items-center gap-1">
                                    名称
                                    <span className="text-red-500">*</span>
                                </Label>
                                <Input 
                                    id="name"
                                    value={value.name} 
                                    onChange={(e) => setValue({ ...value, name: e.target.value })} 
                                    placeholder="例如: API限流策略"
                                    className="w-full"
                                />
                            </div>
                            
                            <div className="flex items-center space-x-3 pt-6">
                                <Switch
                                    id="enable"
                                    checked={value.enable} 
                                    onCheckedChange={(checked) => setValue({ ...value, enable: checked })} 
                                />
                                <Label htmlFor="enable" className="cursor-pointer font-medium">
                                    启用限流策略
                                </Label>
                            </div>
                        </div>
                    </div>

                    {/* Rate Limit Settings */}
                    <div className="space-y-4">
                        <h3 className="text-lg font-semibold flex items-center gap-2">
                            <div className="w-2 h-2 bg-green-500 rounded-full" />
                            限流规则
                        </h3>
                        
                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="endpoint" className="text-sm font-medium flex items-center gap-1">
                                    限流端点
                                    <span className="text-red-500">*</span>
                                </Label>
                                <Input 
                                    id="endpoint"
                                    value={value.endpoint} 
                                    onChange={(e) => setValue({ ...value, endpoint: e.target.value })} 
                                    placeholder="如: /api/*"
                                />
                            </div>
                            
                            <div className="space-y-2">
                                <Label htmlFor="limit" className="text-sm font-medium flex items-center gap-1">
                                    限流值
                                    <span className="text-red-500">*</span>
                                </Label>
                                <Input 
                                    id="limit"
                                    type="number"
                                    value={value.limit} 
                                    onChange={(e) => setValue({ ...value, limit: parseInt(e.target.value) || 1 })} 
                                    placeholder="请求数量"
                                    min="1"
                                />
                            </div>
                            
                            <div className="space-y-2">
                                <Label htmlFor="period" className="text-sm font-medium flex items-center gap-1">
                                    限流周期
                                    <span className="text-red-500">*</span>
                                </Label>
                                <Select
                                    value={value.period}
                                    onValueChange={(value) => setValue({ ...value, period: value })}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="选择周期" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        {periodOptions.map(option => (
                                            <SelectItem key={option.value} value={option.value}>
                                                {option.label}
                                            </SelectItem>
                                        ))}
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>
                    </div>

                    {/* Whitelist Settings */}
                    <div className="space-y-4">
                        <h3 className="text-lg font-semibold flex items-center gap-2">
                            <div className="w-2 h-2 bg-orange-500 rounded-full" />
                            白名单设置
                        </h3>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="endpointWhitelist" className="text-sm font-medium">
                                    端点白名单
                                </Label>
                                <Textarea 
                                    id="endpointWhitelist"
                                    value={value.endpointWhitelist.join('\n')} 
                                    onChange={(e) => {
                                        const values = e.target.value.split('\n').filter(v => v.trim());
                                        setValue({ ...value, endpointWhitelist: values })
                                    }}
                                    placeholder="每行输入一个端点路径
例如:
/api/health
/static/*"
                                    rows={4}
                                    className="font-mono text-sm"
                                />
                            </div>
                            
                            <div className="space-y-2">
                                <Label htmlFor="ipWhitelist" className="text-sm font-medium">
                                    IP白名单
                                </Label>
                                <Textarea 
                                    id="ipWhitelist"
                                    value={value.ipWhitelist.join('\n')} 
                                    onChange={(e) => {
                                        const values = e.target.value.split('\n').filter(v => v.trim());
                                        setValue({ ...value, ipWhitelist: values })
                                    }}
                                    placeholder="每行输入一个IP地址
例如:
192.168.1.0/24
10.0.0.1"
                                    rows={4}
                                    className="font-mono text-sm"
                                />
                            </div>
                        </div>
                    </div>
                </div>
                
                <div className="border-t bg-muted/50 px-6 py-4">
                    <DialogFooter className="p-0 gap-3">
                        <Button variant="outline" onClick={onClose} className="w-full sm:w-auto">
                            取消
                        </Button>
                        <Button onClick={save} className="w-full sm:w-auto">
                            保存限流策略
                        </Button>
                    </DialogFooter>
                </div>
            </DialogContent>
        </Dialog>
    );
}

export default CreateRateLimitPage;