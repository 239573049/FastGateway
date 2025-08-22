import React from 'react';
import { Checkbox } from '@/components/ui/checkbox';
import { Label } from '@/components/ui/label';
import { Tooltip, TooltipContent, TooltipTrigger, TooltipProvider } from "@/components/ui/tooltip";

interface LabeledCheckboxProps {
    checked?: boolean;
    onCheckedChange?: (checked: boolean) => void;
    onChange?: (e: { target: any }) => void;
    disabled?: boolean;
    label: string | React.ReactNode;
    tooltip?: string;
    id?: string;
    style?: React.CSSProperties;
}

const FCheckbox: React.FC<LabeledCheckboxProps> = ({ label, tooltip, id, style, onChange, onCheckedChange, ...rest }) => {
    const checkboxId = id || `checkbox-${Math.random().toString(36).substr(2, 9)}`;
    
    const handleCheckedChange = (checked: boolean) => {
        if (onCheckedChange) {
            onCheckedChange(checked);
        }
        if (onChange) {
            onChange({ target: { checked } });
        }
    };
    
    return (
        <TooltipProvider>
            <div className="flex items-center space-x-2" style={style}>
                <Checkbox id={checkboxId} onCheckedChange={handleCheckedChange} {...rest} />
                <Label htmlFor={checkboxId}>
                    {tooltip ? (
                        <Tooltip>
                            <TooltipTrigger asChild>
                                <span>{label}</span>
                            </TooltipTrigger>
                            <TooltipContent>
                                <p>{tooltip}</p>
                            </TooltipContent>
                        </Tooltip>
                    ) : label}
                </Label>
            </div>
        </TooltipProvider>
    );
};

export default FCheckbox;