import { Divider } from '@douyinfe/semi-ui'
import './index.css'
import { VChart } from "@visactor/react-vchart";

export default function DataStatistics() {

  const spec = {
    type: 'bar',
    data: [
      {
        id: 'barData',
        values: [
          { month: '01', sales: 0 },
          { month: '02', sales: 1 },
          { month: '03', sales: 2 },
          { month: '04', sales: 5 },
          { month: '05', sales: 1 }
        ]
      }
    ],
    xField: 'month',
    yField: 'sales'
  };

  return (
    <div className="dashboard">
      <div className="dashboard-header">
        <h1>数据统计</h1>
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
          QPS

          <VChart
            spec={{
              ...spec,
            }}
          />
        </div>
        <div className="stat-block large" style={{ backgroundColor: '#777' }}>Block 5</div>
        <div className="stat-block" style={{ backgroundColor: '#888' }}>Block 6</div>
        <div className="stat-block" style={{ backgroundColor: '#999' }}>Block 7</div>
        <div className="stat-block" style={{ backgroundColor: '#aaa' }}>Block 8</div>
      </div>
    </div>
  );
}