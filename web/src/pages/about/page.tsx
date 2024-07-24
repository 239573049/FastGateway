import { Typography } from 'antd';
const { Title, Paragraph } = Typography;

export default function AboutPage() {
  return (
    <div style={{ margin: '20px', display: 'flex', justifyContent: 'center' }}>

      <Typography>
        <Title level={2}>关于 FastGateway</Title>
        <Paragraph>
          FastGateway 是一个基于 Yarp+.NET 9+EFCore 实现的网关系统。由 Token 研发，是一个免费开源的项目。
        </Paragraph>
        <Paragraph>
          该项目旨在提供一个高性能、易于配置的API网关解决方案，帮助开发者快速部署和管理微服务架构下的服务。
        </Paragraph>
        <Title level={3}>
          贡献者
        </Title>
        <Paragraph>
          <a href="https://github.com/239573049/FastGateway/graphs/contributors">
            <img src="https://contrib.rocks/image?repo=239573049/FastGateway" />
          </a>
        </Paragraph>
        <Title level={3}>版本信息</Title>
        <Paragraph>
          当前版本：2.0.0-dev
        </Paragraph>
      </Typography>
    </div>
  );
}