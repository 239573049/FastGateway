import { property } from "@/services/FileStorageService";
import { Dialog, DialogContent, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { useEffect, useState } from "react";
import { message } from '@/utils/toast';
import { bytesToSize } from "@/utils/byte";

interface PropertyProps {
    open: boolean;
    fullPath: string;
    onClose: () => void;
}

const Property = (props: PropertyProps) => {

    const [item, setItem] = useState<any>(null);

    useEffect(() => {
        if (props.fullPath && props.fullPath.trim()) {
            property(props.fullPath)
                .then((res) => {
                    if (res.success) {
                        setItem(res.data);
                    } else {
                        message.error(res.message || 'Unknown error');
                    }
                })
                .catch((e) => {
                    console.error(e);
                });
        }
    }, [props.fullPath]);

    function renderProperty() {
        if(!item){
            return null;
        }

        if (item?.type === "File") {
            return (
                <Card>
                    <CardContent className="p-4 space-y-2">
                        <div>文件名：
                            <Badge variant="secondary">
                                {item.name}
                            </Badge>
                        </div>
                        <div>全路径：{item.fullName}</div>
                        <div>文件大小：{bytesToSize(item.length)}</div>
                        <div>创建时间：{item.creationTime}</div>
                        <div>最后访问时间：{item.lastAccessTime}</div>
                        <div>最后写入时间：{item.lastWriteTime}</div>
                    </CardContent>
                </Card>
            )
        }else{
            return (
                <Card>
                    <CardContent className="p-4 space-y-2">
                        <div>文件夹名：<Badge variant="secondary">
                                {item.name}
                            </Badge></div>
                        <div>全路径：{item.fullName}</div>
                        <div>创建时间：{item.creationTime}</div>
                        <div>最后访问时间：{item.lastAccessTime}</div>
                        <div>最后写入时间：{item.lastWriteTime}</div>
                    </CardContent>
                </Card>
            )
        }
    }

    return (
        <Dialog open={props.open} onOpenChange={(open) => !open && props.onClose()}>
            <DialogContent>
                <DialogHeader>
                    <DialogTitle>{item?.type === "File" ? "文件属性" : "文件夹属性"}</DialogTitle>
                </DialogHeader>
                <div className="space-y-4">
                    {renderProperty()}
                </div>
            </DialogContent>
        </Dialog>
    )
}

export default Property;