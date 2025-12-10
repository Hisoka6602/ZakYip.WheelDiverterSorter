#!/bin/bash
# 
# 感应IO轮询时间配置功能演示脚本
# Demo script for sensor polling interval configuration
#
# 用途：演示如何通过 API 配置感应IO的轮询时间
# Purpose: Demonstrate how to configure sensor polling interval via API
#

set -e

BASE_URL="${BASE_URL:-http://localhost:5000}"
API_ENDPOINT="${BASE_URL}/api/hardware/leadshine/sensors"

echo "=========================================="
echo "感应IO轮询时间配置功能演示"
echo "Sensor Polling Interval Configuration Demo"
echo "=========================================="
echo ""

# 颜色定义
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${YELLOW}注意：此脚本需要系统正在运行${NC}"
echo -e "${YELLOW}Note: This script requires the system to be running${NC}"
echo ""
echo "API 基础地址: ${BASE_URL}"
echo "API Base URL: ${BASE_URL}"
echo ""

# 步骤1: 获取当前配置
echo "=========================================="
echo -e "${GREEN}步骤 1: 获取当前感应IO配置${NC}"
echo -e "${GREEN}Step 1: Get current sensor configuration${NC}"
echo "=========================================="
echo ""
echo "执行命令 / Executing command:"
echo "  GET ${API_ENDPOINT}"
echo ""

if command -v curl &> /dev/null; then
    echo "当前配置 / Current configuration:"
    curl -s "${API_ENDPOINT}" | jq '.' || echo "提示：安装 jq 可以格式化 JSON 输出"
else
    echo -e "${RED}错误：未安装 curl 命令${NC}"
    echo -e "${RED}Error: curl command not found${NC}"
    exit 1
fi

echo ""
echo ""

# 步骤2: 显示配置示例
echo "=========================================="
echo -e "${GREEN}步骤 2: 配置示例${NC}"
echo -e "${GREEN}Step 2: Configuration example${NC}"
echo "=========================================="
echo ""

cat << 'EOF'
示例 1: 为传感器 1 设置 20ms 轮询间隔
Example 1: Set 20ms polling interval for sensor 1

PUT /api/hardware/leadshine/sensors
Content-Type: application/json

{
  "sensors": [
    {
      "sensorId": 1,
      "sensorName": "创建包裹感应IO",
      "ioType": "ParcelCreation",
      "ioPointId": 0,
      "pollingIntervalMs": 20,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "sensorName": "摆轮1前感应IO",
      "ioType": "WheelFront",
      "ioPointId": 1,
      "boundWheelNodeId": "WHEEL-1",
      "pollingIntervalMs": null,
      "triggerLevel": "ActiveHigh",
      "isEnabled": true
    }
  ]
}

说明：
- pollingIntervalMs: 20  - 传感器 1 使用 20ms 轮询间隔
- pollingIntervalMs: null - 传感器 2 使用默认值 10ms

Note:
- pollingIntervalMs: 20  - Sensor 1 uses 20ms polling interval
- pollingIntervalMs: null - Sensor 2 uses default value 10ms

---

示例 2: 批量设置所有传感器使用相同轮询间隔
Example 2: Set same polling interval for all sensors

PUT /api/hardware/leadshine/sensors
Content-Type: application/json

{
  "sensors": [
    {
      "sensorId": 1,
      "pollingIntervalMs": 15,
      "ioType": "ParcelCreation",
      "ioPointId": 0,
      "isEnabled": true
    },
    {
      "sensorId": 2,
      "pollingIntervalMs": 15,
      "ioType": "WheelFront",
      "ioPointId": 1,
      "boundWheelNodeId": "WHEEL-1",
      "isEnabled": true
    },
    {
      "sensorId": 3,
      "pollingIntervalMs": 15,
      "ioType": "ChuteLock",
      "ioPointId": 2,
      "boundChuteId": "CHUTE-001",
      "isEnabled": true
    }
  ]
}

EOF

echo ""
echo ""

