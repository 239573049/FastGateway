import { Button, Input, Toast } from "@douyinfe/semi-ui";
import { getToken } from "../../service/AuthorityService";
import { useState } from "react";
import { useNavigate } from "react-router-dom";

export default function Login() {

    const navigate = useNavigate();
    const [username, setUsername] = useState<string>('');
    const [password, setPassword] = useState<string>('');

    const handleSubmit = () => {
        // 判断用户名和密码是否为空
        if (username === '' || password === '') {
            return;
        }

        getToken(username, password).then((res:any) => {
            debugger
            if(res.code !== 0) {
                Toast.error(res.message);
                return;
            }
            Toast.success('登录成功');
            localStorage.setItem('token', res.data.token);
            navigate('/')
        })
    };

    return (
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', height: '100vh', background: 'linear-gradient(135deg, #1e5799 0%,#2989d8 50%,#207cca 51%,#7db9e8 100%)' }}>
            <div style={{ width: '400px', padding: '24px', boxShadow: '0 2px 8px rgba(0, 0, 0, 0.1)', borderRadius: '4px', backgroundColor: '#fff', display: 'flex', flexDirection: 'column', alignItems: 'center' }}>
                <h1 style={{ textAlign: 'center', marginBottom: '24px', textShadow: '2px 2px 4px rgba(0, 0, 0, 0.3)' }}>登录Gateway系统</h1>
                <Input value={username} onChange={(e) => setUsername(e)} size='large' placeholder="账号" style={{ marginBottom: '16px', width: '100%' }} />
                <Input value={password} onChange={(e) => setPassword(e)} size='large' type="password" placeholder="密码" style={{ marginBottom: '24px', width: '100%' }} />
                <Button size='large' type="primary" block onClick={handleSubmit} style={{ marginTop: '16px', border: 'none', boxShadow: '0 2px 4px rgba(0, 0, 0, 0.2)', width: '100%' }}>Login</Button>
            </div>
        </div>
    );
}
