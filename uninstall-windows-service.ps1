#!/usr/bin/env pwsh
# Windows Service 卸载脚本
# Windows Service Uninstallation Script

param(
    [string]$ServiceName = "WheelDiverterSorter"
)

# 检查管理员权限
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "错误：此脚本需要管理员权限运行" -ForegroundColor Red
    Write-Host "Error: This script requires administrator privileges" -ForegroundColor Red
    Write-Host "请右键点击 PowerShell 并选择'以管理员身份运行'" -ForegroundColor Yellow
    Write-Host "Please right-click PowerShell and select 'Run as Administrator'" -ForegroundColor Yellow
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Service 卸载程序" -ForegroundColor Cyan
Write-Host "Windows Service Uninstaller" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# 检查服务是否存在
$service = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if (-not $service) {
    Write-Host "服务 '$ServiceName' 不存在" -ForegroundColor Yellow
    Write-Host "Service '$ServiceName' does not exist" -ForegroundColor Yellow
    exit 0
}

Write-Host "服务名称 / Service Name: $ServiceName" -ForegroundColor White
Write-Host "当前状态 / Current Status: $($service.Status)" -ForegroundColor White
Write-Host ""

# 确认卸载
$response = Read-Host "确认要卸载服务吗？(y/n) / Are you sure you want to uninstall the service? (y/n)"

if ($response -ne 'y' -and $response -ne 'Y') {
    Write-Host "取消卸载 / Uninstallation cancelled" -ForegroundColor Yellow
    exit 0
}

try {
    # 停止服务（如果正在运行）
    if ($service.Status -eq 'Running') {
        Write-Host "正在停止服务... / Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction Stop
        Write-Host "服务已停止 / Service stopped" -ForegroundColor Green
        
        # 等待服务完全停止
        Start-Sleep -Seconds 2
    }
    
    # 删除服务
    Write-Host "正在删除服务... / Removing service..." -ForegroundColor Yellow
    sc.exe delete $ServiceName
    
    # 等待服务完全删除
    Start-Sleep -Seconds 2
    
    # 验证服务是否已删除
    $serviceCheck = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue
    
    if ($serviceCheck) {
        Write-Host ""
        Write-Host "警告：服务可能未完全删除，请重启计算机后重试" -ForegroundColor Yellow
        Write-Host "Warning: Service may not be completely removed, please restart the computer and try again" -ForegroundColor Yellow
    } else {
        Write-Host ""
        Write-Host "服务卸载成功！/ Service uninstalled successfully!" -ForegroundColor Green
    }
    
} catch {
    Write-Host ""
    Write-Host "错误：卸载服务失败 / Error: Failed to uninstall service" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "注意：服务卸载后，相关的日志和数据文件仍然保留" -ForegroundColor Cyan
Write-Host "Note: After uninstalling the service, log and data files are still retained" -ForegroundColor Cyan
Write-Host ""
