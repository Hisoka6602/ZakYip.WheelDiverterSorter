# 面板配置与仿真 API 使用指南

## 概述

本文档介绍电柜操作面板的配置和仿真功能的 API 使用方法。

面板系统包含三个主要控制器：
1. **PanelConfigController** (`/api/config/panel`) - 面板配置管理
2. **PanelSimulationController** (`/api/simulation/panel`) - 面板硬件仿真（按钮/信号塔）
3. **SimulationPanelController** (`/api/sim/panel`) - 系统状态控制（启动/停止/急停）

## 一、面板配置 API (`/api/config/panel`)

### 1.1 查询当前面板配置

**端点**: `GET /api/config/panel`

**响应示例**:
```json
{
  "enabled": true,
  "useSimulation": true,
  "pollingIntervalMs": 100,
  "debounceMs": 50
}
```

**字段说明**:
- `enabled`: 是否启用面板功能
- `useSimulation`: 是否使用仿真模式（true=仿真，false=硬件）
- `pollingIntervalMs`: 按钮轮询间隔（毫秒）
- `debounceMs`: 按钮防抖时间（毫秒）

### 1.2 更新面板配置

**端点**: `PUT /api/config/panel`

**请求示例**:
```json
{
  "enabled": true,
  "useSimulation": true,
  "pollingIntervalMs": 100,
  "debounceMs": 50
}
```

**参数约束**:
- `pollingIntervalMs`: 50-1000 毫秒
- `debounceMs`: 10-500 毫秒
- `debounceMs` 必须小于 `pollingIntervalMs`

**成功响应**: 200 OK，返回更新后的配置

**错误响应**:
- 400 Bad Request - 参数验证失败
- 500 Internal Server Error - 服务器错误

### 1.3 重置为默认配置

**端点**: `POST /api/config/panel/reset`

**默认配置**:
```json
{
  "enabled": false,
  "useSimulation": true,
  "pollingIntervalMs": 100,
  "debounceMs": 50
}
```

### 1.4 获取配置模板

**端点**: `GET /api/config/panel/template`

返回默认配置模板，可用作参考或初始配置。

## 二、面板仿真 API (`/api/simulation/panel`)

### 2.1 模拟按下按钮

**端点**: `POST /api/simulation/panel/press?buttonType={buttonType}`

**按钮类型**:
- `Start` - 启动按钮
- `Stop` - 停止按钮
- `Emergency` - 急停按钮
- `Reset` - 复位按钮

**示例**:
```bash
POST /api/simulation/panel/press?buttonType=Start
```

**成功响应**:
```json
{
  "message": "已模拟按下按钮: Start",
  "buttonType": "Start"
}
```

**注意**: 仅在仿真模式下可用。

### 2.2 模拟释放按钮

**端点**: `POST /api/simulation/panel/release?buttonType={buttonType}`

**示例**:
```bash
POST /api/simulation/panel/release?buttonType=Start
```

模拟释放按钮，通常与 press 配合使用。

### 2.3 查询面板状态

**端点**: `GET /api/simulation/panel/state`

**响应示例**:
```json
{
  "buttons": [
    {
      "buttonType": "Start",
      "isPressed": false,
      "lastChangedAt": "2025-11-21T14:00:00",
      "pressedDurationMs": 0
    }
  ],
  "signalTower": [
    {
      "channel": "Red",
      "isActive": false,
      "isBlinking": false,
      "blinkIntervalMs": 0
    }
  ]
}
```

### 2.4 重置所有按钮状态

**端点**: `POST /api/simulation/panel/reset`

将所有按钮重置为未按下状态。仅在仿真模式下可用。

### 2.5 查询信号塔历史

**端点**: `GET /api/simulation/panel/signal-tower/history`

**响应示例**:
```json
{
  "count": 5,
  "changes": [
    {
      "channel": "Red",
      "isActive": true,
      "isBlinking": false,
      "changedAt": "2025-11-21T14:00:00"
    }
  ]
}
```

## 三、系统状态控制 API (`/api/sim/panel`)

### 3.1 启动系统

