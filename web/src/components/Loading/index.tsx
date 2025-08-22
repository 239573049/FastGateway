import { Loader2 } from 'lucide-react';
import { memo } from 'react';
import { Center, Flexbox } from 'react-layout-kit';

const Loading = memo(() => {
    return <Flexbox height={'100%'} style={{ userSelect: 'none' }} width={'100%'}>
        <Center flex={1} gap={12} width={'100%'}>
            <span style={{
                fontSize: '40px',
                fontWeight: 'bold',
                fontFamily: 'Arial, sans-serif',
                userSelect: 'none',
            }}>
                Fast Gateway
            </span>
            <Center gap={16} horizontal>
                <Loader2 className="h-4 w-4 animate-spin" />
                加载中...
            </Center>
        </Center>
    </Flexbox>;
})

export default Loading;