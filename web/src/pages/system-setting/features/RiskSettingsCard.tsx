import { useEffect, useState } from "react";
import { ShieldAlert } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";
import { message } from "@/utils/toast";
import type { RiskChallengeProvider, RiskSettings } from "@/types";
import { getRiskSettings, updateRiskSettings } from "@/services/RiskService";

type NumberField =
  | "challengeThreshold"
  | "strongChallengeThreshold"
  | "blockThreshold"
  | "powDifficulty"
  | "clearanceQuota"
  | "clearanceMinutes";

const NUMBER_FIELDS: { key: NumberField; label: string; hint: string }[] = [
  { key: "challengeThreshold", label: "要求验证分数", hint: "风险分达到后要求人机验证" },
  { key: "strongChallengeThreshold", label: "强验证分数", hint: "达到后使用强验证方式" },
  { key: "blockThreshold", label: "封禁分数", hint: "达到后临时封禁来源 IP" },
  { key: "powDifficulty", label: "PoW 难度（位）", hint: "每 +1 计算量翻倍，强验证 +3" },
  { key: "clearanceQuota", label: "每次验证的登录次数", hint: "用完后需要重新验证" },
  { key: "clearanceMinutes", label: "验证有效期（分钟）", hint: "超时后需要重新验证" },
];

const PROVIDER_OPTIONS: { value: RiskChallengeProvider; label: string }[] = [
  { value: "pow", label: "内置计算验证（PoW）" },
  { value: "turnstile", label: "Cloudflare Turnstile" },
];

export default function RiskSettingsCard() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [value, setValue] = useState<RiskSettings | null>(null);
  const [saved, setSaved] = useState<RiskSettings | null>(null);

  useEffect(() => {
    let cancelled = false;

    getRiskSettings()
      .then((res) => {
        if (cancelled || !res?.data) return;
        setValue(res.data);
        setSaved(res.data);
      })
      .catch(() => {
        if (!cancelled) message.error("读取登录风控设置失败");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const dirty = JSON.stringify(value) !== JSON.stringify(saved);

  const handleSave = async () => {
    if (!value) return;
    setSaving(true);
    try {
      const res = await updateRiskSettings(value);
      if (res && res.success === false) {
        message.error(res.message || "保存失败");
        return;
      }
      setSaved(value);
      message.success("登录风控设置已保存");
    } catch {
      message.error("保存失败，请稍后重试");
    } finally {
      setSaving(false);
    }
  };

  const update = (patch: Partial<RiskSettings>) => setValue((prev) => (prev ? { ...prev, ...patch } : prev));

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ShieldAlert className="size-4" />
          登录风控
        </CardTitle>
        <CardDescription>
          作用于开启「登录保护」的路由。风险分由登录失败次数、尝试的账号数、网段失败数等信号累加；
          内置计算验证无需外部服务，Turnstile 需在 appsettings 的 BotProtection 节配置密钥且仅用于 HTTPS 站点，
          不可用时自动改用加难的计算验证。
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-5">
        {loading || !value ? (
          <Skeleton className="h-40 w-full" />
        ) : (
          <>
            <div className="grid gap-4 sm:grid-cols-3">
              {NUMBER_FIELDS.map((field) => (
                <div key={field.key} className="space-y-1.5">
                  <Label htmlFor={`risk-${field.key}`}>{field.label}</Label>
                  <Input
                    id={`risk-${field.key}`}
                    type="number"
                    value={value[field.key]}
                    onChange={(e) => update({ [field.key]: Number(e.target.value) })}
                  />
                  <p className="text-xs text-muted-foreground">{field.hint}</p>
                </div>
              ))}
            </div>

            <div className="grid gap-4 sm:grid-cols-2">
              {(["mediumProvider", "strongProvider"] as const).map((key) => (
                <div key={key} className="space-y-1.5">
                  <Label htmlFor={`risk-${key}`}>{key === "mediumProvider" ? "中风险验证方式" : "高风险验证方式"}</Label>
                  <Select
                    value={value[key]}
                    onValueChange={(next) => update({ [key]: next as RiskChallengeProvider })}
                  >
                    <SelectTrigger id={`risk-${key}`}>
                      <SelectValue />
                    </SelectTrigger>
                    <SelectContent>
                      {PROVIDER_OPTIONS.map((option) => (
                        <SelectItem key={option.value} value={option.value}>
                          {option.label}
                        </SelectItem>
                      ))}
                    </SelectContent>
                  </Select>
                </div>
              ))}
            </div>

            <div className="space-y-3">
              <div className="flex items-center justify-between gap-4 rounded-lg border bg-muted/20 px-3 py-2">
                <div className="space-y-0.5">
                  <Label htmlFor="risk-failure-delay">登录失败渐进延迟</Label>
                  <div className="text-xs text-muted-foreground">10 分钟内失败 5 次后，每次失败的响应逐步变慢（最长 3 秒）</div>
                </div>
                <Switch
                  id="risk-failure-delay"
                  checked={value.failureDelay}
                  onCheckedChange={(failureDelay) => update({ failureDelay })}
                />
              </div>
              <div className="flex items-center justify-between gap-4 rounded-lg border bg-muted/20 px-3 py-2">
                <div className="space-y-0.5">
                  <Label htmlFor="risk-admin-challenge">管理后台登录验证</Label>
                  <div className="text-xs text-muted-foreground">管理后台登录失败 5 次后，每次登录都要先在浏览器中完成计算验证</div>
                </div>
                <Switch
                  id="risk-admin-challenge"
                  checked={value.adminLoginChallenge}
                  onCheckedChange={(adminLoginChallenge) => update({ adminLoginChallenge })}
                />
              </div>
            </div>
          </>
        )}

        <div className="flex items-center gap-3">
          <Button onClick={handleSave} disabled={loading || saving || !dirty}>
            {saving ? "保存中..." : "保存"}
          </Button>
          {!loading && dirty && <span className="text-sm text-muted-foreground">有未保存的修改</span>}
        </div>
      </CardContent>
    </Card>
  );
}
