import { Modal } from "@lobehub/ui";
import { Flexbox } from 'react-layout-kit';
import { Button } from "antd";
import FInput from "@/components/FInput";
import FCheckbox from "@/components/FCheckbox";
import { Select, message } from 'antd';
import { useState } from "react";
import { CreateBlacklist } from "@/services/BlacklistAndWhitelistService";

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
        ips: [],
        name: '',
        description: '',
        enable: true,
        isBlacklist: isBlacklist,
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
        <Modal onCancel={() => onClose()} footer={[]} open={visible} title={isBlacklist ? '新增黑名单' : '新增白名单'}>
            <Flexbox
                justify='column'>
                <FInput value={value.name} onChange={handleChange("name")} label='名称' style={{
                    marginBottom: '10px'
                }} placeholder='请输入名称' />
                <FInput value={value.description} onChange={handleChange("description")} label='描述' style={{
                    marginBottom: '10px'
                }} placeholder='请输入描述' />

                <Select
                    value={value.ips}
                    mode="tags"
                    onChange={(e) => {
                        // 判断是否为正常ip格式
                        const reg = /^(\d+)\.(\d+)\.(\d+)\.(\d+)$/;
                        const isIp = e.every((item: string) => reg.test(item));
                        if (!isIp) {
                            message.error('请输入正确的IP格式');
                            return;
                        }

                        setValue({ ...value, ips: e })
                    }}
                    placeholder='请输入IP，支持格式（xxx.xxx.xxx.xxx）'
                    style={{
                        width: '100%',
                        flex: 1,
                        marginBottom: '10px',
                    }}
                >
                </Select>
                <FCheckbox tooltip={`启用或禁用当前${isBlacklist ? "黑名单" : "白名单"}`} style={{
                    marginTop: '10px',
                    marginBottom: '10px'
                }} value={value.enable} onChange={handleChange('enable')} label='是否启用' />
                <Button onClick={() => save()}>
                    保存
                </Button>
            </Flexbox>
        </Modal>
    );
}

export default CreateBlacklistAndWhitelist;