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
import { UploadCert } from "@/services/CertService";
import { message } from "@/utils/toast";
import { useEffect, useRef, useState } from "react";

interface UploadCertProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
    // 传入则为“重新上传/替换”已有证书
    editItem?: { id: string; domain: string } | null;
}

type CertType = "pfx" | "pem";

export default function UploadCertPage({
    visible,
    onClose,
    onOk,
    editItem,
}: UploadCertProps) {
    const [domain, setDomain] = useState("");
    const [certType, setCertType] = useState<CertType>("pfx");
    const [password, setPassword] = useState("");
    const [certFile, setCertFile] = useState<File | null>(null);
    const [keyFile, setKeyFile] = useState<File | null>(null);
    const [submitting, setSubmitting] = useState(false);

    const certInputRef = useRef<HTMLInputElement>(null);
    const keyInputRef = useRef<HTMLInputElement>(null);

    useEffect(() => {
        if (visible) {
            setDomain(editItem?.domain ?? "");
            setCertType("pfx");
            setPassword("");
            setCertFile(null);
            setKeyFile(null);
            setSubmitting(false);
            if (certInputRef.current) certInputRef.current.value = "";
            if (keyInputRef.current) keyInputRef.current.value = "";
        }
    }, [visible, editItem]);

    async function handleOk() {
        if (submitting) return;

        if (!domain.trim()) {
            message.error("请输入域名");
            return;
        }
        if (!certFile) {
            message.error(certType === "pem" ? "请选择证书文件（.pem/.crt）" : "请选择证书文件（.pfx/.p12）");
            return;
        }
        if (certType === "pem" && !keyFile) {
            message.error("请选择证书私钥文件（.key）");
            return;
        }

        const formData = new FormData();
        if (editItem?.id) formData.append("id", editItem.id);
        formData.append("domain", domain.trim());
        formData.append("certType", certType);
        formData.append("file", certFile);
        if (certType === "pem" && keyFile) {
            formData.append("keyFile", keyFile);
        }
        if (certType === "pfx") {
            formData.append("password", password);
        }

        try {
            setSubmitting(true);
            const res = await UploadCert(formData);
            if (res && res.success === false) {
                message.error(res.message || "上传失败");
                return;
            }
            message.success("上传成功");
            onOk();
        } catch (err: any) {
            message.error(err?.message || "上传失败");
        } finally {
            setSubmitting(false);
        }
    }

    return (
        <Dialog open={visible} onOpenChange={(open) => !open && onClose()}>
            <DialogContent className="max-w-md">
                <DialogHeader>
                    <DialogTitle className="text-lg font-semibold">
                        {editItem ? "重新上传证书" : "上传证书"}
                    </DialogTitle>
                </DialogHeader>
                <div className="space-y-4 py-4">
                    <div className="space-y-2">
                        <Label className="text-sm font-medium">证书域名</Label>
                        <Input
                            value={domain}
                            onChange={(e) => setDomain(e.target.value)}
                            placeholder="请输入域名，如 example.com 或 *.example.com"
                            className="w-full"
                            disabled={!!editItem}
                        />
                        <p className="text-xs text-muted-foreground">
                            {editItem
                                ? "重新上传将替换该域名现有的证书。"
                                : "该域名将作为 SNI 匹配的主机名，请与证书中的域名保持一致，支持泛域名 *.example.com。"}
                        </p>
                    </div>

                    <div className="space-y-2">
                        <div className="text-sm font-medium text-foreground">证书格式</div>
                        <Highlight
                            controlledItems
                            value={certType}
                            onValueChange={(v) => setCertType((v as CertType) ?? "pfx")}
                            className="inset-0 bg-background shadow-sm rounded-md pointer-events-none"
                        >
                            <div className="inline-flex h-9 w-full items-center justify-center rounded-lg bg-muted p-1 text-muted-foreground">
                                <HighlightItem value="pfx" className="flex-1">
                                    <button
                                        type="button"
                                        className={cn(
                                            "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                            certType === "pfx" ? "text-foreground" : "hover:text-foreground"
                                        )}
                                    >
                                        PFX / P12
                                    </button>
                                </HighlightItem>
                                <HighlightItem value="pem" className="flex-1">
                                    <button
                                        type="button"
                                        className={cn(
                                            "w-full rounded-md px-3 py-1 text-sm font-medium transition-colors",
                                            certType === "pem" ? "text-foreground" : "hover:text-foreground"
                                        )}
                                    >
                                        PEM / CRT
                                    </button>
                                </HighlightItem>
                            </div>
                        </Highlight>
                    </div>

                    <div className="space-y-2">
                        <Label className="text-sm font-medium">
                            {certType === "pem" ? "证书文件（.pem / .crt）" : "证书文件（.pfx / .p12）"}
                        </Label>
                        <Input
                            ref={certInputRef}
                            type="file"
                            accept={certType === "pem" ? ".pem,.crt,.cer" : ".pfx,.p12"}
                            onChange={(e) => setCertFile(e.target.files?.[0] ?? null)}
                            className="w-full"
                        />
                    </div>

                    {certType === "pem" ? (
                        <div className="space-y-2">
                            <Label className="text-sm font-medium">私钥文件（.key）</Label>
                            <Input
                                ref={keyInputRef}
                                type="file"
                                accept=".key,.pem"
                                onChange={(e) => setKeyFile(e.target.files?.[0] ?? null)}
                                className="w-full"
                            />
                        </div>
                    ) : (
                        <div className="space-y-2">
                            <Label className="text-sm font-medium">证书密码</Label>
                            <Input
                                type="password"
                                value={password}
                                onChange={(e) => setPassword(e.target.value)}
                                placeholder="若证书无密码可留空"
                                className="w-full"
                            />
                        </div>
                    )}

                    <p className="text-xs text-muted-foreground">
                        上传的证书不会参与自动续期，到期后请手动重新上传。
                    </p>
                </div>
                <DialogFooter className="gap-2">
                    <Button variant="outline" onClick={onClose} className="flex-1">
                        取消
                    </Button>
                    <Button onClick={handleOk} className="flex-1" disabled={submitting}>
                        {submitting ? "上传中..." : "确定"}
                    </Button>
                </DialogFooter>
            </DialogContent>
        </Dialog>
    );
}
