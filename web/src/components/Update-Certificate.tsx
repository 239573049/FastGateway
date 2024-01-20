import { Button, SideSheet, Toast } from "@douyinfe/semi-ui";
import { CertificateEntity } from "../index.d";
import { IconClose } from '@douyinfe/semi-icons';
import { useState } from "react";
import { uploadFile } from "../service/FileStroageService";
import { updateCertificatePath } from '../service/CertificateService';

interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
    certificate?: CertificateEntity;
}


export default function UpdateCertificate({ visible, onCancel, onSusccess, certificate }: IProps) {
    const [file, setFile] = useState<File>({} as File);
    const [isDragging, setIsDragging] = useState(false); // New state variable

    function openuploadFile() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.pfx';
        input.onchange = (e: any) => handleFileUpload(e.target.files);
        input.click();
    }

    function handleFileUpload(files: FileList) {
        const selectedFile = files[0];
        if (!selectedFile) {
            return;
        }

        // Check file extension
        if (selectedFile.name.indexOf('.pfx') === -1) {
            Toast.error('证书文件格式不正确，必须是.pfx格式');
            return;
        }

        setFile(selectedFile);
    }


    function handleDragOver(event: React.DragEvent<HTMLDivElement>) {
        event.preventDefault();
        setIsDragging(true); // Set isDragging to true when file is being dragged over
    }

    function handleDragLeave(event: React.DragEvent<HTMLDivElement>) {
        event.preventDefault();
        setIsDragging(false); // Set isDragging to false when file is cancelled or leaves
    }

    function handleDrop(event: React.DragEvent<HTMLDivElement>) {
        event.preventDefault();
        setIsDragging(false); // Set isDragging to false when file is dropped
        handleFileUpload(event.dataTransfer.files);
    }

    async function handleSubmit() {
        if (!file.name) {
            Toast.error('请上传证书文件');
            return
        }

        const path = await uploadFile(file);

        updateCertificatePath(certificate!.id, path.data)
            .then(() => {
                Toast.success('更新成功');
                onSusccess();
            })
            .catch(() => {
                Toast.error('更新失败');
            });

        Toast.success('更新成功');
        onSusccess();

    }

    return (
        <SideSheet width={500} title="更新证书" visible={visible} onCancel={onCancel}>
            {!file.name ?
                <div id="upload-div" onClick={() => openuploadFile()} onDragOver={handleDragOver} onDragLeave={handleDragLeave} onDrop={handleDrop} style={{
                    height: '100px',
                    width: '99%',
                    border: isDragging ? '2px dashed blue' : '1px solid #ccc', // Change border color when file is being dragged over
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    borderRadius: '5px',
                    marginBottom: '20px',
                    userSelect: 'none',
                }}>
                    点击或拖拽上传证书文件。
                </div> : <div style={{
                    height: '100px',
                    width: '99%',
                    border: '1px solid #ccc',
                    display: 'flex',
                    justifyContent: 'center',
                    alignItems: 'center',
                    borderRadius: '5px',
                    marginBottom: '20px',
                    userSelect: 'none',

                }}>
                    {file.name}
                    <Button onClick={() => setFile({} as File)} style={{
                        marginLeft: '20px',
                    }} size='small' theme='borderless' icon={<IconClose />}></Button>
                </div>}

            <Button block onClick={() => {
                handleSubmit();
            }
            } theme='solid' style={{ marginRight: '10px' }}>
                更新证书
            </Button>
        </SideSheet>
    )
}