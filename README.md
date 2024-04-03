# FastGateway 管理端

FastGateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

## 支持功能

- [x] 登录授权
- [x] 自动申请HTTPS证书
- [x] 自动续期HTTPS证书
- [x] dashboard监控
- [x] 静态文件服务
- [x] 单服务代理
- [x] 集群代理
- [x] 请求来源分析

## 技术栈

### 后端技术栈

- Asp.Net 8.0 用于提供基础服务
- Yarp 用于提供反向代理服务
- EntityFrameworkCore用于提供数据库服务
- JWT 用于提供登录授权服务
- MiniApis 提供WebApi服务

### 前端技术栈

- reset-css 用于重置浏览器默认样式
- semi 用于提供基础组件
- react-router-dom 用于路由管理

## 快速运行Gateway

```bash
docker run -d --restart=always --name=gateway-api -e PASSWORD=Aa123456 -p 8080:8080 -p 80:80 -p 443:443 -v $(pwd)/data:/data/ registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0
```

## Docker-Compose文件

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0
    restart: always
    container_name: gateway-api
    environment:
      PASSWORD: Aa123456
    ports:
      - 8080:8080 # 提供给web端调用的管理接口
      - 80:80 # Http代理端口
      - 443:443 # Https代理端口
    volumes:
      - ./data:/data/
```

如果并没有提供密码则默认

密码：Aa123456

## 支持HTTP3的docker-compose

```yml

services:
  gateway-api:
    image: registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0-h3
    restart: always
    container_name: gateway-api
    environment:
      PASSWORD: Aa123456
    ports:
      - 8080:8080 # web管理端
      - 80:80 # Http代理端口
      - 443:443/udp # Https代理端口
      - 443:443/tcp # Https代理端口 Http3需要开启UDP和TCP，请注意防火墙设置是否允许
    volumes:
      - ./data:/data/
```



## 第三方下载

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) 用于ip离线归属地
