import { Fragment, useCallback, useEffect, useMemo, useState, type ReactElement, type ReactNode } from "react";
import { BookOpen, Cpu, Github, Info, RefreshCw, Server, Users } from "lucide-react";

import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";

import { AvatarGroup, AvatarGroupTooltip } from "@animate-ui/components-animate-avatar-group";
import { getGatewayInfo, type GatewayInfo } from "@/services/SystemService";

type GitHubContributor = {
  login: string;
  avatar_url: string;
  html_url: string;
  contributions: number;
};

const GITHUB_OWNER = "239573049";
const GITHUB_REPO = "FastGateway";
const CONTRIBUTORS_PAGE_URL = `https://github.com/${GITHUB_OWNER}/${GITHUB_REPO}/graphs/contributors`;
const CONTRIBUTORS_API_URL = `https://api.github.com/repos/${GITHUB_OWNER}/${GITHUB_REPO}/contributors?per_page=100`;
const CONTRIBUTORS_CACHE_KEY = "fastgateway:github-contributors:v1";
const CONTRIBUTORS_CACHE_TTL_MS = 6 * 60 * 60 * 1000;
const CONTRIBUTORS_DISPLAY_LIMIT = 12;

function formatValue(value: unknown) {
  if (value === null || value === undefined) return "-";
  if (typeof value === "string") return value.trim().length ? value : "-";
  return String(value);
}

function formatUptime(seconds?: number | null) {
  if (seconds === null || seconds === undefined || !Number.isFinite(seconds)) return "-";

  const total = Math.max(0, Math.floor(seconds));
  if (total < 60) return `${total} 秒`;

  const days = Math.floor(total / 86400);
  const hours = Math.floor((total % 86400) / 3600);
  const minutes = Math.floor((total % 3600) / 60);

  const parts: string[] = [];
  if (days) parts.push(`${days} 天`);
  if (hours) parts.push(`${hours} 小时`);
  if (minutes || parts.length === 0) parts.push(`${minutes} 分钟`);
  return parts.join(" ");
}

function InfoTable({
  rows,
  loading,
}: {
  rows: Array<{ label: string; value: ReactNode }>;
  loading: boolean;
}) {
  return (
    <div className="rounded-lg border border-border/60">
      <Table>
        <TableHeader>
          <TableRow className="bg-muted/30 hover:bg-muted/30">
            <TableHead className="w-[180px]">项目</TableHead>
            <TableHead>值</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          {loading
            ? Array.from({ length: Math.max(6, rows.length || 6) }).map((_, i) => (
                <TableRow key={`skeleton-${i}`}>
                  <TableCell>
                    <Skeleton className="h-4 w-28" />
                  </TableCell>
                  <TableCell>
                    <Skeleton className="h-4 w-64" />
                  </TableCell>
                </TableRow>
              ))
            : rows.map((row) => (
                <TableRow key={row.label} className="hover:bg-muted/30">
                  <TableCell className="text-muted-foreground">{row.label}</TableCell>
                  <TableCell className="font-mono text-sm text-foreground">{row.value}</TableCell>
                </TableRow>
              ))}
        </TableBody>
      </Table>
    </div>
  );
}

