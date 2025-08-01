@echo off
setlocal enabledelayedexpansion

REM ========================================
REM TCP数据转发系统插件自动复制脚本
REM 编译后自动复制到MainAPP程序插件库
REM ========================================

echo [INFO] 开始复制XPluginTcpRelay插件到MainAPP...

REM 设置路径变量
set "SOURCE_DIR=%~dp0..\bin\Debug\net8.0-windows"
set "TARGET_DIR=D:\windows11\installed_files\dev_tools\JetBrains\codespace\Rider\WinFormsApp2\MainAPP\bin\Debug\net8.0-windows\XPlugins"
set "PLUGIN_NAME=XPluginTcpRelay"

REM 检查源目录是否存在
if not exist "%SOURCE_DIR%" (
    echo [ERROR] 源目录不存在: %SOURCE_DIR%
    echo [INFO] 请先编译XPluginTcpRelay项目
    pause
    exit /b 1
)

REM 创建目标插件目录
if not exist "%TARGET_DIR%" (
    echo [INFO] 创建插件目录: %TARGET_DIR%
    mkdir "%TARGET_DIR%"
)

REM 创建插件专用目录
set "PLUGIN_TARGET_DIR=%TARGET_DIR%\%PLUGIN_NAME%"
if not exist "%PLUGIN_TARGET_DIR%" (
    echo [INFO] 创建插件专用目录: %PLUGIN_TARGET_DIR%
    mkdir "%PLUGIN_TARGET_DIR%"
)

REM 复制主要插件文件
echo [INFO] 复制插件主文件...
copy "%SOURCE_DIR%\%PLUGIN_NAME%.dll" "%PLUGIN_TARGET_DIR%\" >nul
if errorlevel 1 (
    echo [ERROR] 复制%PLUGIN_NAME%.dll失败
    pause
    exit /b 1
)

REM 复制PDB文件（调试信息）
if exist "%SOURCE_DIR%\%PLUGIN_NAME%.pdb" (
    echo [INFO] 复制调试信息文件...
    copy "%SOURCE_DIR%\%PLUGIN_NAME%.pdb" "%PLUGIN_TARGET_DIR%\" >nul
)

REM 复制依赖文件（如果有的话）
echo [INFO] 检查并复制依赖文件...

REM 复制配置文件模板
if exist "%SOURCE_DIR%\relay_config.json" (
    echo [INFO] 复制配置文件模板...
    copy "%SOURCE_DIR%\relay_config.json" "%PLUGIN_TARGET_DIR%\" >nul
)

REM 复制其他资源文件
if exist "%SOURCE_DIR%\Resources" (
    echo [INFO] 复制资源文件...
    xcopy "%SOURCE_DIR%\Resources" "%PLUGIN_TARGET_DIR%\Resources" /E /I /Y >nul
)

REM 检查MainAPP是否正在运行
tasklist /FI "IMAGENAME eq XPanel.exe" 2>NUL | find /I /N "XPanel.exe">NUL
if "%ERRORLEVEL%"=="0" (
    echo [WARNING] 检测到MainAPP正在运行，建议重启程序以加载新插件
)

echo [SUCCESS] 插件复制完成！
echo [INFO] 插件位置: %PLUGIN_TARGET_DIR%
echo [INFO] 文件列表:
dir "%PLUGIN_TARGET_DIR%" /B

echo.
echo [INFO] 复制操作完成，按任意键退出...
pause >nul
