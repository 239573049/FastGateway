@echo off
REM ========================================
REM FastGateway 构建脚本
REM 功能: 构建前端, 构建后端, 构建TunnelClient, 将前端dist复制到后端wwwroot
REM ========================================

setlocal enabledelayedexpansion

REM 设置颜色输出
REM 绿色: 0A, 黄色: 0E, 红色: 0C
set "GREEN=[92m"
set "YELLOW=[93m"
set "RED=[91m"
set "RESET=[0m"

REM 获取脚本所在目录
set "SCRIPT_DIR=%~dp0"
set "WEB_DIR=%SCRIPT_DIR%web"
set "BACKEND_DIR=%SCRIPT_DIR%src\FastGateway"
set "TUNNEL_CLIENT_DIR=%SCRIPT_DIR%src\TunnelClient"
set "SOLUTION_FILE=%SCRIPT_DIR%FastGateway.sln"
set "BACKEND_WWWROOT=%BACKEND_DIR%\wwwroot"
set "FRONTEND_DIST=%WEB_DIR%\dist"

echo.
echo ========================================
echo FastGateway 构建开始
echo ========================================
echo.

REM 检查必要的工具
echo [*] 检查必要工具...
where dotnet >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 dotnet 命令！请确保已安装 .NET SDK
    exit /b 1
)
echo [✓] dotnet 已安装

where node >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 node 命令！请确保已安装 Node.js
    exit /b 1
)
echo [✓] node 已安装

where npm >nul 2>&1
if errorlevel 1 (
    echo [错误] 未找到 npm 命令！请确保已安装 npm
    exit /b 1
)
echo [✓] npm 已安装
echo.

REM ========================================
REM 第一步: 构建前端
REM ========================================
echo ========================================
echo 第一步: 构建前端应用
echo ========================================
echo.

if not exist "%WEB_DIR%" (
    echo [错误] 前端目录不存在: %WEB_DIR%
    exit /b 1
)

cd /d "%WEB_DIR%"
if errorlevel 1 (
    echo [错误] 无法进入前端目录
    exit /b 1
)

echo [*] 安装前端依赖...
call npm install
if errorlevel 1 (
    echo [错误] 前端依赖安装失败
    exit /b 1
)
echo [✓] 前端依赖安装完成
echo.

echo [*] 构建前端...
call npm run build
if errorlevel 1 (
    echo [错误] 前端构建失败
    exit /b 1
)
echo [✓] 前端构建完成
echo.

REM 检查dist目录
if not exist "%FRONTEND_DIST%" (
    echo [错误] 前端dist目录未生成: %FRONTEND_DIST%
    exit /b 1
)
echo [✓] 前端dist目录已生成
echo.

REM ========================================
REM 第二步: 清理后端旧的wwwroot
REM ========================================
echo ========================================
echo 第二步: 清理后端wwwroot目录
echo ========================================
echo.

if exist "%BACKEND_WWWROOT%" (
    echo [*] 删除旧的 wwwroot 目录...
    rmdir /s /q "%BACKEND_WWWROOT%"
    if errorlevel 1 (
        echo [警告] 删除 wwwroot 失败，继续进行...
    ) else (
        echo [✓] 旧 wwwroot 已删除
    )
) else (
    echo [*] wwwroot 目录不存在，无需清理
)
echo.

REM ========================================
REM 第三步: 构建后端
REM ========================================
echo ========================================
echo 第三步: 构建后端应用
echo ========================================
echo.

cd /d "%SCRIPT_DIR%"
if errorlevel 1 (
    echo [错误] 无法回到项目根目录
    exit /b 1
)

echo [*] 恢复NuGet包...
dotnet restore "%SOLUTION_FILE%"
if errorlevel 1 (
    echo [错误] NuGet包恢复失败
    exit /b 1
)
echo [✓] NuGet包恢复完成
echo.

echo [*] 编译后端项目 (Release)...
dotnet publish "%SOLUTION_FILE%" -c Release -o "%BACKEND_DIR%\publish"
if errorlevel 1 (
    echo [错误] 后端构建失败
    exit /b 1
)
echo [✓] 后端构建完成
echo.

REM ========================================
REM 第四步: 复制前端dist到后端wwwroot
REM ========================================
echo ========================================
echo 第四步: 复制前端文件到后端
echo ========================================
echo.

if not exist "%BACKEND_DIR%\publish\wwwroot" (
    echo [*] 创建 wwwroot 目录...
    mkdir "%BACKEND_DIR%\publish\wwwroot"
    if errorlevel 1 (
        echo [错误] 无法创建 wwwroot 目录
        exit /b 1
    )
)

echo [*] 复制前端dist文件到后端wwwroot...
xcopy "%FRONTEND_DIST%\*" "%BACKEND_DIR%\publish\wwwroot\" /E /I /Y
if errorlevel 1 (
    echo [错误] 复制前端文件失败
    exit /b 1
)
echo [✓] 前端文件复制完成
echo.

REM ========================================
REM 第五步: 构建 TunnelClient
REM ========================================
echo ========================================
echo 第五步: 构建 TunnelClient 应用
echo ========================================
echo.

if not exist "%TUNNEL_CLIENT_DIR%" (
    echo [错误] TunnelClient 目录不存在: %TUNNEL_CLIENT_DIR%
    exit /b 1
)

cd /d "%SCRIPT_DIR%"
if errorlevel 1 (
    echo [错误] 无法回到项目根目录
    exit /b 1
)

echo [*] 编译 TunnelClient 项目 (Release)...
dotnet publish "%TUNNEL_CLIENT_DIR%\TunnelClient.csproj" -c Release -o "%TUNNEL_CLIENT_DIR%\publish"
if errorlevel 1 (
    echo [错误] TunnelClient 构建失败
    exit /b 1
)
echo [✓] TunnelClient 构建完成
echo.

REM ========================================
REM 构建完成
REM ========================================
echo ========================================
echo 构建成功完成！
echo ========================================
echo.
echo [✓] 前端构建: %FRONTEND_DIST%
echo [✓] 后端构建: %BACKEND_DIR%\publish
echo [✓] TunnelClient 构建: %TUNNEL_CLIENT_DIR%\publish
echo [✓] 前端文件已复制到: %BACKEND_DIR%\publish\wwwroot
echo.
echo 后端发布文件位于: %BACKEND_DIR%\publish
echo TunnelClient 发布文件位于: %TUNNEL_CLIENT_DIR%\publish
echo.

endlocal
exit /b 0
