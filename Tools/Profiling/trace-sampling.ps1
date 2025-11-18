#!/usr/bin/env pwsh
# dotnet-trace 采样脚本
# 用于对运行中的.NET应用程序进行性能采样

param(
    [Parameter(Mandatory=$true)]
    [int]$ProcessId,
    
    [Parameter(Mandatory=$false)]
    [int]$Duration = 30,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = "trace-$(Get-Date -Format 'yyyyMMdd-HHmmss').nettrace",
    
    [Parameter(Mandatory=$false)]
    [string]$Profile = "cpu-sampling"
)

Write-Host "======================================"
Write-Host "dotnet-trace 性能采样工具"
Write-Host "======================================"
Write-Host ""
Write-Host "配置信息:"
Write-Host "  进程ID: $ProcessId"
Write-Host "  采样时长: $Duration 秒"
Write-Host "  输出文件: $OutputPath"
Write-Host "  采样配置: $Profile"
Write-Host ""

# 检查是否安装了 dotnet-trace
$traceInstalled = dotnet tool list -g | Select-String "dotnet-trace"
if (-not $traceInstalled) {
    Write-Host "未检测到 dotnet-trace 工具，正在安装..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-trace
    if ($LASTEXITCODE -ne 0) {
        Write-Host "安装 dotnet-trace 失败" -ForegroundColor Red
        exit 1
    }
}

# 检查进程是否存在
$process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Host "错误: 进程 $ProcessId 不存在" -ForegroundColor Red
    exit 1
}

Write-Host "开始采样进程: $($process.ProcessName) (PID: $ProcessId)" -ForegroundColor Green
Write-Host "采样进行中，请等待 $Duration 秒..." -ForegroundColor Cyan

# 执行采样
dotnet trace collect --process-id $ProcessId --duration $Duration --output $OutputPath --profile $Profile

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "✓ 采样完成!" -ForegroundColor Green
    Write-Host "追踪文件已保存至: $OutputPath"
    Write-Host ""
    Write-Host "分析方式:"
    Write-Host "  1. 使用 PerfView (Windows): 打开 .nettrace 文件"
    Write-Host "  2. 使用 Visual Studio: 文件 -> 打开 -> 文件 -> 选择 .nettrace"
    Write-Host "  3. 使用 speedscope (跨平台): https://www.speedscope.app/"
    Write-Host ""
} else {
    Write-Host ""
    Write-Host "✗ 采样失败" -ForegroundColor Red
    exit 1
}

# 显示文件大小
$fileSize = (Get-Item $OutputPath).Length / 1MB
Write-Host "追踪文件大小: $([math]::Round($fileSize, 2)) MB"

<#
.SYNOPSIS
对运行中的.NET应用程序进行性能采样

.DESCRIPTION
使用 dotnet-trace 工具对指定进程进行性能采样，生成 .nettrace 文件用于性能分析。

.PARAMETER ProcessId
目标进程的 PID (必需)

.PARAMETER Duration
采样时长（秒），默认 30 秒

.PARAMETER OutputPath
输出文件路径，默认为带时间戳的文件名

.PARAMETER Profile
采样配置文件:
  - cpu-sampling: CPU 采样（默认）
  - gc-verbose: 详细 GC 信息
  - gc-collect: GC 收集信息

.EXAMPLE
.\trace-sampling.ps1 -ProcessId 12345
对进程 12345 进行 30 秒的 CPU 采样

.EXAMPLE
.\trace-sampling.ps1 -ProcessId 12345 -Duration 60 -Profile gc-verbose
对进程 12345 进行 60 秒的详细 GC 采样

.NOTES
使用前请确保：
1. 已安装 .NET SDK
2. 目标进程是 .NET Core/.NET 5+ 应用
3. 有足够的磁盘空间存储追踪文件
#>
