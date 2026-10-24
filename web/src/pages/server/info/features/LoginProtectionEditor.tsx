import { useState } from "react";
import { Plus, X } from "lucide-react";

import { LoginEndpointRule, RiskBlockScope, RiskProtection } from "@/types";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
    SelectValue,
} from "@/components/ui/select";
import { Switch } from "@/components/ui/switch";
import {
    LOGIN_METHODS,
    createLoginEndpoint,
    createRiskProtection,
    parseStatusCodes,
} from "./loginProtection";

interface LoginProtectionEditorProps {
    idPrefix: string;
    value?: RiskProtection | null;
    onChange: (value: RiskProtection | null) => void;
}

export default function LoginProtectionEditor({ idPrefix, value, onChange }: LoginProtectionEditorProps) {
    // 状态码输入框按原文编辑，失焦后再规范化显示，否则输入 "401, " 时会被立即改写
    const [codeDrafts, setCodeDrafts] = useState<Record<number, string>>({});
    const enabled = value?.enable ?? false;
    const endpoints = value?.loginEndpoints ?? [];

    const update = (patch: Partial<RiskProtection>) => {
        onChange({ ...(value ?? createRiskProtection()), ...patch });
    };

    const updateEndpoint = (index: number, patch: Partial<LoginEndpointRule>) => {
        update({
            loginEndpoints: endpoints.map((item, i) => (i === index ? { ...item, ...patch } : item)),
        });
    };

    const removeEndpoint = (index: number) => {
        setCodeDrafts({});
        update({ loginEndpoints: endpoints.filter((_, i) => i !== index) });
    };

    return (
        <div className="space-y-4">
            <div className="flex items-center justify-between gap-4 rounded-lg border bg-muted/20 px-3 py-2">
                <div className="space-y-0.5">
                    <Label htmlFor={`${idPrefix}-risk-enable`} className="text-sm font-medium">
                        启用登录保护
                    </Label>
                    <div className="text-xs text-muted-foreground">
                        按 IP、网段和账号统计登录失败，异常来源需先通过人机验证，严重时临时封禁。
                    </div>
                </div>
                <Switch
                    id={`${idPrefix}-risk-enable`}
                    checked={enabled}
                    onCheckedChange={(checked) =>
                        value ? update({ enable: checked }) : onChange(checked ? createRiskProtection() : null)
                    }
                />
            </div>

            {value ? (
                <>
                    <div className="grid gap-2">
                        <Label htmlFor={`${idPrefix}-risk-scope`} className="text-sm font-medium">
                            封禁范围
                        </Label>
                        <Select
                            value={value.blockScope.toString()}
                            onValueChange={(next) => update({ blockScope: Number(next) as RiskBlockScope })}
                        >
                            <SelectTrigger id={`${idPrefix}-risk-scope`}>
                                <SelectValue />
                            </SelectTrigger>
                            <SelectContent>
                                <SelectItem value={RiskBlockScope.LoginOnly.toString()}>
                                    只拦截登录接口
                                </SelectItem>
                                <SelectItem value={RiskBlockScope.WholeRoute.toString()}>
                                    拦截该路由的全部请求
                                </SelectItem>
                            </SelectContent>
                        </Select>
                    </div>

                    {endpoints.map((rule, index) => (
                        <Card key={index}>
                            <CardHeader className="flex flex-row items-center justify-between space-y-0 py-3">
                                <CardTitle className="text-sm">登录接口 {index + 1}</CardTitle>
                                <Button
                                    type="button"
                                    variant="ghost"
                                    size="icon"
                                    aria-label="移除登录接口"
                                    onClick={() => removeEndpoint(index)}
                                >
                                    <X className="h-4 w-4" />
                                </Button>
                            </CardHeader>
                            <CardContent className="space-y-3">
                                <div className="grid gap-3 sm:grid-cols-[120px_1fr]">
                                    <div className="space-y-2">
                                        <Label htmlFor={`${idPrefix}-risk-method-${index}`}>方法</Label>
                                        <Select
                                            value={rule.method}
                                            onValueChange={(method) => updateEndpoint(index, { method })}
                                        >
                                            <SelectTrigger id={`${idPrefix}-risk-method-${index}`}>
                                                <SelectValue />
                                            </SelectTrigger>
                                            <SelectContent>
                                                {LOGIN_METHODS.map((method) => (
                                                    <SelectItem key={method} value={method}>
                                                        {method}
                                                    </SelectItem>
                                                ))}
                                            </SelectContent>
                                        </Select>
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor={`${idPrefix}-risk-path-${index}`}>路径</Label>
                                        <Input
                                            id={`${idPrefix}-risk-path-${index}`}
                                            value={rule.path}
                                            onChange={(e) => updateEndpoint(index, { path: e.target.value })}
                                            placeholder="例如：/api/auth/login"
                                        />
                                    </div>
                                </div>

                                <div className="grid gap-3 sm:grid-cols-3">
                                    <div className="space-y-2">
                                        <Label htmlFor={`${idPrefix}-risk-username-${index}`}>用户名字段</Label>
                                        <Input
                                            id={`${idPrefix}-risk-username-${index}`}
                                            value={rule.usernameField ?? ""}
                                            onChange={(e) =>
                                                updateEndpoint(index, { usernameField: e.target.value })
                                            }
                                            placeholder="例如：username"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor={`${idPrefix}-risk-codes-${index}`}>失败状态码</Label>
                                        <Input
                                            id={`${idPrefix}-risk-codes-${index}`}
                                            value={codeDrafts[index] ?? rule.failureStatusCodes.join(", ")}
                                            onChange={(e) => {
                                                const text = e.target.value;
                                                setCodeDrafts((prev) => ({ ...prev, [index]: text }));
                                                updateEndpoint(index, { failureStatusCodes: parseStatusCodes(text) });
                                            }}
                                            onBlur={() =>
                                                setCodeDrafts((prev) => {
                                                    const next = { ...prev };
                                                    delete next[index];
                                                    return next;
                                                })
                                            }
                                            placeholder="例如：401, 403"
                                        />
                                    </div>
                                    <div className="space-y-2">
                                        <Label htmlFor={`${idPrefix}-risk-header-${index}`}>结果响应头</Label>
                                        <Input
                                            id={`${idPrefix}-risk-header-${index}`}
                                            value={rule.resultHeader ?? ""}
                                            onChange={(e) =>
                                                updateEndpoint(index, { resultHeader: e.target.value })
                                            }
                                            placeholder="例如：X-Auth-Result"
                                        />
                                    </div>
                                </div>
                            </CardContent>
                        </Card>
                    ))}

                    <Button
                        type="button"
                        variant="outline"
                        onClick={() => update({ loginEndpoints: [...endpoints, createLoginEndpoint()] })}
                    >
                        <Plus className="mr-2 h-4 w-4" />
                        添加登录接口
                    </Button>

                    <div className="space-y-1 rounded-md border border-primary/30 bg-primary/5 p-3 text-xs leading-relaxed text-muted-foreground">
                        <p>
                            路径填写访问者请求的完整路径，匹配时忽略大小写和末尾斜杠。填写用户名字段后，网关会识别撞库和针对单个账号的分布式爆破（只保存账号的哈希）。
                        </p>
                        <p>
                            登录失败也返回 200 的系统，请让上游在响应头中返回 <code>X-Auth-Result: fail</code> 或{" "}
                            <code>success</code> 并填写到「结果响应头」，网关读取后会把它从响应中移除。
                        </p>
                        <p>
                            人机验证默认使用内置的计算验证（PoW），无需外部服务；风险分阈值、验证方式等在「系统设置 → 登录风控」中调整。
                        </p>
                    </div>
                </>
            ) : null}
        </div>
    );
}
