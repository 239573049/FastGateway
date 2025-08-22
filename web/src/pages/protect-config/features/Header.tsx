
import React from 'react';
import { Separator } from '@/components/ui/separator';
import { cn } from '@/lib/utils';

interface ProtectConfigHeaderProps {
    title: string;
    action?: React.ReactNode;
    className?: string;
}

function Header({
    title,
    action,
    className
}: ProtectConfigHeaderProps) {
    return (
        <div className={cn("mb-6", className)}>
            <div className="flex items-center justify-between mb-4">
                <div>
                    <h1 className="text-2xl font-bold text-foreground">{title}</h1>
                    <p className="text-sm text-muted-foreground mt-1">
                        配置和管理安全策略
                    </p>
                </div>
                {action && (
                    <div className="flex items-center">
                        {action}
                    </div>
                )}
            </div>
            <Separator className="bg-border/50" />
        </div>
    );
}

export default Header;