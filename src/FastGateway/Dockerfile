# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器。
# 本 Dockerfile 已切换为 Native AOT 发布：产出自包含原生可执行文件，无需 .NET 运行时。

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
# Native AOT 原生链接所需工具链
RUN apt-get update \
    && apt-get install -y --no-install-recommends clang zlib1g-dev \
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

# 最终阶段：runtime-deps 已含 Native AOT 运行所需原生依赖（libicu/openssl 等），无需完整运行时。
FROM mcr.microsoft.com/dotnet/runtime-deps:10.0-noble AS final
USER root
RUN ln -snf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime && echo Asia/Shanghai > /etc/timezone
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && curl -sSL -O https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y libmsquic \
    && apt-get clean && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
# ip2region 离线IP归属地库（仓库不入库，构建时下载；失败不阻断构建，运行时统计归属地降级为“其他”）
RUN curl -sSL --retry 3 -o /app/ip2region.xdb https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb || true
# Native AOT 产出原生可执行文件，直接运行（不再经 dotnet 宿主）
ENTRYPOINT ["./FastGateway"]
