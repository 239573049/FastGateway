# 更新日志 / Changelog

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)（SemVer）。
更早版本请参见 [GitHub Releases](https://github.com/239573049/FastGateway/releases)。

## [Unreleased]

### 修复 Fixed

- **网关（HTTPS 服务）**：`X-Frame-Options` 由硬编码 `DENY` 改为默认 `SAMEORIGIN`。`DENY` 连同源 iframe 一起禁止，会让依赖「父窗口驱动同源预览」的应用预览空白，并因拿到跨源空 frame 而持续抛 `SecurityError`。
- **网关**：安全头改为「仅补齐、不覆盖」；上游已声明 `X-Frame-Options` 或 CSP `frame-ancestors` 时网关不再注入，避免反向代理改写源站自己的策略。
- **网关**：新增服务级 `FrameOptions` 配置（`SAMEORIGIN` / `DENY` / `NONE`），管理界面「服务 → 功能设置」可选；旧配置文件缺该字段时默认 `SAMEORIGIN`。

### 测试 Tests

- 新增 `FrameOptionsTests`，覆盖默认值、大小写、非法值回退、禁用与上游策略优先。

## [2.15.0] - 2026-08-12

### 新增 Added

- **集群管理（多节点分布式网关）**：
  - 支持主（Master）/ 从（Worker）角色：主网关生成接入码，从网关粘贴接入码即可一键加入集群，无需手工同步配置。
  - 主网关通过 WebSocket 长连接向所有从节点推送**全量配置快照**（服务、域名路由、黑白名单、限流策略、L4 端口转发、证书及 PFX 文件），从节点自动落盘并热更新网关实例。
  - 节点心跳与在线状态展示，支持移除节点、从节点主动退出、解散集群；从节点不参与 ACME 证书续期（证书统一由主网关签发并下发）。
  - 新增「集群管理」前端页面与 `/api/v1/cluster` 接口组。
- **节点间二进制同步协议**：基于 **MessagePack（LZ4 压缩）** 的二进制编码，序列化经 Roslyn 源生成器完成，**完全兼容 Native AOT**（零反射）；证书文件以原生二进制传输，无 Base64 膨胀。
- **文件存储**：支持大文件分片上传。
- **系统设置**：支持配置请求日志保留时长。
- **网关**：服务级客户端 IP 来源配置（`X-Forwarded-For` / `X-Real-IP` / `CF-Connecting-IP`），适配 CDN / 多层代理场景。

### 优化 Changed

- **限流引擎重写**：移除 `AspNetCoreRateLimit` 依赖，改用 .NET 内置 `System.Threading.RateLimiting` 实现按 IP 分区的固定窗口限流；未命中规则的请求零额外开销，空闲分区自动回收。
- **静态站点**：响应携带 `ETag` / `Last-Modified` 并支持协商缓存（命中返回 304，省去响应体传输）；文件探测合并为单次 `stat`，优化 `try_files` 查找。
- **统计链路性能**：UV 去重集合内存封顶、响应时间改用无锁环形缓冲、GeoIP / User-Agent 解析引入两代缓存、SQLite 批量写入复用参数对象、客户端 IP 按 `Span` 切片解析。
- **配置服务并发安全**：所有读写在锁内完成、读取返回副本，杜绝 API 修改配置时并发枚举异常。
- **前端**：证书、限流、安全、隧道、端口转发等页面补齐入场 / 列表动画，服务卡片信息布局改进。

### 修复 Fixed

- **ACME（AOT）**：修复挑战验证阶段匿名 `{}` 载荷在 Native AOT 下无 `JsonTypeInfo` 导致崩溃的问题；修复 `internal` 载荷属性被裁剪导致 finalize CSR 为空的问题。

## [2.14.0] - 2026-07-03

### 新增 Added

- **证书管理**：支持上传用户自定义证书，兼容 **PFX / P12** 与 **PEM / CRT** 两种格式，自动读取有效期与颁发机构。
- **证书管理**：支持 **泛域名证书**（如 `*.example.com`），通过 Let's Encrypt **DNS-01** 手动验证方式申请（生成 `_acme-challenge` TXT 记录，添加生效后一键签发）。
- **证书管理**：SNI 支持泛域名匹配（`a.example.com` 命中 `*.example.com`）。
- **网关**：支持路由级健康检查，可配置检查路径、Interval、Timeout，并在界面展示健康状态。
- **网关**：新增集群请求故障转移（failover）控制。
- **网关**：新增请求超时配置，增强服务稳定性。
- **构建**：Release 工作流新增 Linux / Windows / macOS 的 x64 与 arm64 全平台产物。
- **构建**：Docker 镜像改为 `linux/amd64` + `linux/arm64` 多架构发布。

### 修复 Fixed

- 修复 **HTTPS 重定向未生效** 的问题：`Server.RedirectHttps` 此前从未被消费，现在网关会在 ACME 校验之后将 80 端口的明文请求跳转到 HTTPS（不影响 HTTP-01 证书签发）。
- 修复隧道断开后网关无法自动重连的问题。

### 优化 Changed

- 性能优化。
- 更新 Dockerfile 与 CI 配置，优化 Node.js 环境与构建流程。
- 更新网关镜像仓库地址。
- 自定义上传及泛域名证书不参与 HTTP-01 自动续期（泛域名需手动 DNS 验证续期）。

### 文档 Docs

- README（中文 / 英文）补充证书管理说明（自动申请 / 泛域名 DNS-01 / 上传自定义证书）与多平台下载说明。
