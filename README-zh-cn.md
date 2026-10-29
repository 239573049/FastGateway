# FastGateway 管理端

<p align="center">
  <img src="docs/images/hero.jpg" alt="FastGateway 控制面" width="100%">
</p>

FastGateway 是一个自托管的反向代理与隧道网关，提供 JWT 登录授权、React 管理面板、动态 HTTP/HTTPS 网关、TCP/UDP 四层转发、通过 TunnelClient 访问内网服务，以及可选的主/从集群。

-----
文档语言: [English](README.md) | [简体中文](README-zh-cn.md)

版本说明见 [CHANGELOG.md](CHANGELOG.md)。

<p align="center">
  <img src="docs/images/dashboard.jpg" alt="FastGateway 管理面板" width="100%">
</p>

## 架构概览

FastGateway 是一个可单机部署的管理控制面与代理运行时。`src/FastGateway` 承载管理 API 和管理面板，并为每个启用的 `Server` 配置创建一个进程内 Kestrel/YARP 网关；`TunnelClient` 是面向内网服务的独立出站客户端。多台 FastGateway 可组成集群：主网关持有配置真相，从节点落盘并热更新，还可按路由把流量中继到指定访问节点。

```text
                                   ┌─────────────────────────────┐
 浏览器 / 客户端 ─HTTP(S)/H2/H3───►│ FastGateway                  │
                                   │ 管理宿主                     │
                                   │ JWT API + React 管理面板     │
                                   └──────────────┬──────────────┘
                                                  │
        ┌──────────────────────────┬──────────────┼──────────────────────────┐
        ▼                          ▼              ▼                          ▼
 每个 Server 的 Kestrel + YARP  隧道管理器     四层转发管理器              集群
 域名/路径路由                  HTTP/2 CONNECT  TCP / UDP / 两者           主 / 从
 单服务/集群/静态文件/          / WebSocket     上游节点池                 配置同步 +
 隧道 / 访问节点                ⇄ TunnelClient                             数据面中继
        │                          │              │                          │
        ▼                          ▼              ▼                          ▼
 HTTP 服务与静态文件            内网服务       TCP/UDP 服务               对端网关
```

### 源码目录

| 路径 | 职责 |
| --- | --- |
| `src/FastGateway` | ASP.NET Core 主宿主、JWT API、管理面板静态文件、动态 HTTP 网关、证书、访问控制、限流、统计、文件存储与隧道管理。 |
| `src/FastGateway/Gateway` | 按服务构建 YARP 路由/集群，以及 TCP/UDP `StreamProxyManager`。 |
| `src/FastGateway/Services` | Minimal API 端点组和基于配置的业务服务。 |
| `src/FastGateway/Middleware` | 客户端 IP 解析、统计、超时、访问控制、故障转移、代理错误和异常 IP 中间件。 |
| `src/FastGateway/Tunnels` | 服务端隧道注册、节点生命周期、控制通道和按请求创建的数据隧道。 |
| `src/FastGateway/Cluster` | 主/从集群：接入码加入、WebSocket 配置同步、MessagePack 协议与数据面中继。 |
| `src/TunnelClient` | 独立隧道客户端。读取 `tunnel.json`，通过 HTTP/2 或 WebSocket 连接网关，并通过 YARP 转发到本地服务。 |
| `src/Core` | 服务端与客户端共享的流包装器、网关实体和枚举。 |
| `src/Certes` | 内置的、无 Newtonsoft.Json/BouncyCastle 依赖且兼容 AOT 的 ACME 客户端，用于申请 Let's Encrypt 证书。 |
| `web` | React 19 + TypeScript + Vite 管理面板。Docker 和 Release 构建会把 `dist` 复制到 `src/FastGateway/wwwroot`。 |

### 运行链路

