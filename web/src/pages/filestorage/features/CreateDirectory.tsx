import { createDirectory } from "@/services/FileStorageService";
import { Input, Modal } from "@lobehub/ui";
import { Button, message } from "antd";
import { useState } from "react";

interface CreateDirectoryProps {
    visible: boolean;
    onClose: () => void;
    onOk: () => void;
    drives: string;
    path: string;
}

export default function CreateDirectory(props: CreateDirectoryProps) {
    const [name, setName] = useState('');
    function save() {
        createDirectory(props.path, props.drives, name)
            .then(() => {
                props.onOk();
                message.success('创建成功');
            })
    }

    return (
        <Modal title='新建目录' open={props.visible} onCancel={() => {
            props.onClose();
        }} footer={[]}>
            <Input name={name} onChange={(e) => {
                setName(e.target.value)
            }} placeholder='请输入文件夹名称' />
            <Button onClick={() => save()} style={{
                marginTop: '10px',
            }} block>
                创建
            </Button>
        </Modal>
    )
}