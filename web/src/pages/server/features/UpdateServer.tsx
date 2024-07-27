import FCheckbox from "@/components/FCheckbox";
import FInput from "@/components/FInput";
import { createServer, updateServer } from "@/services/ServerService";
import { Server } from "@/types";
import { Button, Modal, message } from "antd";
import { useEffect, useState } from "react";

import { Flexbox } from 'react-layout-kit';


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
        copyRequestHost: true
    });

    useEffect(() => {
        if (!server) {
            return;
        }
        setValue(server);
    }, [server]);

    const handleChange = (field: string) => (e: { target: any; }) => {
        const { target } = e;
        const newValue = target.type === 'checkbox' ? target.checked : target.value;
        setValue({ ...value, [field]: newValue });
    };

    function save() {
        // 校验name
        if (!value.name) {
            message.error('服务名称不能为空');
            return;
        }
        // 校验端口
        if (!value.listen) {
            message.error('服务端口不能为空');
            return;
        }

        // 端口不能小于0 也不能大于65535
        if (value.listen < 0 || value.listen > 65535) {
            message.error('端口范围为0-65535');
            return;
        }

        updateServer(value.id,value)
            .then(() => {
                message.success('创建成功');
                onOk();
            })
            .catch(() => {
                message.error('创建失败');
            });
    }

    return (
        <Modal footer={[]} open={visible} title='编辑服务' onCancel={onClose} onOk={onOk}>
            <Flexbox
                justify='column'>
                <FInput value={value.listen} tooltip="设置代理服务监听的端口"  onChange={handleChange('listen')}  label='服务端口' placeholder='请输入服务端口' />
                <FInput value={value.name} tooltip="代理服务名称，用于分辨服务" onChange={handleChange('name')} label='服务名称' placeholder='请输入服务名称' />
                <FInput value={value.description ?? ''} tooltip="服务描述，用于描述服务" onChange={handleChange('description')} label='服务描述' placeholder='请输入服务描述' />
                <FCheckbox tooltip="是否立即启动当前创建的服务，或跟随网关启动"  style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} label='启动服务' checked={value.enable} onChange={handleChange('enable')} />
                <FCheckbox tooltip="对于静态代理的内容压缩" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.staticCompress} onChange={handleChange('staticCompress')} label='启用静态压缩' />
                <FCheckbox tooltip="使用全局黑名单进行拦截" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.enableBlacklist} onChange={handleChange('enableBlacklist')} label='启用黑名单' />
                <FCheckbox tooltip="使用全局白名单进行过滤"  style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.enableWhitelist} onChange={handleChange('enableWhitelist')} label='启用白名单' />
                <FCheckbox tooltip="启用内网穿透隧道" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.enableTunnel} onChange={handleChange('enableTunnel')} label='启用隧道' />
                <FCheckbox tooltip="如果当前服务是HTTP,使用重定向，那么默认在启动HTTPS服务监听443端口，然后将请求转发到443端口中" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.redirectHttps} onChange={handleChange('redirectHttps')} label='重定向到HTTPS' />
                <FCheckbox tooltip="是否使用HTTPS，如果使用了重定向HTTPS那么是必须的" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} checked={value.isHttps} onChange={handleChange('isHttps')} label='是否启用HTTPS' /><FCheckbox tooltip="如果使用了复制响应域名，那么你在使用域请求代理服务的时候网关也会吧域名复制请求到服务去" style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }}  checked={value.copyRequestHost} onChange={handleChange('copyRequestHost')} label='是否复制响应域名' />
                <Button onClick={()=>save()}>
                    编辑
                </Button>
            </Flexbox>
        </Modal>
    );
}