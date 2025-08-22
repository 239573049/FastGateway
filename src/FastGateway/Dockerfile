FROM mcr.microsoft.com/dotnet/nightly/aspnet:10.0-preview AS base
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
EXPOSE 8081

FROM node:alpine AS builder
WORKDIR /src
COPY web .
RUN npm i
RUN npm run build

FROM mcr.microsoft.com/dotnet/nightly/sdk:10.0-preview AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["src/FastGateway.Service/FastGateway.Service.csproj", "src/FastGateway.Service/"]
COPY ["src/FastGateway.Entities/FastGateway.Entities.csproj", "src/FastGateway.Entities/"]
RUN dotnet restore "src/FastGateway.Service/FastGateway.Service.csproj"
COPY . .
WORKDIR "/src/src/FastGateway.Service"
RUN dotnet build "./FastGateway.Service.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FastGateway.Service.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
COPY --from=builder /src/dist ./wwwroot
ENTRYPOINT ["dotnet", "FastGateway.Service.dll"]
