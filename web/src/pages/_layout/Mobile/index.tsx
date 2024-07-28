import { memo } from "react";

const MobileLayout = memo((
    children
) => {
    return (
        <>
            {children}
        </>
    );
});

export default MobileLayout;