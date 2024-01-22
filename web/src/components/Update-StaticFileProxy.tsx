import { SideSheet, Form, Button, ArrayField, Toast } from "@douyinfe/semi-ui";
import { IconMinusCircle, IconPlusCircle } from '@douyinfe/semi-icons';
import { StaticFileProxyEntity } from "../index.d";
import { updateStaticFileProxy } from "../service/StaticFileProxyService";
import React from "react";

interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
    entity?: StaticFileProxyEntity;
}

const { Input, TagInput, Checkbox, } = Form;
export default function UpdateStaticFileProxy({ visible, onCancel, onSusccess, entity }: IProps) {
    function handleSubmit(values: any): void {
        // 将responseHeaders转换字典
        let responseHeaders = {} as { [key: string]: string; };
        values.responseHeaders?.forEach((item: any) => {
            responseHeaders[item.key] = item.value;
        })

        values.responseHeaders = responseHeaders;
        values.id = entity?.id;

        updateStaticFileProxy(values as StaticFileProxyEntity).then((value: any) => {
            if (value.code === 0) {
                Toast.success('修改成功');
                onSusccess();
            } else {
                Toast.error(value.message);
            }
        });
    }

    function responseHeaders(responseHeaders: { [key: string]: string; } | undefined) {
        if (!responseHeaders) {
            return [];
        }

        // 转换对象
        let result = [] as any[];
        // 遍历responseHeaders
        for (let key in responseHeaders) {
            result.push({
                key,
                value: responseHeaders[key],
            })
        }
        return result;
    }

    return (
        <SideSheet width={500} title="修改静态文件代理配置" visible={visible} onCancel={onCancel}>
            <Form style={{
                overflowX: 'hidden',
                overflowY: 'auto',
            }} onSubmit={values => handleSubmit(values)} >
                {({ formState, values, formApi }) => (
                    <>
                        <Input
                            initValue={entity?.name}
                            rules={[
                                { required: true, message: '代理名称是必填的' },
                                { validator: (rule, value) => value.length < 10, message: '路由名称长度不能超过10位' },
                            ]} field='name' required={true} size='large' label='路由名称' style={{ width: '100%' }} placeholder='请输入路由名称'></Input>
                        <Input 
                            initValue={entity?.description} field='description' label='代理描述' size='large' style={{ width: '100%' }} placeholder='请输入路由描述'></Input>
                        <Input 
                            initValue={entity?.root} rules={[
                            { required: true, message: '指定目录是必填的' },
                        ]} field='root' label='指定目录' size='large' style={{ width: '100%' }} placeholder='请输入指定目录'></Input>
                        <Input
                            initValue={entity?.path} rules={[
                            { required: true, message: '路由匹配路径是必填的' },
                        ]} field='path' label='路由匹配路径' size='large' style={{ width: '100%' }} placeholder='请输入匹配路径'></Input>
                        <TagInput
                            initValue={entity?.hosts}  field="hosts" label='匹配域名' placeholder="请输入匹配域名（支持多个）" />
                        <Checkbox field='gZip'
                            initValue={entity?.gZip}  label='是否启用GZip压缩' />
                        <ArrayField initValue={responseHeaders(entity?.responseHeaders)}  field='responseHeaders' >
                            {({ add, arrayFields }) => (
                                <React.Fragment>
                                    <Button onClick={add} block icon={<IconPlusCircle />} theme='light'>添加响应头</Button>
                                    {
                                        arrayFields.map(({ field, key, remove }, i) => (
                                            <div key={key} style={{ width: 800, display: 'flex' }}>
                                                <Input
                                                    field={`${field}[key]`}
                                                    label={'响应头Key'}
                                                    size='large'
                                                    required={true}
                                                    style={{ width: 200, marginRight: 16 }}
                                                >
                                                </Input>
                                                <Input
                                                    field={`${field}[value]`}
                                                    size='large'
                                                    label={`响应头Value`}
                                                    style={{ width: 170, marginRight: 16 }}
                                                >
                                                </Input>
                                                <Button
                                                    type='danger'
                                                    theme='borderless'
                                                    size='large'
                                                    icon={<IconMinusCircle />}
                                                    onClick={remove}
                                                    style={{ margin: 12 }}
                                                />
                                            </div>
                                        ))
                                    }
                                </React.Fragment>
                            )}
                        </ArrayField>

                        <Input initValue={entity?.index} field='index' label='默认文件' size='large' style={{ width: '100%' }} placeholder='请输入默认文件地址'></Input>
                        <TagInput initValue={entity?.tryFiles} field="tryFiles" label='尝试的文件列表' placeholder="此参数用于尝试的文件列表，如果找不到请求的文件，则会尝试下一个。" />
                        <Button size='large' block style={{
                            marginTop: '20px',
                        }} htmlType='submit' type="secondary" theme='solid'>提交</Button>
                    </>
                )}
            </Form>
        </SideSheet>)
}