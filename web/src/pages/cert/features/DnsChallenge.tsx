import { Button } from "@/components/animate-ui/components/ui/button";
import {
    Dialog,
    DialogContent,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/animate-ui/components/ui/dialog";
import { PrepareCertDns, ValidateCertDns } from "@/services/CertService";
import { message } from "@/utils/toast";
import { Copy, Loader2, RefreshCw } from "lucide-react";
import { useCallback, useEffect, useState } from "react";

interface DnsChallengeProps {
    visible: boolean;
    item: { id: string; domain: string } | null;
    onClose: () => void;
    onOk: () => void;
}

interface DnsRecord {
    recordName: string;
    recordType: string;
    recordValue: string;
}

async function copyText(text: string) {
    try {
        await navigator.clipboard.writeText(text);
        message.success("已复制");
    } catch {
        message.error("复制失败");
    }
}

export default function DnsChallengePage({
    visible,
    item,
    onClose,
    onOk,
}: DnsChallengeProps) {
    const [preparing, setPreparing] = useState(false);
    const [validating, setValidating] = useState(false);
    const [record, setRecord] = useState<DnsRecord | null>(null);

    const prepare = useCallback(async () => {
        if (!item) return;
        setPreparing(true);
        setRecord(null);
        try {
            const res = await PrepareCertDns(item.id);
            if (res?.success === false) {
                message.error(res.message || "获取 DNS 记录失败");
                return;
            }
            setRecord(res?.data as DnsRecord);
        } catch (err: any) {
            message.error(err?.message || "获取 DNS 记录失败");
        } finally {
            setPreparing(false);
        }
    }, [item]);

    useEffect(() => {
        if (visible && item) {
            setRecord(null);
            setValidating(false);
            prepare();
        }
    }, [visible, item, prepare]);

    async function handleValidate() {
        if (!item || validating) return;
        setValidating(true);
        try {
            const res = await ValidateCertDns(item.id);
            if (res?.success === false) {
                message.error(res.message || "验证失败");
                return;
            }
            message.success("证书签发成功");
            onOk();
        } catch (err: any) {
            message.error(err?.message || "验证失败");
        } finally {
            setValidating(false);
        }
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-lg">
                <DialogHeader>
                    <DialogTitle className="text-lg font-semibold">DNS 验证申请证书</DialogTitle>
                </DialogHeader>

                <div className="space-y-4 py-2">
                    <div className="rounded-md border bg-muted/30 px-3 py-2 text-sm">
                        <div className="text-xs uppercase tracking-wide text-muted-foreground">域名</div>
                        <div className="mt-1 font-medium text-foreground">{item?.domain}</div>
                    </div>

                    <p className="text-sm text-muted-foreground">
                        请在你的域名解析服务商处添加以下 TXT 记录，等待生效后点击「验证并签发」。
                        泛域名证书（*.example.com）仅支持此 DNS 验证方式。
                    </p>

                    {preparing ? (
                        <div className="flex items-center justify-center gap-2 py-8 text-sm text-muted-foreground">
                            <Loader2 className="h-4 w-4 animate-spin" />
                            正在获取验证记录...
                        </div>
                    ) : record ? (
                        <div className="space-y-3">
                            {[
                                { label: "记录类型", value: record.recordType },
                                { label: "主机记录", value: record.recordName },
                                { label: "记录值", value: record.recordValue },
                            ].map((row) => (
                                <div key={row.label} className="space-y-1">
                                    <div className="text-xs font-medium text-muted-foreground">
                                        {row.label}
                                    </div>
                                    <div className="flex items-center gap-2">
                                        <code className="flex-1 break-all rounded-md border bg-muted/40 px-3 py-2 text-xs">
                                            {row.value}
                                        </code>
                                        <Button
                                            variant="ghost"
                                            size="icon"
                                            className="h-8 w-8 shrink-0"
                                            onClick={() => copyText(row.value)}
                                        >
                                            <Copy className="h-4 w-4" />
                                        </Button>
                                    </div>
                                </div>
                            ))}
                        </div>
                    ) : (
                        <div className="flex items-center justify-center py-6">
                            <Button variant="outline" size="sm" onClick={prepare}>
                                <RefreshCw className="mr-2 h-4 w-4" />
                                重新获取记录
                            </Button>
                        </div>
                    )}

                    <p className="text-xs text-muted-foreground">
                        DNS 记录生效可能需要几分钟，验证过程最长可能持续 2-3 分钟，请耐心等待。
                        签发成功后到期需再次通过 DNS 验证续期。
                    </p>
                </div>

                <DialogFooter className="gap-2">
                    <Button variant="outline" onClick={onClose} className="flex-1" disabled={validating}>
                        取消
                    </Button>
                    <Button
                        onClick={handleValidate}
                        className="flex-1"
                        disabled={validating || preparing || !record}
                    >
                        {validating ? (
                            <>
                                <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                                验证中...
                            </>
                        ) : (
                            "验证并签发"
                        )}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