export default function AboutPage() {
  const [gatewayInfo, setGatewayInfo] = useState<GatewayInfo | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null);

  const [contributors, setContributors] = useState<GitHubContributor[]>([]);
  const [contributorsLoading, setContributorsLoading] = useState(true);
  const [contributorsError, setContributorsError] = useState<string | null>(null);

  const loadGatewayInfo = useCallback(async () => {
    setLoading(true);
    setError(null);

    try {
      const res = await getGatewayInfo();

      if (!res?.success) {
        throw new Error(res?.message || "接口返回失败");
      }

      setGatewayInfo(res.data);
      setLastUpdated(new Date());
    } catch (e) {
      console.error("获取网关信息失败:", e);
      setGatewayInfo(null);
      setError(e instanceof Error ? e.message : "未知错误");
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadGatewayInfo();
  }, [loadGatewayInfo]);

  const loadContributors = useCallback(async ({ force = false }: { force?: boolean } = {}) => {
    setContributorsLoading(true);
    setContributorsError(null);

    try {
      if (!force) {
        try {
          const cachedRaw = localStorage.getItem(CONTRIBUTORS_CACHE_KEY);
          if (cachedRaw) {
            const cached = JSON.parse(cachedRaw) as {
              timestamp: number;
              data: GitHubContributor[];
            };

            if (
              cached?.timestamp &&
              Array.isArray(cached?.data) &&
              Date.now() - cached.timestamp < CONTRIBUTORS_CACHE_TTL_MS
            ) {
              setContributors(cached.data);
              return;
            }
          }
        } catch {
        }
      }

      const response = await window.fetch(CONTRIBUTORS_API_URL, {
        headers: { Accept: "application/vnd.github+json" },
      });

      if (!response.ok) {
        throw new Error(`GitHub API 请求失败（${response.status}）`);
      }

      const data = (await response.json()) as unknown;
      if (!Array.isArray(data)) {
        throw new Error("GitHub API 返回数据异常");
      }

      const normalized = (data as Array<Partial<GitHubContributor>>)
        .filter((item) => Boolean(item?.login && item?.avatar_url && item?.html_url))
        .map((item) => ({
          login: String(item!.login),
          avatar_url: String(item!.avatar_url),
          html_url: String(item!.html_url),
          contributions: Number(item?.contributions ?? 0),
        }));

      setContributors(normalized);

      try {
        localStorage.setItem(
          CONTRIBUTORS_CACHE_KEY,
          JSON.stringify({ timestamp: Date.now(), data: normalized }),
        );
      } catch {
      }
    } catch (e) {
      console.error("获取贡献者信息失败:", e);
      setContributors([]);
      setContributorsError(e instanceof Error ? e.message : "未知错误");
    } finally {
      setContributorsLoading(false);
    }
  }, []);

  useEffect(() => {
    void loadContributors();
  }, [loadContributors]);

  const buildRows = useMemo(() => {
    return [
      { label: "网关版本", value: formatValue(gatewayInfo?.version) },
      { label: "程序集名称", value: formatValue(gatewayInfo?.name) },
      { label: "产品名称", value: formatValue(gatewayInfo?.product) },
      { label: "InformationalVersion", value: formatValue(gatewayInfo?.informationalVersion) },
      { label: "FileVersion", value: formatValue(gatewayInfo?.fileVersion) },
      { label: "YARP 版本", value: formatValue(gatewayInfo?.yarpVersion) },
    ];
  }, [gatewayInfo]);

  const runtimeRows = useMemo(() => {
    return [
      { label: ".NET 运行时", value: formatValue(gatewayInfo?.framework) },
      { label: "操作系统", value: formatValue(gatewayInfo?.os) },
      { label: "系统架构", value: formatValue(gatewayInfo?.osArchitecture) },
      { label: "进程架构", value: formatValue(gatewayInfo?.processArchitecture) },
      { label: "环境", value: formatValue(gatewayInfo?.environmentName) },
      { label: "机器名", value: formatValue(gatewayInfo?.machineName) },
      { label: "进程 ID", value: formatValue(gatewayInfo?.processId) },
      { label: "服务时间", value: formatValue(gatewayInfo?.serverTime) },
      { label: "启动时间", value: formatValue(gatewayInfo?.processStartTime) },
      { label: "运行时长", value: formatUptime(gatewayInfo?.uptimeSeconds) },
    ];
  }, [gatewayInfo]);

  const visibleContributors = useMemo(() => {
    return contributors.slice(0, CONTRIBUTORS_DISPLAY_LIMIT);
  }, [contributors]);

  const remainingContributors = useMemo(() => {
    return Math.max(0, contributors.length - visibleContributors.length);
  }, [contributors.length, visibleContributors.length]);

  const contributorAvatarElements = useMemo(() => {
    const items: ReactElement[] = visibleContributors.map((contributor) => (
      <Fragment key={contributor.login}>
        <a
          href={contributor.html_url}
          target="_blank"
          rel="noreferrer"
          aria-label={`打开 ${contributor.login} 的 GitHub 主页`}
          className="block rounded-full"
        >
          <Avatar className="h-8 w-8 ring-2 ring-background">
            <AvatarImage src={contributor.avatar_url} alt={contributor.login} />
            <AvatarFallback className="text-[10px] font-semibold">
              {contributor.login.slice(0, 2).toUpperCase()}
            </AvatarFallback>
          </Avatar>
        </a>
        <AvatarGroupTooltip>{`${contributor.login} · 贡献 ${contributor.contributions}`}</AvatarGroupTooltip>
      </Fragment>
    ));

    if (remainingContributors > 0) {
      items.push(
        <Fragment key="more-contributors">
          <a
            href={CONTRIBUTORS_PAGE_URL}
            target="_blank"
            rel="noreferrer"
            aria-label="查看全部贡献者"
            className="block rounded-full"
          >
            <Avatar className="h-8 w-8 ring-2 ring-background">
              <AvatarFallback className="text-[10px] font-semibold">{`+${remainingContributors}`}</AvatarFallback>
            </Avatar>
          </a>
          <AvatarGroupTooltip>{`还有 ${remainingContributors} 位贡献者`}</AvatarGroupTooltip>
        </Fragment>,
      );
    }

    return items;
  }, [remainingContributors, visibleContributors]);

  return (
    <div className="mx-auto max-w-5xl space-y-6 p-6">
      <header className="flex flex-col gap-3 md:flex-row md:items-end md:justify-between">
        <div className="space-y-2">
          <p className="text-xs uppercase tracking-[0.4em] text-muted-foreground">FastGateway 控制台</p>
          <div>
            <h1 className="text-3xl font-semibold tracking-tight">关于 FastGateway</h1>
            <p className="text-sm text-muted-foreground">
              {gatewayInfo?.description ||
                "一个更方便的代理网关：监控流量、动态配置证书、支持隧道等能力，助力微服务与反向代理的快速部署与治理。"}
            </p>
          </div>
        </div>

        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="outline" className="rounded-full">
            版本 {loading ? "加载中" : formatValue(gatewayInfo?.version)}
          </Badge>
          <Badge variant="outline" className="rounded-full">
            {loading ? "运行时加载中" : formatValue(gatewayInfo?.framework)}
          </Badge>
          {gatewayInfo?.environmentName ? (
            <Badge variant="secondary" className="rounded-full">
              {gatewayInfo.environmentName}
            </Badge>
          ) : null}
          <Button
            variant="outline"
            size="sm"
            className="h-8 gap-2"
            onClick={loadGatewayInfo}
            disabled={loading}
          >
            <RefreshCw className={loading ? "h-4 w-4 animate-spin" : "h-4 w-4"} />
            刷新
          </Button>
        </div>
      </header>

      {error ? (
        <Alert variant="destructive">
          <AlertTitle>获取网关信息失败</AlertTitle>
          <AlertDescription>
            <p>{error}</p>
            <p className="text-xs">接口：/api/v1/system/info</p>
          </AlertDescription>
        </Alert>
      ) : null}

      <Tabs defaultValue="overview" className="w-full">
        <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
          <TabsList className="w-full justify-start md:w-auto">
            <TabsTrigger value="overview" className="gap-2">
              <Info className="h-4 w-4" />
              概览
            </TabsTrigger>
            <TabsTrigger value="runtime" className="gap-2">
              <Cpu className="h-4 w-4" />
              运行信息
            </TabsTrigger>
            <TabsTrigger value="community" className="gap-2">
              <Users className="h-4 w-4" />
              开源与社区
            </TabsTrigger>
          </TabsList>

          <div className="flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            <Badge variant="outline" className="rounded-full">
              License: Apache-2.0
            </Badge>
            {lastUpdated ? (
              <span>
                最后更新：
                {lastUpdated.toLocaleTimeString("zh-CN", {
                  hour: "2-digit",
                  minute: "2-digit",
                  second: "2-digit",
                })}
              </span>
            ) : null}
          </div>
        </div>

        <TabsContent value="overview" className="mt-4 space-y-6">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="flex items-center gap-2 text-base font-semibold">
                <Server className="h-4 w-4 text-blue-500" />
                项目简介
              </CardTitle>
              <CardDescription>面向网关业务的可视化管理与运行洞察</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="text-sm leading-relaxed text-muted-foreground">
                <p>
                  FastGateway 是一个基于 YARP 的高性能 API 网关与反向代理管理系统，提供动态路由、证书管理、访问控制、限流与隧道等能力。
                </p>
              </div>
              <Separator className="bg-border/60" />
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="rounded-lg border border-border/60 bg-muted/10 p-3">
                  <div className="text-xs text-muted-foreground">核心能力</div>
                  <ul className="mt-2 space-y-1 text-sm text-foreground">
                    <li>动态配置与热更新</li>
                    <li>证书自动续期与托管</li>
                    <li>黑白名单、限流、鉴权</li>
                    <li>隧道与多节点治理</li>
                  </ul>
                </div>
                <div className="rounded-lg border border-border/60 bg-muted/10 p-3">
                  <div className="text-xs text-muted-foreground">控制台技术栈</div>
                  <ul className="mt-2 space-y-1 text-sm text-muted-foreground">
                    <li>React + TypeScript + Vite</li>
                    <li>Tailwind CSS + CSS Variables</li>
                    <li>Radix UI + shadcn/ui</li>
                  </ul>
                </div>
              </div>
            </CardContent>
          </Card>

          <div className="grid gap-6 md:grid-cols-2">
            <Card className="border-border/60 bg-card/80 shadow-sm">
              <CardHeader className="space-y-1">
                <CardTitle className="text-base font-semibold">快速信息</CardTitle>
                <CardDescription>来自服务端接口 `/api/v1/system/info`</CardDescription>
              </CardHeader>
              <CardContent className="space-y-2 text-sm">
                {loading ? (
                  <>
                    <Skeleton className="h-4 w-56" />
                    <Skeleton className="h-4 w-72" />
                    <Skeleton className="h-4 w-64" />
                  </>
                ) : (
                  <div className="space-y-1 text-muted-foreground">
                    <div>
                      版本：<span className="font-mono text-foreground">{formatValue(gatewayInfo?.version)}</span>
                    </div>
                    <div>
                      运行时：<span className="font-mono text-foreground">{formatValue(gatewayInfo?.framework)}</span>
                    </div>
                    <div>
                      系统：<span className="font-mono text-foreground">{formatValue(gatewayInfo?.os)}</span>
                    </div>
                  </div>
                )}
              </CardContent>
            </Card>

            <Card className="border-border/60 bg-card/80 shadow-sm">
              <CardHeader className="space-y-1">
                <CardTitle className="text-base font-semibold">快速链接</CardTitle>
                <CardDescription>文档、源码与贡献入口</CardDescription>
              </CardHeader>
              <CardContent className="flex flex-wrap gap-2">
                <Button asChild variant="outline" size="sm" className="gap-2">
                  <a href="https://github.com/239573049/FastGateway" target="_blank" rel="noreferrer">
                    <Github className="h-4 w-4" />
                    GitHub
                  </a>
                </Button>
                <Button asChild variant="outline" size="sm" className="gap-2">
                  <a href="https://github.com/239573049/FastGateway#readme" target="_blank" rel="noreferrer">
                    <BookOpen className="h-4 w-4" />
                    README
                  </a>
                </Button>
                <Button asChild variant="outline" size="sm" className="gap-2">
                  <a
                    href="https://github.com/239573049/FastGateway/graphs/contributors"
                    target="_blank"
                    rel="noreferrer"
                  >
                    <Users className="h-4 w-4" />
                    贡献者
                  </a>
                </Button>
              </CardContent>
            </Card>
          </div>
        </TabsContent>

        <TabsContent value="runtime" className="mt-4 space-y-6">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="text-base font-semibold">版本与构建</CardTitle>
              <CardDescription>用于排查版本差异与发布信息</CardDescription>
            </CardHeader>
            <CardContent>
              <InfoTable rows={buildRows} loading={loading} />
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="text-base font-semibold">运行环境</CardTitle>
              <CardDescription>操作系统与进程基础信息</CardDescription>
            </CardHeader>
            <CardContent>
              <InfoTable rows={runtimeRows} loading={loading} />
            </CardContent>
          </Card>
        </TabsContent>

        <TabsContent value="community" className="mt-4 space-y-6">
          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="text-base font-semibold">贡献者</CardTitle>
              <CardDescription>感谢每一位让雪橇跑得更快的工程师</CardDescription>
            </CardHeader>
            <CardContent className="space-y-4">
              {contributorsError ? (
                <Alert>
                  <AlertTitle>贡献者列表加载失败</AlertTitle>
                  <AlertDescription>
                    <p>{contributorsError}</p>
                    <p className="text-xs">数据源：GitHub Contributors API</p>
                  </AlertDescription>
                </Alert>
              ) : null}

              {contributorsLoading ? (
                <div className="flex items-center justify-between gap-3">
                  <div className="flex -space-x-2">
                    {Array.from({ length: 8 }).map((_, i) => (
                      <Skeleton
                        key={`contributor-skeleton-${i}`}
                        className="h-8 w-8 rounded-full ring-2 ring-background"
                      />
                    ))}
                  </div>
                  <Skeleton className="h-8 w-24 rounded-md" />
                </div>
              ) : visibleContributors.length ? (
                <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
                  <AvatarGroup
                    className="h-8 -space-x-2"
                    sideOffset={14}
                    openDelay={200}
                    closeDelay={150}
                    children={contributorAvatarElements}
                  />

                  <div className="flex flex-wrap items-center gap-2">
                    <Button
                      asChild
                      variant="outline"
                      size="sm"
                      className="h-8 gap-2"
                    >
                      <a
                        href={CONTRIBUTORS_PAGE_URL}
                        target="_blank"
                        rel="noreferrer"
                      >
                        查看全部
                      </a>
                    </Button>
                    <Button
                      variant="outline"
                      size="sm"
                      className="h-8 gap-2"
                      onClick={() => loadContributors({ force: true })}
                      disabled={contributorsLoading}
                    >
                      <RefreshCw
                        className={
                          contributorsLoading ? "h-4 w-4 animate-spin" : "h-4 w-4"
                        }
                      />
                      刷新
                    </Button>
                  </div>
                </div>
              ) : (
                <div className="text-sm text-muted-foreground">
                  暂未获取到贡献者信息。
                  <a
                    className="ml-2 text-primary hover:underline"
                    href={CONTRIBUTORS_PAGE_URL}
                    target="_blank"
                    rel="noreferrer"
                  >
                    去 GitHub 查看
                  </a>
                </div>
              )}

              <p className="text-xs text-muted-foreground">
                贡献者数据来自 GitHub，结果会在本地缓存 6 小时以减少请求次数。
              </p>
            </CardContent>
          </Card>

          <Card className="border-border/60 bg-card/80 shadow-sm">
            <CardHeader className="space-y-1">
              <CardTitle className="text-base font-semibold">开源协议</CardTitle>
              <CardDescription>Apache License 2.0</CardDescription>
            </CardHeader>
            <CardContent className="text-sm text-muted-foreground">
              <p>
                本项目遵循 Apache-2.0 协议发布。更多细节请查看仓库根目录的 LICENSE 文件。
              </p>
            </CardContent>
          </Card>
        </TabsContent>
      </Tabs>
    </div>
  );
}
