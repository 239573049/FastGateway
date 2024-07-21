import { Result } from 'antd';
import { memo } from 'react';

const NotFoundPage = memo(() => {
  return (
    <Result
      status="404"
      title="404"
      subTitle="抱歉，您访问的页面不存在。"
      style={{
        paddingTop: '10%', // 页面内容向下偏移，以便居中显示
        height: '100vh', // 高度设置为视窗的100%，确保背景色填满整个屏幕
        boxSizing: 'border-box'
      }}
    />
  );
});

export default NotFoundPage;