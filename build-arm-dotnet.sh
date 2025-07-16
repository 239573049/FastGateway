#!/bin/bash

# FastGateway ARM 构建脚本
# 用于构建和发布 ARM 架构的 .NET 程序到 ./output 目录

set -e  # 遇到错误时立即退出

# 颜色定义
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# 配置变量
PROJECT_PATH="src/FastGateway.Service/FastGateway.Service.csproj"
OUTPUT_DIR="./output"
TARGET_FRAMEWORK="net8.0"
RUNTIME_IDENTIFIER="linux-arm64"
CONFIGURATION="Release"

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

# 函数：检查文件是否存在
check_file() {
    if [ ! -f "$1" ]; then
        print_error "文件 $1 不存在"
        exit 1
    fi
}

# 函数：清理输出目录
cleanup_output() {
    print_info "清理输出目录..."
    if [ -d "$OUTPUT_DIR" ]; then
        rm -rf "$OUTPUT_DIR"
        print_success "输出目录清理完成"
    fi
}

# 函数：还原 NuGet 包
restore_packages() {
    print_info "还原 NuGet 包..."
    dotnet restore "$PROJECT_PATH" --runtime "$RUNTIME_IDENTIFIER"
    print_success "NuGet 包还原完成"
}

# 函数：构建项目
build_project() {
    print_info "构建项目..."
    dotnet build "$PROJECT_PATH" \
        --configuration "$CONFIGURATION" \
        --runtime "$RUNTIME_IDENTIFIER" \
        --no-restore
    print_success "项目构建完成"
}

# 函数：发布项目
publish_project() {
    print_info "发布 ARM 架构程序到 $OUTPUT_DIR..."
    
    dotnet publish "$PROJECT_PATH" \
        --configuration "$CONFIGURATION" \
        --runtime "$RUNTIME_IDENTIFIER" \
        --output "$OUTPUT_DIR" \
        --self-contained true \
        --no-restore \
        --no-build \
        /p:PublishSingleFile=true \
        /p:PublishTrimmed=true \
        /p:IncludeNativeLibrariesForSelfExtract=true
    
    print_success "ARM 程序发布完成"
}

# 函数：显示发布信息
show_publish_info() {
    print_info "发布信息："
    echo "  - 目标框架: $TARGET_FRAMEWORK"
    echo "  - 运行时标识符: $RUNTIME_IDENTIFIER"
    echo "  - 配置: $CONFIGURATION"
    echo "  - 输出目录: $OUTPUT_DIR"
    echo "  - 单文件发布: 是"
    echo "  - 自包含: 是"
    echo "  - 代码裁剪: 是"
    
    if [ -d "$OUTPUT_DIR" ]; then
        echo ""
        print_info "输出文件："
        ls -la "$OUTPUT_DIR"
    fi
}

# 函数：验证发布结果
verify_publish() {
    print_info "验证发布结果..."
    
    if [ ! -d "$OUTPUT_DIR" ]; then
        print_error "输出目录不存在"
        exit 1
    fi
    
    # 检查主要的可执行文件
    if [ -f "$OUTPUT_DIR/FastGateway.Service" ]; then
        print_success "可执行文件 FastGateway.Service 已生成"
        
        # 显示文件大小
        file_size=$(du -h "$OUTPUT_DIR/FastGateway.Service" | cut -f1)
        echo "  文件大小: $file_size"
        
        # 检查文件权限
        if [ ! -x "$OUTPUT_DIR/FastGateway.Service" ]; then
            print_info "设置可执行权限..."
            chmod +x "$OUTPUT_DIR/FastGateway.Service"
        fi
    else
        print_error "可执行文件未找到"
        exit 1
    fi
    
    # 检查配置文件
    if [ -f "$OUTPUT_DIR/appsettings.json" ]; then
        print_success "配置文件 appsettings.json 已复制"
    fi
    
    print_success "发布验证完成"
}

# 主函数
main() {
    print_info "开始构建 FastGateway ARM 版本..."
    
    # 检查项目文件
    check_file "$PROJECT_PATH"
    
    # 显示 .NET 版本信息
    print_info ".NET 版本信息："
    dotnet --version
    
    # 执行构建流程
    cleanup_output
    restore_packages
    build_project
    publish_project
    verify_publish
    show_publish_info
    
    print_success "FastGateway ARM 版本构建完成！"
    print_info "你可以将 $OUTPUT_DIR 目录复制到 ARM 设备上运行"
    print_info "运行命令: ./FastGateway.Service"
}

# 显示使用帮助
show_help() {
    echo "FastGateway ARM 构建脚本"
    echo ""
    echo "用法: $0 [选项]"
    echo ""
    echo "选项:"
    echo "  -h, --help     显示此帮助信息"
    echo "  --clean        仅清理输出目录"
    echo "  --restore      仅还原 NuGet 包"
    echo "  --build        仅构建项目"
    echo "  --publish      仅发布项目"
    echo ""
    echo "示例:"
    echo "  $0              # 完整构建流程"
    echo "  $0 --clean      # 仅清理输出目录"
    echo "  $0 --publish    # 仅发布项目"
}

# 处理命令行参数
case "${1:-}" in
    -h|--help)
        show_help
        exit 0
        ;;
    --clean)
        cleanup_output
        exit 0
        ;;
    --restore)
        check_file "$PROJECT_PATH"
        restore_packages
        exit 0
        ;;
    --build)
        check_file "$PROJECT_PATH"
        restore_packages
        build_project
        exit 0
        ;;
    --publish)
        check_file "$PROJECT_PATH"
        publish_project
        verify_publish
        show_publish_info
        exit 0
        ;;
    "")
        main
        ;;
    *)
        print_error "未知选项: $1"
        show_help
        exit 1
        ;;
esac
