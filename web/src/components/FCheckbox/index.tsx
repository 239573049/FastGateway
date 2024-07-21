import { Checkbox } from 'antd';

import React from 'react';
import { Tooltip } from "@lobehub/ui";

interface LabeledCheckboxProps extends React.ComponentPropsWithoutRef<typeof Checkbox> {
    label: string | React.ReactNode;
    tooltip?: string;
}

const FCheckbox: React.FC<LabeledCheckboxProps> = ({ label, tooltip, ...rest }) => {
    return (tooltip ?
        <Checkbox {...rest}>
            <Tooltip title={tooltip}>
                {label}
            </Tooltip>
        </Checkbox> : <Checkbox {...rest}>{label}</Checkbox>
    );
};

export default FCheckbox;