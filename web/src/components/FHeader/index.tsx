
import Divider from '@lobehub/ui/es/Form/components/FormDivider';
import { Flexbox } from 'react-layout-kit';

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
            <Flexbox
                horizontal
                style={{
                    width: '100%',
                }}
            >
                <h1 style={{
                    flex: 1,
                }}>{title}</h1>
                {/* 将action显示在右侧 */}
                <Flexbox style={{
                    marginRight: '20px',
                }}>
                    {action}
                </Flexbox>
            </Flexbox>
            <Divider />
        </>
    );
}

export default FHeader;