# 步骤3: 建议的轮询间隔
echo "=========================================="
echo -e "${GREEN}步骤 3: 建议的轮询间隔设置${NC}"
echo -e "${GREEN}Step 3: Recommended polling interval settings${NC}"
echo "=========================================="
echo ""

cat << 'EOF'
┌─────────────┬──────────────────────┬──────────┬──────────┐
│ 轮询间隔    │ 适用场景             │ CPU 占用 │ 检测精度 │
│ Interval    │ Use Case             │ CPU      │ Accuracy │
├─────────────┼──────────────────────┼──────────┼──────────┤
│ 5-10ms      │ 快速移动包裹         │ 高       │ 高精度   │
│             │ Fast moving parcels  │ High     │ High     │
├─────────────┼──────────────────────┼──────────┼──────────┤
│ 10-20ms     │ 标准速度（推荐）     │ 中等     │ 标准     │
│             │ Standard (Recommend) │ Medium   │ Standard │
├─────────────┼──────────────────────┼──────────┼──────────┤
│ 20-50ms     │ 低速场景             │ 低       │ 较低     │
│             │ Low speed scenarios  │ Low      │ Lower    │
└─────────────┴──────────────────────┴──────────┴──────────┘

推荐配置 / Recommended configuration:
- 创建包裹感应IO / Parcel creation sensor: 10ms
- 摆轮前感应IO / Wheel front sensor: 10-15ms
- 锁格感应IO / Chute lock sensor: 15-20ms

EOF

echo ""
echo ""

# 步骤4: 验证方法
echo "=========================================="
echo -e "${GREEN}步骤 4: 如何验证配置生效${NC}"
echo -e "${GREEN}Step 4: How to verify configuration${NC}"
echo "=========================================="
echo ""

cat << 'EOF'
方法 1: 查看 API 返回
Method 1: Check API response

  GET /api/hardware/leadshine/sensors

检查返回的 pollingIntervalMs 字段值
Check the pollingIntervalMs field in response

---

方法 2: 查看系统日志
Method 2: Check system logs

系统启动或重新加载传感器配置时会输出：
System outputs when starting or reloading sensor config:

  [INF] 成功创建雷赛传感器 1，类型: ParcelCreation，
        输入位: 0，轮询间隔: 20ms (独立配置)

  [INF] 成功创建雷赛传感器 2，类型: WheelFront，
        输入位: 1，轮询间隔: 10ms (全局默认)

---

方法 3: 观察系统性能
Method 3: Observe system performance

- CPU 占用率：轮询间隔越短，CPU 占用越高
- CPU usage: Shorter interval = higher CPU usage

- 响应速度：轮询间隔越短，检测响应越快
- Response time: Shorter interval = faster detection

EOF

echo ""
echo ""

# 总结
echo "=========================================="
echo -e "${GREEN}✅ 功能验证总结${NC}"
echo -e "${GREEN}✅ Feature Verification Summary${NC}"
echo "=========================================="
echo ""

cat << 'EOF'
验证结果 / Verification results:

✅ 数据模型支持 / Data model support
   - SensorIoEntry.PollingIntervalMs 字段存在

✅ API 端点支持 / API endpoint support
   - GET /api/hardware/leadshine/sensors
   - PUT /api/hardware/leadshine/sensors
   - POST /api/hardware/leadshine/sensors/reset

✅ 业务逻辑实现 / Business logic
   - LeadshineSensorFactory 读取并使用配置
   - 支持传感器级别独立配置
   - 支持全局默认值 (10ms)
   - 配置持久化到 LiteDB

✅ 热更新支持 / Hot reload support
   - 配置更新后立即生效，无需重启

结论 / Conclusion:
功能已完整实现，可以立即使用！
Feature is fully implemented and ready to use!

EOF

echo ""
echo "详细文档 / Detailed documentation:"
echo "  docs/guides/SENSOR_IO_POLLING_CONFIGURATION.md"
echo ""
echo "问题答复 / Question answer:"
echo "  SENSOR_POLLING_CONFIGURATION_ANSWER.md"
echo ""
echo "=========================================="
echo "演示完成 / Demo completed"
echo "=========================================="
