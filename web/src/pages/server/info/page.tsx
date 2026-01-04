import { memo } from "react";

import DomainNamesList from "./features/DomainNamesList";
import Header from "./features/Header";

const ServerInfoPage = memo(() => {
    return (
        <div className="mx-auto max-w-7xl space-y-6 px-4 py-6 md:px-6 lg:px-10">
            <Header />
            <DomainNamesList />
        </div>
    );
});

export default ServerInfoPage;
