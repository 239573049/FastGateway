import { Divider, Select, Tag } from '@douyinfe/semi-ui'
import './index.css'
import { useEffect, useState } from 'react';
import * as echarts from 'echarts';
import { GetSelectList } from '../../services/ApiServiceService';
import { getQpsChart } from '../../services/QpsService';

export default function DataStatistics() {
  const [qps, setQps] = useState(0)
  const [serviceOptions, setServiceOptions] = useState([])
  const [serviceId, setServiceId] = useState('')
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
      data: ['0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0', '0'],
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
      data: [0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
      name: 'QPS',
      type: 'bar',
      barMinHeight: 5,
      itemStyle: {
        barBorderRadius: [3, 3, 3, 3],
        color: "#42C4C1"
      }
    }]
  });

  let qps_service: string;
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

    getSelectList();

    return () => {
      window.removeEventListener('resize', resizeHandler);
    }

  }, []);

  function getSelectList() {
    GetSelectList()
      .then(res => {
        if (res.success) {
          res.data.push({
            value: '',
            label: '全部服务'
          })
          setServiceOptions(res.data);
        }
      })

  }

  useEffect(() => {
    // 定时任务，10s一次

    async function stream() {
      const response = await getQpsChart(serviceId) as any;
      for await (const data of response) {
        qps_chartData.xAxis.data.shift();
        qps_chartData.xAxis.data.push(data.now);
        qps_chartData.series[0].data.shift();
        qps_chartData.series[0].data.push(data.qps);
        setQps(data.qps);
        qps_chart?.setOption(qps_chartData);
      }
    }

    stream()

    const intervalId = setInterval(stream, 10900);
    return () => {
      clearInterval(intervalId);
    }

  }, [serviceId]);
  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <Select
          style={{
            position: 'relative',
            float: 'right',
            marginRight: '20px',
            width: '200px',
            marginTop: '10px',
          }}
          defaultValue={serviceId}
          onChange={(value) => {
            qps_service = value as string;
            setServiceId(value as string)
          }}
          value={serviceId}
          optionList={serviceOptions}
        />
      </div>
      <div className="dashboard-content">
        <div className="stat-block" style={{ backgroundColor: '#333' }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天请求总量 <span>0</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天4xx错误数量 <span>0 </span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            今天5xx错误数量 <span>0</span>
          </div>
        </div>
        <div className="stat-block" style={{ backgroundColor: '#333' }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            请求总量 <span>0</span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            4xx错误数量 <span>0 </span>
          </div>
          <Divider />
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            lineHeight: '50px'
          }}>
            5xx错误数量 <span>0</span>
          </div>
        </div>
        <div className="stat-block" style={{ backgroundColor: '#666', gridColumn: 'span 2' }}>
          <div style={{
            display: 'flex',
            justifyContent: 'space-between',
            marginBottom: '3px'
          }}>
            实时QPS <Tag color='green' >{qps}</Tag>
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
        <div className="stat-block large" style={{ backgroundColor: '#777' }}>Block 5</div>
        <div className="stat-block" style={{ backgroundColor: '#888' }}>Block 6</div>
        <div className="stat-block" style={{ backgroundColor: '#999' }}>Block 7</div>
      </div>
    </div>
  );
}