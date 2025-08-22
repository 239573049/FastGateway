
import React from 'react';
import { Separator } from '@/components/ui/separator';

interface FHeaderProps {
    title: string;
    action?: React.ReactNode;
}

function FHeader({
    title,
    action
}: FHeaderProps) {
    return (
        <>
            <div className="flex items-center justify-between w-full">
                <h1 className="text-2xl font-bold tracking-tight">{title}</h1>
                {action && <div className="flex items-center space-x-2">{action}</div>}
            </div>
            <Separator className="my-4" />
        </>
    );
}

export default FHeader;