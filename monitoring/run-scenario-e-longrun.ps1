# ================================================================
# 场景 E 长跑仿真启动脚本 (PowerShell)
# Script to run Scenario E Long-Run Simulation (PowerShell)
# ================================================================
#
# 功能说明 / Description:
# 启动场景 E 长时间仿真，包括：
# - 10 台摆轮，中间长度不一致
# - 异常口在末端 (ChuteId=11)
# - 每 300ms 创建包裹，默认总数 1000 个
# - 单包裹从入口到异常口约 2 分钟
# - 启用 Prometheus metrics 端点 (http://localhost:9091/metrics)
#
# This script starts Scenario E long-run simulation with:
# - 10 wheel diverters with varying segment lengths
# - Exception chute at the end (ChuteId=11)
# - Create parcels every 300ms, default 1000 parcels
# - Travel time ~2 minutes from entry to exception chute
# - Enable Prometheus metrics endpoint (http://localhost:9091/metrics)
#
# ================================================================

param(
    [int]$ParcelCount = 1000,
    [string]$LongRunDuration = "",
    [bool]$StartMonitoring = $true
)

$ErrorActionPreference = "Stop"

# Color functions
function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$ForegroundColor = "White"
    )
    Write-Host $Message -ForegroundColor $ForegroundColor
}

# Get the base directory (repository root)
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot = Split-Path -Parent $ScriptDir

Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput "场景 E 长跑仿真启动" "Cyan"
Write-ColorOutput "Scenario E Long-Run Simulation" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-Host ""
Write-ColorOutput "配置参数 / Configuration:" "Blue"
Write-Host "  包裹数量 / Parcel Count: $ParcelCount"
if ($LongRunDuration) {
    Write-Host "  运行时长 / Duration: $LongRunDuration"
} else {
    Write-Host "  运行时长 / Duration: 不限制 / Unlimited"
}
Write-Host "  启动监控 / Start Monitoring: $StartMonitoring"
Write-Host ""

# Function to check if a command exists
function Test-CommandExists {
    param([string]$Command)
    return $null -ne (Get-Command $Command -ErrorAction SilentlyContinue)
}

# Function to check if a port is in use
function Test-PortInUse {
    param([int]$Port)
    $connections = Get-NetTCPConnection -LocalPort $Port -ErrorAction SilentlyContinue
    return $null -ne $connections
}

# Step 1: Start monitoring stack if requested
if ($StartMonitoring) {
    Write-ColorOutput "步骤 1/3: 启动监控栈 (Prometheus + Grafana)" "Yellow"
    Write-Host ""
    
    if (Test-CommandExists "docker") {
        Set-Location $RepoRoot
        
        # Check if docker-compose.monitoring.yml exists
        if (Test-Path "docker-compose.monitoring.yml") {
            Write-Host "启动 Docker Compose 监控服务..."
            
            if (Test-CommandExists "docker-compose") {
                docker-compose -f docker-compose.monitoring.yml up -d
            } else {
                docker compose -f docker-compose.monitoring.yml up -d
            }
            
            Write-Host ""
            Write-ColorOutput "✓ 监控栈已启动" "Green"
            Write-Host "  Prometheus: http://localhost:9090"
            Write-Host "  Grafana:    http://localhost:3000 (admin/admin)"
            Write-Host ""
            
            # Wait a moment for services to start
            Start-Sleep -Seconds 3
        } else {
            Write-ColorOutput "⚠ docker-compose.monitoring.yml 未找到，跳过监控栈启动" "Yellow"
            Write-Host ""
        }
    } else {
        Write-ColorOutput "⚠ Docker 未安装，跳过监控栈启动" "Yellow"
        Write-Host "  如需监控功能，请手动启动 Prometheus 和 Grafana"
        Write-Host ""
    }
} else {
    Write-ColorOutput "步骤 1/3: 跳过监控栈启动 (StartMonitoring=`$false)" "Yellow"
    Write-Host ""
}

# Step 2: Check if dotnet is installed
Write-ColorOutput "步骤 2/3: 检查环境" "Yellow"
Write-Host ""

if (-not (Test-CommandExists "dotnet")) {
    Write-ColorOutput "✗ 错误: 未找到 dotnet 命令" "Red"
    Write-Host "  请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download"
    exit 1
}

$dotnetVersion = dotnet --version
Write-ColorOutput "✓ .NET SDK 已安装: $dotnetVersion" "Green"
Write-Host ""

