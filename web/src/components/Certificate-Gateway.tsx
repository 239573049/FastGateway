import { Button, List, Toast } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { CertificateEntity } from "../index.d";
import CreateCertificate from "./Create-Certificate";
import { getCertificates, deleteCertificate } from "../service/CertificateService";
import './Certificate-Gateway.css'
import UpdateCertificate from "./Update-Certificate";


interface IProps {
    activeKey: string;
}


export default function CertificateGateway({ activeKey }: IProps) {

    const [createCertificateVisible, setCreateCertificateVisible] = useState<boolean>(false);
    const [updateCertificateVisible, setUpdateCertificateVisible] = useState<boolean>(false);
    const [updateCertificate, setUpdateCertificate] = useState<CertificateEntity>({} as CertificateEntity); // 更新证书时，需要传递证书信息，这里保存证书信息
    const [data, setData] = useState<any[]>([]);

    function loadingCertificate() {
        getCertificates().then((res) => {
            setData(res.data);
        })
    }

    useEffect(() => {
        loadingCertificate();
    }, []);

    /**
     * 删除证书
     * @param id 
     */
    function deleteCertificateById(id: string) {
        deleteCertificate(id).then(() => {
            loadingCertificate();
            Toast.success('删除成功');
        }).catch(() => {
            Toast.error('删除失败');
        })
    }

    return (
        <>
            <List
                size="large"
                header={
                    <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                        <div>
                            证书管理
                        </div>
                        <Button onClick={() => setCreateCertificateVisible(true)} theme='solid' style={{}}>
                            添加证书
                        </Button>
                    </div>
                }
                bordered
                style={{
                    height: 'calc(100vh - 280px)',
                    overflow: 'auto',
                    padding: '10px',
                }}
                dataSource={data}
                renderItem={(item: CertificateEntity, index) =>
                    <>
                        <div className="certificate-item">
                            <div>
                                <div style={{ display: 'flex', justifyContent: 'space-between' }}>
                                    <div style={{ marginRight: '20px' }}>
                                        <div style={{ marginBottom: '10px' }}>
                                            证书名称：{item.name}
                                        </div>
                                        <div style={{ marginBottom: '10px' }}>
                                            证书描述：{item.description}
                                        </div>
                                    </div>
                                    <div style={{ marginRight: '20px' }} >
                                        <div style={{ marginBottom: '10px' }}>
                                            证书私钥：{item.password}
                                        </div>
                                        <div style={{ marginBottom: '10px' }}>
                                            上传时间：{item.createTime}
                                        </div>
                                    </div>
                                    <div>
                                        <div style={{ marginBottom: '10px' }}>
                                            域名：{item.host}
                                        </div>
                                        <div style={{ marginBottom: '10px' }}>
                                            过期时间：{item.expirationTime}
                                        </div>
                                    </div>
                                </div>
                            </div>
                            <div>
                                <Button onClick={() => {
                                    setUpdateCertificateVisible(true);
                                    setUpdateCertificate(item);
                                }} theme='solid' style={{ marginRight: '10px' }}>
                                    更新证书
                                </Button>
                                <Button onClick={() => deleteCertificateById(item.id)} theme='solid' style={{ marginRight: '10px' }}>
                                    删除
                                </Button>
                            </div>
                        </div>
                    </>}
            />
            <CreateCertificate visible={createCertificateVisible} onCancel={() => setCreateCertificateVisible(false)} onSusccess={() => {
                setCreateCertificateVisible(false);
                loadingCertificate();
            }} />
            <UpdateCertificate certificate={updateCertificate} visible={updateCertificateVisible} onCancel={() => setUpdateCertificateVisible(false)} onSusccess={() => {
                setUpdateCertificateVisible(false);
                loadingCertificate();
                setUpdateCertificate({} as CertificateEntity);
            }} />
        </>
    )
}