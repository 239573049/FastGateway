import { Divider, Switch, Tag } from '@douyinfe/semi-ui'
import './index.css'
import { useEffect, useState } from 'react';
import * as echarts from 'echarts';
import { getQpsChart } from '../../services/QpsService';
import { DayRequestCount, GetStatisticTimeCount, GetLocation, TotalRequestCount } from '../../services/StatisticRequestService';
import { bytesToSize } from '../../utils/data-util';
import Map from './features/map';
import MapList from './features/map-list';


export default function DataStatistics() {
  const [qps, setQps] = useState(0)
  const [locationData, setLocationData] = useState([] as any[]);
  const [dayRequestCount, setDayRequestCount] = useState({
    requestCount: 0,
    error4xxCount: 0,
    error5xxCount: 0,
  })
  const [totalRequestCount, setTotalRequestCount] = useState({
    requestCount: 0,
    error4xxCount: 0,
    error5xxCount: 0,
  })

  const [netWork, setNetWork] = useState({
    upload: 0,
    download: 0
  })
  const [isGlobal, setIsGlobal] = useState(true)
  const [timeCount, setTimeCount] = useState([] as any[])
  const [qps_chartData] = useState({
    tooltip: {
      trigger: 'axis',
      axisPointer: {
        type: "shadow"
      }
    },
    //图表离容器的距离
    grid: {
      left: 0,
      top: 0,
      right: 0,
      bottom: 0
    },
    //X轴
    xAxis: {
      type: "category",
      data: ['0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0'],
      axisLabel: {
        show: false
      },
      axisTick: {
        show: false //隐藏X轴刻度尺
      },
      axisLine: {
        show: false
      }
    },
    //y轴
    yAxis: {
      min: 0,
      max: 10,
      axisLabel: {
        show: true
      },
      splitLine: {
        show: false
      }
    },
    series: [{
      data: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      name: 'QPS',
      type: 'bar',
      barMinHeight: 5,
      itemStyle: {
        barBorderRadius: [3, 3, 3, 3],
        color: "#42C4C1"
      }
    }]
  });

  var qps_chart: any;
  useEffect(() => {

    qps_chart = echarts.init(document.getElementById('qps_chart'));
    // 将qps_chartData.series[0].data中的数据全部替换为0
    qps_chartData.series[0].data = qps_chartData.series[0].data.map(() => 0);
    // 将qps_chartData.xAxis.data中的数据全部替换为0
    qps_chartData.xAxis.data = qps_chartData.xAxis.data.map(() => '0:00:00');

    // 等待DOM加载完成
    setTimeout(() => {
      qps_chart.setOption(qps_chartData);
    }, 0);

    const resizeHandler = () => {
      qps_chart.resize();
    };
    window.addEventListener('resize', resizeHandler);

    async function stream() {
      const response = await getQpsChart('') as any;
      for await (const data of response) {
        qps_chartData.xAxis.data.shift();
        qps_chartData.xAxis.data.push(data.now);
        qps_chartData.series[0].data.shift();
        qps_chartData.series[0].data.push(data.qps);
        setQps(data.qps);
        setNetWork({
          upload: data.upload,
          download: data.download
        })
        qps_chart?.setOption(qps_chartData);
      }
    }


    DayRequestCount().then((res) => {
      setDayRequestCount(res.data)
    })

    TotalRequestCount().then((res) => {
      setTotalRequestCount(res.data)
    })

    stream()

    const intervalId = setInterval(stream, 10900);

    loading()

    loadDayStatisticLocationCount()


    return () => {
      clearInterval(intervalId);
      window.removeEventListener('resize', resizeHandler);
    }

  }, []);

  function loadDayStatisticLocationCount() {
    GetStatisticTimeCount()
      .then((res) => {
        setTimeCount(res)

          let option = {
            grid: {
              top: '30%',
              left: '3%',
              right: '4%',
              bottom: '3%',
            },
            tooltip: {
              trigger: 'axis',
            },
            xAxis: [
              {
                type: 'category',
                boundaryGap: false,
                axisLabel: {
            formatter: '{value}',
            fontSize: 12,
                },
                axisTick: {
            show: false,
                },
                data: res.map((item: { time: any; }) => item.time),
                axisLine: {
            show: false,
                },
              },
            ],
            yAxis: [
              {
                boundaryGap: false,
                type: 'value',
                axisLabel: {
            textStyle: {
              color: '#7ec7ff',
            },
                },
                nameTextStyle: {
            color: '#fff',
            fontSize: 12,
            lineHeight: 40,
                },
                axisLine: {
            show: false,
            lineStyle: {
              color: '#283352',
            },
                },
                axisTick: {
            show: false,
                },
                splitLine: {
            show: false,
                },
              },
            ],
            series: [
              {
                name: '访问数量',
                type: 'line',
                smooth: true,
                showSymbol: false,
                itemStyle: {
            color: '#19a3df',
            borderColor: '#a3c8d8',
                },
                data: res.map((item: { count: any; }) => item.count),
              },
            ],
          };

        let timeCount = echarts.init(document.getElementById('timeCount'));
        timeCount.setOption(option);

      })
  }

  function loading() {
    GetLocation()
      .then((res) => {
        setLocationData(res)
      }).catch((err) => {
        console.log(err)
      })
  }


  return (
    <div className="dashboard">
      <div className="dashboard-header">
      </div>
      <div className="dashboard-content">
        <div className="stat-block" style={{}}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天请求总量 <span>{dayRequestCount.requestCount}</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天4xx错误数量 <span>{dayRequestCount.error4xxCount}</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天5xx错误数量 <span>{dayRequestCount.error5xxCount}</span>
          </div>
        </div>
        <div className="stat-block" style={{}}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            请求总量 <span>{totalRequestCount.requestCount}</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            4xx错误数量 <span>{totalRequestCount.error4xxCount}</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            5xx错误数量 <span>{totalRequestCount.error5xxCount}</span>
          </div>
        </div>
        <div className="stat-block" style={{ gridColumn: 'span 2' }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            marginBottom: '3px'
          }}>
            实时QPS
            <span>
              <Tag style={{
                marginRight: '10px'
              }} color='green' >
                上传{bytesToSize(netWork.upload)}
              </Tag>
              <Tag style={{
                marginRight: '10px'
              }} color='blue' >
                下载{bytesToSize(netWork.download)}
              </Tag>
              <Tag color='green' >{qps}</Tag>
            </span>
          </div>
          <div
            id='qps_chart'
            style={{
              width: '100%',
              height: 'calc(100% - 5px)',
            }}
          >
          </div>
        </div>
        <div className="stat-block large" style={{
          gridColumn: 'span 3'
        }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            widows: '100%',
            lineHeight: '50px',
            overflow: 'hidden'
          }}>
            30天访问来源地区
            <span style={{
              display: 'flex',
              alignItems: 'center'
            }}>
              {isGlobal ? '世界' : '中国'}
            </span>
            <Switch
              style={{
                marginRight: '10px'
              }}
              size='large'
              defaultChecked={isGlobal} onChange={(v) =>
                setIsGlobal(v)
              } ></Switch>
          </div>
          <Divider />
          <div style={{
            justifyContent: 'space-between',
            lineHeight: '50px',
            maxHeight: 'calc(100% - 60px)',
            overflowX: 'hidden',
            overflowY: 'auto',
            userSelect: 'none',
            scrollbarWidth: 'thin',
            scrollbarColor: 'transparent transparent',
            display: 'flex',

          }}>
            <Map isGlobal={isGlobal} data={locationData} />
            <MapList isGlobal={isGlobal} data={locationData} />
          </div>
        </div>
        <div className="stat-block " style={{}}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px',
            overflow: 'hidden'
          }}>
            访问情况
          </div>
          <div id='timeCount' style={{
            width: '100%',
            height: 'calc(100% - 50px)',
          }}>

          </div>
        </div>
      </div>
    </div>
  );
}