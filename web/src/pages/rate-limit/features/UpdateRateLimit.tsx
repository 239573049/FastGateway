import { Button, Collapse, Form, Modal, Notification } from "@douyinfe/semi-ui";
import {  UpdateRateLimit } from "../../../services/RateLimitService";

interface IUpdateRateLimitProps {
    visible: boolean;
    onClose: () => void;
    onOk: (data: RateLimit) => void; // Assuming onOk will receive the form data
    value:any;
}

interface RateLimit {
    // Omitting some properties for brevity
    name: string;
    enable: boolean;
    endpointWhitelist: string[];
    clientIdHeader: string;
    clientWhitelist: string[];
    realIpHeader: string;
    ipWhitelist: string[];
    httpStatusCode: number;
    quotaExceededMessage: string;
    rateLimitCounterPrefix: string;
    enableEndpointRateLimiting: boolean;
    disableRateLimitHeaders: boolean;
    enableRegexRuleMatching: boolean;
    generalRules: GeneralRule[];
}

interface GeneralRule {
    endpoint: string;
    period: string;
    limit: number;
}

const { Input, Checkbox,TextArea, Select, InputNumber } = Form;

export default function UpdateRateLimitPage({
    visible,
    onClose,
    onOk,
    value
}: IUpdateRateLimitProps) {

    function handleSubmit(values: RateLimit) {
        if (values.generalRules.length === 0) {
            Notification.error({
                title: '请添加速率限制规则',
            });
            return;
        }

        UpdateRateLimit(values,values.name).then(() => {
            Notification.success({
                title: '创建成功',
            });
            onOk(values);
        }
        ).catch(() => {
            Notification.error({
                title: '创建失败',
            });
        });
    }

    return (
        <Modal
            title="创建限流策略"
            onCancel={onClose}
            fullScreen={true}
            footer={[]}
            visible={visible}
        >
            <Form
                labelCol={{ span: 4 }}
                initValues={value}
                defaultValue={value}
                onSubmit={(values: RateLimit) => {
                    handleSubmit(values);
                }}
                style={{
                    padding: '20px',
                    border: '1px solid var(--semi-color-border)',
                    borderRadius: '8px',
                    overflow: 'auto',
                    height: 'calc(100vh - 150px)'
                }}
            >
                {
                    (({ values, formApi }) => {
                        return (<>

                            <Input
                                label="限流策略名称"
                                field="name"
                                rules={[{ required: true, message: '请输入限流策略名称' }]}
                            />
                            <Checkbox
                                label="是否启用"
                                initValue={true}
                                field="enable"
                            />
                            <Input
                                label="客户端ID头部"
                                field="clientIdHeader"
                                initValue={"X-ClientId"}
                            />
                            <Select
                                field="endpointWhitelist"
                                label="端点白名单"
                                style={{ width: '100%' }}
                                allowCreate={true}
                                multiple={true}
                                placeholder="输入并按Enter添加"
                                filter={true}
                                defaultActiveFirstOption
                            />
                            <Select
                                field="clientWhitelist"
                                label="客户端白名单"
                                style={{ width: '100%' }}
                                allowCreate={true}
                                multiple={true}
                                placeholder="输入并按Enter添加"
                                filter={true}
                                defaultActiveFirstOption
                            />
                            <Select
                                field="ipWhitelist"
                                label="IP白名单"
                                style={{ width: '100%' }}
                                allowCreate={true}
                                multiple={true}
                                placeholder="输入并按Enter添加"
                                filter={true}
                                defaultActiveFirstOption
                            />
                            <Input
                                label="真实IP头部"
                                field="realIpHeader"
                                initValue={"X-Real-IP"}
                            />
                            <InputNumber
                                initValue={429}
                                label="HTTP状态码"
                                field="httpStatusCode"
                            />
                            <Input
                                initValue={"text/html"}
                                label="错误内容类型"
                                defaultValue={"text/html"}
                                field="rateLimitContentType"
                            />
                            <TextArea
                                label="超出配额消息"
                                field="quotaExceededMessage"
                                style={{
                                }}
                                rows={8}
                                placeholder="超出配额消息（支持html）"
                            />
                            <Input
                                label="速率限制计数器前缀"
                                initValue={'crlc'}
                                field="rateLimitCounterPrefix"
                            />
                            <Checkbox
                                label="启用端点速率限制"
                                field="enableEndpointRateLimiting"
                            />
                            <Checkbox
                                label="禁用速率限制头部"
                                field="disableRateLimitHeaders"
                            />
                            <Checkbox
                                label="启用正则规则匹配"
                                field="enableRegexRuleMatching"
                            />
                            <Button block onClick={() => {
                                if (!values.generalRules) {
                                    values.generalRules = [];
                                }

                                values.generalRules.push({
                                    endpoint: '',
                                    period: '1s',
                                    limit: 1
                                });

                                formApi.setValue(values, values);

                            }}>
                                添加速率限制规则
                            </Button>
                            <Collapse>
                                {
                                    values.generalRules?.map((rule: any, index: any) => {
                                        return (
                                            <Collapse.Panel
                                                key={index}
                                                itemKey={"index" + index}
                                                header={`规则${index + 1}`}
                                            >

                                                <Input
                                                    label="端点"
                                                    field="endpoint"
                                                    initValue={rule.endpoint}
                                                    defaultValue={rule.endpoint}
                                                    onChange={(e) => {
                                                        values.generalRules[index].endpoint = e;
                                                        formApi.setValue(values, values);
                                                    }}
                                                    // 后面显示关闭按钮
                                                    suffix={
                                                        <Button
                                                            color="red"
                                                            style={{
                                                                color: 'red',
                                                                border: 'none'
                                                            }}
                                                            onClick={() => {
                                                                values.generalRules.splice(index, 1);
                                                                formApi.setValue(values, values);
                                                            }
                                                            }>
                                                            删除
                                                        </Button>

                                                    }
                                                    rules={[
                                                        { required: true, message: '请输入端点' },
                                                        // 支持 */ */api
                                                        { pattern: /^(\*|\/|\/[a-zA-Z0-9_-]+)+$/, message: '请输入正确的端点' },
                                                    ]}
                                                />
                                                <Input
                                                    label="m(分钟)|h(小时)|d(天)|s(秒)"
                                                    field="period"
                                                    initValue={rule.period}
                                                    defaultValue={rule.period}
                                                    onChange={(e) => {
                                                        values.generalRules[index].period = e;
                                                        formApi.setValue(values, values);
                                                    }}
                                                    rules={[
                                                        { required: true, message: '请输入周期' },
                                                        {
                                                            pattern: /^[1-9]\d*[mhd]$/,
                                                            message: '请输入正确的周期，示例: 1m, 1h, 1d, 1s'
                                                        }
                                                    ]}
                                                />
                                                <InputNumber
                                                    label="限制"
                                                    initValue={rule.limit}
                                                    defaultValue={rule.limit}
                                                    onChange={(e) => {
                                                        values.generalRules[index].limit = e;
                                                        formApi.setValue(values, values);
                                                    }}
                                                    field="limit"
                                                    rules={[
                                                        { required: true, message: '请输入限制' },
                                                        { type: 'number', message: '请输入数字' },
                                                    ]}
                                                />
                                            </Collapse.Panel>
                                        );
                                    })
                                }
                            </Collapse>

                            <Button
                                style={{ marginTop: '20px' }}
                                htmlType="submit"
                                block
                            >
                                提交
                            </Button>
                        </>)
                    })
                }
            </Form>
        </Modal>
    );
}