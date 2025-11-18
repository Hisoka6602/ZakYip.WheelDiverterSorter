#!/usr/bin/env pwsh
# dotnet-counters 实时监控脚本
# 用于实时监控.NET应用程序的性能指标

param(
    [Parameter(Mandatory=$true)]
    [int]$ProcessId,
    
    [Parameter(Mandatory=$false)]
    [int]$RefreshInterval = 1,
    
    [Parameter(Mandatory=$false)]
    [string]$OutputFile = "",
    
    [Parameter(Mandatory=$false)]
    [switch]$GcOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$CpuOnly,
    
    [Parameter(Mandatory=$false)]
    [switch]$MemoryOnly
)

Write-Host "======================================"
Write-Host "dotnet-counters 实时监控工具"
Write-Host "======================================"
Write-Host ""

# 检查是否安装了 dotnet-counters
$countersInstalled = dotnet tool list -g | Select-String "dotnet-counters"
if (-not $countersInstalled) {
    Write-Host "未检测到 dotnet-counters 工具，正在安装..." -ForegroundColor Yellow
    dotnet tool install --global dotnet-counters
    if ($LASTEXITCODE -ne 0) {
        Write-Host "安装 dotnet-counters 失败" -ForegroundColor Red
        exit 1
    }
}

# 检查进程是否存在
$process = Get-Process -Id $ProcessId -ErrorAction SilentlyContinue
if (-not $process) {
    Write-Host "错误: 进程 $ProcessId 不存在" -ForegroundColor Red
    exit 1
}

Write-Host "监控进程: $($process.ProcessName) (PID: $ProcessId)" -ForegroundColor Green
Write-Host "刷新间隔: $RefreshInterval 秒" -ForegroundColor Cyan
Write-Host ""

# 构建计数器列表
$counters = @()

if ($GcOnly) {
    # 仅 GC 指标
    $counters = @(
        "System.Runtime[gen-0-gc-count]",
        "System.Runtime[gen-1-gc-count]",
        "System.Runtime[gen-2-gc-count]",
        "System.Runtime[gen-0-size]",
        "System.Runtime[gen-1-size]",
        "System.Runtime[gen-2-size]",
        "System.Runtime[loh-size]",
        "System.Runtime[alloc-rate]",
        "System.Runtime[gc-heap-size]",
        "System.Runtime[time-in-gc]"
    )
    Write-Host "监控模式: GC 指标" -ForegroundColor Yellow
} elseif ($CpuOnly) {
    # 仅 CPU 指标
    $counters = @(
        "System.Runtime[cpu-usage]",
        "System.Runtime[threadpool-thread-count]",
        "System.Runtime[monitor-lock-contention-count]",
        "System.Runtime[threadpool-queue-length]",
        "System.Runtime[threadpool-completed-items-count]"
    )
    Write-Host "监控模式: CPU 和线程池指标" -ForegroundColor Yellow
} elseif ($MemoryOnly) {
    # 仅内存指标
    $counters = @(
        "System.Runtime[working-set]",
        "System.Runtime[gc-heap-size]",
        "System.Runtime[alloc-rate]",
        "System.Runtime[gen-0-size]",
        "System.Runtime[gen-1-size]",
        "System.Runtime[gen-2-size]",
        "System.Runtime[loh-size]"
    )
    Write-Host "监控模式: 内存指标" -ForegroundColor Yellow
} else {
    # 默认：综合指标
    $counters = @(
        "System.Runtime[cpu-usage]",
        "System.Runtime[working-set]",
        "System.Runtime[gc-heap-size]",
        "System.Runtime[gen-0-gc-count]",
        "System.Runtime[gen-1-gc-count]",
        "System.Runtime[gen-2-gc-count]",
        "System.Runtime[alloc-rate]",
        "System.Runtime[threadpool-thread-count]",
        "System.Runtime[threadpool-queue-length]",
        "System.Runtime[exception-count]",
        "System.Runtime[time-in-gc]",
        "System.Runtime[monitor-lock-contention-count]"
    )
    Write-Host "监控模式: 综合指标" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "按 Ctrl+C 停止监控" -ForegroundColor Cyan
Write-Host "======================================"
Write-Host ""

# 构建命令参数
$counterArgs = @(
    "monitor",
    "--process-id", $ProcessId,
    "--refresh-interval", $RefreshInterval,
    "--counters", ($counters -join ",")
)

if ($OutputFile) {
    $counterArgs += "--output", $OutputFile
    Write-Host "输出将保存到: $OutputFile" -ForegroundColor Green
}

# 执行监控
& dotnet counters @counterArgs

if ($OutputFile -and (Test-Path $OutputFile)) {
    Write-Host ""
    Write-Host "✓ 监控数据已保存至: $OutputFile" -ForegroundColor Green
}

<#
.SYNOPSIS
实时监控.NET应用程序的性能指标

.DESCRIPTION
使用 dotnet-counters 工具实时监控指定进程的性能计数器，包括 CPU、内存、GC 等信息。

.PARAMETER ProcessId
目标进程的 PID (必需)

.PARAMETER RefreshInterval
刷新间隔（秒），默认 1 秒

.PARAMETER OutputFile
可选：将监控数据保存到 CSV 文件

.PARAMETER GcOnly
仅监控 GC 相关指标

.PARAMETER CpuOnly
仅监控 CPU 和线程池相关指标

.PARAMETER MemoryOnly
仅监控内存相关指标

.EXAMPLE
.\counters-monitor.ps1 -ProcessId 12345
监控进程 12345 的综合性能指标

.EXAMPLE
.\counters-monitor.ps1 -ProcessId 12345 -GcOnly
仅监控进程 12345 的 GC 指标

.EXAMPLE
.\counters-monitor.ps1 -ProcessId 12345 -OutputFile metrics.csv
监控进程 12345 并将数据保存到 CSV 文件

.EXAMPLE
.\counters-monitor.ps1 -ProcessId 12345 -RefreshInterval 5
每 5 秒刷新一次监控数据

.NOTES
使用前请确保：
1. 已安装 .NET SDK
2. 目标进程是 .NET Core/.NET 5+ 应用
3. 有权限访问目标进程

常见指标说明：
- cpu-usage: CPU 使用率 (%)
- working-set: 工作集内存 (MB)
- gc-heap-size: GC 堆大小 (MB)
- gen-X-gc-count: 第 X 代 GC 次数
- alloc-rate: 内存分配速率 (MB/s)
- time-in-gc: GC 时间占比 (%)
- threadpool-thread-count: 线程池线程数
- exception-count: 异常次数
#>
