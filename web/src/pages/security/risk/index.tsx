import { memo, useCallback, useEffect, useMemo, useState } from "react";
import { ColumnDef } from "@tanstack/react-table";
import { AlertTriangle, Ban, RefreshCw, ShieldAlert, Undo2 } from "lucide-react";

import { Reveal } from "@/components/motion";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { DataTable } from "@/components/ui/data-table";
import { message } from "@/utils/toast";
import { RiskModelMode, type RiskDecision, type RiskModelEvaluation, type RiskSettings, type RiskTag } from "@/types";
import { AddAbnormalIpToBlacklist } from "@/services/AbnormalIpService";
import { getAdminChallengeConfig } from "@/services/AuthorizationService";
import {
  getRiskDecisions,
  getRiskModelEvaluations,
  getRiskSettings,
  getRiskTags,
  removeRiskTag,
} from "@/services/RiskService";

const TAG_KIND_LABELS: Record<string, string> = {
  block: "临时封禁",
  account_under_attack: "账号被攻击",
  admin_lock: "后台登录锁定",
  model_suspect: "AI 模型判定",
};

const SUBJECT_LABELS: Record<string, string> = {
  ip: "IP",
  subnet: "网段",
  account: "账号",
};

const PROVIDER_LABELS: Record<string, string> = { pow: "计算验证", turnstile: "Turnstile" };

function describeRules(settings: RiskSettings) {
  return [
    {
      label: "要求人机验证",
      value: `≥ ${settings.challengeThreshold} 分 · ${PROVIDER_LABELS[settings.mediumProvider]}`,
    },
    {
      label: "强验证",
      value: `≥ ${settings.strongChallengeThreshold} 分 · ${PROVIDER_LABELS[settings.strongProvider]}`,
    },
    { label: "临时封禁", value: `≥ ${settings.blockThreshold} 分` },
    { label: "一次验证可尝试登录", value: `${settings.clearanceQuota} 次 / ${settings.clearanceMinutes} 分钟` },
  ];
}

const SIGNALS = [
  "IP 10 分钟内登录失败 5 / 20 / 50 次：+30 / +60 / +90",
  "IP 10 分钟内尝试 5 / 15 个账号：+40 / +70",
  "同一网段 10 分钟内失败 30 次：+30",
  "非浏览器客户端：+20；24 小时内被封禁过：+30",
  "封禁时长按 24 小时内次数升级：5 分钟 → 30 分钟 → 24 小时",
];

const percent = (value?: number | null) => (value == null ? "-" : `${Math.round(value * 100)}%`);

function usePaged<T>(items: T[], pageSize = 10) {
  const [page, setPage] = useState(1);
  const pageCount = Math.max(1, Math.ceil(items.length / pageSize));
  const current = Math.min(page, pageCount);
  const rows = useMemo(
    () => items.slice((current - 1) * pageSize, current * pageSize),
    [items, current, pageSize]
  );
  return { rows, current, setPage };
}

