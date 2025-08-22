@echo off
echo Installing FastGateway TunnelClient as Windows Service...

:: Check if running as administrator
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo This script requires administrator privileges.
    echo Please run as administrator.
    pause
    exit /b 1
)

:: Set service details
set SERVICE_NAME=TunnelClient
set SERVICE_DISPLAY_NAME=Tunnel Client
set SERVICE_DESCRIPTION=tunnel client service for secure connections
set EXECUTABLE_FILE=%~dp0TunnelClient.exe
set CONFIG_FILE=./tunnel.json
set EXECUTABLE_PATH=%EXECUTABLE_FILE% -c "%CONFIG_FILE%"

:: Check if executable exists
if not exist "%EXECUTABLE_FILE%" (
    echo Error: TunnelClient.exe not found in current directory.
    echo Please ensure the executable is built and placed in the same directory as this script.
    pause
    exit /b 1
)

:: Stop and delete existing service if it exists
sc query %SERVICE_NAME% >nul 2>&1
if %errorLevel% equ 0 (
    echo Stopping existing service...
    sc stop %SERVICE_NAME%
    timeout /t 5 /nobreak
    echo Deleting existing service...
    sc delete %SERVICE_NAME%
    timeout /t 2 /nobreak
)

:: Create the service with working directory
echo Creating service...
set WORKING_DIR=%~dp0
sc create %SERVICE_NAME% binPath= "%EXECUTABLE_PATH%" DisplayName= "%SERVICE_DISPLAY_NAME%" start= auto obj= LocalSystem

if %errorLevel% neq 0 (
    echo Failed to create service.
    pause
    exit /b 1
)

:: Set service description
sc description %SERVICE_NAME% "%SERVICE_DESCRIPTION%"

:: Set service to restart on failure
sc failure %SERVICE_NAME% reset= 60 actions= restart/5000/restart/10000/restart/30000

:: Start the service
echo Starting service...
sc start %SERVICE_NAME%

if %errorLevel% equ 0 (
    echo Service installed and started successfully!
    echo Service Name: %SERVICE_NAME%
    echo Display Name: %SERVICE_DISPLAY_NAME%
) else (
    echo Service created but failed to start. Please check the logs.
)

pause