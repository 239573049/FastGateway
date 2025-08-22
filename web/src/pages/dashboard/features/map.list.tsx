import { useEffect } from "react";
import * as echarts from "echarts";

interface IDataStatisticsProps {
    data: any[];
    isGlobal: boolean;
}

export default function MapList({
    data,
    isGlobal
}: IDataStatisticsProps) {
    useEffect(() => {
        let value = [] as any[];
        
        if(isGlobal) {
            // 循环data数组，将国家相同的count相加
            for(let i = 0; i < data.length; i++) {
                const hasValue = value.findIndex((a: { country: any; }) => a.country === data[i].country);
                hasValue === -1 && value.push({ province: data[i].country, count: data[i].count, ratio: data[i].ratio, country: data[i].country });
                hasValue !== -1 && (value[hasValue].count = value[hasValue].count + data[i].count);
            }

            // 只要前10个并且按照count排序
            value = value.sort((a, b) => b.count - a.count).slice(0, 10);
            

        }else{

            // 将数据合并

            // 只要中国
            data = data.filter((item) => item.country === '中国');

            value = data.reduce((arr, cur) => {
                const hasValue = arr.findIndex((a: { province: any; }) => a.province === cur.province);
                hasValue === -1 && arr.push({ province: cur.province, count: cur.count, ratio: cur.ratio, country: cur.country });
                hasValue !== -1 && (arr[hasValue].count = arr[hasValue].count + cur.count);
                return arr;
            }, []);

            // 然后按照count排序
            value = value.sort((a, b) => b.count - a.count)
                .slice(0, 10);
        }

        const mapListOptions = {
            grid: {
                top: "10",
                left: "20",
                right: "10",
                bottom: "10",
            },
            yAxis: [
                // 图标
                {
                    type: "category",
                    data: value.map((item: any) => item.province),
                    inverse: true,
                    axisLabel: {
                        show: false,
                    },
                    axisLine: {
                        show: false,
                    },
                    axisTick: {
                        show: false,
                    },
                    splitLine: {
                        show: false,
                    },
                },
                // 文字
                {
                    gridIndex: 0,
                    splitLine: "none",
                    axisTick: "none",
                    axisLine: "none",
                    data: value.map((item) => item.count),
                    inverse: true,
                    axisLabel: {
                        show: true,
                        verticalAlign: "bottom",
                        align: "right",
                        padding: 10,
                        formatter: function (value: string) {
                            return "{x|" + value + "}";
                        },
                        rich: {
                            x: {
                                color: "#42C4C1",
                                fontSize: 12
                            },
                        },
                    },
                },
            ],
            xAxis: {
                type: "value",

                axisTick: {
                    show: false,
                },
                axisLine: {
                    show: false,
                },
                splitLine: {
                    show: false,
                },
                axisLabel: {
                    show: false,
                },
            },

            series: [
                {
                    name: "",
                    type: "bar",
                    zlevel: 2,
                    barWidth: 4,
                    itemStyle: {
                        borderRadius: 10,
                        color: "#42C4C1",
                    },
                    data: value.map(item => {
                        return { name: item.province, value: item.count }
                    }),
                    label: {
                        show: true,
                        position: [0, -20],
                        fontSize: 12,
                        color: "#42C4C1",
                        rich: {
                            a: {
                                fontSize: 12,
                            },
                        },
                        formatter: function (a: any) {
                            return "{a|" + a.name + "}";
                        }
                    },
                },
                {
                    // 背景
                    type: "bar",
                    barGap: "-100%",
                    barWidth: 4,
                    itemStyle: {
                        color: "#e3e8ef"
                    },
                    data: value.map((item) => (item.count / (item.ratio / 100)))
                },
            ],
        };

        const myEchartsList = echarts.init(document.getElementById('map-list'));

        myEchartsList.setOption(mapListOptions);

        // 监听窗口变化
        window.addEventListener('resize', () => {
            myEchartsList.resize();
        });

        return () => {
            myEchartsList.dispose();
        }
    }, [
        data,isGlobal
    ])

    return (
        <div 
            id="map-list" 
            className="h-[400px] lg:h-[500px] w-full rounded-lg border bg-card/50"
        />
    )
}