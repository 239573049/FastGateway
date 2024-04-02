import { Button, Form, Modal, Notification } from "@douyinfe/semi-ui";
import { CreateCert } from "../../../services/CertService";

interface ICreateCertProps {
    visible: boolean;
    onCancel: () => void;
    onOk: () => void;

}

const { Input,  Select } = Form;

export default function CreateCertPage({
    visible,
    onCancel,
    onOk
}: ICreateCertProps) {
    return (
        <Modal
            title="创建证书"
            visible={visible}
            onOk={onOk}
            footer={[]}
            onCancel={onCancel}
        >
            <Form
                wrapperCol={{ span: 14 }}
                onSubmit={(values) => {
                    values.autoRenew = values.autoRenew === 'true';
                    CreateCert(values).then((result) => {
                        if(!result.success){
                            Notification.error({
                                title: '创建失败',
                                content: result.message
                            });
                            return;
                        }
                        Notification.success({
                            title: '创建成功'
                        });
                        onOk();
                    });
                }}
            >
                {
                    ({ values, formApi }: any) => (
                        <>
                            <Select
                                name="type"
                                label="证书类型"
                                onChange={(value) => {
                                    values.type = value;
                                    formApi.setValues(values);
                                }}
                                initValue={"true"}
                                field="autoRenew"
                                optionList={[{
                                    label: "自动申请免费证书",
                                    value: "true"
                                }, {
                                    label: "上传已有证书",
                                    value: "false"
                                }]}
                            />
                            {
                                values.autoRenew === "true" && (
                                    <>

                                        <Select
                                            field="domains"
                                            label="域名"
                                            rules={[{
                                                required: true,
                                                message: '域名不能为空',
                                            }]}
                                            style={{
                                                width: '100%',
                                                borderRadius: '8px',
                                                padding: '3px',
                                                border: '1px solid var(--semi-color-border)',
                                                fontSize: '14px',
                                            }}
                                            allowCreate={true}
                                            multiple={true}
                                            filter={true}
                                            onChange={v => {
                                                values.domains = v;
                                                formApi.setValues(values);
                                            }}
                                            defaultActiveFirstOption
                                        ></Select>
                                        <Input
                                            field="email"
                                            label="邮箱"
                                            rules={[{
                                                required: true,
                                                message: '邮箱不能为空',
                                            }]}
                                            onChange={v => {
                                                values.email = v;
                                                formApi.setValues(values);
                                            }}
                                            style={{
                                                width: '100%',
                                                borderRadius: '8px',
                                                padding: '3px',
                                                border: '1px solid var(--semi-color-border)',
                                                fontSize: '14px',
                                            }}
                                        />
                                    </>
                                )
                            }
                            {
                                values.autoRenew === "false" && (
                                    <>
                                        <Input
                                            field="certFile"
                                            label="证书文件（.pfx）"
                                            rules={[{
                                                required: true,
                                                message: '证书不能为空',
                                            }]}
                                            onChange={v => {
                                                values.certFile = v;
                                                formApi.setValues(values);
                                            }}
                                            style={{
                                                width: '100%',
                                                borderRadius: '8px',
                                                padding: '3px',
                                                border: '1px solid var(--semi-color-border)',
                                                fontSize: '14px',
                                            }}
                                        />
                                        <Input
                                            field="certPassword"
                                            label="证书密码"
                                            rules={[{
                                                required: true,
                                                message: '证书密码不能为空',
                                            }]}
                                            onChange={v => {
                                                values.certPassword = v;
                                                formApi.setValues(values);
                                            }}
                                            style={{
                                                width: '100%',
                                                borderRadius: '8px',
                                                padding: '3px',
                                                border: '1px solid var(--semi-color-border)',
                                                fontSize: '14px',
                                            }}
                                        />
                                    </>
                                )
                            }

                            <Button htmlType='submit' block>
                                提交
                            </Button>
                        </>)
                }
            </Form>
        </Modal>
    )
}