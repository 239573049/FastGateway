import { useEffect, useState } from "react";
import { Trash2 } from "lucide-react";

import { Reveal } from "@/components/motion";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Skeleton } from "@/components/ui/skeleton";
import { message } from "@/utils/toast";
import {
  LOG_RETENTION_KEY,
  LOG_RETENTION_OPTIONS,
  getSetting,
  setSetting,
} from "@/services/SettingService";
import AdminLoginProtectionCard from "./features/AdminLoginProtectionCard";
import RiskModelProviderCard from "./features/RiskModelProviderCard";
import RiskSettingsCard from "./features/RiskSettingsCard";

const DEFAULT_RETENTION = "7";

export default function SystemSettingPage() {
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [retention, setRetention] = useState(DEFAULT_RETENTION);
  const [savedRetention, setSavedRetention] = useState(DEFAULT_RETENTION);

  useEffect(() => {
    let cancelled = false;

    getSetting(LOG_RETENTION_KEY)
      .then((res) => {
        if (cancelled) return;
        const value = typeof res?.data === "string" ? res.data : "";
        const current = LOG_RETENTION_OPTIONS.some((o) => o.value === value)
          ? value
          : DEFAULT_RETENTION;
        setRetention(current);
        setSavedRetention(current);
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
      const res = await setSetting(LOG_RETENTION_KEY, retention);
      if (res && res.success === false) {
        message.error(res.message || "保存失败");
        return;
      }
      setSavedRetention(retention);
      message.success("日志清理配置已保存");
    } catch {
      message.error("保存失败，请稍后重试");
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto max-w-3xl space-y-4">
      <Reveal>
      <Card>
        <CardHeader>
          <CardTitle className="flex items-center gap-2">
            <Trash2 className="size-4" />
            日志清理
          </CardTitle>
          <CardDescription>
            访问明细日志（请求记录）的保留天数，超过保留期的日志由后台每小时自动清理一次；
            聚合统计数据不受影响。默认保留 7 天。
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          {loading ? (
            <Skeleton className="h-9 w-48" />
          ) : (
            <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
              <Label htmlFor="log-retention" className="shrink-0">
                日志保留天数
              </Label>
              <Select value={retention} onValueChange={setRetention}>
                <SelectTrigger id="log-retention" className="w-48">
                  <SelectValue placeholder="选择保留天数" />
                </SelectTrigger>
                <SelectContent>
                  {LOG_RETENTION_OPTIONS.map((option) => (
                    <SelectItem key={option.value} value={option.value}>
                      {option.label}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
          )}

          <div className="flex items-center gap-3">
            <Button
              onClick={handleSave}
              disabled={loading || saving || retention === savedRetention}
            >
              {saving ? "保存中..." : "保存"}
            </Button>
            {!loading && retention !== savedRetention && (
              <span className="text-sm text-muted-foreground">有未保存的修改</span>
            )}
          </div>
        </CardContent>
      </Card>
      </Reveal>
      <Reveal index={1}>
        <AdminLoginProtectionCard />
      </Reveal>
      <Reveal index={2}>
        <RiskSettingsCard />
      </Reveal>
      <Reveal index={3}>
        <RiskModelProviderCard />
      </Reveal>
    </div>
  );
}
