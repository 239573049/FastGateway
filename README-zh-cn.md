# FastGateway 管理端

FastGateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

-----
文档语言: [English](README.md) | [简体中文](README.zh-cn.md)

## 支持功能

- [x] 登录授权
- [x] 自动申请HTTPS证书
- [x] 自动续期HTTPS证书
- [x] dashboard监控
- [x] 静态文件服务
- [x] 单服务代理
- [x] 集群代理
- [x] 请求来源分析
- [x] 支持yaml导入导出
- [x] 支持自定义限流策略
- [x] 支持黑白名单

## 技术栈

### 后端技术栈

- Asp.Net 8.0 用于提供基础服务
- Yarp 用于提供反向代理服务
- FreeSql用于提供数据库服务
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
      - ./certs:/app/certs/
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
      - ./certs:/app/certs/
```

## Linux使用`systemd`启动服务

下载Linux压缩包，然后解压程序，使用nano创建`fastgateway.service`

```shell
nano /etc/systemd/system/fastgateway.service
```

填写以下内容的时候记得替换配置

```tex
[Unit]
Description=FastGateway

[Service]
WorkingDirectory=你解压的目录
ExecStart=/usr/bin/dotnet 你解压的目录/FastGateway.dll
Restart=always
# Restart service after 10 seconds if the dotnet service crashes:
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=dotnet-fastgateway
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

接下来，重新加载 systemd 以使新的服务单元文件生效：

```shell
systemctl daemon-reload
```

现在你可以启动服务了：

```shell
systemctl start fastgateway.service
```

要使服务在系统启动时自动启动，请启用它：

```shell
systemctl enable fastgateway.service
```

你可以使用下命令检查服务的状态：

```shell
systemctl status fastgateway.service
```

如果你需要停止服务，可以使用：

```shell
systemctl stop fastgateway.service
```

如果你对服务做了更改并需要重新加载配置，可以重新启动服务：

```shell
systemctl restart fastgateway.service
```

## 镜像列表

- 默认镜像：registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0
- 提供HTTP3镜像：registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0-h3
- ARM64镜像：registry.cn-shenzhen.aliyuncs.com/tokengo/gateway-api:v1.0.0-arm64

## 第三方下载

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) 用于ip离线归属地
