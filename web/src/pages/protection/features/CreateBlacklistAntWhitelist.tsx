import { Button, Form, Modal, Notification } from "@douyinfe/semi-ui";
import { ProtectionType } from "../../../module";
import { CreateBlacklistAndWhitelist } from "../../../services/ProtectionService";

interface ICreateBlacklistAntWhitelistProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

const { Input, Select, Checkbox } = Form;

export default function CreateBlacklistAntWhitelist({
    visible,
    onClose,
    onOk,
}: ICreateBlacklistAntWhitelistProps) {

    function handleSubmit(values: any) {
        CreateBlacklistAndWhitelist(values).then(() => {
            Notification.success({
                title: '创建成功',
            })
            onOk();
        })

    }
    return (
        <Modal
            title="创建黑名单/白名单"
            visible={visible}
            onCancel={() => onClose()}
            footer={[

            ]}
        >
            <Form
                wrapperCol={{ span: 14 }}
                onSubmit={handleSubmit}
            >
                <Input
                    label="名称"
                    field="name"
                    name="name"
                    rules={[{ required: true, message: '请输入名称' }]}
                >
                </Input>
                <Input
                    label="描述"
                    field="description"
                    name="description"
                    rules={[{ required: true, message: '请输入描述' }]}
                >
                </Input>
                <Select
                    label="类型"
                    field="type"
                    style={{
                        width: '100%',

                    }}
                    name="type"
                    initValue={ProtectionType.Blacklist}
                    optionList={[
                        { label: '黑名单', value: ProtectionType.Blacklist },
                        { label: '白名单', value: ProtectionType.Whitelist },
                    ]}
                    rules={[{ required: true, message: '请选择类型' }]}
                >
                </Select>
                <Checkbox
                    label="是否启用"
                    style={{
                        width: '100%',

                    }}
                    field="enable"
                    name="enable"
                    
                    rules={[{ required: true, message: '请选择是否启用' }]}
                >
                </Checkbox>
                <Select
                    label="IP列表"
                    field="ips"
                    name="ips"
                    allowCreate={true}
                    multiple={true}
                    filter={true}
                    style={{
                        width: '100%',
                    }}
                    defaultActiveFirstOption
                    rules={[{ required: true, message: '请输入IP列表' }, {
                        pattern: /^((\d{1,3}\.){3}\d{1,3})|((\d{1,3}\.){3}\d{1,3}-(\d{1,3}\.){3}\d{1,3})|((\d{1,3}\.){3}\d{1,3}\/\d{1,2})$/,
                        message: '请输入正确的IP列表，示例:10.0.0.1-10.0.0.255 或 172.16.0.1/24 或 192.168.1.1'
                    }]}
                />
                <Button htmlType='submit' block type="primary">确定</Button>

            </Form>
        </Modal>
    )
}