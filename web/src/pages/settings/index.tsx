import { Button, Divider, Input, Toast } from "@douyinfe/semi-ui";
import { useEffect, useState } from "react";
import { getList ,update} from "../../service/SettingService";



export default function Setting() {


    return (
        <div>
            <h1 style={{
                textAlign: 'center',
                fontSize: '24px',
                fontWeight: 'bold',
                marginBottom: '20px',

            }}>系统设置</h1>
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