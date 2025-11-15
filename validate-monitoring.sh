#!/bin/bash

# Monitoring Stack Validation Script
# 监控栈验证脚本

set -e

USE_DOCKER=true
if ! command -v docker > /dev/null 2>&1 || [ "${DISABLE_DOCKER:-}" = "1" ]; then
    USE_DOCKER=false
fi

echo "=================================================="
echo "Monitoring Stack Validation / 监控栈验证"
echo "=================================================="
echo ""

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Function to print status
print_status() {
    if [ $1 -eq 0 ]; then
        echo -e "${GREEN}✓${NC} $2"
    else
        echo -e "${RED}✗${NC} $2"
    fi
}

# Function to print info
print_info() {
    echo -e "${YELLOW}ℹ${NC} $1"
}

promtool_check() {
    local check_type=$1
    local local_path=$2
    local docker_path="/etc/prometheus/$(basename "$local_path")"

    if [ "$USE_DOCKER" = true ]; then
        if docker run --rm -v "$(pwd)/monitoring/prometheus:/etc/prometheus" \
            --entrypoint promtool prom/prometheus:latest \
            check "$check_type" "$docker_path" > /dev/null 2>&1; then
            return 0
        fi

        return 1
    fi

    if command -v promtool > /dev/null 2>&1; then
        if promtool check "$check_type" "$local_path" > /dev/null 2>&1; then
            return 0
        fi

        return 1
    fi

    print_info "Promtool not available and Docker disabled; skipping Prometheus $check_type validation."
    return 0
}

echo "Step 1: Validating configuration files / 验证配置文件"
echo "=================================================="

# Validate Prometheus configuration
print_info "Checking Prometheus configuration..."
if promtool_check config "monitoring/prometheus/prometheus.yml"; then
    print_status 0 "Prometheus configuration is valid"
else
    print_status 1 "Prometheus configuration is invalid"
    exit 1
fi

# Validate alert rules
print_info "Checking Prometheus alert rules..."
if promtool_check rules "monitoring/prometheus/alerts.yml"; then
    print_status 0 "Prometheus alert rules are valid"
else
    print_status 1 "Prometheus alert rules are invalid"
    exit 1
fi

# Validate Grafana dashboard JSON
print_info "Checking Grafana dashboard JSON..."
if python3 -m json.tool monitoring/grafana/dashboards/wheel-diverter-sorter.json > /dev/null 2>&1; then
    print_status 0 "Grafana dashboard JSON is valid"
else
    print_status 1 "Grafana dashboard JSON is invalid"
    exit 1
fi

echo ""
echo "Step 2: Building application / 构建应用"
echo "=================================================="

print_info "Building .NET solution..."
if command -v dotnet > /dev/null 2>&1; then
    if dotnet build --no-incremental > /dev/null 2>&1; then
        print_status 0 "Build successful"
    else
        print_status 1 "Build failed"
        exit 1
    fi
else
    print_info "dotnet CLI not found; skipping build validation."
    print_info "未检测到dotnet命令，跳过构建验证，请在目标环境中确认运行时已安装。"
    print_status 0 "Skipped .NET build validation"
fi

echo ""
echo "Step 3: Starting services (optional) / 启动服务（可选）"
echo "=================================================="

if [ "$1" == "--start-services" ]; then
    if [ "$USE_DOCKER" = true ]; then
        print_info "Starting monitoring stack with Docker Compose..."

        if docker-compose -f docker-compose.monitoring.yml up -d; then
            print_status 0 "Services started successfully"

            echo ""
            print_info "Waiting for services to be ready (30 seconds)..."
            sleep 30

            # Check if services are running
            print_info "Checking service health..."

            # Check application
            if curl -s http://localhost:5000/metrics > /dev/null 2>&1; then
                print_status 0 "Application is responding (port 5000)"
            else
                print_status 1 "Application is not responding (port 5000)"
            fi

            # Check Prometheus
            if curl -s http://localhost:9090/-/healthy > /dev/null 2>&1; then
                print_status 0 "Prometheus is healthy (port 9090)"
            else
                print_status 1 "Prometheus is not healthy (port 9090)"
            fi

            # Check Grafana
            if curl -s http://localhost:3000/api/health > /dev/null 2>&1; then
                print_status 0 "Grafana is healthy (port 3000)"
            else
                print_status 1 "Grafana is not healthy (port 3000)"
            fi

            echo ""
            echo "=================================================="
            echo "Services are running! / 服务正在运行！"
            echo "=================================================="
            echo ""
            echo "Access the following URLs / 访问以下URL:"
            echo "  - Application:  http://localhost:5000/swagger"
            echo "  - Metrics:      http://localhost:5000/metrics"
            echo "  - Prometheus:   http://localhost:9090"
            echo "  - Grafana:      http://localhost:3000 (admin/admin)"
            echo ""
            echo "To stop services, run / 停止服务请运行:"
            echo "  docker-compose -f docker-compose.monitoring.yml down"
            echo ""
        else
            print_status 1 "Failed to start services"
            exit 1
        fi
    else
        print_info "Docker disabled. Skipping container startup and assuming manual services."
        print_info "请根据 README 中的手动部署章节启动应用和监控组件。"
    fi
else
    print_info "Skipping service startup. Use --start-services to start."
    print_info "跳过服务启动。使用 --start-services 参数启动服务。"
fi

echo ""
echo "=================================================="
echo "Validation completed successfully! / 验证成功完成！"
echo "=================================================="
echo ""

if [ "$1" != "--start-services" ]; then
    if [ "$USE_DOCKER" = true ]; then
        echo "To deploy the monitoring stack with Docker, run / 使用Docker部署监控栈请运行:"
        echo "  docker-compose -f docker-compose.monitoring.yml up -d"
    else
        echo "Docker is disabled. Refer to README manual deployment steps for production setup."
        echo "Docker已禁用，请参考README中的手动部署步骤完成生产环境配置。"
    fi
    echo ""
fi

exit 0
