# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器。
# 本 Dockerfile 已切换为 Native AOT 发布：产出自包含原生可执行文件，无需 .NET 运行时。
# 最终镜像使用 chiseled-extra（无 shell/apt、非 root）以显著缩小体积，同时保留 glibc 性能与多架构。

# 前端构建阶段：始终在构建机架构($BUILDPLATFORM)上运行。
# 前端产物为架构无关的静态文件，这样多架构构建时无需在 QEMU 模拟下重复构建前端。
FROM --platform=$BUILDPLATFORM node:22-slim AS web
WORKDIR /web
COPY web/package.json ./
RUN npm install
COPY web/ ./
RUN npm run build

# 生成阶段：随目标平台($TARGETPLATFORM)运行，保证 Native AOT 在目标架构上原生编译
# （AOT 交叉编译复杂，buildx 会为每个目标架构分别执行此阶段）。
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release
ARG TARGETARCH
# Native AOT 原生链接所需工具链；curl 用于下载离线 IP 库
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev curl \
    && apt-get clean && rm -rf /var/lib/apt/lists/*
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/FastGateway/FastGateway.csproj", "src/FastGateway/"]
COPY ["src/Core/Core.csproj", "src/Core/"]
COPY ["src/Certes/Certes.csproj", "src/Certes/"]
# TARGETARCH(amd64/arm64) → .NET RID(linux-x64/linux-arm64)
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) \
    && dotnet restore "./src/FastGateway/FastGateway.csproj" -r $RID
COPY . .
# 将前端构建产物复制到 wwwroot，随 dotnet publish 一起打包
COPY --from=web /web/dist ./src/FastGateway/wwwroot
WORKDIR "/src/src/FastGateway"
RUN RID=$([ "$TARGETARCH" = "arm64" ] && echo linux-arm64 || echo linux-x64) \
    && dotnet publish "./FastGateway.csproj" -c $BUILD_CONFIGURATION -r $RID \
       --no-restore -o /app/publish

# 依赖收集阶段：chiseled 最终镜像无 apt/shell，故在此（有 shell 的构建镜像）安装 libmsquic
# 并把 HTTP/3 所需原生库与离线 IP 库归拢到 /deps，供最终阶段直接 COPY。
# libmsquic 不在 Ubuntu 官方仓库，需先注册微软 apt 源（ubuntu/24.04 = noble）。
RUN curl -sSL -O https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y --no-install-recommends libmsquic \
    && mkdir -p /deps/lib \
    # 动态解析 libmsquic 的实际 .so 路径（amd64/arm64 目录名不同），复制其全部 so 文件
    && dpkg -L libmsquic | grep -E '\.so' | xargs -I{} cp -av {} /deps/lib/ \
    && apt-get clean && rm -rf /var/lib/apt/lists/*
# ip2region 离线IP归属地库（仓库不入库，构建时下载；失败不阻断构建，运行时统计归属地降级为“其他”）
# 兜底 touch：即便下载失败也保证文件存在，否则最终阶段的 COPY 会让构建失败。
RUN (curl -sSL --retry 3 -o /deps/ip2region.xdb https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb || true) \
    && [ -f /deps/ip2region.xdb ] || touch /deps/ip2region.xdb

# 最终阶段：chiseled-extra 含 Native AOT 运行所需原生依赖与 tzdata/ICU 等，且不含 shell、以非 root 运行。
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble-chiseled-extra AS final
# chiseled 无法执行 ln/apt，改用 TZ 环境变量设置时区（镜像已内置 tzdata）
ENV TZ=Asia/Shanghai
WORKDIR /app
EXPOSE 8080
# HTTP/3 所需 libmsquic 原生库（构建阶段已按目标架构解析并归拢）
COPY --from=build /deps/lib/ /usr/lib/
# 离线 IP 归属地库（下载失败时该文件不存在，运行时归属地降级为“其他”）
COPY --from=build /deps/ip2region.xdb /app/ip2region.xdb
COPY --from=build /app/publish .
# Native AOT 产出原生可执行文件，直接运行（不再经 dotnet 宿主）
ENTRYPOINT ["./FastGateway"]
