# ========================================
# TCP数据转发系统插件自动复制脚本 (PowerShell版本)
# 编译后自动复制到MainAPP程序插件库
# ========================================

param(
    [string]$Configuration = "Debug",
    [switch]$Force,
    [switch]$Verbose
)

# 设置错误处理
$ErrorActionPreference = "Stop"

# 颜色输出函数
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Write-Info { param([string]$Message) Write-ColorOutput "[INFO] $Message" "Cyan" }
function Write-Success { param([string]$Message) Write-ColorOutput "[SUCCESS] $Message" "Green" }
function Write-Warning { param([string]$Message) Write-ColorOutput "[WARNING] $Message" "Yellow" }
function Write-Error { param([string]$Message) Write-ColorOutput "[ERROR] $Message" "Red" }

try {
    Write-Info "开始复制XPluginTcpRelay插件到MainAPP..."
    
    # 设置路径变量
    $ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $ProjectRoot = Split-Path -Parent $ScriptDir
    $SourceDir = Join-Path $ProjectRoot "bin\$Configuration\net8.0-windows"
    $TargetDir = "D:\windows11\installed_files\dev_tools\JetBrains\codespace\Rider\WinFormsApp2\MainAPP\bin\Debug\net8.0-windows\XPlugins"
    $PluginName = "XPluginTcpRelay"
    $PluginTargetDir = Join-Path $TargetDir $PluginName
    
    if ($Verbose) {
        Write-Info "配置: $Configuration"
        Write-Info "源目录: $SourceDir"
        Write-Info "目标目录: $PluginTargetDir"
    }
    
    # 检查源目录是否存在
    if (-not (Test-Path $SourceDir)) {
        Write-Error "源目录不存在: $SourceDir"
        Write-Info "请先编译XPluginTcpRelay项目"
        exit 1
    }
    
    # 检查主要插件文件是否存在
    $MainDll = Join-Path $SourceDir "$PluginName.dll"
    if (-not (Test-Path $MainDll)) {
        Write-Error "插件主文件不存在: $MainDll"
        Write-Info "请确保项目编译成功"
        exit 1
    }
    
    # 创建目标目录
    if (-not (Test-Path $TargetDir)) {
        Write-Info "创建插件根目录: $TargetDir"
        New-Item -ItemType Directory -Path $TargetDir -Force | Out-Null
    }
    
    if (-not (Test-Path $PluginTargetDir)) {
        Write-Info "创建插件专用目录: $PluginTargetDir"
        New-Item -ItemType Directory -Path $PluginTargetDir -Force | Out-Null
    }
    
    # 检查MainAPP是否正在运行
    $MainAppProcess = Get-Process -Name "XPanel" -ErrorAction SilentlyContinue
    if ($MainAppProcess) {
        if ($Force) {
            Write-Warning "强制模式：MainAPP正在运行，尝试终止进程..."
            $MainAppProcess | Stop-Process -Force
            Start-Sleep -Seconds 2
        } else {
            Write-Warning "检测到MainAPP正在运行，建议先关闭程序或使用 -Force 参数"
        }
    }
    
    # 定义要复制的文件列表
    $FilesToCopy = @(
        @{ Source = "$PluginName.dll"; Required = $true; Description = "插件主文件" }
        @{ Source = "$PluginName.pdb"; Required = $false; Description = "调试信息文件" }
        @{ Source = "$PluginName.deps.json"; Required = $false; Description = "依赖信息文件" }
        @{ Source = "relay_config.json"; Required = $false; Description = "配置文件模板" }
    )
    
    # 复制文件
    $CopiedFiles = @()
    foreach ($FileInfo in $FilesToCopy) {
        $SourceFile = Join-Path $SourceDir $FileInfo.Source
        $TargetFile = Join-Path $PluginTargetDir $FileInfo.Source
        
        if (Test-Path $SourceFile) {
            Write-Info "复制 $($FileInfo.Description): $($FileInfo.Source)"
            Copy-Item $SourceFile $TargetFile -Force
            $CopiedFiles += $FileInfo.Source
        } elseif ($FileInfo.Required) {
            Write-Error "必需文件不存在: $SourceFile"
            exit 1
        } else {
            if ($Verbose) {
                Write-Info "可选文件不存在，跳过: $($FileInfo.Source)"
            }
        }
    }
    
    # 复制资源目录（如果存在）
    $ResourcesDir = Join-Path $SourceDir "Resources"
    if (Test-Path $ResourcesDir) {
        $TargetResourcesDir = Join-Path $PluginTargetDir "Resources"
        Write-Info "复制资源目录..."
        Copy-Item $ResourcesDir $TargetResourcesDir -Recurse -Force
    }
    
    # 创建插件信息文件
    $PluginInfo = @{
        Name = $PluginName
        Version = "1.0.0"
        Description = "TCP数据转发系统插件"
        Author = "XPlugin Team"
        CopyTime = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
        Configuration = $Configuration
        Files = $CopiedFiles
    }
    
    $PluginInfoFile = Join-Path $PluginTargetDir "plugin-info.json"
    $PluginInfo | ConvertTo-Json -Depth 3 | Out-File $PluginInfoFile -Encoding UTF8
    
    # 显示结果
    Write-Success "插件复制完成！"
    Write-Info "插件位置: $PluginTargetDir"
    Write-Info "已复制文件:"
    foreach ($File in $CopiedFiles) {
        Write-Host "  - $File" -ForegroundColor Gray
    }
    
    # 验证文件完整性
    $MainDllTarget = Join-Path $PluginTargetDir "$PluginName.dll"
    if (Test-Path $MainDllTarget) {
        $FileInfo = Get-Item $MainDllTarget
        Write-Info "主文件大小: $([math]::Round($FileInfo.Length / 1KB, 2)) KB"
        Write-Info "修改时间: $($FileInfo.LastWriteTime)"
    }
    
    Write-Success "所有操作完成！"
    
    if ($MainAppProcess -and -not $Force) {
        Write-Warning "建议重启MainAPP以加载新插件"
    }
    
} catch {
    Write-Error "脚本执行失败: $($_.Exception.Message)"
    Write-Host $_.ScriptStackTrace -ForegroundColor Red
    exit 1
}
