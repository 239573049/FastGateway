import { memo } from "react";
import ProxyList from "./features/ProxyList";
import Header from "./features/Header";

const ServerPage = memo(() => {
    return (
        <>
            <Header/>
            <ProxyList/>
        </>
    );
});

export default ServerPage;