import React from 'react';
import { Input, Tooltip } from "@lobehub/ui";

interface LabeledInputProps extends React.ComponentPropsWithoutRef<typeof Input> {
    label: string | React.ReactNode;
    tooltip?: string;
    layoutStyle?: React.CSSProperties;
}

const FInput: React.FC<LabeledInputProps> = ({ label, tooltip, layoutStyle, ...rest }) => {

    const renderLabel = () => {
        if (typeof label === 'string') {
            return <span style={{
                fontSize: '14px',
            }}>{label}</span>;
        }
        return label;
    };

    return (
        <div style={{ alignItems: 'center', ...layoutStyle }}>
            <label>
                {tooltip ? <Tooltip
                    title={tooltip}>
                    {renderLabel()}
                </Tooltip> : renderLabel()}
            </label>
            <Input {...rest} />
        </div>
    );
};

export default FInput;