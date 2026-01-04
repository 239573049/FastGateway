import { memo } from "react";

import Header from "./features/Header";
import ProxyList from "./features/ProxyList";

const ServerPage = memo(() => {
    return (
        <div className="mx-auto max-w-7xl space-y-6 px-4 py-6 md:px-6 lg:px-10">
            <Header />
            <ProxyList />
        </div>
    );
});

export default ServerPage;
