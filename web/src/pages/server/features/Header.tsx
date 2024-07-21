import Divider from "@lobehub/ui/es/Form/components/FormDivider";
import { memo, useState } from "react";
import {
    Button
} from 'antd';
import CreateServer from "./CreateServer";
import { useServerStore } from "@/store/server";
const Header = memo(() => {
    const [createVisible, setCreateVisible] = useState(false);
    const { setLoadingServers, loadingServers } = useServerStore();

    return (<>

        <div>
            <Button style={{
                // 靠右显示
                float: 'right',
                marginRight: '20px',
                marginBottom: '20px'
            }}
                onClick={() => {
                    setCreateVisible(true);
                }}
            >
                新增服务
            </Button>
        </div>
        <Divider />
        <CreateServer visible={createVisible} onClose={() => {
            setCreateVisible(false);
        }} onOk={() => {
            setCreateVisible(false);
            setLoadingServers(!loadingServers);
        }} />
    </>
    );
});

export default Header;