1. `Program` 初始化 `FastGatewayOptions`、JWT 认证、后台服务、`ConfigurationService` 和 `ClusterStateService`。
2. 配置服务加载 `data/gateway.config`（不存在时自动创建），配置变更通过原子替换写回磁盘。集群角色与节点成员关系保存在 `data/cluster.json`。
3. 每个启用的 `Server` 配置创建独立的 Kestrel/YARP 网关；每个启用的 `StreamForward` 配置由 `StreamProxyManager` 启动 TCP/UDP 监听。
4. 每条域名路由都会编译为内存中的 YARP 路由和集群，可指向单个服务、服务集群、本地静态文件目录，或已注册的隧道节点（`node_<name>`）。集群模式下还可指定访问节点，由其他网关中继而不是各自直连上游。
5. 网关中间件依次完成客户端 IP 解析、ACME HTTP-01 验证与 HTTPS 跳转、统计采集、超时/限流/黑名单控制、故障转移和错误处理，最后交给 YARP 转发。
6. 显式 reload API 和隧道注册会更新内存路由，无需重启进程。主网关上的配置变更会去抖动后推送给在线从节点。

### 隧道数据路径

<p align="center">
  <img src="docs/images/tunnel.jpg" alt="FastGateway 内网隧道" width="100%">
</p>

`TunnelClient` 使用 `-c <配置文件>` 启动，先调用 `/internal/gateway/Server/register` 注册节点，再维护到 `/internal/gateway/Server` 的控制连接。传输类型支持 `h2`（HTTP/2 CONNECT）和 `ws`（WebSocket），两者使用 `FastGateway` 子协议。当公网请求匹配隧道路由时，服务端分配隧道 ID，客户端建立对应数据流，双方以全双工方式将字节转发到本地服务。

### 管理 API

后端使用 ASP.NET Core Minimal API，主要分组包括 `/api/v1/authorization`、`/server`、`/domain`、`/cert`、`/tunnel`、`/stream-forward`、`/cluster`、`/black-and-white`、`/rate-limit`、`/abnormal-ip`、`/statistics`、`/qps`、`/filestorage`、`/setting` 和 `/system`。除登录接口外，大多数管理分组需要 `POST /api/v1/authorization` 签发的 JWT。集群加入/注册/同步端点使用接入码或节点令牌鉴权，不走 JWT。

### 运行时数据

以下路径均相对于程序目录；在 Docker 镜像中程序目录为 `/app`。

| 路径 | 用途 |
| --- | --- |
| `data/gateway.config` | 持久化网关、域名、证书、访问控制、限流、系统设置和四层转发配置。 |
| `data/cluster.json` | 集群角色、接入码和主/从节点成员关系。 |
| `data/stats.db` | 统计后台服务使用的 SQLite 请求统计数据库。 |
| `data/keys/` | 按邮箱缓存 ACME 账户密钥，用于证书续期。 |
| `certs/` | 自动生成或上传的 PFX 证书，按 SNI 选择。 |
| `gateway.pfx` | 没有匹配证书时使用的内置回退证书。 |
| `ip2region.xdb` | 用于流量分析的可选离线 IP 归属地数据库。 |

## 支持功能

- [x] 登录授权
- [x] 自动申请 HTTPS 证书（Let's Encrypt / HTTP-01）
- [x] 自动续期 HTTPS 证书
- [x] 泛域名证书（Let's Encrypt / DNS-01）
- [x] 上传自定义 HTTPS 证书（PFX / PEM）
- [x] dashboard 监控
- [x] 静态文件服务（ETag / Last-Modified，304）
- [x] 单服务代理
- [x] 集群代理
- [x] TCP/UDP 四层端口转发
- [x] 通过 TunnelClient 转发内网服务
- [x] 主/从集群：配置同步与流量中继
- [x] 路由级访问节点（由哪个集群成员访问上游）
- [x] 服务级客户端 IP 来源（`X-Forwarded-For` / `X-Real-IP` / `CF-Connecting-IP`）
- [x] 上游健康检查和请求级故障转移
- [x] 流量统计和 IP 归属地分析
- [x] 请求来源分析
- [x] 可配置请求日志保留时长（1 / 7 / 15 / 30 天）
- [x] 自定义限流策略（按 IP 固定窗口）
- [x] 黑白名单
- [x] 异常 IP 检测
- [x] 进程内文件管理（支持分片上传）

## 集群管理

<p align="center">
  <img src="docs/images/cluster.jpg" alt="FastGateway 主从集群" width="100%">
</p>

在管理面板打开「集群管理」。新实例默认为**独立运行**，集群为可选项，单节点行为与以往完全一致。

