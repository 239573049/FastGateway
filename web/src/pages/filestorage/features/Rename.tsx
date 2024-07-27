import { Input, Modal } from "@lobehub/ui";
import { useEffect, useState } from "react";
import { Button, message } from "antd";
import { renameFile } from "@/services/FileStorageService";

interface RenameProps {
    visible: boolean;
    id: string;
    name: string;
    drives: string;
    path: string;
    isFile: boolean;
    onClose: () => void;
    onOk: (name: string) => void;
}


export default function Rename(props: RenameProps) {
    const { visible, id, name, isFile, onClose, onOk, path, drives } = props;
    const [inputValue, setInputValue] = useState(name);
    const [loading, setLoading] = useState(false);

    const onInputChange = (e: React.ChangeEvent<HTMLInputElement>) => {
        setInputValue(e.target.value);
    }

    const onOkClick = async () => {
        if (loading) {
            return;
        }
        setLoading(true);

        renameFile(path, drives, inputValue)
            .then(() => {
                message.success('重命名成功');
                onOk(inputValue);
            })
            .catch(e => {
                console.error(e);
            })
            .finally(() => {
                setLoading(false);
            });
    }

    useEffect(() => {
        setInputValue(name);
    }, [name]);


    return (
        <Modal
            footer={[]}
            title={`重命名${isFile ? '文件' : '文件夹'}`}
            open={visible}
            onCancel={onClose}
            okButtonProps={{ loading }}
        >
            <Input value={inputValue} onChange={onInputChange} />
            <Button style={{
                marginTop: 20,

            }}
            onClick={()=>{
                onOkClick()
            }}
            block>
                重命名
            </Button>
        </Modal>
    )
}