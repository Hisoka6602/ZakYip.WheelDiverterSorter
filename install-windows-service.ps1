#!/usr/bin/env pwsh
# Windows Service 安装脚本
# Windows Service Installation Script

param(
    [string]$ServiceName = "WheelDiverterSorter",
    [string]$DisplayName = "摆轮分拣系统服务 (Wheel Diverter Sorter Service)",
    [string]$Description = "直线摆轮分拣系统服务，提供自动化包裹分拣功能 / Linear wheel diverter sorting system service providing automated parcel sorting",
    [string]$BinaryPath = "",
    [ValidateSet("Automatic", "Manual", "Disabled")]
    [string]$StartupType = "Automatic"
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

# 如果未指定二进制路径，使用默认的发布输出路径
if ([string]::IsNullOrEmpty($BinaryPath)) {
    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $BinaryPath = Join-Path $scriptDir "publish\win-x64\ZakYip.WheelDiverterSorter.Host.exe"
}

# 验证二进制文件是否存在
if (-not (Test-Path $BinaryPath)) {
    Write-Host "错误：找不到可执行文件: $BinaryPath" -ForegroundColor Red
    Write-Host "Error: Executable not found: $BinaryPath" -ForegroundColor Red
    Write-Host ""
    Write-Host "请先运行发布脚本生成可执行文件：" -ForegroundColor Yellow
    Write-Host "Please run the publish script to generate the executable first:" -ForegroundColor Yellow
    Write-Host "  .\publish-win-x64.ps1" -ForegroundColor Cyan
    exit 1
}

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Windows Service 安装程序" -ForegroundColor Cyan
Write-Host "Windows Service Installer" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "服务名称 / Service Name: $ServiceName" -ForegroundColor White
Write-Host "显示名称 / Display Name: $DisplayName" -ForegroundColor White
Write-Host "服务描述 / Description: $Description" -ForegroundColor White
Write-Host "可执行文件路径 / Binary Path: $BinaryPath" -ForegroundColor White
Write-Host "启动类型 / Startup Type: $StartupType" -ForegroundColor White
Write-Host ""

# 检查服务是否已存在
$existingService = Get-Service -Name $ServiceName -ErrorAction SilentlyContinue

if ($existingService) {
    Write-Host "警告：服务 '$ServiceName' 已经存在" -ForegroundColor Yellow
    Write-Host "Warning: Service '$ServiceName' already exists" -ForegroundColor Yellow
    
    $response = Read-Host "是否要先删除现有服务？(y/n) / Do you want to remove the existing service first? (y/n)"
    
    if ($response -eq 'y' -or $response -eq 'Y') {
        Write-Host "正在停止服务... / Stopping service..." -ForegroundColor Yellow
        Stop-Service -Name $ServiceName -Force -ErrorAction SilentlyContinue
        
        Write-Host "正在删除服务... / Removing service..." -ForegroundColor Yellow
        sc.exe delete $ServiceName
        
        # 等待服务完全删除
        Start-Sleep -Seconds 2
    } else {
        Write-Host "取消安装 / Installation cancelled" -ForegroundColor Yellow
        exit 0
    }
}

try {
    # 创建服务
    Write-Host "正在创建服务... / Creating service..." -ForegroundColor Green
    
    New-Service -Name $ServiceName `
                -BinaryPathName $BinaryPath `
                -DisplayName $DisplayName `
                -Description $Description `
                -StartupType $StartupType `
                -ErrorAction Stop
    
    Write-Host ""
    Write-Host "服务创建成功！/ Service created successfully!" -ForegroundColor Green
    Write-Host ""
    
    # 询问是否立即启动服务
    $response = Read-Host "是否立即启动服务？(y/n) / Do you want to start the service now? (y/n)"
    
    if ($response -eq 'y' -or $response -eq 'Y') {
        Write-Host "正在启动服务... / Starting service..." -ForegroundColor Yellow
        Start-Service -Name $ServiceName
        
        # 等待服务启动
        Start-Sleep -Seconds 2
        
        # 检查服务状态
        $service = Get-Service -Name $ServiceName
        if ($service.Status -eq 'Running') {
            Write-Host ""
            Write-Host "服务已成功启动！/ Service started successfully!" -ForegroundColor Green
        } else {
            Write-Host ""
            Write-Host "警告：服务启动失败，当前状态: $($service.Status)" -ForegroundColor Red
            Write-Host "Warning: Service failed to start, current status: $($service.Status)" -ForegroundColor Red
            Write-Host ""
            Write-Host "请检查日志文件以获取更多信息 / Please check the log files for more information" -ForegroundColor Yellow
        }
    }
    
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "服务管理命令 / Service Management Commands:" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "启动服务 / Start Service:" -ForegroundColor White
    Write-Host "  Start-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host ""
    Write-Host "停止服务 / Stop Service:" -ForegroundColor White
    Write-Host "  Stop-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host ""
    Write-Host "重启服务 / Restart Service:" -ForegroundColor White
    Write-Host "  Restart-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host ""
    Write-Host "查看服务状态 / Check Service Status:" -ForegroundColor White
    Write-Host "  Get-Service -Name $ServiceName" -ForegroundColor Gray
    Write-Host ""
    Write-Host "卸载服务 / Uninstall Service:" -ForegroundColor White
    Write-Host "  .\uninstall-windows-service.ps1" -ForegroundColor Gray
    Write-Host ""
    
} catch {
    Write-Host ""
    Write-Host "错误：创建服务失败 / Error: Failed to create service" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
}
