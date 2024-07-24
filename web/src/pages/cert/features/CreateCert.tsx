import FCheckbox from "@/components/FCheckbox";
import FInput from "@/components/FInput";
import { CreateCert } from "@/services/CertService";
import { Modal } from "@lobehub/ui";
import { Button, message } from 'antd';
import { useState } from "react";
import { Flexbox } from 'react-layout-kit';

interface CreateCertProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

export default function CreateCertPage({
    visible,
    onClose,
    onOk
}: CreateCertProps) {
    const [value, setValue] = useState({
        domain: '',
        autoRenew: true,
        email: '',
    });

    function handleOk() {
        // 判断是否输入了域名
        if (!value.domain) {
            message.error('请输入域名');
            return;
        }
        // 判断是否输入了邮箱
        if (!value.email) {
            message.error('请输入邮箱');
            return;
        }

        // 判断邮箱格式是否正确
        if (!/^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$/.test(value.email)) {
            message.error('邮箱格式不正确');
            return;
        }

        CreateCert(value)
            .then(() => {
                message.success('新增成功');
                onOk();
            }).catch(() => {
                message.error('新增失败');
            });
        
        onOk();
    }

    const handleChange = (field: string) => (e: { target: any; }) => {
        const { target } = e;
        const newValue = target.type === 'checkbox' ? target.checked : target.value;
        setValue({ ...value, [field]: newValue });
    };



    return (
        <Modal footer={[]} title='新增证书' open={visible} onCancel={() => onClose()} onClose={() => onClose()}>
            <Flexbox
                justify='column'
                style={{
                    paddingTop: '20px',
                }}
            ><FInput 
                    style={{
                        marginTop: '20px'
                    }}
                    value={value.domain} onChange={handleChange("domain")} label='证书域名' />
                <FInput
                    style={{
                        marginTop: '20px'
                    }}
                    value={value.email} onChange={handleChange("email")} label='邮箱' />
                <FCheckbox
                    checked={value.autoRenew}
                    onChange={handleChange("autoRenew")}
                    style={{
                        marginTop: '20px',
                        marginBottom: '20px'
                    }}
                    label='自动续期' type='checkbox' />
                <Button onClick={() => {
                    handleOk();
                }}>确定</Button>
            </Flexbox>
        </Modal>
    )
}