# Check if metrics port is available
if (Test-PortInUse -Port 9091) {
    Write-ColorOutput "⚠ 警告: 端口 9091 已被占用" "Yellow"
    Write-Host "  Prometheus metrics 端点可能无法启动"
    Write-Host "  请关闭占用该端口的程序或修改配置"
    Write-Host ""
}

# Step 3: Run Scenario E simulation
Write-ColorOutput "步骤 3/3: 启动场景 E 仿真" "Yellow"
Write-Host ""

Set-Location "$RepoRoot\ZakYip.WheelDiverterSorter.Simulation"

# Build the simulation arguments
$Args = @(
    "--Simulation:IsLongRunMode=true",
    "--Simulation:ParcelCount=$ParcelCount",
    "--Simulation:LineSpeedMmps=1000",
    "--Simulation:ParcelInterval=00:00:00.300",
    "--Simulation:SortingMode=RoundRobin",
    "--Simulation:ExceptionChuteId=11",
    "--Simulation:IsEnableRandomFriction=true",
    "--Simulation:IsEnableRandomDropout=false",
    "--Simulation:FrictionModel:MinFactor=0.95",
    "--Simulation:FrictionModel:MaxFactor=1.05",
    "--Simulation:MinSafeHeadwayMm=300",
    "--Simulation:MinSafeHeadwayTime=00:00:00.300",
    "--Simulation:DenseParcelStrategy=RouteToException",
    "--Simulation:MetricsPushIntervalSeconds=30",
    "--Simulation:IsEnableVerboseLogging=false",
    "--Simulation:IsPauseAtEnd=false"
)

# Add duration if specified
if ($LongRunDuration) {
    $Args += "--Simulation:LongRunDuration=$LongRunDuration"
}

Write-Host "执行命令 / Executing:"
Write-Host "  dotnet run -c Release -- $($Args -join ' ')"
Write-Host ""

Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput "场景 E 仿真运行中..." "Cyan"
Write-ColorOutput "Scenario E Simulation Running..." "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-Host ""
Write-ColorOutput "监控端点 / Monitoring Endpoints:" "Blue"
Write-Host "  Metrics:     http://localhost:9091/metrics"
Write-Host "  Prometheus:  http://localhost:9090"
Write-Host "  Grafana:     http://localhost:3000"
Write-Host ""
Write-ColorOutput "关键指标查询 / Key Metrics Queries:" "Blue"
Write-Host "  总包裹数:     sorting_total_parcels"
Write-Host "  失败包裹:     sorting_failed_parcels_total"
Write-Host "  成功延迟:     sorting_success_latency_seconds"
Write-Host "  状态切换:     system_state_changes_total"
Write-Host "  错分计数:     simulation_mis_sort_total"
Write-Host ""
Write-Host "按 Ctrl+C 停止仿真 / Press Ctrl+C to stop"
Write-Host ""

# Run the simulation
$process = Start-Process -FilePath "dotnet" -ArgumentList "run","-c","Release","--",$Args -NoNewWindow -Wait -PassThru

# Capture exit code
$ExitCode = $process.ExitCode

Write-Host ""
Write-ColorOutput "==========================================" "Cyan"
Write-ColorOutput "场景 E 仿真完成" "Cyan"
Write-ColorOutput "Scenario E Simulation Completed" "Cyan"
Write-ColorOutput "==========================================" "Cyan"
Write-Host ""

if ($ExitCode -eq 0) {
    Write-ColorOutput "✓ 仿真成功完成" "Green"
    Write-Host ""
    Write-ColorOutput "查看结果 / View Results:" "Blue"
    Write-Host "  1. 查看 Prometheus 指标:"
    Write-Host "     Invoke-WebRequest -Uri http://localhost:9091/metrics | Select-String -Pattern '(sorting|simulation)_'"
    Write-Host ""
    Write-Host "  2. 在 Grafana 中查看仪表板:"
    Write-Host "     打开 http://localhost:3000 并导入 monitoring/grafana/dashboards/"
    Write-Host ""
    Write-Host "  3. 查询关键指标示例:"
    Write-Host "     # 成功率 (每分钟)"
    Write-Host "     rate(simulation_parcel_total{status=`"SortedToTargetChute`"}[5m]) * 60"
    Write-Host ""
    Write-Host "     # P95 延迟"
    Write-Host "     histogram_quantile(0.95, rate(sorting_success_latency_seconds_bucket[5m]))"
    Write-Host ""
} else {
    Write-ColorOutput "✗ 仿真失败 (退出码: $ExitCode)" "Red"
    Write-Host ""
}

exit $ExitCode
