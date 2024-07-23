import { RateLimit } from "@/types";
import { useState } from "react";
import { message, Button, Select } from 'antd';
import { CreateRateLimit } from "@/services/RateLimitService";
import { Modal } from "@lobehub/ui";
import { Flexbox } from 'react-layout-kit';
import FInput from "@/components/FInput";
import FCheckbox from "@/components/FCheckbox";

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
        <Modal onCancel={() => onClose()} onClose={() => onClose()} footer={[]} open={visible} title='新增限流'>
            <Flexbox
                justify='column'>
                <FInput value={value.name} onChange={handleChange("name")} label='名称' style={{
                    marginBottom: '10px'
                }} placeholder='请输入名称' />
                <FCheckbox checked={value.enable} tooltip="启用当前限流策略" onChange={handleChange("enable")} label='是否启用' style={{
                    marginBottom: '10px'
                }} />
                <FInput value={value.endpoint} onChange={(e) => {
                    value.endpoint = e.target.value;
                    setValue({ ...value });
                }
                } min={1} label='限流的端点' style={{
                    marginBottom: '10px',

                }} placeholder='请输入限流端点' />
                <FInput value={value.limit} onChange={(e) => {
                    value.limit = new Number(e.target.value).valueOf();
                    setValue({ ...value });
                }
                } label='限流值' style={{
                    marginBottom: '10px'
                }} placeholder='请输入限流值' />
                <FInput value={value.period} onChange={(e) => {
                    value.period = e.target.value;
                    setValue({ ...value });
                }
                } label='限流周期（s(秒)|m(分钟)|h(小时)|d(天)）' style={{
                    marginBottom: '10px'
                }} placeholder='请输入限流周期' />

                <Select
                    value={value.endpointWhitelist}
                    mode="tags"
                    onChange={(e) => {
                        setValue({ ...value, endpointWhitelist: e })
                    }}
                    placeholder='请输入端点白名单的端点'
                    style={{
                        width: '100%',
                        flex: 1,
                        marginBottom: '10px',
                    }}
                >
                </Select>
                <Select
                    value={value.ipWhitelist}
                    mode="tags"
                    onChange={(e) => {
                        setValue({ ...value, ipWhitelist: e })
                    }}
                    placeholder='请输入IP白名单的ip'
                    style={{
                        width: '100%',
                        flex: 1,
                        marginBottom: '10px',
                    }}
                >
                </Select>
                <Button onClick={save} style={{
                    marginTop: '10px'
                }} type='primary'>保存</Button>
            </Flexbox>
        </Modal>
    );
}

export default CreateRateLimitPage;