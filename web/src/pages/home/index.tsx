import {Panel} from '../../service/RequestLogService';
import {useEffect, useState} from "react";
import { VChart } from "@visactor/react-vchart";

export default function Home() {
    const [hours, setHours] = useState<number>(24);
    const [data, setData] = useState<any>({
        spec: {

            type: 'area',
            data: {
                values: [
                ]
            },
            xField: 'time',
            yField: 'value'
        }
    });
    async function loadingPanel() {
        const response = await Panel(hours);
        console.log(response);
        setData({
            spec: {
                type: 'area',
                data: {
                    values: response
                },
                xField: 'time',
                yField: 'value'
            }
        });
    }

    useEffect(() => {
        loadingPanel();
    }, [hours]);

    return (
        <div className="App">
            <h2>24小时请求记录</h2>
            <VChart
                spec={{
                    height: 200,
                    width: 500,
                    ...data.spec,
                }}
            />
        </div>
    );
}