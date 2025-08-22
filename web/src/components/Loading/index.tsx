import { Loader2 } from 'lucide-react';
import { memo } from 'react';

const Loading = memo(() => {
    return (
        <div className="flex h-screen w-full items-center justify-center">
            <div className="flex flex-col items-center space-y-4">
                <h1 className="text-4xl font-bold tracking-tight">
                    Fast Gateway
                </h1>
                <div className="flex items-center space-x-2 text-muted-foreground">
                    <Loader2 className="h-4 w-4 animate-spin" />
                    <span className="text-sm">加载中...</span>
                </div>
            </div>
        </div>
    );
})

export default Loading;