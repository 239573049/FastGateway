@echo off
echo Uninstalling TunnelClient Windows Service...

:: Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

:: Set service name
set SERVICE_NAME=TunnelClient

:: Check if service exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% neq 0 (
    echo Service %SERVICE_NAME% does not exist.
    pause
    exit /b 0
)

:: Stop the service
echo Stopping service...
sc stop %SERVICE_NAME%
timeout /t 5 /nobreak

:: Delete the service
echo Deleting service...
sc delete %SERVICE_NAME%

if %errorLevel% equ 0 (
    echo Service uninstalled successfully!
) else (
    echo Failed to uninstall service.
)

pause