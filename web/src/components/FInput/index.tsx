import React from 'react';
import { Input } from "@/components/ui/input";
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from "@/components/ui/tooltip";

interface LabeledInputProps extends React.ComponentPropsWithoutRef<typeof Input> {
    label: string | React.ReactNode;
    tooltip?: string;
    layoutStyle?: React.CSSProperties;
    suffix?: React.ReactNode;
}

const FInput: React.FC<LabeledInputProps> = ({ label, tooltip, layoutStyle, suffix, ...rest }) => {

    const renderLabel = () => {
        if (typeof label === 'string') {
            return <span style={{
                fontSize: '14px',
            }}>{label}</span>;
        }
        return label;
    };

    return (
        <TooltipProvider>
            <div style={{ alignItems: 'center', ...layoutStyle }}>
                <label>
                    {tooltip ? (
                        <Tooltip>
                            <TooltipTrigger asChild>
                                {renderLabel()}
                            </TooltipTrigger>
                            <TooltipContent>
                                <p>{tooltip}</p>
                            </TooltipContent>
                        </Tooltip>
                    ) : renderLabel()}
                </label>
                <div className="flex items-center">
                    <Input {...rest} />
                    {suffix && <div className="ml-2">{suffix}</div>}
                </div>
            </div>
        </TooltipProvider>
    );
};

export default FInput;