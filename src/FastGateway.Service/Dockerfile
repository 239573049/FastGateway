FROM mcr.microsoft.com/dotnet/aspnet:9.0-bookworm-slim AS base
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


FROM node:alpine AS builder
WORKDIR /src
COPY web .
RUN npm i
RUN npm run build

# 此阶段用于生成服务项目
FROM mcr.microsoft.com/dotnet/sdk:9.0-bookworm-slim AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["Directory.Build.props", "."]
COPY ["src/FastGateway.Service/FastGateway.Service.csproj", "src/FastGateway.Service/"]
COPY ["src/FastGateway.Entities/FastGateway.Entities.csproj", "src/FastGateway.Entities/"]
RUN dotnet restore "./src/FastGateway.Service/FastGateway.Service.csproj"
COPY . .
WORKDIR "/src/src/FastGateway.Service"
RUN dotnet build "./FastGateway.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

# 此阶段用于发布要复制到最终阶段的服务项目
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FastGateway.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# 此阶段在生产中使用，或在常规模式下从 VS 运行时使用(在不使用调试配置时为默认值)
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=builder /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "FastGateway.Service.dll"]
.