
import { useEffect, useState } from 'react';
import { DomainName, ServiceType } from '@/types';
import { CircleX, Plus, Trash2, Globe, Server, FileText, Link2} from 'lucide-react';
import { check, checkSrvcie, createDomain } from '@/services/DomainNameService';
import { useParams } from 'react-router-dom';
import { useDomainStore } from '@/store/server';
import { toast } from 'sonner';

import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Card, CardContent } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Badge } from '@/components/ui/badge';

interface CreateDomainProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

export default function CreateDomain({
    visible,
    onClose,
    onOk,
}: CreateDomainProps) {

    const { id } = useParams<{ id: string }>();
    const [value, setValue] = useState<DomainName>({
        domains: [],
        serverId: '',
        serviceType: ServiceType.Service,
        headers: [],
        enable: true,
        service: '',
        upStreams: [],
        root: '',
        path: '',
        tryFiles: []
    });

    const {
        setLoadingDomains,
        loadingDomains
    } = useDomainStore();

    useEffect(() => {
        if (id) {
            setValue({
                ...value,
                serverId: id
            });
        }
    }, [id]);

    function save() {
        // 路由需要/开头
        if (!value.path.startsWith('/')) {
            toast.error('路由需要以/开头');
            return;
        }

        createDomain(value)
            .then(() => {
                toast.success('路由创建成功');
                onOk();
                setLoadingDomains(!loadingDomains);
            });
    }

    const [domainInput, setDomainInput] = useState('');
    const [tryFilesInput, setTryFilesInput] = useState('');

    const handleAddDomain = () => {
        if (domainInput.trim()) {
            setValue({ ...value, domains: [...value.domains, domainInput.trim()] });
            setDomainInput('');
        }
    };

    const handleRemoveDomain = (index: number) => {
        const newDomains = value.domains.filter((_, i) => i !== index);
        setValue({ ...value, domains: newDomains });
    };

    const handleAddTryFile = () => {
        if (tryFilesInput.trim()) {
            setValue({ ...value, tryFiles: [...value.tryFiles, tryFilesInput.trim()] });
            setTryFilesInput('');
        }
    };

    const handleRemoveTryFile = (index: number) => {
        const newTryFiles = value.tryFiles.filter((_, i) => i !== index);
        setValue({ ...value, tryFiles: newTryFiles });
    };

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-4xl max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle className="text-lg font-semibold flex items-center gap-2">
                        <Globe className="w-5 h-5 text-primary" />
                        创建路由策略
                    </DialogTitle>
                    <p className="text-sm text-muted-foreground">配置域名路由和代理设置以管理流量</p>
                </DialogHeader>

                <div className="space-y-6">
                    {/* 基本设置 */}
                    <div className="space-y-4">
                        <h3 className="text-lg font-semibold flex items-center gap-2">
                            <Globe className="w-5 h-5 text-blue-500" />
                            基本设置
                        </h3>
                        
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div className="space-y-2">
                                <Label htmlFor="path" className="text-sm font-medium flex items-center gap-1">
                                    匹配路由
                                    <span className="text-red-500">*</span>
                                </Label>
                                <div className="relative">
                                    <Input
                                        id="path"
                                        value={value.path}
                                        onChange={(e) => setValue({ ...value, path: e.target.value })}
                                        placeholder="例如: /api"
                                        className="font-mono text-sm"
                                    />
                                </div>
                            </div>
                            
                            <div className="space-y-2">
                                <Label className="text-sm font-medium flex items-center gap-1">
                                    服务类型
                                    <span className="text-red-500">*</span>
                                </Label>
                                <Select
                                    value={value.serviceType.toString()}
                                    onValueChange={(e) => setValue({ ...value, serviceType: parseInt(e) as ServiceType })}
                                >
                                    <SelectTrigger>
                                        <SelectValue placeholder="选择服务类型" />
                                    </SelectTrigger>
                                    <SelectContent>
                                        <SelectItem value={ServiceType.Service.toString()}>
                                            <div className="flex items-center gap-2">
                                                <Server className="w-4 h-4" />
                                                代理单个网络服务
                                            </div>
                                        </SelectItem>
                                        <SelectItem value={ServiceType.ServiceCluster.toString()}>
                                            <div className="flex items-center gap-2">
                                                <Link2 className="w-4 h-4" />
                                                代理多个网络服务集群
                                            </div>
                                        </SelectItem>
                                        <SelectItem value={ServiceType.StaticFile.toString()}>
                                            <div className="flex items-center gap-2">
                                                <FileText className="w-4 h-4" />
                                                静态文件服务代理
                                            </div>
                                        </SelectItem>
                                    </SelectContent>
                                </Select>
                            </div>
                        </div>

                        {/* 域名管理 */}
                        <div className="space-y-2">
                            <Label className="text-sm font-medium flex items-center gap-1">
                                域名列表
                            </Label>
                            <div className="space-y-2">
                                <div className="flex items-center gap-2">
                                    <Input
                                        value={domainInput}
                                        onChange={(e) => setDomainInput(e.target.value)}
                                        placeholder="输入域名 (例如: example.com)"
                                        className="flex-1 h-10"
                                        onKeyPress={(e) => {
                                            if (e.key === 'Enter') {
                                                e.preventDefault();
                                                handleAddDomain();
                                            }
                                        }}
                                    />
                                    <Button 
                                        type="button" 
                                        variant="outline" 
                                        size="default"
                                        className="h-10 px-3"
                                        onClick={handleAddDomain}
                                    >
                                        添加
                                    </Button>
                                </div>
                                
                                <div className="flex flex-wrap gap-2">
                                    {value.domains.map((domain, index) => (
                                        <Badge key={index} variant="secondary" className="px-2 py-1">
                                            {domain}
                                            <Button
                                                type="button"
                                                variant="ghost"
                                                size="sm"
                                                className="h-4 w-4 p-0 ml-1"
                                                onClick={() => handleRemoveDomain(index)}
                                            >
                                                <CircleX className="h-3 w-3" />
                                            </Button>
                                        </Badge>
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    {value.serviceType === ServiceType.Service && (
                        <div className="space-y-2">
                            <Label htmlFor="service" className="text-sm font-medium">
                                请求服务地址
                                <span className="text-red-500">*</span>
                            </Label>
                            <div className="flex items-center gap-2">
                                <Input
                                    id="service"
                                    value={value.service ?? ""}
                                    onChange={(e) => setValue({ ...value, service: e.target.value })}
                                    placeholder="请输入请求服务地址 (示例：http://127.0.0.1:8080)"
                                    className="flex-1 h-10"
                                />
                                <Button 
                                    variant="outline" 
                                    size="default"
                                    className="h-10 px-3"
                                    onClick={() => {
                                        checkSrvcie({
                                            path: value.service
                                        }).then((res) => {
                                            if (res.success) {
                                                toast.success('服务访问正常');
                                            } else {
                                                toast.error(res.message);
                                            }
                                        });
                                    }}
                                >
                                    检测
                                </Button>
                            </div>
                        </div>
                    )}
                    
                    {value.serviceType === ServiceType.StaticFile && (
                        <div className="space-y-4">
                            <div className="space-y-2">
                                <Label htmlFor="root" className="text-sm font-medium">
                                    根目录
                                    <span className="text-red-500">*</span>
                                </Label>
                                <div className="flex items-center gap-2">
                                    <Input
                                        id="root"
                                        value={value.root ?? ""}
                                        onChange={(e) => setValue({ ...value, root: e.target.value })}
                                        placeholder="请输入根目录"
                                        className="flex-1 h-10"
                                    />
                                    <Button 
                                        variant="outline" 
                                        size="default"
                                        className="h-10 px-3"
                                        onClick={() => {
                                            check({
                                                path: value.root
                                            }).then((res) => {
                                                if (res.data) {
                                                    toast.success('文件或目录存在');
                                                } else {
                                                    toast.error('文件或目录不存在');
                                                }
                                            });
                                        }}
                                    >
                                        检测
                                    </Button>
                                </div>
                            </div>
                            <div className="space-y-2">
                                <Label className="text-sm font-medium">TryFile</Label>
                                <div className="space-y-2">
                                    <div className="flex items-center gap-2">
                                        <Input
                                            value={tryFilesInput}
                                            onChange={(e) => setTryFilesInput(e.target.value)}
                                            placeholder="输入异常时的文件路径 (例如: /index.html)"
                                            className="flex-1 h-10"
                                            onKeyPress={(e) => {
                                                if (e.key === 'Enter') {
                                                    e.preventDefault();
                                                    handleAddTryFile();
                                                }
                                            }}
                                        />
                                        <Button 
                                            type="button" 
                                            variant="outline" 
                                            size="default"
                                            className="h-10 px-3"
                                            onClick={handleAddTryFile}
                                        >
                                            添加
                                        </Button>
                                    </div>
                                    
                                    <div className="flex flex-wrap gap-2">
                                        {value.tryFiles.map((file, index) => (
                                            <Badge key={index} variant="secondary" className="px-2 py-1">
                                                {file}
                                                <Button
                                                    type="button"
                                                    variant="ghost"
                                                    size="sm"
                                                    className="h-4 w-4 p-0 ml-1"
                                                    onClick={() => handleRemoveTryFile(index)}
                                                >
                                                    <CircleX className="h-3 w-3" />
                                                </Button>
                                            </Badge>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}
                    {value.serviceType === ServiceType.ServiceCluster && (
                        <Card>
                            <CardContent className="pt-6 space-y-4">
                                <Label className="text-sm font-medium">代理节点配置</Label>
                                {value.upStreams.map((item, index) => (
                                    <div key={index} className="flex items-end gap-3">
                                        <div className="flex-1 space-y-2">
                                            <Label className="text-sm font-medium">
                                                服务名称
                                                <span className="text-red-500">*</span>
                                            </Label>
                                            <div className="flex items-center gap-2">
                                                <Input
                                                    value={item.service}
                                                    onChange={(e) => {
                                                        const newValue = [...value.upStreams];
                                                        newValue[index] = { ...newValue[index], service: e.target.value };
                                                        setValue({ ...value, upStreams: newValue });
                                                    }}
                                                    placeholder="请输入服务名称"
                                                    className="flex-1 h-10"
                                                />
                                                <Button 
                                                    variant="outline" 
                                                    size="default"
                                                    className="h-10 px-3"
                                                    onClick={() => {
                                                        checkSrvcie({
                                                            path: item.service
                                                        }).then((res) => {
                                                            if (res.success) {
                                                                toast.success('服务访问正常');
                                                            } else {
                                                                toast.error(res.message);
                                                            }
                                                        });
                                                    }}
                                                >
                                                    检测
                                                </Button>
                                            </div>
                                        </div>
                                        <Button 
                                            variant="ghost" 
                                            size="icon"
                                            onClick={() => {
                                                const newValue = [...value.upStreams];
                                                newValue.splice(index, 1);
                                                setValue({ ...value, upStreams: newValue });
                                            }}
                                            className="text-destructive"
                                        >
                                            <Trash2 className="h-4 w-4" />
                                        </Button>
                                    </div>
                                ))}
                                <Button 
                                    variant="outline" 
                                    className="w-full"
                                    onClick={() => {
                                        setValue({
                                            ...value,
                                            upStreams: [
                                                ...value.upStreams,
                                                {
                                                    service: '',
                                                    weight: 1
                                                }
                                            ]
                                        })
                                    }}
                                >
                                    <Plus className="h-4 w-4 mr-2" />
                                    添加代理节点
                                </Button>
                            </CardContent>
                        </Card>
                    )}
                    <Separator className="my-4" />
                    
                    <Card>
                        <CardContent className="pt-6 space-y-4">
                            <Label className="text-sm font-medium">自定义Headers</Label>
                            {value.headers.map((item, index) => (
                                <div key={index} className="grid grid-cols-[1fr_1fr_auto] gap-3 items-end">
                                    <div className="space-y-2">
                                        <Label className="text-sm font-medium">Key</Label>
                                        <Input
                                            value={item.key}
                                            onChange={(e) => {
                                                const newValue = [...value.headers];
                                                newValue[index] = { ...newValue[index], key: e.target.value };
                                                setValue({ ...value, headers: newValue });
                                            }}
                                            placeholder="请输入Key"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label className="text-sm font-medium">Value</Label>
                                        <Input
                                            value={item.value}
                                            onChange={(e) => {
                                                const newValue = [...value.headers];
                                                newValue[index] = { ...newValue[index], value: e.target.value };
                                                setValue({ ...value, headers: newValue });
                                            }}
                                            placeholder="请输入Value"
                                        />
                                    </div>
                                    <Button 
                                        variant="ghost" 
                                        size="icon"
                                        onClick={() => {
                                            const newValue = [...value.headers];
                                            newValue.splice(index, 1);
                                            setValue({ ...value, headers: newValue });
                                        }}
                                        className="text-destructive"
                                    >
                                        <Trash2 className="h-4 w-4" />
                                    </Button>
                                </div>
                            ))}
                            <Button 
                                variant="outline" 
                                className="w-full"
                                onClick={() => {
                                    setValue({
                                        ...value,
                                        headers: [
                                            ...value.headers,
                                            {
                                                key: '',
                                                value: ''
                                            }
                                        ]
                                    })
                                }}
                            >
                                <Plus className="h-4 w-4 mr-2" />
                                添加Header
                            </Button>
                        </CardContent>
                    </Card>
                </div>
                
                <DialogFooter>
                    <Button variant="outline" onClick={onClose}>
                        取消
                    </Button>
                    <Button onClick={save}>
                        保存
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}