# ========================================
# XPluginTcpRelay 插件功能测试脚本
# ========================================

param(
    [switch]$StartTestServer,
    [int]$TestPort = 8080,
    [switch]$Verbose
)

function Write-Info { param([string]$Message) Write-Host "[INFO] $Message" -ForegroundColor Cyan }
function Write-Success { param([string]$Message) Write-Host "[SUCCESS] $Message" -ForegroundColor Green }
function Write-Warning { param([string]$Message) Write-Host "[WARNING] $Message" -ForegroundColor Yellow }
function Write-Error { param([string]$Message) Write-Host "[ERROR] $Message" -ForegroundColor Red }

Write-Info "XPluginTcpRelay 插件功能测试"
Write-Info "================================"

# 检查插件是否已复制到MainAPP
$PluginPath = "D:\windows11\installed_files\dev_tools\JetBrains\codespace\Rider\WinFormsApp2\MainAPP\bin\Debug\net8.0-windows\XPluginTcpRelay.dll"
if (Test-Path $PluginPath) {
    $FileInfo = Get-Item $PluginPath
    Write-Success "插件文件已存在: $PluginPath"
    Write-Info "文件大小: $([math]::Round($FileInfo.Length / 1KB, 2)) KB"
    Write-Info "修改时间: $($FileInfo.LastWriteTime)"
} else {
    Write-Error "插件文件不存在: $PluginPath"
    Write-Warning "请先编译插件项目"
    exit 1
}

# 检查MainAPP是否正在运行
$MainAppProcess = Get-Process -Name "XPanel" -ErrorAction SilentlyContinue
if ($MainAppProcess) {
    Write-Info "MainAPP正在运行 (PID: $($MainAppProcess.Id))"
} else {
    Write-Warning "MainAPP未运行，建议启动MainAPP来测试插件"
}

# 检查端口占用情况
Write-Info "检查常用端口占用情况..."
$TestPorts = @(8080, 9090, 9091, 9092)
foreach ($Port in $TestPorts) {
    try {
        $Connection = Test-NetConnection -ComputerName "localhost" -Port $Port -InformationLevel Quiet -WarningAction SilentlyContinue
        if ($Connection) {
            Write-Warning "端口 $Port 已被占用"
        } else {
            Write-Success "端口 $Port 可用"
        }
    } catch {
        Write-Success "端口 $Port 可用"
    }
}

if ($StartTestServer) {
    Write-Info "启动测试TCP服务器 (端口: $TestPort)..."
    
    # 创建简单的TCP测试服务器
    $TestServerScript = @"
`$listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Any, $TestPort)
`$listener.Start()
Write-Host "[TEST-SERVER] TCP服务器已启动，监听端口: $TestPort" -ForegroundColor Green

try {
    while (`$true) {
        `$client = `$listener.AcceptTcpClient()
        `$endpoint = `$client.Client.RemoteEndPoint
        Write-Host "[TEST-SERVER] 客户端连接: `$endpoint" -ForegroundColor Yellow
        
        `$stream = `$client.GetStream()
        `$buffer = New-Object byte[] 1024
        
        while (`$client.Connected) {
            try {
                `$bytesRead = `$stream.Read(`$buffer, 0, `$buffer.Length)
                if (`$bytesRead -eq 0) { break }
                
                `$data = [System.Text.Encoding]::UTF8.GetString(`$buffer, 0, `$bytesRead)
                Write-Host "[TEST-SERVER] 收到数据: `$data" -ForegroundColor Cyan
                
                # 回显数据
                `$response = "ECHO: `$data"
                `$responseBytes = [System.Text.Encoding]::UTF8.GetBytes(`$response)
                `$stream.Write(`$responseBytes, 0, `$responseBytes.Length)
                `$stream.Flush()
            } catch {
                break
            }
        }
        
        `$client.Close()
        Write-Host "[TEST-SERVER] 客户端断开: `$endpoint" -ForegroundColor Yellow
    }
} catch {
    Write-Host "[TEST-SERVER] 服务器异常: `$(`$_.Exception.Message)" -ForegroundColor Red
} finally {
    `$listener.Stop()
    Write-Host "[TEST-SERVER] 服务器已停止" -ForegroundColor Red
}
"@
    
    # 在新的PowerShell窗口中启动测试服务器
    Start-Process powershell -ArgumentList "-NoExit", "-Command", $TestServerScript
    Write-Success "测试服务器已在新窗口中启动"
}

Write-Info ""
Write-Info "测试建议:"
Write-Info "1. 启动MainAPP程序"
Write-Info "2. 加载XPluginTcpRelay插件"
Write-Info "3. 添加路由规则:"
Write-Info "   - A方地址: 127.0.0.1:$TestPort"
Write-Info "   - 本地监听端口: 9090"
Write-Info "4. 启动规则"
Write-Info "5. 使用telnet或其他TCP客户端连接到 127.0.0.1:9090"
Write-Info "6. 发送测试数据，观察日志输出"
Write-Info ""
Write-Success "测试脚本执行完成！"
