import { Button, Form, SideSheet, Toast } from "@douyinfe/semi-ui";
import { IconClose } from '@douyinfe/semi-icons';
import { CertificateEntity } from "../index.d";
import { useState } from "react";
import { uploadFile } from "../service/FileStroageService";
import { createCertificate } from '../service/CertificateService';

interface IProps {
    visible: boolean;
    onCancel: () => void;
    onSusccess: () => void;
}

const { Input, DatePicker } = Form;

export default function CreateCertificate({ visible, onCancel, onSusccess }: IProps) {
    const [file, setFile] = useState<File>({} as File);
    const [isDragging, setIsDragging] = useState(false); // New state variable

    async function handleSubmit(values: CertificateEntity) {
        const path = await uploadFile(file)

        values.path = path.data;

        createCertificate(values).then(() => {
            Toast.success('添加成功');
            onSusccess();
        }).catch(() => {
            Toast.error('添加失败');
        })

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

    function openuploadFile() {
        // 打开文件选择框
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.pfx';
        input.webkitdirectory = false;
        input.multiple = false;
        // 取下*.*选择只能.pfx文件        
        input.click();
        input.onchange = (e: any) => {
            handleFileUpload(e.target.files);
        }
    }

    return (
        <SideSheet width={500} title="创建证书" visible={visible} onCancel={onCancel}>
            <Form style={{
                overflowX: 'hidden',
                overflowY: 'auto',
            }} onSubmit={values => handleSubmit(values)} >
                {({ formState, values, formApi }) => (
                    <>
                        <Input rules={[
                            { required: true, message: '证书名称是必填的' }
                        ]} field='name' size='large' label='证书名称' style={{ width: '100%' }} placeholder='请输入证书名称'></Input>
                        <Input field='description' label='证书描述' size='large' style={{ width: '100%' }} placeholder='请输入证书描述'></Input>
                        <Input rules={[
                            { required: true, message: '域名是必填的' },
                        ]} field='host' label='域名' size='large' style={{ width: '100%' }} placeholder='证书绑定域名（唯一）'></Input>
                        <Input rules={[
                            { required: true, message: '密码是必填的' },
                        ]} type='password' field='password' label='密码' size='large' style={{ width: '100%' }} placeholder='请输入密码'></Input>
                        {/* semi组件的过期时间选择组件 */}
                        <DatePicker field='expireTime' label='过期时间' size='large' style={{ width: '100%' }} placeholder='请选择过期时间'></DatePicker>

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

                        <Button size='large' block style={{
                            marginTop: '20px',
                        }} htmlType='submit' type="secondary" theme='solid'>提交</Button>
                    </>
                )}
            </Form>
        </SideSheet>
    );
}