1. **创建主网关** — 在需要持有配置的网关上，填写从节点可访问的管理地址（例如 `https://gw-a.example.com:8080`），生成接入码。接入码 24 小时内有效。生成第一份接入码后，本节点升为主网关。

2. **加入为从节点** — 在另一台 FastGateway 上粘贴接入码，可选填写节点名称后加入。从节点向主网关注册，并维持 WebSocket 控制通道。主网关立即推送全量配置快照（服务、域名路由、黑白名单、限流策略、四层转发、证书及 PFX 文件）。之后在主网关上的修改会自动下发；从节点本地修改会在下次同步时被覆盖。

3. **访问节点** — 在域名路由上，主网关可指定由哪个成员访问上游：收到请求的节点（默认）、主网关，或某个从节点。请求落到非指定节点时自动中继（从节点 → 主网关走主网关业务端口；主网关 → 从节点走从节点出站的集群隧道；从节点 → 其他从节点经主网关两跳）。

4. **运维操作** — 主网关展示节点在线状态与已同步版本，可立即推送配置、移除节点或解散集群。从节点可主动退出，并保留最后一次同步的配置继续独立转发。从节点不参与 ACME 签发与续期，证书由主网关统一签发并随快照下发。

节点间协议为 WebSocket 二进制帧：1 字节版本号 + LZ4 压缩的 MessagePack，序列化由源生成器完成，兼容 Native AOT（零反射）。

## HTTPS 证书管理

FastGateway 在「证书管理」页面提供三种方式为域名配置 HTTPS 证书：

1. **自动申请（Let's Encrypt，HTTP-01）** — 对于普通域名（如 `example.com`），填写域名与邮箱后点击「申请证书」，网关会自动完成 ACME HTTP-01 验证（需要开启 80 端口服务），并在证书临期前自动续期。

2. **泛域名证书（Let's Encrypt，DNS-01）** — 泛域名（如 `*.example.com`）无法使用 HTTP-01 验证。新增域名后点击「DNS 验证」，网关会生成一条 `_acme-challenge` TXT 记录，请将其添加到域名解析服务商；记录生效后点击「验证并签发」即可完成签发。此方式签发的泛域名证书不会自动续期，到期前需再次通过 DNS 验证。

3. **上传自定义证书** — 点击「上传证书」使用你已有的证书，支持两种格式：
   - **PFX / P12** — 上传 `.pfx`/`.p12` 文件及其密码（无密码可留空）。
   - **PEM / CRT** — 分别上传证书文件（`.pem`/`.crt`）与私钥文件（`.key`）。

   上传的证书（包括泛域名）按 SNI 匹配，不参与自动续期，到期前请重新上传。上传的证书会统一转换为 `.pfx` 保存在 `certs` 目录下。

## 技术栈

### 后端

- .NET 10 和 ASP.NET Core Minimal API
- Kestrel，为配置的 HTTPS 网关提供 HTTP/1.1、HTTP/2 和 HTTP/3 支持
- YARP 2.3，用于反向代理路由、集群、健康检查和转发
- JWT Bearer，用于管理 API 登录授权
- JSON 文件持久化网关配置（`data/gateway.config`）和集群状态（`data/cluster.json`）
- Microsoft.Data.Sqlite，用于请求统计（`data/stats.db`）
- `System.Threading.RateLimiting`，按 IP 分区的固定窗口限流
- MessagePack（LZ4），用于兼容 AOT 的集群快照和证书文件传输
- Certes ACME 客户端，用于 Let's Encrypt HTTP-01/DNS-01 证书流程
- IP2Region.Net 和 `ip2region.xdb`，用于离线 IP 归属地分析

### 前端

- React 19、TypeScript 和 Vite
- React Router，用于管理面板路由和懒加载页面
- Ant Design 和 Radix UI，用于控件与交互基础组件
- Tailwind CSS v4，用于样式构建
- ECharts/Recharts，用于流量和仪表盘可视化

## 快速开始

容器使用 `8080` 提供管理面板和管理 API；HTTP/HTTPS 业务监听端口由管理面板中的启用 `Server` 配置动态打开。

