#!/bin/bash

# FastGateway ARM 构建脚本
# 用于构建和发布 ARM 架构的 FastGateway 服务

set -e  # 遇到错误时立即退出

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 配置变量
IMAGE_NAME="aidotnet/fast-gateway"
ARM_TAG="arm64"
LATEST_TAG="latest-arm64"
DOCKERFILE_ARM="src/FastGateway.Service/Dockerfile.arm"
COMPOSE_FILE="docker-compose.arm.yml"

# 函数：打印带颜色的消息
print_info() {
    echo -e "${BLUE}[INFO]${NC} $1"
}

print_success() {
    echo -e "${GREEN}[SUCCESS]${NC} $1"
}

print_warning() {
    echo -e "${YELLOW}[WARNING]${NC} $1"
}

print_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

# 函数：检查命令是否存在
check_command() {
    if ! command -v $1 &> /dev/null; then
        print_error "$1 未找到，请先安装 $1"
        exit 1
    fi
}

# 函数：检查文件是否存在
check_file() {
    if [ ! -f "$1" ]; then
        print_error "文件 $1 不存在"
        exit 1
    fi
}

# 函数：清理旧的构建缓存
cleanup() {
    print_info "清理 Docker 构建缓存..."
    docker builder prune -f --filter until=24h
    print_success "缓存清理完成"
}

# 函数：构建 ARM 镜像
build_arm_image() {
    print_info "开始构建 ARM 架构镜像..."
    
    # 检查 Dockerfile
    check_file $DOCKERFILE_ARM
    
    # 构建镜像
    docker buildx build \
        --platform linux/arm64 \
        --file $DOCKERFILE_ARM \
        --tag $IMAGE_NAME:$ARM_TAG \
        --tag $IMAGE_NAME:$LATEST_TAG \
        --load \
        .
    
    print_success "ARM 镜像构建完成"
}

# 函数：推送镜像到仓库
push_image() {
    print_info "推送镜像到 Docker Hub..."
    
    # 推送带版本标签的镜像
    docker push $IMAGE_NAME:$ARM_TAG
    docker push $IMAGE_NAME:$LATEST_TAG
    
    print_success "镜像推送完成"
}

# 函数：使用 docker-compose 构建
build_with_compose() {
    print_info "使用 docker-compose 构建 ARM 镜像..."
    
    check_file $COMPOSE_FILE
    
    docker-compose -f $COMPOSE_FILE build --no-cache
    
    print_success "docker-compose 构建完成"
}

# 函数：运行 ARM 容器
run_arm_container() {
    print_info "启动 ARM 容器..."
    
    # 停止现有容器
    docker-compose -f $COMPOSE_FILE down
    
    # 启动新容器
    docker-compose -f $COMPOSE_FILE up -d
    
    print_success "ARM 容器已启动"
    
    # 显示容器状态
    print_info "容器状态："
    docker-compose -f $COMPOSE_FILE ps
}

# 函数：显示帮助信息
show_help() {
    echo "FastGateway ARM 构建脚本"
    echo ""
    echo "用法: $0 [选项]"
    echo ""
    echo "选项:"
    echo "  build       构建 ARM 镜像"
    echo "  push        推送镜像到 Docker Hub"
    echo "  compose     使用 docker-compose 构建"
    echo "  run         运行 ARM 容器"
    echo "  cleanup     清理构建缓存"
    echo "  all         执行完整构建流程（构建+运行）"
    echo "  help        显示此帮助信息"
    echo ""
    echo "示例:"
    echo "  $0 build           # 仅构建镜像"
    echo "  $0 all             # 构建并运行"
    echo "  $0 push            # 推送镜像"
}

# 函数：显示系统信息
show_system_info() {
    print_info "系统信息："
    echo "  操作系统: $(uname -s)"
    echo "  架构: $(uname -m)"
    echo "  Docker 版本: $(docker --version)"
    echo "  Docker Compose 版本: $(docker-compose --version)"
    
    # 检查是否支持 ARM64
    if docker buildx ls | grep -q "linux/arm64"; then
        print_success "支持 ARM64 构建"
    else
        print_warning "可能不支持 ARM64 构建，请确保 Docker Buildx 配置正确"
    fi
}

# 主函数
main() {
    print_info "FastGateway ARM 构建脚本启动"
    
    # 检查必要的命令
    check_command "docker"
    check_command "docker-compose"
    
    # 显示系统信息
    show_system_info
    
    # 根据参数执行相应操作
    case "${1:-help}" in
        "build")
            build_arm_image
            ;;
        "push")
            push_image
            ;;
        "compose")
            build_with_compose
            ;;
        "run")
            run_arm_container
            ;;
        "cleanup")
            cleanup
            ;;
        "all")
            cleanup
            build_arm_image
            run_arm_container
            ;;
        "help"|*)
            show_help
            ;;
    esac
    
    print_success "操作完成"
}

# 脚本入口点
main "$@"
