#!/bin/bash

# ================================================================
# 场景 E 长跑仿真启动脚本
# Script to run Scenario E Long-Run Simulation
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

set -e

# Color codes for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default configuration
PARCEL_COUNT=${PARCEL_COUNT:-1000}
LONG_RUN_DURATION=${LONG_RUN_DURATION:-""}
START_MONITORING=${START_MONITORING:-"true"}

# Get the base directory (repository root)
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
REPO_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${CYAN}=========================================="
echo "场景 E 长跑仿真启动"
echo "Scenario E Long-Run Simulation"
echo -e "==========================================${NC}"
echo ""
echo -e "${BLUE}配置参数 / Configuration:${NC}"
echo "  包裹数量 / Parcel Count: $PARCEL_COUNT"
if [ -n "$LONG_RUN_DURATION" ]; then
    echo "  运行时长 / Duration: $LONG_RUN_DURATION"
else
    echo "  运行时长 / Duration: 不限制 / Unlimited"
fi
echo "  启动监控 / Start Monitoring: $START_MONITORING"
echo ""

# Function to check if a command exists
command_exists() {
    command -v "$1" >/dev/null 2>&1
}

# Function to check if a port is in use
port_in_use() {
    lsof -i:"$1" >/dev/null 2>&1 || netstat -an | grep -q ":$1.*LISTEN"
}

# Step 1: Start monitoring stack if requested
if [ "$START_MONITORING" = "true" ]; then
    echo -e "${YELLOW}步骤 1/3: 启动监控栈 (Prometheus + Grafana)${NC}"
    echo ""
    
    if command_exists docker-compose || command_exists docker; then
        cd "$REPO_ROOT"
        
        # Check if docker-compose.monitoring.yml exists
        if [ -f "docker-compose.monitoring.yml" ]; then
            echo "启动 Docker Compose 监控服务..."
            if command_exists docker-compose; then
                docker-compose -f docker-compose.monitoring.yml up -d
            else
                docker compose -f docker-compose.monitoring.yml up -d
            fi
            
            echo ""
            echo -e "${GREEN}✓${NC} 监控栈已启动"
            echo "  Prometheus: http://localhost:9090"
            echo "  Grafana:    http://localhost:3000 (admin/admin)"
            echo ""
            
            # Wait a moment for services to start
            sleep 3
        else
            echo -e "${YELLOW}⚠${NC} docker-compose.monitoring.yml 未找到，跳过监控栈启动"
            echo ""
        fi
    else
        echo -e "${YELLOW}⚠${NC} Docker 未安装，跳过监控栈启动"
        echo "  如需监控功能，请手动启动 Prometheus 和 Grafana"
        echo ""
    fi
else
    echo -e "${YELLOW}步骤 1/3: 跳过监控栈启动 (START_MONITORING=false)${NC}"
    echo ""
fi

# Step 2: Check if dotnet is installed
echo -e "${YELLOW}步骤 2/3: 检查环境${NC}"
echo ""

if ! command_exists dotnet; then
    echo -e "${RED}✗ 错误: 未找到 dotnet 命令${NC}"
    echo "  请安装 .NET 8.0 SDK: https://dotnet.microsoft.com/download"
    exit 1
fi

echo -e "${GREEN}✓${NC} .NET SDK 已安装: $(dotnet --version)"
echo ""

# Check if metrics port is available
if port_in_use 9091; then
    echo -e "${YELLOW}⚠${NC} 警告: 端口 9091 已被占用"
    echo "  Prometheus metrics 端点可能无法启动"
    echo "  请关闭占用该端口的程序或修改配置"
    echo ""
fi

# Step 3: Run Scenario E simulation
echo -e "${YELLOW}步骤 3/3: 启动场景 E 仿真${NC}"
echo ""

cd "$REPO_ROOT/ZakYip.WheelDiverterSorter.Simulation"

# Build the simulation arguments
ARGS=(
    "--Simulation:IsLongRunMode=true"
    "--Simulation:ParcelCount=$PARCEL_COUNT"
    "--Simulation:LineSpeedMmps=1000"
    "--Simulation:ParcelInterval=00:00:00.300"
    "--Simulation:SortingMode=RoundRobin"
    "--Simulation:ExceptionChuteId=11"
    "--Simulation:IsEnableRandomFriction=true"
    "--Simulation:IsEnableRandomDropout=false"
    "--Simulation:FrictionModel:MinFactor=0.95"
    "--Simulation:FrictionModel:MaxFactor=1.05"
    "--Simulation:MinSafeHeadwayMm=300"
    "--Simulation:MinSafeHeadwayTime=00:00:00.300"
    "--Simulation:DenseParcelStrategy=RouteToException"
    "--Simulation:MetricsPushIntervalSeconds=30"
    "--Simulation:IsEnableVerboseLogging=false"
    "--Simulation:IsPauseAtEnd=false"
)

# Add duration if specified
if [ -n "$LONG_RUN_DURATION" ]; then
    ARGS+=("--Simulation:LongRunDuration=$LONG_RUN_DURATION")
fi

echo "执行命令 / Executing:"
echo "  dotnet run -c Release -- ${ARGS[@]}"
echo ""

echo -e "${CYAN}=========================================="
echo "场景 E 仿真运行中..."
echo "Scenario E Simulation Running..."
echo -e "==========================================${NC}"
echo ""
echo -e "${BLUE}监控端点 / Monitoring Endpoints:${NC}"
echo "  Metrics:     http://localhost:9091/metrics"
echo "  Prometheus:  http://localhost:9090"
echo "  Grafana:     http://localhost:3000"
echo ""
echo -e "${BLUE}关键指标查询 / Key Metrics Queries:${NC}"
echo "  总包裹数:     sorting_total_parcels"
echo "  失败包裹:     sorting_failed_parcels_total"
echo "  成功延迟:     sorting_success_latency_seconds"
echo "  状态切换:     system_state_changes_total"
echo "  错分计数:     simulation_mis_sort_total"
echo ""
echo "按 Ctrl+C 停止仿真 / Press Ctrl+C to stop"
echo ""

# Run the simulation
dotnet run -c Release -- "${ARGS[@]}"

# Capture exit code
EXIT_CODE=$?

echo ""
echo -e "${CYAN}=========================================="
echo "场景 E 仿真完成"
echo "Scenario E Simulation Completed"
echo -e "==========================================${NC}"
echo ""

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓${NC} 仿真成功完成"
    echo ""
    echo -e "${BLUE}查看结果 / View Results:${NC}"
    echo "  1. 查看 Prometheus 指标:"
    echo "     curl http://localhost:9091/metrics | grep -E '(sorting|simulation)_'"
    echo ""
    echo "  2. 在 Grafana 中查看仪表板:"
    echo "     打开 http://localhost:3000 并导入 monitoring/grafana/dashboards/"
    echo ""
    echo "  3. 查询关键指标示例:"
    echo "     # 成功率 (每分钟)"
    echo "     rate(simulation_parcel_total{status=\"SortedToTargetChute\"}[5m]) * 60"
    echo ""
    echo "     # P95 延迟"
    echo "     histogram_quantile(0.95, rate(sorting_success_latency_seconds_bucket[5m]))"
    echo ""
else
    echo -e "${RED}✗${NC} 仿真失败 (退出码: $EXIT_CODE)"
    echo ""
fi

exit $EXIT_CODE
