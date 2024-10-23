import { property } from "@/services/FileStorageService";
import { Modal, Tag } from "@lobehub/ui";
import { useEffect, useState } from "react";
import { Card, message } from 'antd';
import { bytesToSize } from "@/utils/byte";

interface PropertyProps {
    open: boolean;
    fullPath: string;
    onClose: () => void;
}

const Property = (props: PropertyProps) => {

    const [item, setItem] = useState<any>(null);

    useEffect(() => {
        if (props.fullPath) {
            property(props.fullPath)
                .then((res) => {
                    if (res.success) {
                        setItem(res.data);
                    } else {
                        message.error(res.message);
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
                <Card hoverable>
                    <div>文件名：
                        <Tag color="blue">
                            {item.name}
                        </Tag>
                    </div>
                    <div>全路径：{item.fullName}</div>
                    <div>文件大小：{bytesToSize(item.length)}</div>
                    <div>创建时间：{item.creationTime}</div>
                    <div>最后访问时间：{item.lastAccessTime}</div>
                    <div>最后写入时间：{item.lastWriteTime}</div>
                </Card>
            )
        }else{
            return (
                <Card hoverable>
                    <div>文件夹名：<Tag color="blue">
                            {item.name}
                        </Tag></div>
                    <div>全路径：{item.fullName}</div>
                    <div>创建时间：{item.creationTime}</div>
                    <div>最后访问时间：{item.lastAccessTime}</div>
                    <div>最后写入时间：{item.lastWriteTime}</div>
                </Card>
            )
        }
    }

    return (
        <Modal
            title={item?.type === "File" ? "文件属性" : "文件夹属性"}
            open={props.open}
            onCancel={props.onClose}
            footer={[]}
        >
            {renderProperty()}
        </Modal>
    )
}

export default Property;