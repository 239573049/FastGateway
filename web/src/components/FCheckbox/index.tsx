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
    className?: string;
}

const FCheckbox: React.FC<LabeledCheckboxProps> = ({ label, tooltip, id, className, onChange, onCheckedChange, ...rest }) => {
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
            <div className={className}>
                <div className="flex items-center space-x-2">
                    <Checkbox id={checkboxId} onCheckedChange={handleCheckedChange} {...rest} />
                    <Label htmlFor={checkboxId} className="text-sm font-medium leading-none peer-disabled:cursor-not-allowed peer-disabled:opacity-70">
                        {tooltip ? (
                            <Tooltip>
                                <TooltipTrigger asChild>
                                    <span className="cursor-help underline decoration-dotted">{label}</span>
                                </TooltipTrigger>
                                <TooltipContent>
                                    <p className="text-sm">{tooltip}</p>
                                </TooltipContent>
                            </Tooltip>
                        ) : label}
                    </Label>
                </div>
            </div>
        </TooltipProvider>
    );
};

export default FCheckbox;