const RiskEventsPage = memo(() => {
  const [tags, setTags] = useState<RiskTag[]>([]);
  const [decisions, setDecisions] = useState<RiskDecision[]>([]);
  const [evaluations, setEvaluations] = useState<RiskModelEvaluation[]>([]);
  const [loading, setLoading] = useState(false);
  const [busyId, setBusyId] = useState<string | null>(null);
  const [turnstileConfigured, setTurnstileConfigured] = useState(true);
  const [settings, setSettings] = useState<RiskSettings | null>(null);
  const tagPage = usePaged(tags);
  const decisionPage = usePaged(decisions);
  const evaluationPage = usePaged(evaluations);

  const loadData = useCallback(() => {
    setLoading(true);
    Promise.all([getRiskTags(), getRiskDecisions(), getRiskModelEvaluations()])
      .then(([tagRes, decisionRes, evaluationRes]) => {
        setTags(tagRes?.data ?? []);
        setDecisions(decisionRes?.data ?? []);
        setEvaluations(evaluationRes?.data ?? []);
      })
      .catch(() => message.error("加载风控数据失败"))
      .finally(() => setLoading(false));
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  useEffect(() => {
    getAdminChallengeConfig()
      .then((res) => setTurnstileConfigured(res?.data?.configured ?? false))
      .catch(() => {
        // 读取失败时不提示，避免误报
      });
    getRiskSettings()
      .then((res) => setSettings(res?.data ?? null))
      .catch(() => {
        // 读取失败时不展示阈值
      });
  }, []);

  const usesTurnstile =
    settings?.mediumProvider === "turnstile" || settings?.strongProvider === "turnstile";

  const handleRemove = async (tag: RiskTag) => {
    try {
      setBusyId(tag.id);
      const res = await removeRiskTag(tag.id);
      if (res && res.success === false) {
        message.error(res.message || "解除失败");
        return;
      }
      message.success("已解除");
      loadData();
    } catch {
      message.error("解除失败");
    } finally {
      setBusyId(null);
    }
  };

  const handleBlacklist = async (tag: RiskTag) => {
    try {
      setBusyId(tag.id);
      await AddAbnormalIpToBlacklist(tag.subject);
      message.success("已加入黑名单");
    } catch {
      message.error("加入黑名单失败");
    } finally {
      setBusyId(null);
    }
  };

  const tagColumns: ColumnDef<RiskTag>[] = [
    {
      accessorKey: "kind",
      header: "类型",
      cell: ({ row }) => {
        const kind = row.original.kind;
        return (
          <Badge variant={kind === "account_under_attack" ? "secondary" : "destructive"}>
            {TAG_KIND_LABELS[kind] ?? kind}
          </Badge>
        );
      },
    },
    {
      accessorKey: "subject",
      header: "对象",
      cell: ({ row }) => (
        <div className="space-y-1">
          <Badge variant="outline" className="font-mono text-xs">
            {row.original.subject}
          </Badge>
          <div className="text-xs text-muted-foreground">
            {SUBJECT_LABELS[row.original.subjectType] ?? row.original.subjectType}
            {row.original.host ? ` · ${row.original.host}` : " · 管理后台"}
          </div>
        </div>
      ),
    },
    {
      accessorKey: "reason",
      header: "原因",
      cell: ({ row }) => <div className="max-w-[420px] text-sm">{row.original.reason || "-"}</div>,
    },
    {
      accessorKey: "expiresAt",
      header: "到期时间",
      cell: ({ row }) => <div className="font-mono text-sm text-muted-foreground">{row.original.expiresAt}</div>,
    },
    {
      id: "actions",
      header: "操作",
      cell: ({ row }) => {
        const tag = row.original;
        const busy = busyId === tag.id;
        return (
          <div className="flex items-center gap-2">
            <Button size="sm" variant="outline" className="gap-2" disabled={busy} onClick={() => handleRemove(tag)}>
              <Undo2 className="h-4 w-4" />
              解除
            </Button>
            {tag.subjectType === "ip" ? (
              <Button
                size="sm"
                variant="destructive"
                className="gap-2"
                disabled={busy}
                onClick={() => handleBlacklist(tag)}
              >
                <Ban className="h-4 w-4" />
                加入黑名单
              </Button>
            ) : null}
          </div>
        );
      },
    },
  ];

  const decisionColumns: ColumnDef<RiskDecision>[] = [
    {
      accessorKey: "time",
      header: "时间",
      cell: ({ row }) => <div className="font-mono text-sm text-muted-foreground">{row.original.time}</div>,
    },
    {
      accessorKey: "ip",
      header: "IP",
      cell: ({ row }) => (
        <Badge variant="outline" className="font-mono text-xs">
          {row.original.ip}
        </Badge>
      ),
    },
    {
      id: "target",
      header: "请求",
      cell: ({ row }) => (
        <div className="max-w-[260px] truncate font-mono text-xs text-muted-foreground">
          {row.original.host}
          {row.original.path}
        </div>
      ),
    },
    {
      accessorKey: "action",
      header: "处置",
      cell: ({ row }) => (
        <Badge variant={row.original.action === "block" ? "destructive" : "secondary"}>
          {row.original.action === "block" ? "封禁" : "要求验证"}
        </Badge>
      ),
    },
    {
      accessorKey: "score",
      header: "分数",
      cell: ({ row }) => <span className="font-mono tabular-nums">{row.original.score}</span>,
    },
    {
      accessorKey: "reason",
      header: "原因",
      cell: ({ row }) => <div className="max-w-[420px] text-sm">{row.original.reason || "-"}</div>,
    },
  ];

  const evaluationColumns: ColumnDef<RiskModelEvaluation>[] = [
    {
      accessorKey: "time",
      header: "时间",
      cell: ({ row }) => <div className="font-mono text-sm text-muted-foreground">{row.original.time}</div>,
    },
    {
      accessorKey: "ip",
      header: "IP",
      cell: ({ row }) => (
        <Badge variant="outline" className="font-mono text-xs">
          {row.original.ip}
        </Badge>
      ),
    },
    {
      id: "probabilities",
      header: "脚本 / 撞库 / 猜密码",
      cell: ({ row }) =>
        row.original.error ? (
          <span className="text-sm text-destructive">{row.original.error}</span>
        ) : (
          <span className="font-mono text-sm tabular-nums">
            {percent(row.original.automated)} / {percent(row.original.credentialStuffing)} /{" "}
            {percent(row.original.passwordGuessing)}
          </span>
        ),
    },
    {
      id: "scores",
      header: "模型分 / 规则分",
      cell: ({ row }) => (
        <span className="font-mono tabular-nums">
          {row.original.modelScore ?? "-"} / {row.original.ruleScore}
        </span>
      ),
    },
    {
      id: "mode",
      header: "模式",
      cell: ({ row }) => (
        <Badge variant={row.original.applied ? "destructive" : "secondary"}>
          {row.original.mode === RiskModelMode.Enforce ? (row.original.applied ? "已计入" : "参与决策") : "影子"}
        </Badge>
      ),
    },
    {
      id: "followUp",
      header: "之后",
      cell: ({ row }) => (
        <span className="text-sm text-muted-foreground">
          {[
            row.original.laterLoginSucceeded ? "登录成功" : null,
            row.original.laterChallengePassed ? "通过验证" : null,
          ]
            .filter(Boolean)
            .join("、") || "-"}
        </span>
      ),
    },
    {
      accessorKey: "latencyMs",
      header: "延迟",
      cell: ({ row }) => <span className="font-mono text-sm tabular-nums">{row.original.latencyMs} ms</span>,
    },
  ];

  return (
    <div className="container mx-auto max-w-7xl px-4 py-8 sm:px-6 lg:px-8">
      <Reveal className="mb-6">
        <div className="flex items-center gap-2">
          <ShieldAlert className="h-6 w-6 text-destructive" />
          <h1 className="text-3xl font-bold tracking-tight text-foreground">登录风控</h1>
        </div>
        <p className="mt-2 text-muted-foreground">
          对开启「登录保护」的路由按 IP、网段和账号统计登录失败，按风险分要求人机验证或临时封禁；管理后台的登录锁定也在此展示。
        </p>
      </Reveal>

      {!turnstileConfigured && usesTurnstile ? (
        <Alert className="mb-6">
          <AlertTriangle />
          <AlertTitle>Turnstile 未配置</AlertTitle>
          <AlertDescription>
            <p>
              当前设置使用了 Cloudflare Turnstile，但 appsettings 的 BotProtection 节未配置密钥，相应档位会自动改用加难的计算验证。
              如不打算使用 Turnstile，可在「系统设置 → 登录风控」中把验证方式都改为计算验证。
            </p>
          </AlertDescription>
        </Alert>
      ) : null}

      <div className="grid gap-6 lg:grid-cols-[minmax(0,340px)_1fr]">
        <Reveal index={1}>
          <Card className="h-fit border shadow-sm">
            <CardHeader className="border-b bg-muted/50">
              <CardTitle className="text-base font-semibold">判定规则</CardTitle>
              <CardDescription className="text-sm text-muted-foreground">
                风险分由以下信号累加，随统计窗口滑动自动下降
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 pt-4">
              {(settings ? describeRules(settings) : []).map((rule) => (
                <div key={rule.label} className="flex items-center justify-between text-sm">
                  <span className="text-muted-foreground">{rule.label}</span>
                  <span className="font-mono">{rule.value}</span>
                </div>
              ))}
              <ul className="space-y-1.5 border-t pt-3 text-xs leading-relaxed text-muted-foreground">
                {SIGNALS.map((signal) => (
                  <li key={signal}>{signal}</li>
                ))}
              </ul>
              <div className="rounded-md border border-primary/30 bg-primary/5 p-3 text-xs leading-relaxed text-muted-foreground">
                风控状态保存在内存中，重启后清空；集群下每个节点独立统计。
              </div>
            </CardContent>
          </Card>
        </Reveal>

        <div className="space-y-6">
          <Reveal index={2}>
            <Card className="border shadow-sm">
              <CardHeader className="flex flex-row items-center justify-between space-y-0 border-b bg-muted/50">
                <div>
                  <CardTitle className="text-base font-semibold">当前生效的标记</CardTitle>
                  <CardDescription className="text-sm text-muted-foreground">
                    封禁、账号被攻击和管理后台锁定，到期后自动解除
                  </CardDescription>
                </div>
                <Button variant="outline" size="sm" className="gap-2" onClick={loadData}>
                  <RefreshCw className="h-4 w-4" />
                  刷新
                </Button>
              </CardHeader>
              <CardContent className="p-4">
                <DataTable
                  columns={tagColumns}
                  data={tagPage.rows}
                  loading={loading}
                  total={tags.length}
                  current={tagPage.current}
                  onPaginationChange={(page) => tagPage.setPage(page)}
                />
              </CardContent>
            </Card>
          </Reveal>

          <Reveal index={3}>
            <Card className="border shadow-sm">
              <CardHeader className="border-b bg-muted/50">
                <CardTitle className="text-base font-semibold">最近的处置</CardTitle>
                <CardDescription className="text-sm text-muted-foreground">
                  最近 500 次要求验证与封禁记录（仅本节点）
                </CardDescription>
              </CardHeader>
              <CardContent className="p-4">
                <DataTable
                  columns={decisionColumns}
                  data={decisionPage.rows}
                  loading={loading}
                  total={decisions.length}
                  current={decisionPage.current}
                  onPaginationChange={(page) => decisionPage.setPage(page)}
                />
              </CardContent>
            </Card>
          </Reveal>

          <Reveal index={4}>
            <Card className="border shadow-sm">
              <CardHeader className="border-b bg-muted/50">
                <CardTitle className="text-base font-semibold">AI 模型评估</CardTitle>
                <CardDescription className="text-sm text-muted-foreground">
                  最近 1000 次评估（仅本节点）。「之后」列显示该来源随后是否登录成功或通过验证，可用来判断模型是否误判
                </CardDescription>
              </CardHeader>
              <CardContent className="p-4">
                <DataTable
                  columns={evaluationColumns}
                  data={evaluationPage.rows}
                  loading={loading}
                  total={evaluations.length}
                  current={evaluationPage.current}
                  onPaginationChange={(page) => evaluationPage.setPage(page)}
                />
              </CardContent>
            </Card>
          </Reveal>
        </div>
      </div>
    </div>
  );
});

export default RiskEventsPage;
