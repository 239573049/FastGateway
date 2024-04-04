import { Button, Col, Collapse, Divider, Form, InputGroup, Modal, Notification, Row } from "@douyinfe/semi-ui";
import { LoadType, LocationInput, ServiceInput } from "../../../module";
import { CheckDirecotryExistence, CreateApiService } from "../../../services/ApiServiceService";

interface ICreateHttpProxyProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

const { Select, Input, Checkbox, } = Form;

interface IFormValues {
    formState: any;
    values: ServiceInput;
    formApi: any;
}

export default function CreateHttpProxy({
    visible,
    onClose,
    onOk,
}: ICreateHttpProxyProps) {
    return (
        <Modal
            title="新建HTTP代理"
            visible={visible}
            onCancel={onClose}
            fullScreen
            footer={[]}
        >
            <Form
                labelCol={{ span: 4 }}
                onSubmit={(values: ServiceInput) => {
                    CreateApiService(values)
                        .then(() => {
                            Notification.success({
                                title: '创建成功',
                                content: '创建成功',
                            });
                            onOk();
                        })
                }}
                style={{
                    padding: '20px',
                    border: '1px solid var(--semi-color-border)',
                    borderRadius: '8px',
                    overflow: 'auto',
                    height: 'calc(100vh - 150px)',
                }}

            >
                {
                    ({ values, formApi }: IFormValues) => (
                        <>
                            <Row>
                                <Col span={20}>
                                    <Input
                                        field="listen"
                                        label="端口"
                                        type="number"
                                        onChange={(v: any) => {
                                            values.listen = v;
                                            formApi.setValues(values);
                                        }}
                                        min={1}
                                        max={65535}
                                        rules={[{
                                            required: true,
                                            message: '端口不能为空',
                                        }]}
                                        width={
                                            '100%'
                                        }
                                        style={{
                                            borderRadius: '8px',
                                            padding: '3px',
                                            border: '1px solid var(--semi-color-border)',
                                            fontSize: '14px',
                                            marginRight: '10px',

                                        }}
                                    ></Input>
                                </Col>
                                <Col span={4}>
                                    <Checkbox
                                        field="isHttps"
                                        onChange={(v: any) => {
                                            values.isHttps = v;
                                            formApi.setValues(values);
                                        }}
                                        label="SSL"
                                        style={{
                                            marginTop: '10px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                            </Row>
                            <Row>
                                <Col span={12}>
                                    <Checkbox
                                        field="enableFlowMonitoring"
                                        label="启用流量监控"
                                        onChange={(v: any) => {
                                            values.enableFlowMonitoring = v;
                                            formApi.setValues(values);
                                        }}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                                <Col span={12}>
                                    <Checkbox
                                        field="enableRequestSource"
                                        label="启用请求来源"
                                        onChange={(v: any) => {
                                            values.enableRequestSource = v;
                                            formApi.setValues(values);
                                        }}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                            </Row>
                            <Row>
                                <Col span={12}>
                                    <Checkbox
                                        field="enableTunnel"
                                        label="启用隧道"
                                        initValue={false}
                                        onChange={(v: any) => {
                                            values.enableTunnel = v;
                                            formApi.setValues(values);
                                        }}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                                <Col span={12}>
                                    <Checkbox
                                        field="enable"
                                        initValue={true}
                                        onChange={(v: any) => {
                                            values.enable = v;
                                            formApi.setValues(values);
                                        }}
                                        label={"服务状态：" + (values.enable ? '启用' : '停用')}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                            </Row>
                            <Row>
                                <Col span={12}>
                                    <Checkbox
                                        field="enableBlacklist"
                                        label={"黑名单：" + (values.enableBlacklist ? '启用' : '停用')}
                                        initValue={false}
                                        onChange={(v: any) => {
                                            values.enableBlacklist = v;
                                            formApi.setValues(values);
                                        }}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                                <Col span={12}>
                                    <Checkbox
                                        field="enableWhitelist"
                                        onChange={(v: any) => {
                                            values.enableWhitelist = v;
                                            formApi.setValues(values);
                                        }}
                                        label={"白名单：" + (values.enableWhitelist ? '启用' : '停用')}
                                        style={{
                                            marginTop: '5px',
                                            marginLeft: '10px',
                                        }}
                                    ></Checkbox>
                                </Col>
                            </Row>
                            {
                                values.isHttps && (
                                    <Row>
                                        <Col span={12}>
                                            <Checkbox
                                                field="enableHttp3"
                                                label="启用HTTP3"
                                                initValue={false}
                                                onChange={(v: any) => {
                                                    values.enableHttp3 = v;
                                                    formApi.setValues(values);
                                                }}
                                                style={{
                                                    marginTop: '5px',
                                                    marginLeft: '10px',
                                                }}
                                            ></Checkbox>
                                        </Col>
                                    </Row>
                                )
                            }
                            <Divider></Divider>
                            <div style={{
                                display: 'flex',
                                justifyContent: 'center',
                                marginTop: '20px',
                            }} onClick={() => {
                                values.locations = values.locations || [];
                                values.locations.push({
                                    path: '',
                                    serviceNames: [],
                                    loadType: LoadType.IpHash,
                                    addHeader: {},
                                    type: 1,
                                });
                                formApi.setValues(values);
                            }}>
                                <Button block>添加路由代理</Button>
                            </div>
                            <Collapse>
                                {
                                    values.locations?.map((arrayField: LocationInput, index: number) => {
                                        return (<Collapse.Panel header={arrayField.path === '' ? "未填写路由" : arrayField.path} itemKey={index.toString()} key={index} style={{
                                            border: '1px solid var(--semi-color-border)',
                                            borderRadius: '8px',
                                            padding: '10px',
                                            marginTop: '10px',
                                        }}>

                                            <Select
                                                field="serviceNames"
                                                label="域名"
                                                rules={[{
                                                    required: true,
                                                    message: '域名不能为空',
                                                }]}
                                                onChange={(v: any) => {
                                                    console.log(v);
                                                    
                                                    arrayField.serviceNames = v;
                                                    formApi.setValues(values);
                                                }}
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
                                                defaultActiveFirstOption
                                            ></Select>
                                            {/* 关闭按钮 */}
                                            <div style={{
                                                display: 'flex',
                                                justifyContent: 'flex-end',
                                            }}>
                                                <Button
                                                    size='small'
                                                    type='secondary'
                                                    style={{
                                                        padding: '0',
                                                        width: '20px',
                                                        height: '20px',
                                                    }}
                                                    onClick={() => {
                                                        values.locations = values.locations.filter((item: any) => item !== arrayField);
                                                        formApi.setValues(values);
                                                    }}
                                                >X</Button>
                                            </div>
                                            <Input
                                                label="路由绑定"
                                                initValue={arrayField.path}
                                                defaultValue={arrayField.path}
                                                rules={[{
                                                    required: true,
                                                    message: '端口不能为空',
                                                }]}
                                                onChange={(v: any) => {
                                                    arrayField.path = v;
                                                    formApi.setValues(values);
                                                }}
                                                field="path" />
                                            <Select
                                                label="代理类型"
                                                field="type"
                                                style={{
                                                    width: '100%',
                                                    borderRadius: '8px',
                                                    padding: '3px',
                                                    border: '1px solid var(--semi-color-border)',
                                                    fontSize: '14px',
                                                }}
                                                initValue={arrayField.type}
                                                onChange={(v: any) => {
                                                    arrayField.type = v;
                                                    formApi.setValues(values);
                                                }}
                                                optionList={[{
                                                    label: '静态代理',
                                                    value: 1,
                                                }, {
                                                    label: '单个服务',
                                                    value: 2,
                                                }, {
                                                    label: '负载均衡代理',
                                                    value: 3,
                                                }]}
                                            />
                                            {
                                                arrayField.type === 1 && (
                                                    <>
                                                        <InputGroup>
                                                            <Input
                                                                label="根目录"
                                                                initValue={arrayField.root}
                                                                defaultValue={arrayField.root}
                                                                onChange={(v: any) => {
                                                                    arrayField.root = v;
                                                                    formApi.setValues(values);
                                                                }}
                                                                field="root" />
                                                            <Button onClick={() => {
                                                                CheckDirecotryExistence(arrayField.root as string)
                                                                    .then((res) => {
                                                                        if (res.success) {
                                                                            Notification.success({
                                                                                title: '目录存在',
                                                                                content: '目录存在',
                                                                            })
                                                                        } else {
                                                                            Notification.error({
                                                                                title: res.message,
                                                                            })
                                                                        }
                                                                    })
                                                            }} style={{
                                                                marginTop: '24px',
                                                                marginLeft: '10px',
                                                            }}>检查目录是否存在</Button>
                                                        </InputGroup>
                                                        <Select
                                                            label="try_files"
                                                            field="tryFiles"
                                                            multiple={true}
                                                            initValue={['index.html', 'index.htm']}
                                                            allowCreate={true}
                                                            onChange={(v: any) => {
                                                                arrayField.tryFiles = v;
                                                                formApi.setValues(values);
                                                            }}
                                                            filter={true}
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
                                                arrayField.type === 2 && (
                                                    <Input
                                                        label="代理地址"
                                                        initValue={arrayField.proxyPass}
                                                        defaultValue={arrayField.proxyPass}
                                                        onChange={(v: any) => {
                                                            arrayField.proxyPass = v;
                                                            formApi.setValues(values);
                                                        }}
                                                        field="proxyPass" />
                                                )
                                            }
                                            {
                                                arrayField.type === 3 && (
                                                    <>
                                                        <Select
                                                            label="负载均衡"
                                                            field="loadType"
                                                            initValue={arrayField.loadType}
                                                            onChange={(v: any) => {
                                                                arrayField.loadType = v;
                                                                formApi.setValues(values);
                                                            }}
                                                            style={{
                                                                width: '100%',
                                                                borderRadius: '8px',
                                                                padding: '3px',
                                                                border: '1px solid var(--semi-color-border)',
                                                                fontSize: '14px',
                                                            }}
                                                            optionList={[{
                                                                label: 'IP哈希',
                                                                value: LoadType.IpHash,
                                                            }, {
                                                                label: '轮询',
                                                                value: LoadType.RoundRobin,
                                                            }, {
                                                                label: '加权轮询',
                                                                value: LoadType.WeightRoundRobin,
                                                            }]}
                                                        />
                                                        <Divider></Divider>
                                                        <Button
                                                            onClick={() => {
                                                                arrayField.upStreams = arrayField.upStreams || [];
                                                                arrayField.upStreams.push({
                                                                    server: '',
                                                                    weight: 1,
                                                                });
                                                                formApi.setValues(values);
                                                            }}
                                                            style={{
                                                                marginBottom: '10px',
                                                                marginTop: '10px',
                                                            }}
                                                            block>添加服务</Button>
                                                        <Collapse>
                                                            {
                                                                arrayField.upStreams?.map((upStream: any, index: number) => {
                                                                    return (
                                                                        <Collapse.Panel itemKey={index.toString()} header={upStream.server === '' ? "未填写地址" : upStream.server}>
                                                                            <Row>
                                                                                <Col span={12}>
                                                                                    <Input
                                                                                        label="服务地址"
                                                                                        field="server"
                                                                                        initValue={upStream.server}
                                                                                        onChange={(v: any) => {
                                                                                            upStream.server = v;
                                                                                            formApi.setValues(values);
                                                                                        }}
                                                                                    />
                                                                                </Col>
                                                                                <Col span={9} style={{
                                                                                    marginLeft: '10px',
                                                                                }}>
                                                                                    <Input
                                                                                        label="权重"
                                                                                        field="weight"
                                                                                        type="number"
                                                                                        initValue={upStream.weight}
                                                                                        onChange={(v: any) => {
                                                                                            upStream.weight = v;
                                                                                            formApi.setValues(values);
                                                                                        }}
                                                                                    />
                                                                                </Col>
                                                                                <Col span={2}>
                                                                                    <Button
                                                                                        onClick={() => {
                                                                                            arrayField.upStreams = arrayField.upStreams?.filter((item: any) => item !== upStream);
                                                                                            formApi.setValues(values);
                                                                                        }}
                                                                                        type='danger'
                                                                                        style={{
                                                                                            marginTop: '36px',
                                                                                            marginLeft: '10px',

                                                                                        }}
                                                                                    >删除服务</Button>
                                                                                </Col>
                                                                            </Row>
                                                                        </Collapse.Panel>)
                                                                })
                                                            }
                                                        </Collapse>
                                                    </>
                                                )
                                            }

                                        </Collapse.Panel>)
                                    })
                                }
                            </Collapse>

                            <Button style={{
                                marginTop: '20px',

                            }} htmlType="submit" block>
                                提交
                            </Button>
                        </>

                    )
                }
            </Form>
        </Modal>
    );
}