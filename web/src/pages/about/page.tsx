import { useEffect, useState } from 'react';
import { getVersion, type VersionInfo } from '@/services/SystemService';

export default function AboutPage() {
  const [versionInfo, setVersionInfo] = useState<VersionInfo | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    const fetchVersion = async () => {
      try {
        const { data } = await getVersion() as any;
        setVersionInfo(data);
      } catch (error) {
        console.error('获取版本信息失败:', error);
      } finally {
        setLoading(false);
      }
    };

    fetchVersion();
  }, []);

  return (
    <div className="p-6 flex justify-center">
      <div className="w-full max-w-5xl space-y-6">
        <div className="bg-card border rounded-xl p-6 shadow-sm">
          <h2 className="text-2xl font-semibold mb-3 text-foreground">关于 FastGateway</h2>
          <p className="text-muted-foreground mb-2">
            FastGateway 是一个基于 YARP + .NET 9 + EF Core 实现的高性能 API 网关系统，由 Token 研发并开源。
          </p>
          <p className="text-muted-foreground">
            目标是提供高可用、易扩展、易配置的网关管理能力，助力微服务与反向代理的快速部署与治理。
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
          <div className="bg-card border rounded-xl p-6 shadow-sm">
            <h3 className="text-xl font-medium mb-3 text-foreground">贡献者</h3>
            <div className="rounded-lg border bg-card p-2">
              <a href="https://github.com/239573049/FastGateway/graphs/contributors" target="_blank" rel="noreferrer">
                <img
                  className="w-full h-auto rounded-md select-none object-contain"
                  src="https://contrib.rocks/image?repo=239573049/FastGateway"
                  alt="贡献者"
                />
              </a>
            </div>
          </div>

          <div className="bg-card border rounded-xl p-6 shadow-sm">
            <h3 className="text-xl font-medium mb-3 text-foreground">项目信息</h3>
            <ul className="space-y-2 text-sm text-muted-foreground">
              <li>框架：React + TypeScript + Vite</li>
              <li>样式：Tailwind CSS + CSS Variables</li>
              <li>组件：Radix UI / Shadcn 风格组件</li>
            </ul>
            <div className="mt-4 text-sm">
              <div className="text-muted-foreground">版本信息</div>
              {loading ? (
                <div className="font-medium mt-1">加载中...</div>
              ) : versionInfo ? (
                <>
                  <div className="font-medium mt-1">当前版本：{versionInfo.version}</div>
                  <div className="text-muted-foreground text-xs mt-1">框架：{versionInfo.framework}</div>
                  <div className="text-muted-foreground text-xs">系统：{versionInfo.os}</div>
                </>
              ) : (
                <div className="font-medium mt-1 text-destructive">版本信息获取失败</div>
              )}
            </div>
          </div>
        </div>

        <div className="bg-card border rounded-xl p-6 shadow-sm">
          <h3 className="text-xl font-medium mb-3 text-foreground">开源地址</h3>
          <a
            className="text-primary hover:underline break-all"
            href="https://github.com/239573049/FastGateway"
            target="_blank"
            rel="noreferrer"
          >
            https://github.com/239573049/FastGateway
          </a>
        </div>
      </div>
    </div>
  );
}