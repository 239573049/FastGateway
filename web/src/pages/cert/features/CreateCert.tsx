import { Highlight, HighlightItem } from "@/components/animate-ui/primitives/effects/highlight";
import { Button } from "@/components/animate-ui/components/ui/button";
import { Input } from "@/components/animate-ui/components/ui/input";
import { Label } from "@/components/animate-ui/components/ui/label";
import {
    Dialog,
    DialogContent,
    DialogFooter,
    DialogHeader,
    DialogTitle,
} from "@/components/animate-ui/components/ui/dialog";
import { cn } from "@/lib/utils";
import { CreateCert } from "@/services/CertService";
import { message } from "@/utils/toast";
import { useEffect, useState } from "react";

interface CreateCertProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
}

const DEFAULT_VALUE = {
    domain: '',
    autoRenew: true,
    email: '',
};

export default function CreateCertPage({
    visible,
    onClose,
    onOk
}: CreateCertProps) {
    const [value, setValue] = useState(DEFAULT_VALUE);
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        if (visible) {
            setValue(DEFAULT_VALUE);
            setSubmitting(false);
        }
    }, [visible]);

    async function handleOk() {
        if (submitting) return;
        // 判断是否输入了域名
        if (!value.domain) {
            message.error('请输入域名');
            return;
        }
        // 判断是否输入了邮箱
        if (!value.email) {
            message.error('请输入邮箱');
            return;
        }

        // 判断邮箱格式是否正确
        if (!/^\w+([-+.]\w+)*@\w+([-.]\w+)*\.\w+([-.]\w+)*$/.test(value.email)) {
            message.error('邮箱格式不正确');
            return;
        }

        try {
            setSubmitting(true);
            await CreateCert(value);
            message.success('新增成功');
            onOk();
        } catch {
            message.error('新增失败');
        } finally {
            setSubmitting(false);
        }
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-md">
                <DialogHeader>
                    <DialogTitle className="text-lg font-semibold">新增证书</DialogTitle>
                </DialogHeader>
                <div className="space-y-4 py-4">
                    <div className="space-y-2">
                        <Label className="text-sm font-medium">证书域名</Label>
                        <Input
                            value={value.domain}
                            onChange={(e) =>
                                setValue({ ...value, domain: e.target.value })
                            }
                            placeholder="请输入域名，如 example.com"
                            className="w-full"
                        />
                    </div>
                    <div className="space-y-2">
                        <Label className="text-sm font-medium">邮箱</Label>
                        <Input
                            value={value.email}
                            onChange={(e) =>
                                setValue({ ...value, email: e.target.value })
                            }
                            placeholder="请输入邮箱地址"
                            className="w-full"
                        />
                    </div>
                    <div className="space-y-2">
                        <div className="text-sm font-medium text-foreground">续期策略</div>
                        <Highlight
                            controlledItems
                            value={value.autoRenew ? "auto" : "manual"}
                            onValueChange={(v) =>
                                setValue({ ...value, autoRenew: v === "auto" })
                            }
                            className="inset-0 bg-background shadow-sm rounded-md pointer-events-none"
                        >
                            <div className="inline-flex h-9 w-full items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground">
                                <HighlightItem value="auto" className="flex-1">
                                    <button
                                        type="button"
                                        className={cn(
                                            "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                            value.autoRenew
                                                ? "text-foreground"
                                                : "hover:text-foreground"
                                        )}
                                    >
                                        自动续期
                                    </button>
                                </HighlightItem>
                                <HighlightItem value="manual" className="flex-1">
                                    <button
                                        type="button"
                                        className={cn(
                                            "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                            !value.autoRenew
                                                ? "text-foreground"
                                                : "hover:text-foreground"
                                        )}
                                    >
                                        手动管理
                                    </button>
                                </HighlightItem>
                            </div>
                        </Highlight>
                        <p className="text-xs text-muted-foreground">
                            选择“自动续期”后，系统会在证书临期时自动尝试续签。
                        </p>
                    </div>
                </div>
                <DialogFooter className="gap-2">
                    <Button variant="outline" onClick={onClose} className="flex-1">
                        取消
                    </Button>
                    <Button onClick={handleOk} className="flex-1" disabled={submitting}>
                        {submitting ? '提交中...' : '确定'}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    )
}
