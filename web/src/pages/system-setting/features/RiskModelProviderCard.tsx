import { useCallback, useEffect, useState } from "react";
import { Bot, PlugZap, RefreshCw } from "lucide-react";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { message } from "@/utils/toast";
import {
  RiskModelMode,
  type RiskModelInfo,
  type RiskModelProvider,
  type RiskModelProviderInput,
  type RiskModelStatus,
  type RiskModelTestResult,
} from "@/types";
import {
  getRiskModelProvider,
  getRiskModelStatus,
  listRiskModels,
  testRiskModelProvider,
  updateRiskModelProvider,
} from "@/services/RiskService";

const DEFAULT_MODELS = ["jev-latest", "jev-1.14.0", "jev-1.13.0"];

type NumberField = "timeoutMs" | "maxRequestsPerSecond" | "maxConcurrency" | "dailyTokenBudget";

const NUMBER_FIELDS: { key: NumberField; label: string; hint: string }[] = [
  { key: "timeoutMs", label: "超时（毫秒）", hint: "500-10000，超时按失败处理" },
  { key: "maxRequestsPerSecond", label: "每秒请求上限", hint: "超出时跳过评估，只用规则" },
  { key: "maxConcurrency", label: "并发上限", hint: "同时进行的评估数" },
  { key: "dailyTokenBudget", label: "每日 token 预算", hint: "按输入 token 计，0 表示不限" },
];

const toInput = (view: RiskModelProvider): RiskModelProviderInput => ({
  enabled: view.enabled,
  provider: view.provider,
  endpoint: view.endpoint,
  model: view.model,
  mode: view.mode,
  timeoutMs: view.timeoutMs,
  maxRequestsPerSecond: view.maxRequestsPerSecond,
  maxConcurrency: view.maxConcurrency,
  dailyTokenBudget: view.dailyTokenBudget,
  apiKey: "",
  clearApiKey: false,
});