**端点**: `POST /api/sim/panel/start`

**响应示例**:
```json
{
  "success": true,
  "message": "系统已启动",
  "currentState": "Running",
  "previousState": "Ready"
}
```

触发系统状态切换到 **运行** 状态。

### 3.2 停止系统

**端点**: `POST /api/sim/panel/stop`

**响应示例**:
```json
{
  "success": true,
  "message": "系统已停止",
  "currentState": "Ready",
  "previousState": "Running"
}
```

触发系统状态切换到 **就绪** 状态。

### 3.3 急停

**端点**: `POST /api/sim/panel/emergency-stop`

**响应示例**:
```json
{
  "success": true,
  "message": "系统已急停",
  "currentState": "EmergencyStop",
  "previousState": "Running"
}
```

立即触发系统进入 **急停** 状态，所有运动停止。

### 3.4 急停复位

**端点**: `POST /api/sim/panel/emergency-reset`

**响应示例**:
```json
{
  "success": true,
  "message": "急停已解除",
  "currentState": "Ready",
  "previousState": "EmergencyStop"
}
```

解除急停状态，系统切换回 **就绪** 状态。

### 3.5 查询系统状态

**端点**: `GET /api/sim/panel/state`

**响应示例**:
```json
{
  "currentState": "Running",
  "stateValue": 2,
  "canCreateParcel": true,
  "timestamp": "2025-11-21T14:00:00"
}
```

**系统状态枚举**:
- `Init` (0) - 初始化
- `Ready` (1) - 就绪
- `Running` (2) - 运行
- `Paused` (3) - 暂停
- `EmergencyStop` (4) - 急停
- `Fault` (5) - 故障

## 四、完整工作流程示例

### 4.1 配置面板并启动系统

```bash
# 1. 查询当前配置
GET /api/config/panel

# 2. 更新配置（启用面板，使用仿真模式）
PUT /api/config/panel
{
  "enabled": true,
  "useSimulation": true,
  "pollingIntervalMs": 100,
  "debounceMs": 50
}

# 3. 查询系统状态
GET /api/sim/panel/state

# 4. 启动系统
POST /api/sim/panel/start

# 5. 验证系统状态
GET /api/sim/panel/state
# 应该返回 "currentState": "Running"
```

### 4.2 仿真面板按钮操作

```bash
# 1. 查询面板状态
GET /api/simulation/panel/state

# 2. 模拟按下启动按钮
POST /api/simulation/panel/press?buttonType=Start

# 3. 等待一段时间

# 4. 模拟释放启动按钮
POST /api/simulation/panel/release?buttonType=Start

# 5. 查询按钮状态变化
GET /api/simulation/panel/state
```

### 4.3 急停与复位流程

```bash
# 1. 模拟急停按钮
POST /api/sim/panel/emergency-stop

# 2. 验证系统进入急停状态
GET /api/sim/panel/state
# 应该返回 "currentState": "EmergencyStop"

# 3. 急停复位
POST /api/sim/panel/emergency-reset

# 4. 验证系统恢复到就绪状态
GET /api/sim/panel/state
# 应该返回 "currentState": "Ready"
```

## 五、注意事项

### 5.1 仿真模式限制
- 面板仿真 API (`/api/simulation/panel`) 仅在仿真模式下可用
- 使用硬件驱动时，这些端点将返回 400 错误
- 系统状态控制 API (`/api/sim/panel`) 在任何模式下都可用

### 5.2 配置热更新
- 面板配置更新后立即生效，无需重启服务
- 但切换仿真/硬件模式可能需要重启才能完全生效
- 建议在系统停止状态下进行模式切换

### 5.3 参数调优建议
- **轮询间隔**: 不宜过小，避免 CPU 占用过高，建议 100ms
- **防抖时间**: 根据实际按钮特性调整，建议 50ms
- **防抖时间** 必须小于 **轮询间隔**

### 5.4 API 调用顺序
推荐按以下顺序操作：
1. 配置面板参数
2. 查询初始状态
3. 启动系统
4. 执行仿真操作
5. 停止系统

