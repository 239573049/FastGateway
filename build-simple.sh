#!/bin/bash

# 简单的 ARM 构建脚本
set -e

# 清理输出目录
rm -rf ./output

# 发布 ARM 版本
dotnet publish src/FastGateway.Service/FastGateway.Service.csproj \
    --configuration Release \
    --runtime linux-arm64 \
    --output ./output \
    --self-contained true \
    /p:PublishSingleFile=true

echo "构建完成，输出目录: ./output"
