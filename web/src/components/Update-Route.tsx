import { Button, Form, SideSheet, Toast } from "@douyinfe/semi-ui";
import { ClusterEntity, RouteEntity } from "../index.d";
import { getClusters, updateRoute } from "../service/GatewayService";
import { useEffect, useState } from "react";

interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
    router?: RouteEntity;
}

const { Option } = Form.Select;
const { Select, Input, TagInput } = Form;


export default function UpdateCluster({ visible, onCancel, onSusccess, router }: IProps) {

    const [clusterIds, setClusterIds] = useState<ClusterEntity[]>([]);

    async function loadingCluster() {
        const clusters = await getClusters() as any;
        setClusterIds(clusters.data);
    }

    function handleSubmit(value: RouteEntity) {
        value.routeId = router!.routeId;

        if (value.clusterId === undefined) {
            Toast.error('请选择集群');
            return
        }

        updateRoute(value)
            .then(() => {
                Toast.success('更新成功');
                onSusccess();
            }).catch(() => {
                Toast.error('更新失败');
            })


    }

    useEffect(() => {
        loadingCluster()
    }, [
        visible
    ])

    return (
        <SideSheet width={500} title="编辑路由" visible={visible} onCancel={onCancel}>
            <Form style={{
                overflowX: 'hidden',
                overflowY: 'auto',
            }} onSubmit={values => handleSubmit(values)} >
                {({ formState, values, formApi }) => (
                    <>
                        <Input
                            initValue={router?.routeName}
                            rules={[
                                { required: true, message: '路由名称是必填的' },
                                { validator: (rule, value) => value.length < 10, message: '路由名称长度不能超过10位' },
                            ]} field='routeName' required={true} size='large' label='路由名称' style={{ width: '100%' }} placeholder='请输入路由名称'></Input>
                        <Input initValue={router?.description} field='description' label='路由描述' size='large' style={{ width: '100%' }} placeholder='请输入路由描述'></Input>
                        <Select initValue={router?.clusterId} field='clusterId' rules={[
                            {
                                required: true,
                                message: '请选择集群',
                            },
                        ]} label='集群' size='large' style={{ width: '100%' }} placeholder='请选择集群'>
                            {clusterIds.map((x: ClusterEntity) => {
                                return (<Option value={x.clusterId}>{x.clusterName}</Option>)
                            })}
                        </Select>
                        <Input initValue={router?.matchEntities.path} rules={[
                            { required: true, message: '路由匹配路径是必填的' },
                        ]} field='matchEntities.path' label='路由匹配路径' size='large' style={{ width: '100%' }} placeholder='请输入匹配路径'></Input>
                        <TagInput initValue={router?.matchEntities.hosts} field="matchEntities.hosts" label='路由匹配域名' placeholder="请输入匹配域名（支持多个）" />
                        <Button size='large' block style={{
                            marginTop: '20px',
                        }} htmlType='submit' type="secondary" theme='solid'>提交</Button>
                    </>
                )}
            </Form>
        </SideSheet>)
}