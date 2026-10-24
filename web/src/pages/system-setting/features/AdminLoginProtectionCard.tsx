import { useEffect, useState } from "react";
import { ShieldCheck } from "lucide-react";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { message } from "@/utils/toast";
import {
  ADMIN_CLIENT_IP_SOURCE_KEY,
  ADMIN_CLIENT_IP_SOURCE_OPTIONS,
  getSetting,
  setSetting,
} from "@/services/SettingService";

const DEFAULT_SOURCE = "Default";

export default function AdminLoginProtectionCard() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [source, setSource] = useState(DEFAULT_SOURCE);
  const [savedSource, setSavedSource] = useState(DEFAULT_SOURCE);

  useEffect(() => {
    let cancelled = false;

    getSetting(ADMIN_CLIENT_IP_SOURCE_KEY)
      .then((res) => {
        if (cancelled) return;
        const value = typeof res?.data === "string" ? res.data : "";
        const current = ADMIN_CLIENT_IP_SOURCE_OPTIONS.some((o) => o.value === value)
          ? value
          : DEFAULT_SOURCE;
        setSource(current);
        setSavedSource(current);
      })
      .catch(() => {
        // 读取失败时按默认值展示
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const handleSave = async () => {
    setSaving(true);
    try {
      const res = await setSetting(ADMIN_CLIENT_IP_SOURCE_KEY, source);
      if (res && res.success === false) {
        message.error(res.message || "保存失败");
        return;
      }
      setSavedSource(source);
      message.success("登录保护配置已保存");
    } catch {
      message.error("保存失败，请稍后重试");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          <ShieldCheck className="size-4" />
          管理后台登录保护
        </CardTitle>
        <CardDescription>
          同一 IP 在 10 分钟内登录失败 5 次后，每次失败的响应会逐步变慢；失败 20 次锁定该 IP 15 分钟。
          同一网段（IPv4 /24、IPv6 /64）累计失败 60 次锁定整个网段。锁定期内即使密码正确也无法登录。
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {loading ? (
          <Skeleton className="h-9 w-48" />
        ) : (
          <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
            <Label htmlFor="admin-client-ip-source" className="shrink-0">
              客户端 IP 来源
            </Label>
            <Select value={source} onValueChange={setSource}>
              <SelectTrigger id="admin-client-ip-source" className="w-48">
                <SelectValue placeholder="选择 IP 来源" />
              </SelectTrigger>
              <SelectContent>
                {ADMIN_CLIENT_IP_SOURCE_OPTIONS.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </div>
        )}

        <p className="text-sm text-muted-foreground">
          控制台直接对外访问时保持默认。只有前面确实有 Nginx、CDN 等反向代理并由它写入该请求头时才修改：
          否则所有访问者会被识别成代理的同一个 IP，一人触发锁定就会锁住所有人；反过来，在没有代理时使用请求头，
          攻击者可以伪造 IP 绕过限制，或冒用你的 IP 把你锁在外面。
        </p>

        <div className="flex items-center gap-3">
          <Button onClick={handleSave} disabled={loading || saving || source === savedSource}>
            {saving ? "保存中..." : "保存"}
          </Button>
          {!loading && source !== savedSource && (
            <span className="text-sm text-muted-foreground">有未保存的修改</span>
          )}
        </div>
      </CardContent>
    </Card>
  );
}
