import React from 'react';
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from "@/components/ui/tooltip";

interface LabeledInputProps extends React.ComponentPropsWithoutRef<typeof Input> {
    label: string | React.ReactNode;
    tooltip?: string;
    suffix?: React.ReactNode;
}

const FInput: React.FC<LabeledInputProps> = ({ label, tooltip, suffix, ...rest }) => {

    const renderLabel = () => {
        if (typeof label === 'string') {
            return <Label className="text-sm font-medium">{label}</Label>;
        }
        return label;
    };

    return (
        <TooltipProvider>
            <div className="space-y-2">
                {tooltip ? (
                    <Tooltip>
                        <TooltipTrigger asChild>
                            {renderLabel()}
                        </TooltipTrigger>
                        <TooltipContent>
                            <p className="text-sm">{tooltip}</p>
                        </TooltipContent>
                    </Tooltip>
                ) : renderLabel()}
                <div className="flex items-center space-x-2">
                    <Input {...rest} />
                    {suffix}
                </div>
            </div>
        </TooltipProvider>
    );
};

export default FInput;