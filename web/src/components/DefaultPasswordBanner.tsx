import { useEffect, useState } from "react";
import { AlertTriangle } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { getAdminSecurityStatus } from "@/services/AuthorizationService";

export function DefaultPasswordBanner() {
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    let cancelled = false;

    getAdminSecurityStatus()
      .then((res) => {
        if (!cancelled) setVisible(res?.success === true && res.data?.usingDefaultPassword === true);
      })
      .catch(() => {
        // 获取失败时不展示，避免误报
      });

    return () => {
      cancelled = true;
    };
  }, []);

  if (!visible) return null;

  return (
    <Alert variant="destructive" className="mb-4">
      <AlertTriangle />
      <AlertTitle>正在使用默认管理员密码</AlertTitle>
      <AlertDescription>
        <p>
          默认密码是公开的，任何人都能用它登录控制台。请设置环境变量 <code>PASSWORD</code>（或配置项{" "}
          <code>Password</code>）后重启服务。
        </p>
      </AlertDescription>
    </Alert>
  );
}
