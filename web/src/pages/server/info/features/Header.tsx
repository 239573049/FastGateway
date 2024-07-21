
import { Flexbox } from 'react-layout-kit';
import { Button } from 'antd';
import Divider from '@lobehub/ui/es/Form/components/FormDivider';
import { useState } from 'react';
import CreateDomain from './CreateDomain';

export default function Header() {
    const [createVisible, setCreateVisible] = useState(false);
    

    return (<>
        <Flexbox style={{
        }} horizontal>
            <span style={{
                fontSize: '18px',
                fontWeight: 'bold',
                marginLeft: '10px',
                marginTop: '10px',
                marginBottom: '10px',
                flex: 1,

            }}>
                网关路由管理
            </span>
            <div style={{
                marginRight: '20px',
                marginBottom: '20px'
            }}>
                <Button
                    onClick={() => {
                        setCreateVisible(true);
                    }}
                >
                    新增路由
                </Button>
            </div>
        </Flexbox>
        <Divider />
        <CreateDomain visible={createVisible} onClose={() => {
            setCreateVisible(false);
        }
        } onOk={() => {
            setCreateVisible(false);
        }} />

    </>)
}