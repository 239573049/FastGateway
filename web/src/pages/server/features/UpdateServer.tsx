import FCheckbox from "@/components/FCheckbox";
import FInput from "@/components/FInput";
import { updateServer } from "@/services/ServerService";
import { Server } from "@/types";
import { useEffect, useState } from "react";
import { toast } from "sonner";

import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogFooter } from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";


interface UpdateServerProps {
    visible: boolean;
    server: Server | null;
    onClose: () => void;
    onOk: () => void;
}

export default function UpdateServer({
    visible,
    server,
    onClose,
    onOk
}: UpdateServerProps) {
    const [value, setValue] = useState<Server>({
        name: '',
        listen: 80,
        description: '',
        enable: true,
        staticCompress: false,
        enableBlacklist: false,
        enableWhitelist: false,
        enableTunnel: false,
        redirectHttps: false,
        id: null,
        isHttps: false,
        onLine: false,
        copyRequestHost: true,
        maxRequestBodySize: null,
        timeout: 900
    });

    useEffect(() => {
        if (!server) {
            return;
        }
        setValue(server);
    }, [server]);

    const handleChange = (field: string) => (e: { target: any; }) => {
        const { target } = e;
        if(target.value === undefined){
            setValue({ ...value, [field]: target.checked });
        }else{
            setValue({ ...value, [field]: target.value });
        }
    };

    function save() {
        // 校验name
        if (!value.name) {
            toast.error('服务名称不能为空');
            return;
        }
        // 校验端口
        if (!value.listen) {
            toast.error('服务端口不能为空');
            return;
        }

        // 端口不能小于0 也不能大于65535
        if (value.listen < 0 || value.listen > 65535) {
            toast.error('端口范围为0-65535');
            return;
        }

        debugger

        updateServer(value.id,value)
            .then(() => {
                toast.success('更新成功');
                onOk();
            })
            .catch(() => {
                toast.error('更新失败');
            });
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-md max-h-[90vh] overflow-y-auto">
                <DialogHeader>
                    <DialogTitle>编辑服务</DialogTitle>
                </DialogHeader>
                <div className="space-y-4 py-4">
                    <FInput 
                        type="number"
                        value={value.listen} 
                        tooltip="设置代理服务监听的端口" 
                        onChange={handleChange('listen')} 
                        label='服务端口' 
                        placeholder='请输入服务端口' 
                    />
                    <FInput 
                        value={value.name} 
                        tooltip="代理服务名称，用于分辨服务" 
                        onChange={handleChange('name')} 
                        label='服务名称' 
                        placeholder='请输入服务名称' 
                    />
                    <FInput
                        value={value.description ?? ''}
                        tooltip="服务描述，用于描述服务"
                        onChange={handleChange('description')}
                        label='服务描述'
                        placeholder='请输入服务描述'
                    />
                    <FInput
                        type="number"
                        value={value.maxRequestBodySize ?? ''}
                        tooltip="最大请求体大小限制，单位字节。留空表示不限制"
                        onChange={handleChange('maxRequestBodySize')}
                        label='最大请求体大小(字节)'
                        placeholder='留空则不限制'
                    />
                    <FInput
                        type="number"
                        value={value.timeout}
                        tooltip="请求超时时间，单位秒。默认900秒（15分钟）"
                        onChange={handleChange('timeout')}
                        label='超时时间(秒)'
                        placeholder='请输入超时时间'
                    />
                    <div className="space-y-3">
                        <FCheckbox 
                            tooltip="是否立即启动当前创建的服务，或跟随网关启动" 
                            label='启动服务' 
                            checked={value.enable} 
                            onChange={handleChange('enable')} 
                        />
                        <FCheckbox 
                            tooltip="对于静态代理的内容压缩" 
                            checked={value.staticCompress} 
                            onChange={handleChange('staticCompress')} 
                            label='启用静态压缩' 
                        />
                        <FCheckbox 
                            tooltip="使用全局黑名单进行拦截" 
                            checked={value.enableBlacklist} 
                            onChange={handleChange('enableBlacklist')} 
                            label='启用黑名单' 
                        />
                        <FCheckbox 
                            tooltip="使用全局白名单进行过滤" 
                            checked={value.enableWhitelist} 
                            onChange={handleChange('enableWhitelist')} 
                            label='启用白名单' 
                        />
                        <FCheckbox 
                            tooltip="启用内网穿透隧道" 
                            checked={value.enableTunnel} 
                            onChange={handleChange('enableTunnel')} 
                            label='启用隧道' 
                        />
                        <FCheckbox 
                            tooltip="如果当前服务是HTTP,使用重定向，那么默认在启动HTTPS服务监听443端口，然后将请求转发到443端口中" 
                            checked={value.redirectHttps} 
                            onChange={handleChange('redirectHttps')} 
                            label='重定向到HTTPS' 
                        />
                        <FCheckbox 
                            tooltip="是否使用HTTPS，如果使用了重定向HTTPS那么是必须的" 
                            checked={value.isHttps} 
                            onChange={handleChange('isHttps')} 
                            label='是否启用HTTPS' 
                        />
                        <FCheckbox 
                            tooltip="如果使用了复制响应域名，那么你在使用域请求代理服务的时候网关也会吧域名复制请求到服务去" 
                            checked={value.copyRequestHost} 
                            onChange={handleChange('copyRequestHost')} 
                            label='是否复制响应域名' 
                        />
                    </div>
                </div>
                <DialogFooter>
                    <Button variant="outline" onClick={onClose}>
                        取消
                    </Button>
                    <Button onClick={save}>
                        更新
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}