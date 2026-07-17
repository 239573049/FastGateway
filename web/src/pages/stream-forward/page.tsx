import { memo, useState } from "react";

import Header from "./features/Header";
import StreamForwardList from "./features/StreamForwardList";

const StreamForwardPage = memo(() => {
    const [reloadFlag, setReloadFlag] = useState(0);

    return (
        <div className="mx-auto max-w-7xl space-y-6 px-4 py-6 md:px-6 lg:px-10">
            <Header onCreated={() => setReloadFlag((f) => f + 1)} />
            <StreamForwardList reloadFlag={reloadFlag} />
        </div>
    );
});

export default StreamForwardPage;
