import { ArrayField, Button, Form, SideSheet, Toast } from "@douyinfe/semi-ui";
import { ClusterEntity } from "../index.d";
import React from "react";

import { IconPlusCircle, IconMinusCircle } from '@douyinfe/semi-icons';
import { updateCluster } from "../service/GatewayService";

interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
    cluster?: ClusterEntity;
}


const { Option } = Form.Select;
const { Select, Input, TagInput } = Form;

export default function UpdateCluster({ visible, onCancel, onSusccess, cluster }: IProps) {

    function handleSubmit(value: ClusterEntity) {
        value.clusterId = cluster!.clusterId;

        if (!value.destinationsEntities || value.destinationsEntities?.length <= 0) {
            Toast.error('集群端点不能为空');
            return
        }

        // 校验集群端点是否为正常的url
        for (let i = 0; i < value.destinationsEntities.length; i++) {
            const item = value.destinationsEntities[i];
            if (!item.address) {
                Toast.error('集群端点不能为空');
                return
            }
        }

        updateCluster(value).then(() => {
            Toast.success('添加成功');
            onSusccess();
        }).catch(() => {
            Toast.error('添加失败');
        })
        
    }

    return (
        <SideSheet width={500} title="编辑路由" visible={visible} onCancel={onCancel}>
            <Form style={{
                overflowX: 'hidden',
                overflowY: 'auto',
            }} onSubmit={values => handleSubmit(values)} >
                {({ formState, values, formApi }) => (
                    <>
                        <Input rules={[
                            { required: true, message: '集群名称是必填的' },
                            { validator: (rule, value) => value.length < 10, message: '集群名称长度不能超过10位' },
                        ]} initValue={cluster?.clusterName} field='clusterName' size='large' label='集群名称' style={{ width: '100%' }} placeholder='请输入集群名称'></Input>
                        <Input initValue={cluster?.description} field='description' label='集群描述' size='large' style={{ width: '100%' }} placeholder='请输入集群描述'></Input>
                        <ArrayField  initValue={cluster?.destinationsEntities} field='destinationsEntities' >
                            {({ add, arrayFields }) => (
                                <React.Fragment>
                                    <Button onClick={add} block icon={<IconPlusCircle />} theme='light'>添加集群端点</Button>
                                    {
                                        arrayFields.map(({ field, key, remove }, i) => (
                                            <div key={key} style={{ width: 800, display: 'flex' }}>
                                                <Input
                                                    field={`${field}[address]`}
                                                    initValue={cluster?.destinationsEntities[i]?.address}
                                                    label={'集群端点'}
                                                    rules={[
                                                        { required: true, message: '集群端点是必填的' },
                                                        { validator: (rule: any, value: string) => value.startsWith("http://") || value.startsWith("https://"), message: '集群端点必须是http://或https://开头' },
                                                    ]}
                                                    size='large'
                                                    required={true}
                                                    style={{ width: 200, marginRight: 16 }}
                                                >
                                                </Input>
                                                <Input
                                                    field={`${field}[host]`}
                                                    size='large'
                                                    initValue={cluster?.destinationsEntities[i]?.host}
                                                    label={`集群Host`}
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
                        <Button size='large' block style={{
                            marginTop: '20px',
                        }} htmlType='submit' type="secondary" theme='solid'>提交</Button>
                    </>
                )}
            </Form>
        </SideSheet>
    )
}