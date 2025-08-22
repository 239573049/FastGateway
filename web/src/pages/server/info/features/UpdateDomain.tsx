
import FInput from '@/components/FInput';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useEffect, useState } from 'react';
import { DomainName, ServiceType } from '@/types';
import {
    CircleX
} from 'lucide-react';
import { check, checkSrvcie, updateDomain } from '@/services/DomainNameService';
import { useParams } from 'react-router-dom';
import { useDomainStore } from '@/store/server';
import { toast } from "sonner"
import { Button } from '@/components/ui/button';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { MultiSelect } from '@/components/ui/multi-select';


interface UpdateDomainProps {
    visible: boolean;
    domainName: DomainName | null;
    onClose: () => void;
    onOk: () => void;
}

export default function UpdateDomain({
    visible,
    onClose,
    domainName,
    onOk,
}: UpdateDomainProps) {

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

    useEffect(() => {
        if (domainName) {
            setValue(domainName);
        }
    }, [domainName]);

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
        updateDomain(value.id!, value)
            .then(() => {
                ;
                onOk();
                setLoadingDomains(!loadingDomains);
            });
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-2xl max-h-[80vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>编辑路由</DialogTitle>
                </DialogHeader>
                <div className="flex flex-col gap-4">
                    <div className="flex items-center gap-2 mb-2">
                        <div className="text-sm font-bold">
                            域名：
                        </div>
                        <MultiSelect
                             value={value.domains}
                             onChange={(e) => setValue({ ...value, domains: e })}
                             placeholder='请输入域名'
                             className="w-full flex-1 mb-2"
                         />
                    </div>
                    <FInput value={value.path}
                        defaultValue={'/'}
                        onChange={(e) => setValue({ ...value, path: e.target.value })} label='匹配路由:' placeholder='请输入匹配的路由' />

                    <div className="flex items-center gap-2 mb-2">
                        <div className="text-sm font-bold">
                            服务类型：
                        </div>
                        <Select
                             value={value.serviceType.toString()}
                             onValueChange={(e) => setValue({ ...value, serviceType: parseInt(e) as ServiceType })}
                         >
                            <SelectTrigger className="w-full flex-1 mb-2">
                                <SelectValue placeholder="请输入服务类型" />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value={ServiceType.Service.toString()}>代理单个网络服务</SelectItem>
                                <SelectItem value={ServiceType.ServiceCluster.toString()}>代理多个网络服务集群</SelectItem>
                                <SelectItem value={ServiceType.StaticFile.toString()}>静态文件服务代理</SelectItem>
                            </SelectContent>
                        </Select>
                    </div>
                    {
                        value.serviceType === ServiceType.Service && (
                            <div className="flex items-center gap-2">
                                <div className="flex-1">
                                    <FInput value={value.service ?? ""} onChange={(e) => setValue({ ...value, service: e.target.value })} label='服务名称:' placeholder='请输入服务名称' />
                                </div>
                                <Button type='button' onClick={() => {
                                    checkSrvcie({
                                        path: value.service
                                    }).then((res) => {
                                        if (res.success) {
                                            toast.success('服务访问正常');
                                        } else {
                                            toast.error(res.toast);
                                        }
                                    });
                                }}>检测服务状态</Button>
                            </div>
                        )
                    }
                    {
                        value.serviceType === ServiceType.StaticFile && (
                            <>

                                <div className="flex items-center gap-2">
                                    <div className="flex-1">
                                        <FInput value={value.root ?? ""} onChange={(e) => setValue({ ...value, root: e.target.value })} label='根目录:' placeholder='请输入根目录' />
                                    </div>
                                    <Button type='button' onClick={() => {
                                        check({
                                            path: value.root
                                        }).then((res) => {
                                            if (res.data) {
                                                toast.success('文件或目录存在');
                                            } else {
                                                toast.error('文件或目录不存在');
                                            }
                                        });
                                    }}>检测文件或目录是否存在</Button>
                                </div>
                                <div className="flex items-center gap-2 mb-2">
                                    <div className="text-sm font-bold">
                                        TryFile：
                                    </div>
                                    <MultiSelect
                                         value={value.tryFiles}
                                         onChange={(e) => setValue({ ...value, tryFiles: e })}
                                         placeholder='请输入异常时的文件列表'
                                         className="w-full flex-1 mb-2"
                                     />
                                </div>
                            </>
                        )
                    }
                    {
                        value.serviceType === ServiceType.ServiceCluster && value.upStreams.map((item, index) => (
                            <div className="flex items-end gap-2 mb-2 w-full">
                                <div className="flex-1">
                                    <div className="flex items-center gap-2">
                                        <div className="flex-1">
                                            <FInput key={index} value={item.service} onChange={(e) => {
                                                const newValue = [...value.upStreams];
                                                newValue[index] = { ...newValue[index], service: e.target.value };
                                                setValue({ ...value, upStreams: newValue });
                                            }} label='服务名称:' placeholder='请输入服务名称' />
                                        </div>
                                        <Button type='button' onClick={() => {
                                            checkSrvcie({
                                                path: item.service
                                            }).then((res) => {
                                                if (res.success) {
                                                    toast.success('服务访问正常');
                                                } else {
                                                    toast.error(res.message);
                                                }
                                            });
                                        }}>检测服务状态</Button>
                                    </div>
                                </div>
                                <Button onClick={() => {
                                    const newValue = [...value.upStreams];
                                    newValue.splice(index, 1);
                                    setValue({ ...value, upStreams: newValue });
                                }} type='button' className="text-red-500 hover:text-red-700">
                                    <CircleX />
                                </Button>
                            </div>
                        ))
                    }
                    {
                        value.serviceType === ServiceType.ServiceCluster && (
                            <Button onClick={() => {
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
                            }}>
                                添加新的代理节点
                            </Button>
                        )
                    }
                    <br />
                    {

                        value.headers.map((item, index) => (
                            <div className="flex items-end gap-2 mb-2 w-full">
                                <div className="flex-1">
                                    <FInput key={index} value={item.key} onChange={(e) => {
                                        const newValue = [...value.headers];
                                        newValue[index] = { ...newValue[index], key: e.target.value };
                                        setValue({ ...value, headers: newValue });
                                    }} label='Key:' placeholder='请输入Key' />
                                </div>
                                <div className="flex-1">
                                    <FInput key={index} value={item.value} onChange={(e) => {
                                        const newValue = [...value.headers];
                                        newValue[index] = { ...newValue[index], value: e.target.value };
                                        setValue({ ...value, headers: newValue });
                                    }} label='Value:' placeholder='请输入Value' />
                                </div>
                                <Button onClick={() => {
                                    const newValue = [...value.headers];
                                    newValue.splice(index, 1);
                                    setValue({ ...value, headers: newValue });
                                }}>
                                    <CircleX />
                                </Button>
                            </div>

                        ))
                    }
                    <Button className="mt-2" onClick={() => {
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
                    }}>
                        添加新的Header
                    </Button>
                    <Button className="mt-5" onClick={() => save()}>
                        保存
                    </Button>
                </div>
            </DialogContent>
        </Dialog>
    )
}