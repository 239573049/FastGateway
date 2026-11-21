import { useEffect, useState } from "react";
import { Globe } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { getOriginSettings, resetOriginSettings, setOriginSettings, type OriginSettings } from "@/services/SettingService";
import { message } from "@/utils/toast";

export default function OriginSettingsCard() {
  const [settings, setSettings] = useState<OriginSettings | null>(null);
  const [text, setText] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);

  const apply = (value: OriginSettings) => {
    setSettings(value);
    setText(value.origins.join("\n"));
  };

  useEffect(() => {
    let cancelled = false;
    getOriginSettings()
      .then((res) => {
        if (cancelled) return;
        if (!res?.success || !res.data) throw new Error(res?.message || "读取跨域设置失败");
        apply(res.data);
      })
      .catch(() => { if (!cancelled) message.error("读取跨域设置失败，请刷新页面重试"); })
      .finally(() => { if (!cancelled) setLoading(false); });
    return () => { cancelled = true; };
  }, []);

  const save = async (reset: boolean) => {
    setSaving(true);
    try {
      const origins = text.split(/\r?\n/).map((line) => line.trim()).filter(Boolean);
      const res = reset ? await resetOriginSettings() : await setOriginSettings(origins);
      if (!res?.success || !res.data) {
        message.error(res?.message || "保存跨域设置失败");
        return;
      }
      apply(res.data);
      message.success(reset ? "已恢复配置文件中的跨域来源" : "跨域设置已保存并生效");
    } catch {
      message.error("保存跨域设置失败，请稍后重试");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2"><Globe className="size-4" />跨域来源</CardTitle>
        <CardDescription>
          控制网关 WebSocket 的 Origin 校验和网关自身的 CORS，代理路由的 HTTP CORS 仍由上游处理。
          每行一个来源；保存后立即生效，仅影响新请求。
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading ? <Skeleton className="h-32 w-full" /> : settings && (
          <>
            <div className="space-y-2">
              <Label htmlFor="cors-origins">允许的来源</Label>
              <textarea
                id="cors-origins"
                className="flex min-h-32 w-full rounded-md border border-input bg-transparent px-3 py-2 text-sm shadow-sm focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
                value={text}
                onChange={(event) => setText(event.target.value)}
                placeholder={"https://office.example.com\nhttps://*.example.com\n*.example.com"}
                spellCheck={false}
              />
              <p className="text-xs text-muted-foreground">
                例如 *.example.com（请替换为自己的域名）允许任意层级子域、HTTP/HTTPS 和任意端口，不包含根域。
                带协议时须匹配协议及端口；空列表拒绝所有带 Origin 的跨域请求。
                无 Origin 的 WebSocket 请求不受此限制，来源校验不能替代身份认证。
              </p>
              <p className="text-xs text-muted-foreground">
                当前来源：{settings.isOverride ? "界面配置（覆盖环境变量和配置文件）" : "环境变量或配置文件（未配置时使用原有默认值）"}。
              </p>
            </div>
            <div className="flex flex-wrap gap-2">
              <Button onClick={() => void save(false)} disabled={saving}>
                {saving ? "保存中..." : "保存"}
              </Button>
              <Button variant="outline" onClick={() => void save(true)} disabled={saving || !settings.isOverride}>
                恢复配置文件
              </Button>
            </div>
          </>
        )}
      </CardContent>
    </Card>
  );
}
