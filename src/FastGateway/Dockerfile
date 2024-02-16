FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8000
EXPOSE 8080
EXPOSE 8081

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["FastGateway/FastGateway.csproj", "FastGateway/"]
RUN dotnet restore "./FastGateway/./FastGateway.csproj"
COPY . .
WORKDIR "/src/FastGateway"
RUN dotnet build "./FastGateway.csproj" -c $BUILD_CONFIGURATION -o /app/build

FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./FastGateway.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false --os linux

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "FastGateway.dll"]