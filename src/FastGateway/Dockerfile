# 请参阅 https://aka.ms/customizecontainer 以了解如何自定义调试容器,以及 Visual Studio 如何使用此 Dockerfile 生成映像以更快地进行调试。

# 前端构建阶段：始终在构建机架构($BUILDPLATFORM)上运行。
# 前端产物为架构无关的静态文件，这样多架构构建时无需在 QEMU 模拟下重复构建前端。
FROM --platform=$BUILDPLATFORM node:20-slim AS web
WORKDIR /web
COPY web/package.json ./
RUN npm install
COPY web/ ./
RUN npm run build

# 此阶段用于在快速模式(默认为调试配置)下从 VS 运行时
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS base
USER root
RUN ln -snf /usr/share/zoneinfo/Asia/Shanghai /etc/localtime && echo Asia/Shanghai > /etc/timezone
RUN  apt update \
    && apt-get install -y --no-install-recommends curl \
    && curl -sSL -O https://packages.microsoft.com/config/debian/12/packages-microsoft-prod.deb \
    && dpkg -i packages-microsoft-prod.deb \
    && rm packages-microsoft-prod.deb \
    && apt-get update \
    && apt-get install -y libmsquic \
    && apt-get purge -y --auto-remove wget && apt-get clean && rm -rf /var/lib/apt/lists/*
WORKDIR /app
EXPOSE 8080


# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/FastGateway/FastGateway.csproj", "src/FastGateway/"]
COPY ["src/Core/Core.csproj", "src/Core/"]
RUN dotnet restore "./src/FastGateway/FastGateway.csproj"
COPY . .
# 将前端构建产物复制到 wwwroot，随 dotnet publish 一起打包
COPY --from=web /web/dist ./src/FastGateway/wwwroot
WORKDIR "/src/src/FastGateway"
RUN dotnet build "./FastGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FastGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用,或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
# ip2region 离线IP归属地库（仓库不入库，构建时下载；失败不阻断构建，运行时统计归属地降级为“其他”）
RUN curl -sSL --retry 3 -o /app/ip2region.xdb https://tokenfile.oss-cn-beijing.aliyuncs.com/ip2region.xdb || true
ENTRYPOINT ["dotnet", "FastGateway.dll"]
