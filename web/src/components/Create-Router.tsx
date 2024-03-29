import { Button, Form, SideSheet, Toast } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { ClusterEntity, RouteEntity } from "../index.d";
import { getClusters, addRoute } from "../service/GatewayService";


interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
}

const { Option } = Form.Select;
const { Select, Input, TagInput, Checkbox } = Form;

export default function CreateRouter({ visible, onCancel, onSusccess }: IProps) {
    const [clusterIds, setClusterIds] = useState<ClusterEntity[]>([]);

    useEffect(() => {
        if (visible) {
            loadingCluster()
        }
    }, [visible])


    function handleSubmit(values: RouteEntity) {
        if (values.clusterId === undefined) {
            Toast.error('请选择集群');
            return
        }

        addRoute(values)
            .then(() => {
                Toast.success('添加成功');
                onSusccess();
            }).catch(() => {
                Toast.error('添加失败');
            })

    }

    async function loadingCluster() {
        const clusters = await getClusters() as any;
        setClusterIds(clusters.data);
    }

    return (

        <SideSheet width={500} title="创建路由" visible={visible} onCancel={onCancel}>
            <Form style={{
                overflowX: 'hidden',
                overflowY: 'auto',
            }} onSubmit={values => handleSubmit(values)} >
                {({ formState, values, formApi }) => (
                    <>
                        <Input
                            rules={[
                                { required: true, message: '路由名称是必填的' }
                            ]} field='routeName' required={true} size='large' label='路由名称' style={{ width: '100%' }} placeholder='请输入路由名称'></Input>
                        <Input field='description' label='路由描述' size='large' style={{ width: '100%' }} placeholder='请输入路由描述'></Input>
                        <Input field='authorizationPolicy' label='授权策略' size='large' style={{ width: '100%' }} placeholder='请输入授权策略'></Input>
                        <Checkbox field='requireHttpsMetadata' label='认证中心是否需要https' style={{ width: '100%' }}></Checkbox>
                        <Input field='authorizationPolicyAddress' placeholder='请输入授权认证中心地址' size='large' label='授权认证中心地址' style={{ width: '100%' }}></Input>
                        <Input field='maxRequestBodySize' min={16777216} placeholder='请输入请求Body最大长度' type='number' size='large' label='请求Body最大长度' style={{ width: '100%' }}></Input>
                        <Select field='clusterId' rules={[
                            {
                                required: true,
                                message: '请选择集群',
                            },
                        ]} label='集群' size='large' style={{ width: '100%' }} placeholder='请选择集群'>
                            {clusterIds.map((x: ClusterEntity) => {
                                return (<Option value={x.clusterId}>{x.clusterName}</Option>)
                            })}
                        </Select>
                        <Input rules={[
                            { required: true, message: '路由匹配路径是必填的' },
                        ]} field='path' label='路由匹配路径' size='large' style={{ width: '100%' }} placeholder='请输入匹配路径'></Input>
                        <TagInput field="hosts" label='路由匹配域名' placeholder="请输入匹配域名（支持多个）" />
                        <Button size='large' block style={{
                            marginTop: '20px',
                        }} htmlType='submit' type="secondary" theme='solid'>提交</Button>
                    </>
                )}
            </Form>
        </SideSheet>
    )
}