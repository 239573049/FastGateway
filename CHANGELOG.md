# 更新日志 / Changelog

本项目遵循 [语义化版本](https://semver.org/lang/zh-CN/)（SemVer）。
更早版本请参见 [GitHub Releases](https://github.com/239573049/FastGateway/releases)。

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
