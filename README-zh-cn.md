# FastGateway 管理端

FastGateway提供了基本的管理服务，提供简单的登录授权，和实时配置管理，从而实现动态路由的管理。

-----
文档语言: [English](README.md) | [简体中文](README-zh-cn.md)

## 支持功能

- [x] 登录授权
- [x] 自动申请HTTPS证书（Let's Encrypt / HTTP-01）
- [x] 自动续期HTTPS证书
- [x] 泛域名证书（Let's Encrypt / DNS-01）
- [x] 上传自定义HTTPS证书（PFX / PEM）
- [x] dashboard监控
- [x] 静态文件服务
- [x] 单服务代理
- [x] 集群代理
- [x] 请求来源分析
- [x] 支持yaml导入导出
- [x] 支持自定义限流策略
- [x] 支持黑白名单

## HTTPS 证书管理

FastGateway 在「证书管理」页面提供三种方式为域名配置 HTTPS 证书：

1. **自动申请（Let's Encrypt，HTTP-01）** — 对于普通域名（如 `example.com`），填写域名与邮箱后点击「申请证书」，网关会自动完成 ACME HTTP-01 验证（需要开启 80 端口服务），并在证书临期前自动续期。

2. **泛域名证书（Let's Encrypt，DNS-01）** — 泛域名（如 `*.example.com`）无法使用 HTTP-01 验证。新增域名后点击「DNS 验证」，网关会生成一条 `_acme-challenge` TXT 记录，请将其添加到域名解析服务商；记录生效后点击「验证并签发」即可完成签发。此方式签发的泛域名证书不会自动续期，到期前需再次通过 DNS 验证。

3. **上传自定义证书** — 点击「上传证书」使用你已有的证书，支持两种格式：
   - **PFX / P12** — 上传 `.pfx`/`.p12` 文件及其密码（无密码可留空）。
   - **PEM / CRT** — 分别上传证书文件（`.pem`/`.crt`）与私钥文件（`.key`）。

   上传的证书（包括泛域名）按 SNI 匹配，不参与自动续期，到期前请重新上传。上传的证书会统一转换为 `.pfx` 保存在 `certs` 目录下。

## 技术栈

### 后端技术栈

- .NET 10 用于提供基础服务
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
docker run -d --restart=always --name=gateway-api -e PASSWORD=Aa123456 -p 8080:8080 -p 80:80 -p 443:443 -v $(pwd)/data:/data/ aidotnet/fast-gateway
```

## Docker-Compose文件

```yml


﻿services:
  fast-gateway:
    image: aidotnet/fast-gateway
    container_name: fast-gateway
    restart: always
    volumes:
      - ./data:/app/data
      - ./certs:/app/certs
    ports:
        - "8000:8080"
        - "80:80"
        - "443:443"
```

如果并没有提供密码则默认

密码：Aa123456

## 支持HTTP3的docker-compose

```yml

﻿services:
  fast-gateway:
    image: aidotnet/fast-gateway
    container_name: fast-gateway
    restart: always
    volumes:
      - ./data:/app/data
      - ./certs:/app/certs
    ports:
        - "8000:8080"
        - "80:80"
        - "443:443/udp"
        - "443:443/tcp"
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

## 下载

[Releases](../../releases) 页面提供以下平台的自包含（self-contained）预编译包：

| 操作系统 | x64 | ARM64 |
| --- | --- | --- |
| Linux | `fastgateway-linux-x64.tar.gz` | `fastgateway-linux-arm64.tar.gz` |
| Windows | `fastgateway-win-x64.zip` | `fastgateway-win-arm64.zip` |
| macOS | `fastgateway-osx-x64.tar.gz` | `fastgateway-osx-arm64.tar.gz` |

`TunnelClient` 组件同样提供上述所有平台的压缩包（`tunnelclient-<runtime>`）。

Docker 镜像 `aidotnet/fast-gateway` 为多架构镜像，同时支持 `linux/amd64` 与 `linux/arm64`，`docker run`/`docker compose` 会自动拉取与主机匹配的版本。

## 第三方下载

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) 用于ip离线归属地