export default function RiskModelProviderCard() {
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState<"save" | "test" | "models" | null>(null);
  const [saved, setSaved] = useState<RiskModelProvider | null>(null);
  const [value, setValue] = useState<RiskModelProviderInput | null>(null);
  const [status, setStatus] = useState<RiskModelStatus | null>(null);
  const [testResult, setTestResult] = useState<RiskModelTestResult | null>(null);
  const [models, setModels] = useState<RiskModelInfo[]>([]);

  const loadStatus = useCallback(() => {
    getRiskModelStatus()
      .then((res) => setStatus(res?.data ?? null))
      .catch(() => {
        // 状态只是辅助信息，读取失败不提示
      });
  }, []);

  useEffect(() => {
    let cancelled = false;

    getRiskModelProvider()
      .then((res) => {
        if (cancelled || !res?.data) return;
        setSaved(res.data);
        setValue(toInput(res.data));
      })
      .catch(() => {
        if (!cancelled) message.error("读取 AI 风控模型配置失败");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    loadStatus();

    return () => {
      cancelled = true;
    };
  }, [loadStatus]);

  const update = (patch: Partial<RiskModelProviderInput>) =>
    setValue((prev) => (prev ? { ...prev, ...patch } : prev));

  const dirty = Boolean(
    value && saved && (JSON.stringify(value) !== JSON.stringify(toInput(saved)))
  );

  const handleSave = async () => {
    if (!value) return;
    setBusy("save");
    try {
      const res = await updateRiskModelProvider(value);
      if (!res?.success || !res.data) {
        message.error(res?.message || "保存失败");
        return;
      }
      setSaved(res.data);
      setValue(toInput(res.data));
      message.success("AI 风控模型配置已保存");
      loadStatus();
    } catch {
      message.error("保存失败，请稍后重试");
    } finally {
      setBusy(null);
    }
  };

  const handleTest = async () => {
    if (!value) return;
    setBusy("test");
    setTestResult(null);
    try {
      const res = await testRiskModelProvider(value);
      if (!res?.success || !res.data) {
        message.error(res?.message || "测试失败");
        return;
      }
      setTestResult(res.data);
    } catch {
      message.error("测试失败，请稍后重试");
    } finally {
      setBusy(null);
    }
  };

  const handleLoadModels = async () => {
    if (!value) return;
    setBusy("models");
    try {
      const res = await listRiskModels(value);
      if (!res?.success || !res.data) {
        message.error(res?.message || "获取模型列表失败");
        return;
      }
      setModels(res.data);
      message.success(`已获取 ${res.data.length} 个模型`);
    } catch {
      message.error("获取模型列表失败，请稍后重试");
    } finally {
      setBusy(null);
    }
  };

  const modelOptions = Array.from(new Set([...models.map((x) => x.name), ...DEFAULT_MODELS]));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <Bot className="size-4" />
          AI 风控模型（TypeSafe Jev）
        </CardTitle>
        <CardDescription>
          对规则拿不准的登录来源，在后台调用模型判断是否为脚本、撞库或猜密码，不影响登录请求的速度；模型不可用时完全回落到规则。
          启用后，登录次数、失败率、请求间隔等统计特征会发送到所配置的服务地址，不发送 IP、用户名和请求原文。
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {loading || !value || !saved ? (
          <Skeleton className="h-48 w-full" />
        ) : (
          <>
            <div className="flex items-center justify-between gap-4 rounded-lg border bg-muted/20 px-3 py-2">
              <div className="space-y-0.5">
                <Label htmlFor="risk-model-enabled">启用 AI 风控模型</Label>
                <div className="text-xs text-muted-foreground">需要先填写 API Key</div>
              </div>
              <Switch
                id="risk-model-enabled"
                checked={value.enabled}
                onCheckedChange={(enabled) => update({ enabled })}
              />
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="risk-model-mode">运行模式</Label>
                <Select
                  value={value.mode.toString()}
                  onValueChange={(next) => update({ mode: Number(next) as RiskModelMode })}
                >
                  <SelectTrigger id="risk-model-mode">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value={RiskModelMode.Shadow.toString()}>影子模式（只记录，不影响处置）</SelectItem>
                    <SelectItem value={RiskModelMode.Enforce.toString()}>参与决策（模型分计入风险分）</SelectItem>
                  </SelectContent>
                </Select>
                <p className="text-xs text-muted-foreground">
                  建议先用影子模式观察一段时间，在「登录风控」页面对比模型判断与实际结果后再参与决策。
                </p>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="risk-model-endpoint">服务地址</Label>
                <Input
                  id="risk-model-endpoint"
                  value={value.endpoint}
                  onChange={(e) => update({ endpoint: e.target.value })}
                  placeholder="https://api.typesafe.ai"
                />
                <p className="text-xs text-muted-foreground">不含 /v1；更换主机后需要重新填写 API Key</p>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              <div className="space-y-1.5">
                <Label htmlFor="risk-model-key">API Key</Label>
                <div className="flex gap-2">
                  <Input
                    id="risk-model-key"
                    type="password"
                    autoComplete="new-password"
                    value={value.apiKey ?? ""}
                    disabled={value.clearApiKey}
                    onChange={(e) => update({ apiKey: e.target.value })}
                    placeholder={
                      value.clearApiKey
                        ? "保存后清除"
                        : saved.apiKeyConfigured
                          ? `已配置 ${saved.apiKeyHint ?? ""}，留空则不修改`
                          : "填写 TypeSafe 控制台中的 API Key"
                    }
                  />
                  {saved.apiKeyConfigured ? (
                    <Button
                      type="button"
                      variant="outline"
                      onClick={() => update({ clearApiKey: !value.clearApiKey, apiKey: "" })}
                    >
                      {value.clearApiKey ? "撤销" : "清除"}
                    </Button>
                  ) : null}
                </div>
              </div>
              <div className="space-y-1.5">
                <Label htmlFor="risk-model-name">模型</Label>
                <div className="flex gap-2">
                  <Input
                    id="risk-model-name"
                    list="risk-model-options"
                    value={value.model}
                    onChange={(e) => update({ model: e.target.value })}
                    placeholder="jev-latest"
                  />
                  <datalist id="risk-model-options">
                    {modelOptions.map((name) => (
                      <option key={name} value={name} />
                    ))}
                  </datalist>
                  <Button
                    type="button"
                    variant="outline"
                    className="shrink-0 gap-2"
                    disabled={busy !== null}
                    onClick={handleLoadModels}
                  >
                    <RefreshCw className="h-4 w-4" />
                    {busy === "models" ? "获取中..." : "获取列表"}
                  </Button>
                </div>
              </div>
            </div>

            <div className="grid gap-4 sm:grid-cols-4">
              {NUMBER_FIELDS.map((field) => (
                <div key={field.key} className="space-y-1.5">
                  <Label htmlFor={`risk-model-${field.key}`}>{field.label}</Label>
                  <Input
                    id={`risk-model-${field.key}`}
                    type="number"
                    value={value[field.key]}
                    onChange={(e) => update({ [field.key]: Number(e.target.value) })}
                  />
                  <p className="text-xs text-muted-foreground">{field.hint}</p>
                </div>
              ))}
            </div>

            {testResult ? (
              <div className="rounded-md border bg-muted/20 p-3 text-sm">
                {testResult.success ? (
                  <span>
                    连接成功：延迟 <span className="font-mono">{testResult.latencyMs} ms</span>，模型{" "}
                    <span className="font-mono">{testResult.model}</span>，示例脚本流量判定为自动化的概率{" "}
                    <span className="font-mono">{((testResult.automated ?? 0) * 100).toFixed(0)}%</span>
                  </span>
                ) : (
                  <span className="text-destructive">
                    连接失败：{testResult.message}（{testResult.latencyMs} ms）
                  </span>
                )}
              </div>
            ) : null}

            {status ? (
              <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
                <Badge variant={status.usable ? "default" : "secondary"}>{status.usable ? "运行中" : "未启用"}</Badge>
                <span>
                  今日 token {status.tokensUsedToday.toLocaleString()}
                  {status.dailyTokenBudget > 0 ? ` / ${status.dailyTokenBudget.toLocaleString()}` : ""}
                </span>
                <span>近 5 分钟调用 {status.calls5m} 次，失败 {status.failures5m} 次</span>
                {status.skipped + status.dropped > 0 ? <span>跳过 {status.skipped + status.dropped} 次</span> : null}
                {status.circuitOpenUntil ? <span className="text-destructive">熔断至 {status.circuitOpenUntil}</span> : null}
                {status.lastError ? (
                  <span>
                    最近错误：{status.lastError}（{status.lastErrorAt}）
                  </span>
                ) : null}
              </div>
            ) : null}
          </>
        )}

        <div className="flex flex-wrap items-center gap-3">
          <Button onClick={handleSave} disabled={loading || busy !== null || !dirty}>
            {busy === "save" ? "保存中..." : "保存"}
          </Button>
          <Button variant="outline" className="gap-2" onClick={handleTest} disabled={loading || busy !== null}>
            <PlugZap className="h-4 w-4" />
            {busy === "test" ? "测试中..." : "测试连接"}
          </Button>
          {!loading && dirty && <span className="text-sm text-muted-foreground">有未保存的修改</span>}
        </div>
      </CardContent>
    </Card>
  );
}
