import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button as ShadcnButton } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { message } from '@/utils/toast';
import { useEffect, useMemo, useState } from "react";
import { CreateBlacklist, UpdateBlacklist, GetRegions, RegionCatalog } from "@/services/BlacklistAndWhitelistService";
import { Badge } from "@/components/ui/badge";
import { X, Shield, ShieldCheck, Globe2 } from "lucide-react";
import { Textarea } from "@/components/ui/textarea";
import { BlacklistAndWhitelistItem } from "@/types";
import { Switch } from "@/components/ui/switch";
import { MultiSelect } from "@/components/ui/multi-select";

const IPV4 = String.raw`(?:(?:25[0-5]|2[0-4]\d|1?\d?\d)\.){3}(?:25[0-5]|2[0-4]\d|1?\d?\d)`;
// 单个 IPv4、CIDR（5.34.219.0/24）、区间（10.0.0.1-10.0.0.255）
const IPV4_RULE = new RegExp(`^${IPV4}(?:/(?:3[0-2]|[12]?\\d)|-${IPV4})?$`);

function isValidIpRule(rule: string) {
    if (IPV4_RULE.test(rule)) return true;
    // IPv6 单地址或 CIDR
    const [addr, prefix, ...rest] = rule.split('/');
    if (rest.length || !addr.includes(':') || !/^[0-9a-fA-F:.]+$/.test(addr)) return false;
    return prefix === undefined || (/^\d{1,3}$/.test(prefix) && Number(prefix) <= 128);
}

interface CreateBlacklistAndWhitelistProps {
    isBlacklist: boolean;
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
    /** 传入则为编辑模式，并以其内容回填表单 */
    editing?: BlacklistAndWhitelistItem | null;
}

const CreateBlacklistAndWhitelist: React.FC<CreateBlacklistAndWhitelistProps> = ({
    visible,
    onClose,
    onOk,
    isBlacklist,
    editing
}: CreateBlacklistAndWhitelistProps) => {
    const [value, setValue] = useState({
        ips: [] as string[],
        regions: [] as string[],
        name: '',
        description: '',
        enable: true,
        isBlacklist: isBlacklist,
    });
    const [ipInput, setIpInput] = useState('');

    useEffect(() => {
        if (!visible) return;
        setIpInput('');
        setValue({
            ips: editing?.ips ?? [],
            regions: editing?.regions ?? [],
            name: editing?.name ?? '',
            description: editing?.description ?? '',
            enable: editing?.enable ?? true,
            isBlacklist: isBlacklist,
        });
    }, [visible, editing, isBlacklist]);

    const addIp = () => {
        const rule = ipInput.trim();
        if (!isValidIpRule(rule)) {
            message.error('请输入正确的IP格式，如 1.2.3.4、1.2.3.0/24 或 1.2.3.1-1.2.3.9');
            return;
        }
        if (!value.ips.includes(rule)) {
            setValue({ ...value, ips: [...value.ips, rule] });
        }
        setIpInput('');
    };
    const [regionCatalog, setRegionCatalog] = useState<RegionCatalog | null>(null);

    useEffect(() => {
        if (!visible || !isBlacklist || regionCatalog) return;
        GetRegions()
            .then((res) => setRegionCatalog(res.data))
            .catch(() => setRegionCatalog({ countries: [], chinaProvinces: [] }));
    }, [visible, isBlacklist, regionCatalog]);

    // 地区选项：国家 + 中国省份（规则值 "中国|省份"），名称与 ip2region.xdb 完全一致
    const regionOptions = useMemo(() => {
        if (!regionCatalog) return [];
        const countries = (regionCatalog.countries ?? []).map((c) => ({ label: c, value: c }));
        const provinces = (regionCatalog.chinaProvinces ?? []).map((p) => ({
            label: `中国 · ${p}`,
            value: `中国|${p}`,
        }));
        return [...countries, ...provinces];
    }, [regionCatalog]);

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
        if (isBlacklist) {
            if (!value.ips.length && !value.regions.length) {
                message.error('请至少添加一个 IP 或封禁地区');
                return;
            }
        } else if (!value.ips.length) {
            message.error('请输入IP');
            return
        }

        const body = { ...value, regions: isBlacklist ? value.regions : undefined };
        const action = editing ? '更新' : '新增';
        (editing ? UpdateBlacklist(editing.id, body) : CreateBlacklist(body))
            .then(() => {
                message.success(`${action}成功`);
                onOk();
            }).catch(() => {
                message.error(`${action}失败`);
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
                            {`${editing ? '编辑' : '新增'}${isBlacklist ? '黑名单' : '白名单'}`}
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
                            {!isBlacklist && <span className="text-destructive ml-1">*</span>}
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
                                    placeholder="请输入IP、CIDR 或区间 (如: 192.168.1.1 / 5.34.219.0/24)"
                                    onKeyPress={(e) => {
                                        if (e.key === 'Enter') {
                                            e.preventDefault();
                                            addIp();
                                        }
                                    }}
                                    className="flex-1"
                                />
                                <ShadcnButton
                                    type="button"
                                    variant="outline"
                                    size="sm"
                                    onClick={() => {
                                        addIp();
                                    }}
                                >
                                    添加
                                </ShadcnButton>
                            </div>
                        </div>
                    </div>

                    {/* 地区封禁仅黑名单支持：规则在保存后预编译为 IP 区间，请求时零 GeoIP 查询 */}
                    {isBlacklist && (
                        <div className="space-y-2">
                            <Label className="text-sm font-medium flex items-center gap-1.5">
                                <Globe2 className="h-4 w-4 text-muted-foreground" />
                                封禁地区
                            </Label>
                            <MultiSelect
                                value={value.regions}
                                onChange={(regions) => setValue({ ...value, regions })}
                                options={regionOptions}
                                allowCustom={false}
                                placeholder={regionCatalog ? "搜索并选择国家或中国省份..." : "地区列表加载中..."}
                            />
                            <p className="text-xs text-muted-foreground">
                                来自该地区的所有 IPv4 请求将被拒绝（403）。基于 ip2region 离线库，IPv6 不受地区规则约束。
                            </p>
                        </div>
                    )}

                    {/* 黑名单为安全防护强制启用，不提供开关 */}
                    {!isBlacklist && (
                        <div className="flex items-center justify-between rounded-lg border p-4">
                            <div className="space-y-0.5">
                                <Label htmlFor="enable" className="text-sm font-medium cursor-pointer">
                                    启用白名单
                                </Label>
                                <p className="text-sm text-muted-foreground">
                                    启用后将允许列表中的IP访问
                                </p>
                            </div>
                            <Switch
                                id="enable"
                                checked={value.enable}
                                onCheckedChange={(checked: boolean) => setValue({ ...value, enable: checked })}
                            />
                        </div>
                    )}
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