```bash
mkdir -p data certs

docker run -d --restart=always --name=fast-gateway \
  --user 0:0 \
  -e PASSWORD='change-this-password' \
  -e TunnelToken='change-this-tunnel-token' \
  -p 8080:8080 \
  -p 80:80/tcp \
  -p 443:443/tcp \
  -p 443:443/udp \
  -v "$(pwd)/data:/app/data" \
  -v "$(pwd)/certs:/app/certs" \
  aidotnet/fast-gateway:latest
```

容器启动后访问 `http://localhost:8080`。如果没有提供密码，首次启动会生成随机初始密码并保存到 `data/admin-password`（启动日志中也会提示路径）；将管理端口暴露到公网前请务必通过 `PASSWORD` 设置正式密码。HTTP/3 需要映射 `443/udp`。

发布镜像为 Native AOT，基于 chiseled 精简根文件系统（无 shell）。基础镜像默认非 root（UID 1654），本仓库镜像与 Compose 以 `user: "0:0"` 运行，这样挂载 `data`/`certs` 才能写入统计库和证书。`aidotnet/fast-gateway` 为多架构镜像，同时支持 `linux/amd64` 与 `linux/arm64`。

## Docker Compose

仓库中的 `docker-compose.yml` 会构建 `src/FastGateway/Dockerfile`，持久化 `data` 和 `certs`，将管理端口映射为 `8000`，镜像名为 `registry.cn-shenzhen.aliyuncs.com/token-ai/fast-gateway`：

```bash
docker compose -f docker-compose.yml up -d --build
```

该文件映射 `8000:8080`、`80:80`，以及 `443/tcp` 和 `443/udp`（HTTP/3）。如果使用预构建的多架构镜像，可将镜像替换为 `aidotnet/fast-gateway:latest` 并移除 `build` 配置；生产环境请在 Compose 的 `environment` 中设置 `PASSWORD` 和 `TunnelToken`。

## Linux 使用 `systemd` 启动服务

从 [Releases](../../releases) 下载 Linux 压缩包，解压到例如 `/opt/fastgateway`，然后创建 `fastgateway.service`：

```shell
nano /etc/systemd/system/fastgateway.service
```

填写以下内容时请替换路径和密钥：

```ini
[Unit]
Description=FastGateway

[Service]
WorkingDirectory=/opt/fastgateway
ExecStart=/opt/fastgateway/FastGateway
Restart=always
RestartSec=10
KillSignal=SIGINT
SyslogIdentifier=dotnet-fastgateway
User=root
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=PASSWORD=change-this-password
Environment=TunnelToken=change-this-tunnel-token

[Install]
WantedBy=multi-user.target
```

重新加载 systemd 并启动服务：

```shell
systemctl daemon-reload
systemctl start fastgateway.service
systemctl enable fastgateway.service
systemctl status fastgateway.service
```

停止或重启使用 `systemctl stop fastgateway.service` 和 `systemctl restart fastgateway.service`。

## 开发与构建

开发环境需要 .NET 10 SDK、Node.js 20+ 与 npm，以及目标平台所需的原生编译工具链。当前项目默认启用主应用的 Native AOT 分析/发布；如果要生成 Release 流程使用的可移植单文件产物，可通过 `-p:PublishAot=false` 覆盖。

```bash
# 构建 .NET 解决方案
dotnet build FastGateway.sln

# 构建管理面板
cd web
npm install
npm run build
cd ..

# 使用启动配置运行管理宿主（http://localhost:5202）
dotnet run --project src/FastGateway/FastGateway.csproj

# 运行隧道客户端；客户端入口要求提供 -c
dotnet run --project src/TunnelClient/TunnelClient.csproj -- -c ./src/TunnelClient/tunnel.json
```

只开发管理面板时，可在 `web` 目录执行 `npm run dev`；Vite 会把 `/api` 代理到 `http://localhost:5202`。Docker 会在 Node 22 阶段构建面板，再为 `linux/amd64` 和 `linux/arm64` 发布 FastGateway Native AOT 镜像。Release 工作流会为 Linux、Windows、macOS 的 x64 和 ARM64 构建 FastGateway 与 TunnelClient 自包含单文件压缩包。

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

- [ip2region.xdb](https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb) 用于 IP 离线归属地
