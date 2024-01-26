import { Button, Divider, Input, Toast } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { getList ,update} from "../../service/SettingService";



export default function Setting() {

    const [maxBodySize, setMaxBodySize] = useState(100);
    const LogRetentionTime = "Gateway:Setting:LogRetentionTime";

    const maxRequestBodySize = "Gateway:Setting:MaxRequestBodySize";


    async function loading() {
        const values = await getList() as any;
        
        // 获取values的数组中你的name为maxRequestBodySize
        const v1 = values.find((item: any) => item.name === maxRequestBodySize);
        setMaxBodySize(v1?.defaultValue);
    }

    function save() {
        update(maxRequestBodySize, maxBodySize)
            .then(() => {
                Toast.success('保存成功')
            })
    }

    useEffect(() => { loading() }, []);

    return (
        <div>
            <h1 style={{
                textAlign: 'center',
                fontSize: '24px',
                fontWeight: 'bold',
                marginBottom: '20px',

            }}>系统设置</h1>
            <Divider style={{
                marginBottom: '20px',
            }}></Divider>
            <div style={{
                fontSize: '16px',
                fontWeight: 'bold',
                marginBottom: '20px',
            }}>
                <span>
                    最大请求Body大小（MB）
                </span>
                <Input value={maxBodySize} onChange={(e) => {
                    setMaxBodySize(e as any)
                }} size='large' style={{
                    marginTop: '20px',
                }} placeholder='最大请求Body大小（MB）'></Input>
                <Button size='large' style={{
                    marginTop: '20px',
                }} onClick={()=>save()} block>保存</Button>
            </div>
            <Divider></Divider>
            <div style={{
                textAlign: 'center',
                fontSize: '16px',
                fontWeight: 'bold',
                marginBottom: '20px',
                marginTop: '20px',
            }}>
                <h2 style={{
                    textAlign: 'center',
                    fontSize: '24px',
                    marginBottom: '20px',
                }}>关于</h2>
                <p style={{
                    textAlign: 'center',
                    fontSize: '16px',
                    marginBottom: '20px',
                }}>作者：Token</p>
                <p>邮箱：239573049@qq.com</p>
            </div>
        </div>
    )
}