### 5.5 错误处理
所有 API 端点在发生错误时返回标准错误响应：
```json
{
  "message": "错误描述",
  "error": "详细错误信息（可选）"
}
```

## 六、集成测试示例

### 6.1 基础测试脚本 (bash)

```bash
#!/bin/bash
BASE_URL="http://localhost:5000"

echo "=== 面板配置测试 ==="

# 获取当前配置
echo "1. 获取当前配置"
curl -X GET "$BASE_URL/api/config/panel"

# 更新配置
echo -e "\n2. 更新配置"
curl -X PUT "$BASE_URL/api/config/panel" \
  -H "Content-Type: application/json" \
  -d '{
    "enabled": true,
    "useSimulation": true,
    "pollingIntervalMs": 100,
    "debounceMs": 50
  }'

# 启动系统
echo -e "\n3. 启动系统"
curl -X POST "$BASE_URL/api/sim/panel/start"

# 模拟按钮操作
echo -e "\n4. 模拟按下启动按钮"
curl -X POST "$BASE_URL/api/simulation/panel/press?buttonType=Start"

echo -e "\n5. 查询面板状态"
curl -X GET "$BASE_URL/api/simulation/panel/state"

echo -e "\n=== 测试完成 ==="
```

### 6.2 Python 测试脚本

```python
import requests
import time

BASE_URL = "http://localhost:5000"

def test_panel_config_and_simulation():
    print("=== 面板配置与仿真测试 ===")
    
    # 1. 获取配置
    response = requests.get(f"{BASE_URL}/api/config/panel")
    print(f"1. 当前配置: {response.json()}")
    
    # 2. 更新配置
    config = {
        "enabled": True,
        "useSimulation": True,
        "pollingIntervalMs": 100,
        "debounceMs": 50
    }
    response = requests.put(f"{BASE_URL}/api/config/panel", json=config)
    print(f"2. 更新配置: {response.json()}")
    
    # 3. 启动系统
    response = requests.post(f"{BASE_URL}/api/sim/panel/start")
    print(f"3. 启动系统: {response.json()}")
    
    # 4. 模拟按钮操作
    response = requests.post(f"{BASE_URL}/api/simulation/panel/press?buttonType=Start")
    print(f"4. 按下按钮: {response.json()}")
    
    time.sleep(0.5)
    
    response = requests.post(f"{BASE_URL}/api/simulation/panel/release?buttonType=Start")
    print(f"5. 释放按钮: {response.json()}")
    
    # 6. 查询状态
    response = requests.get(f"{BASE_URL}/api/simulation/panel/state")
    print(f"6. 面板状态: {response.json()}")
    
    print("=== 测试完成 ===")

if __name__ == "__main__":
    test_panel_config_and_simulation()
```

## 七、API 清单总结

| 控制器 | 端点 | 方法 | 说明 |
|--------|------|------|------|
| PanelConfigController | `/api/config/panel` | GET | 查询面板配置 |
| | `/api/config/panel` | PUT | 更新面板配置 |
| | `/api/config/panel/reset` | POST | 重置为默认配置 |
| | `/api/config/panel/template` | GET | 获取配置模板 |
| PanelSimulationController | `/api/simulation/panel/press` | POST | 模拟按下按钮 |
| | `/api/simulation/panel/release` | POST | 模拟释放按钮 |
| | `/api/simulation/panel/state` | GET | 查询面板状态 |
| | `/api/simulation/panel/reset` | POST | 重置按钮状态 |
| | `/api/simulation/panel/signal-tower/history` | GET | 查询信号塔历史 |
| SimulationPanelController | `/api/sim/panel/start` | POST | 启动系统 |
| | `/api/sim/panel/stop` | POST | 停止系统 |
| | `/api/sim/panel/emergency-stop` | POST | 急停 |
| | `/api/sim/panel/emergency-reset` | POST | 急停复位 |
| | `/api/sim/panel/state` | GET | 查询系统状态 |

---

**文档版本**: 1.0  
**创建日期**: 2025-11-21  
**作者**: GitHub Copilot  
**维护**: ZakYip Development Team
