// Login.js

import { useState } from 'react';
import './index.css';
import { authorize } from '../../services/AuthorizeService';
import { Button, Notification } from '@douyinfe/semi-ui';
import { useNavigate } from 'react-router-dom';

const Login = () => {
    const navigate = useNavigate();
    const [password, setPassword] = useState('');
    
    function handleLogin() {
        authorize({password}).then((res)=>{
            if(res.success){
                localStorage.setItem('token', res.data);

                Notification.success({
                    title: '登录成功',
                });

                // 等待1s
                setTimeout(()=>{
                    navigate('/');
                }, 1000);
            }else{
                Notification.error({
                    title: res.message,
                });
            }
        });
    }

    return (
        <div className="login-container">
            <div className="outer-border">
                <div className="inner-content">
                    <h2>FastGateway管理系统</h2>
                    <div className="input-container">
                        <input
                            value={password}
                            onChange={(e) => setPassword(e.target.value)}
                            type="password"
                            placeholder="请输入密码"
                        />
                    </div>
                    <Button
                        onClick={()=>handleLogin()}
                        block
                        type='secondary'
                        theme='solid'
                        className="login-button"
                    >
                        登录
                    </Button>
                </div>
            </div>
        </div>
    );
};

export default Login;
