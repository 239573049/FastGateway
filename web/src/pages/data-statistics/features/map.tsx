import { useEffect } from 'react';
import china from '../../../assets/map/china.json';
import world from '../../../assets/map/world.json';
import worlds from '../../../assets/map/worlds.json';
import * as echarts from 'echarts';

interface IDataStatisticsProps {
    data: any[];
    isGlobal: boolean;
}

export default function Map(
    {
        data,
        isGlobal
    }: IDataStatisticsProps
) {
    useEffect(() => {
        echarts.registerMap('china', china as any);
        echarts.registerMap('world', worlds as any);
    }, [])

    useEffect(() => {
        
        let list; 
        if(isGlobal){
            list = data.reduce((arr, cur) => {
                let hasValue = arr.findIndex((a: { name: any; }) => a.name === cur.country);
                hasValue === -1 && arr.push({ name: cur.country, value: cur.count });
                hasValue !== -1 && (arr[hasValue].value = arr[hasValue].value + cur.count);
                return arr;
            }, []);

            
        }else{
            list = data.filter((item) => item.country === '中国').reduce((arr, cur) => {
                let hasValue = arr.findIndex((a: { name: any; }) => a.name === cur.country);
                hasValue === -1 && arr.push({ name: cur.province, value: cur.count });
                hasValue !== -1 && (arr[hasValue].value = arr[hasValue].value + cur.count);
                return arr;
            }, []);
        }

        let option = {
            grid: {
                width: "100%",
                height: "100%",
                left: "0%",
                right: "0%",
                bottom: "0%",
                containLabel: true,
            },
            // 提示框组件
            tooltip: {
                triggerOn: "mousemove",
                formatter: function (e: any) {
                    return e.seriesName + "<br />" + e.name + "：" + (e.value || 0);
                },
            },
            visualMap: {
                type: "continuous",
                text: ["", ""],
                min: 0,
                max: 10000000,
                showLabel: true,
                inRange: {
                    color: ["#B6C7FE", "#68adee", "#1395ec", "#0099ff", "#00497a"],
                },
                splitNumber: 0,
            },
            series: [
                {
                    type: "map",
                    name: "访问量",
                    map: isGlobal ? "world" : "china",
                    roam: true,
                    label: {
                        show: isGlobal
                    },
                    zoom: isGlobal ? 1.7 : 1.2,
                    top: isGlobal ? 120 : 50,
                    itemStyle: {
                        borderWidth: 0.5,
                        borderColor: "#000",
                        borderType: "solid",
                    },
                    emphasis: {
                        label: {
                            show: true,
                            color: "#fff"
                        },
                        itemStyle: {
                            areaColor: "#0b66ff"
                        },
                    },
                    nameMap: world.namemap,
                    data: list,
                },
            ],
        };
        let myEcharts = echarts.init(document.getElementById('map'));

        myEcharts.setOption(option);

        window.addEventListener('resize', () => {
            myEcharts.resize();
        });

        return () => {
            myEcharts.dispose();
        }

    }, [isGlobal,data])

    return (<div style={{
        height: '500px',
        flex: 3,
    }} id='map'>
    </div>)
}