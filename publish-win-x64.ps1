#!/usr/bin/env pwsh
# 发布脚本 - Windows x64 自包含部署
# Publish Script - Windows x64 Self-Contained Deployment

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = "./publish/win-x64"
)

Write-Host "开始发布 Windows x64 自包含版本..." -ForegroundColor Green
Write-Host "Publishing Windows x64 self-contained version..." -ForegroundColor Green

# 清理旧的发布输出
if (Test-Path $OutputPath) {
    Write-Host "清理旧的发布输出 / Cleaning old publish output..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $OutputPath
}

# 发布项目
dotnet publish src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj `
    --configuration $Configuration `
    --runtime win-x64 `
    --self-contained true `
    --output $OutputPath `
    -p:PublishReadyToRun=true

if ($LASTEXITCODE -eq 0) {
    Write-Host "`n发布成功！输出位置: $OutputPath" -ForegroundColor Green
    Write-Host "Published successfully! Output location: $OutputPath" -ForegroundColor Green
    Write-Host "`n可执行文件: $OutputPath\ZakYip.WheelDiverterSorter.Host.exe" -ForegroundColor Cyan
    Write-Host "Executable: $OutputPath\ZakYip.WheelDiverterSorter.Host.exe" -ForegroundColor Cyan
    
    # 显示文件大小
    $exePath = Join-Path $OutputPath "ZakYip.WheelDiverterSorter.Host.exe"
    if (Test-Path $exePath) {
        $size = (Get-Item $exePath).Length / 1MB
        Write-Host "`n可执行文件大小: $([math]::Round($size, 2)) MB" -ForegroundColor Yellow
        Write-Host "Executable size: $([math]::Round($size, 2)) MB" -ForegroundColor Yellow
    }
} else {
    Write-Host "`n发布失败！" -ForegroundColor Red
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}
