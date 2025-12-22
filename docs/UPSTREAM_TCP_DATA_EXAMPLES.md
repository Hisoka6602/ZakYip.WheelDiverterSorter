# 上游通信 TCP 数据示例 (Upstream Communication TCP Data Examples)

> **文档用途**: 提供所有与上游通信的 TCP 数据示例，用于调试上游程序
> 
> **协议说明**: 所有数据均为 JSON 格式，使用 UTF-8 编码，每条消息以换行符 `\n` 结尾

---

## 目录

- [通信概述](#通信概述)
- [数据格式](#数据格式)
- [1. ParcelDetectionNotification (包裹检测通知)](#1-parceldetectionnotification-包裹检测通知)
- [2. ChuteAssignmentNotification (格口分配通知)](#2-chuteassignmentnotification-格口分配通知)
- [3. SortingCompletedNotification (落格完成通知)](#3-sortingcompletednotification-落格完成通知)
- [完整通信流程示例](#完整通信流程示例)
- [测试工具](#测试工具)

---

## 通信概述

### 通信模型

系统采用 **Fire-and-Forget** 异步通信模式：

```
┌──────────────────┐                      ┌──────────────────┐
│   分拣系统        │                      │   RuleEngine     │
│  (WheelDiverter) │                      │   (上游系统)      │
└────────┬─────────┘                      └────────┬─────────┘
         │                                         │
         │  1. ParcelDetectionNotification         │
         │  ─────────────────────────────────────▶ │
         │                                         │
         │  2. ChuteAssignmentNotification         │
         │  ◀───────────────────────────────────── │
         │                                         │
         │  3. SortingCompletedNotification        │
         │  ─────────────────────────────────────▶ │
         │                                         │
```

### 通信特点

- **协议**: TCP Socket
- **编码**: UTF-8
- **格式**: JSON (每条消息以 `\n` 结尾)
- **方向**: 双向异步
- **连接**: 支持 Client/Server 两种模式
- **重连**: 指数退避，最大间隔 2 秒，无限重试

---

## 数据格式

### 消息边界

每条 JSON 消息以换行符 `\n` 结尾作为消息边界：

```
{JSON 消息内容}\n
```

### 时间格式

所有时间字段使用 ISO 8601 格式 (含时区)：

```
"2024-12-01T18:57:43.1234567+08:00"
```

### ID 类型

- **ParcelId**: `long` 类型，毫秒时间戳（如：`1701446263000`）
- **ChuteId**: `long` 类型，格口编号（如：`101`, `999`）

---

## 1. ParcelDetectionNotification (包裹检测通知)

### 用途

当系统检测到包裹进入分拣线时，向上游发送此通知。

### 发送方向

**分拣系统 → 上游 RuleEngine**

### 数据结构

```json
{
  "Type": "ParcelDetected",
  "ParcelId": 1701446263000,
  "DetectionTime": "2024-12-01T18:57:43.1234567+08:00",
  "Metadata": {
    "SensorId": "Sensor001",
    "LineId": "Line01"
  }
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `Type` | string | ✅ | 消息类型，固定值 "ParcelDetected" | `"ParcelDetected"` |
| `ParcelId` | long | ✅ | 包裹唯一标识（毫秒时间戳） | `1701446263000` |
| `DetectionTime` | DateTimeOffset | ✅ | 检测时间（ISO 8601 格式） | `"2024-12-01T18:57:43+08:00"` |
| `Metadata` | Dictionary<string, string> | ❌ | 额外元数据（可选） | `{"SensorId": "Sensor001"}` |

### 完整示例

#### 示例 1: 基本通知（无元数据）

```json
{
  "Type": "ParcelDetected",
  "ParcelId": 1733058000000,
  "DetectionTime": "2024-12-01T18:00:00.0000000+08:00",
  "Metadata": null
}
```

**TCP 原始数据（含换行符）:**
```
{"Type":"ParcelDetected","ParcelId":1733058000000,"DetectionTime":"2024-12-01T18:00:00.0000000+08:00","Metadata":null}\n
```

**字节数**: 约 130 字节

---

#### 示例 2: 完整通知（含元数据）

```json
{
  "Type": "ParcelDetected",
  "ParcelId": 1733058123456,
  "DetectionTime": "2024-12-01T18:02:03.4560000+08:00",
  "Metadata": {
    "SensorId": "PE001",
    "LineId": "Line01",
    "Position": "Entrance"
  }
}
```

**TCP 原始数据（含换行符）:**
```
{"Type":"ParcelDetected","ParcelId":1733058123456,"DetectionTime":"2024-12-01T18:02:03.4560000+08:00","Metadata":{"SensorId":"PE001","LineId":"Line01","Position":"Entrance"}}\n
```

**字节数**: 约 190 字节

---

#### 示例 3: 高峰期连续包裹

```json
{"Type":"ParcelDetected","ParcelId":1733058200000,"DetectionTime":"2024-12-01T18:03:20.0000000+08:00","Metadata":null}\n
{"Type":"ParcelDetected","ParcelId":1733058200500,"DetectionTime":"2024-12-01T18:03:20.5000000+08:00","Metadata":null}\n
{"Type":"ParcelDetected","ParcelId":1733058201000,"DetectionTime":"2024-12-01T18:03:21.0000000+08:00","Metadata":null}\n
```

> **注意**: 高峰期可能出现包裹间隔小于 1 秒的情况

---

## 2. ChuteAssignmentNotification (格口分配通知)

### 用途

上游 RuleEngine **主动推送**的格口分配结果（非请求响应）。

### 发送方向

**上游 RuleEngine → 分拣系统**

### 数据结构

```json
{
  "ParcelId": 1701446263000,
  "ChuteId": 101,
  "AssignedAt": "2024-12-01T18:57:43.5000000+08:00",
  "DwsPayload": {
    "WeightGrams": 500.0,
    "LengthMm": 300.0,
    "WidthMm": 200.0,
    "HeightMm": 100.0,
    "VolumetricWeightGrams": 600.0,
    "Barcode": "PKG123456",
    "MeasuredAt": "2024-12-01T18:57:42.0000000+08:00"
  },
  "Metadata": null
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `ParcelId` | long | ✅ | 包裹ID（必须与检测通知中的ID一致） | `1701446263000` |
| `ChuteId` | long | ✅ | 目标格口编号 | `101` |
| `AssignedAt` | DateTimeOffset | ✅ | 分配时间（ISO 8601） | `"2024-12-01T18:57:43.5+08:00"` |
| `DwsPayload` | DwsMeasurementDto | ❌ | DWS 测量数据（可选） | 见下方 DWS 结构 |
| `Metadata` | Dictionary<string, string> | ❌ | 额外元数据（可选） | `{"OrderId": "ORD12345"}` |

### DWS 测量数据结构 (DwsMeasurementDto)

| 字段 | 类型 | 必填 | 说明 | 单位 |
|------|------|------|------|------|
| `WeightGrams` | decimal | ✅ | 实际重量 | 克 (g) |
| `LengthMm` | decimal | ✅ | 长度 | 毫米 (mm) |
| `WidthMm` | decimal | ✅ | 宽度 | 毫米 (mm) |
| `HeightMm` | decimal | ✅ | 高度 | 毫米 (mm) |
| `VolumetricWeightGrams` | decimal | ❌ | 体积重量（可选） | 克 (g) |
| `Barcode` | string | ❌ | 条码（可选） | - |
| `MeasuredAt` | DateTimeOffset | ✅ | 测量时间 | ISO 8601 |

### 完整示例

#### 示例 1: 基本分配（无 DWS 数据）

```json
{
  "ParcelId": 1733058000000,
  "ChuteId": 101,
  "AssignedAt": "2024-12-01T18:00:00.2000000+08:00",
  "DwsPayload": null,
  "Metadata": null
}
```

**TCP 原始数据（含换行符）:**
```
{"ParcelId":1733058000000,"ChuteId":101,"AssignedAt":"2024-12-01T18:00:00.2000000+08:00","DwsPayload":null,"Metadata":null}\n
```

**字节数**: 约 125 字节

---

#### 示例 2: 完整分配（含 DWS 数据和条码）

```json
{
  "ParcelId": 1733058123456,
  "ChuteId": 205,
  "AssignedAt": "2024-12-01T18:02:03.7000000+08:00",
  "DwsPayload": {
    "WeightGrams": 1250.5,
    "LengthMm": 450.0,
    "WidthMm": 350.0,
    "HeightMm": 200.0,
    "VolumetricWeightGrams": 1575.0,
    "Barcode": "SF1234567890",
    "MeasuredAt": "2024-12-01T18:02:02.5000000+08:00"
  },
  "Metadata": {
    "OrderId": "ORD-20241201-001",
    "Destination": "Beijing"
  }
}
```

**TCP 原始数据（含换行符）:**
```
{"ParcelId":1733058123456,"ChuteId":205,"AssignedAt":"2024-12-01T18:02:03.7000000+08:00","DwsPayload":{"WeightGrams":1250.5,"LengthMm":450.0,"WidthMm":350.0,"HeightMm":200.0,"VolumetricWeightGrams":1575.0,"Barcode":"SF1234567890","MeasuredAt":"2024-12-01T18:02:02.5000000+08:00"},"Metadata":{"OrderId":"ORD-20241201-001","Destination":"Beijing"}}\n
```

**字节数**: 约 380 字节

---

#### 示例 3: 异常格口分配

```json
{
  "ParcelId": 1733058300000,
  "ChuteId": 999,
  "AssignedAt": "2024-12-01T18:05:00.0000000+08:00",
  "DwsPayload": null,
  "Metadata": {
    "Reason": "Address_Unknown",
    "OriginalChute": "103"
  }
}
```

> **说明**: `ChuteId=999` 通常表示异常格口（Exception Chute）

**TCP 原始数据（含换行符）:**
```
{"ParcelId":1733058300000,"ChuteId":999,"AssignedAt":"2024-12-01T18:05:00.0000000+08:00","DwsPayload":null,"Metadata":{"Reason":"Address_Unknown","OriginalChute":"103"}}\n
```

**字节数**: 约 175 字节

---

## 3. SortingCompletedNotification (落格完成通知)

### 用途

包裹完成分拣落格后，向上游发送完成通知。

### 发送方向

**分拣系统 → 上游 RuleEngine**

### 数据结构

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1701446263000,
  "ActualChuteId": 101,
  "CompletedAt": "2024-12-01T18:57:45.0000000+08:00",
  "IsSuccess": true,
  "FinalStatus": "Success",
  "FailureReason": null,
  "AffectedParcelIds": null
}
```

### 字段说明

| 字段 | 类型 | 必填 | 说明 | 示例值 |
|------|------|------|------|--------|
| `Type` | string | ✅ | 消息类型，固定值 "SortingCompleted" | `"SortingCompleted"` |
| `ParcelId` | long | ✅ | 包裹ID | `1701446263000` |
| `ActualChuteId` | long | ✅ | 实际落格格口ID（Lost 时为 0） | `101` |
| `CompletedAt` | DateTimeOffset | ✅ | 完成时间（ISO 8601） | `"2024-12-01T18:57:45+08:00"` |
| `IsSuccess` | bool | ✅ | 是否成功 | `true` |
| `FinalStatus` | string | ✅ | 最终状态（Success/Timeout/Lost） | `"Success"` |
| `FailureReason` | string | ❌ | 失败原因（如果失败） | `"PathExecutionTimeout"` |
| `AffectedParcelIds` | long[] | ❌ | 受影响的包裹ID列表（仅 Lost 时有值） | `[1733058400000, 1733058401000]` |

### FinalStatus 枚举值

| 值 | 说明 | ActualChuteId | IsSuccess |
|----|------|---------------|-----------|
| `Success` | 成功分拣到目标格口 | 目标格口 ID | `true` |
| `Timeout` | 分配或落格超时，路由到异常格口 | 异常格口 ID (如 999) | `false` |
| `Lost` | 包裹丢失，无法确定位置 | `0` | `false` |

### 完整示例

#### 示例 1: 成功落格

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1733058000000,
  "ActualChuteId": 101,
  "CompletedAt": "2024-12-01T18:00:02.5000000+08:00",
  "IsSuccess": true,
  "FinalStatus": "Success",
  "FailureReason": null,
  "AffectedParcelIds": null
}
```

**TCP 原始数据（含换行符）:**
```
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":101,"CompletedAt":"2024-12-01T18:00:02.5000000+08:00","IsSuccess":true,"FinalStatus":"Success","FailureReason":null,"AffectedParcelIds":null}\n
```

**字节数**: 约 190 字节

---

#### 示例 2: 分配超时，路由到异常格口

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1733058100000,
  "ActualChuteId": 999,
  "CompletedAt": "2024-12-01T18:01:50.0000000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Timeout",
  "FailureReason": "ChuteAssignmentTimeout",
  "AffectedParcelIds": null
}
```

**TCP 原始数据（含换行符）:**
```
{"Type":"SortingCompleted","ParcelId":1733058100000,"ActualChuteId":999,"CompletedAt":"2024-12-01T18:01:50.0000000+08:00","IsSuccess":false,"FinalStatus":"Timeout","FailureReason":"ChuteAssignmentTimeout","AffectedParcelIds":null}\n
```

**字节数**: 约 210 字节

---

#### 示例 3: 包裹丢失

```json
{
  "Type": "SortingCompleted",
  "ParcelId": 1733058200000,
  "ActualChuteId": 0,
  "CompletedAt": "2024-12-01T18:03:30.0000000+08:00",
  "IsSuccess": false,
  "FinalStatus": "Lost",
  "FailureReason": "ParcelLost_ExceededMaxSurvivalTime",
  "AffectedParcelIds": [
    1733058201000,
    1733058202000,
    1733058203000
  ]
}
```

**TCP 原始数据（含换行符）:**
```
{"Type":"SortingCompleted","ParcelId":1733058200000,"ActualChuteId":0,"CompletedAt":"2024-12-01T18:03:30.0000000+08:00","IsSuccess":false,"FinalStatus":"Lost","FailureReason":"ParcelLost_ExceededMaxSurvivalTime","AffectedParcelIds":[1733058201000,1733058202000,1733058203000]}\n
```

**字节数**: 约 260 字节

> **重要**: 
> - Lost 状态时 `ActualChuteId` 必须为 `0`
> - `AffectedParcelIds` 包含在丢失包裹创建后、丢失检测前创建的包裹，这些包裹的任务方向已被改为直行以导向异常格口

---

## 完整通信流程示例

### 场景 1: 正常分拣流程

```plaintext
时间轴：18:00:00 - 18:00:03

[T=18:00:00.000] 分拣系统 → 上游
{"Type":"ParcelDetected","ParcelId":1733058000000,"DetectionTime":"2024-12-01T18:00:00.0000000+08:00","Metadata":null}\n

[T=18:00:00.200] 上游 → 分拣系统
{"ParcelId":1733058000000,"ChuteId":101,"AssignedAt":"2024-12-01T18:00:00.2000000+08:00","DwsPayload":null,"Metadata":null}\n

[T=18:00:02.500] 分拣系统 → 上游
{"Type":"SortingCompleted","ParcelId":1733058000000,"ActualChuteId":101,"CompletedAt":"2024-12-01T18:00:02.5000000+08:00","IsSuccess":true,"FinalStatus":"Success","FailureReason":null,"AffectedParcelIds":null}\n
```

**流程说明**:
1. **T+0ms**: 包裹通过传感器，系统立即发送检测通知
2. **T+200ms**: 上游系统完成路由计算，推送格口分配（响应时间 200ms）
3. **T+2500ms**: 包裹完成分拣并成功落入格口 101，发送完成通知

---

### 场景 2: 包含 DWS 数据的完整流程

```plaintext
时间轴：18:02:00 - 18:02:05

[T=18:02:00.000] 分拣系统 → 上游
{"Type":"ParcelDetected","ParcelId":1733058520000,"DetectionTime":"2024-12-01T18:02:00.0000000+08:00","Metadata":{"SensorId":"PE001","LineId":"Line01"}}\n

[T=18:02:00.350] 上游 → 分拣系统
{"ParcelId":1733058520000,"ChuteId":205,"AssignedAt":"2024-12-01T18:02:00.3500000+08:00","DwsPayload":{"WeightGrams":850.0,"LengthMm":400.0,"WidthMm":300.0,"HeightMm":150.0,"VolumetricWeightGrams":900.0,"Barcode":"SF9876543210","MeasuredAt":"2024-12-01T18:01:59.5000000+08:00"},"Metadata":{"OrderId":"ORD-002","Destination":"Shanghai"}}\n

[T=18:02:04.200] 分拣系统 → 上游
{"Type":"SortingCompleted","ParcelId":1733058520000,"ActualChuteId":205,"CompletedAt":"2024-12-01T18:02:04.2000000+08:00","IsSuccess":true,"FinalStatus":"Success","FailureReason":null,"AffectedParcelIds":null}\n
```

---

### 场景 3: 分配超时场景

```plaintext
时间轴：18:05:00 - 18:05:08

[T=18:05:00.000] 分拣系统 → 上游
{"Type":"ParcelDetected","ParcelId":1733058900000,"DetectionTime":"2024-12-01T18:05:00.0000000+08:00","Metadata":null}\n

[T=18:05:05.000] (超时，未收到格口分配)

[T=18:05:07.000] 分拣系统 → 上游 (超时后路由到异常格口)
{"Type":"SortingCompleted","ParcelId":1733058900000,"ActualChuteId":999,"CompletedAt":"2024-12-01T18:05:07.0000000+08:00","IsSuccess":false,"FinalStatus":"Timeout","FailureReason":"ChuteAssignmentTimeout","AffectedParcelIds":null}\n
```

**流程说明**:
1. **T+0ms**: 发送检测通知
2. **T+5000ms**: 超过分配超时时间（SafetyFactor × 理论通过时间），未收到格口分配
3. **T+7000ms**: 系统自动将包裹路由到异常格口 999，并发送超时通知

---

### 场景 4: 包裹丢失场景

```plaintext
时间轴：18:10:00 - 18:10:15

[T=18:10:00.000] 分拣系统 → 上游
{"Type":"ParcelDetected","ParcelId":1733059200000,"DetectionTime":"2024-12-01T18:10:00.0000000+08:00","Metadata":null}\n

[T=18:10:00.200] 上游 → 分拣系统
{"ParcelId":1733059200000,"ChuteId":103,"AssignedAt":"2024-12-01T18:10:00.2000000+08:00","DwsPayload":null,"Metadata":null}\n

[T=18:10:01.000] 分拣系统 → 上游 (第2个包裹)
{"Type":"ParcelDetected","ParcelId":1733059201000,"DetectionTime":"2024-12-01T18:10:01.0000000+08:00","Metadata":null}\n

[T=18:10:02.000] 分拣系统 → 上游 (第3个包裹)
{"Type":"ParcelDetected","ParcelId":1733059202000,"DetectionTime":"2024-12-01T18:10:02.0000000+08:00","Metadata":null}\n

[T=18:10:15.000] (第1个包裹超过最大存活时间仍未落格，判定为丢失)

[T=18:10:15.000] 分拣系统 → 上游
{"Type":"SortingCompleted","ParcelId":1733059200000,"ActualChuteId":0,"CompletedAt":"2024-12-01T18:10:15.0000000+08:00","IsSuccess":false,"FinalStatus":"Lost","FailureReason":"ParcelLost_ExceededMaxSurvivalTime","AffectedParcelIds":[1733059201000,1733059202000]}\n
```

**流程说明**:
1. **T+0ms**: 第1个包裹检测
2. **T+200ms**: 收到格口分配（ChuteId=103）
3. **T+1000ms**: 第2个包裹检测（在第1个包裹完成前）
4. **T+2000ms**: 第3个包裹检测
5. **T+15000ms**: 第1个包裹超过最大存活时间（LostDetectionSafetyFactor × 线长 / 速度），判定为丢失
6. **AffectedParcelIds**: 第2、第3个包裹（在丢失检测前创建）受影响，任务方向已改为直行

---

## 测试工具

### 1. 使用 netcat 监听上游消息

监听分拣系统发送的消息：

```bash
# 启动 TCP 服务器监听端口 5000
nc -l 5000

# 或使用 socat (支持更多功能)
socat TCP-LISTEN:5000,reuseaddr,fork STDOUT
```

### 2. 使用 netcat 发送格口分配通知

向分拣系统（Server 模式）发送格口分配：

```bash
# 连接到分拣系统（假设监听在 5000 端口）
nc localhost 5000

# 然后输入（注意：必须以换行符结尾）
{"ParcelId":1733058000000,"ChuteId":101,"AssignedAt":"2024-12-01T18:00:00.2000000+08:00","DwsPayload":null,"Metadata":null}

# 按 Enter 发送
```

### 3. 使用 Python 脚本测试

#### 接收分拣系统消息（充当上游 RuleEngine）

```python
#!/usr/bin/env python3
import socket
import json
from datetime import datetime, timezone, timedelta

def start_tcp_server(host='0.0.0.0', port=5000):
    """启动 TCP 服务器，接收并显示分拣系统消息"""
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
        s.bind((host, port))
        s.listen(1)
        print(f"监听中: {host}:{port}")
        
        conn, addr = s.accept()
        with conn:
            print(f"连接来自: {addr}")
            buffer = b""
            
            while True:
                data = conn.recv(4096)
                if not data:
                    break
                
                buffer += data
                while b'\n' in buffer:
                    line, buffer = buffer.split(b'\n', 1)
                    try:
                        msg = json.loads(line.decode('utf-8'))
                        print(f"\n收到消息 [{datetime.now()}]:")
                        print(json.dumps(msg, indent=2, ensure_ascii=False))
                        
                        # 自动回复格口分配（可选）
                        if msg.get('Type') == 'ParcelDetected':
                            response = {
                                "ParcelId": msg['ParcelId'],
                                "ChuteId": 101,  # 固定分配到格口 101
                                "AssignedAt": datetime.now(timezone(timedelta(hours=8))).isoformat(),
                                "DwsPayload": None,
                                "Metadata": None
                            }
                            conn.sendall((json.dumps(response) + '\n').encode('utf-8'))
                            print(f"已回复格口分配: ChuteId=101")
                    except Exception as e:
                        print(f"解析错误: {e}")

if __name__ == '__main__':
    start_tcp_server()
```

#### 发送格口分配通知（充当上游 RuleEngine）

```python
#!/usr/bin/env python3
import socket
import json
from datetime import datetime, timezone, timedelta

def send_chute_assignment(host='localhost', port=5000, parcel_id=1733058000000, chute_id=101):
    """向分拣系统发送格口分配通知"""
    notification = {
        "ParcelId": parcel_id,
        "ChuteId": chute_id,
        "AssignedAt": datetime.now(timezone(timedelta(hours=8))).isoformat(),
        "DwsPayload": {
            "WeightGrams": 500.0,
            "LengthMm": 300.0,
            "WidthMm": 200.0,
            "HeightMm": 100.0,
            "VolumetricWeightGrams": 600.0,
            "Barcode": "TEST123456",
            "MeasuredAt": datetime.now(timezone(timedelta(hours=8))).isoformat()
        },
        "Metadata": {"TestMode": "True"}
    }
    
    with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as s:
        s.connect((host, port))
        message = json.dumps(notification) + '\n'
        s.sendall(message.encode('utf-8'))
        print(f"已发送格口分配: ParcelId={parcel_id}, ChuteId={chute_id}")

if __name__ == '__main__':
    send_chute_assignment()
```

### 4. 使用 Postman 测试（通过 API 端点）

如果分拣系统提供了测试端点，可以通过 API 模拟：

```bash
# 发送测试包裹检测
curl -X POST http://localhost:5000/api/communication/test-parcel \
  -H "Content-Type: application/json" \
  -d '{
    "parcelId": 1733058000000,
    "targetChuteId": 101
  }'
```

---

## 相关文档

- **上游连接配置**: [docs/guides/UPSTREAM_CONNECTION_GUIDE.md](guides/UPSTREAM_CONNECTION_GUIDE.md)
- **系统配置指南**: [docs/guides/SYSTEM_CONFIG_GUIDE.md](guides/SYSTEM_CONFIG_GUIDE.md)
- **超时处理机制**: [docs/TIMEOUT_HANDLING_MECHANISM.md](TIMEOUT_HANDLING_MECHANISM.md)

---

## 源码参考

| 文件 | 路径 | 说明 |
|------|------|------|
| `ParcelDetectionNotification` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/ParcelDetectionNotification.cs` | 包裹检测通知模型 |
| `ChuteAssignmentNotification` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/ChuteAssignmentNotification.cs` | 格口分配通知模型 |
| `SortingCompletedNotificationDto` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/SortingCompletedNotification.cs` | 落格完成通知模型 |
| `TouchSocketTcpRuleEngineClient` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Clients/TouchSocketTcpRuleEngineClient.cs` | TCP 客户端实现 |
| `TouchSocketTcpRuleEngineServer` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Servers/TouchSocketTcpRuleEngineServer.cs` | TCP 服务器实现 |
| `JsonMessageSerializer` | `src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/Infrastructure/JsonMessageSerializer.cs` | JSON 序列化器 |

---

**文档版本**: 1.0  
**最后更新**: 2024-12-22  
**维护团队**: ZakYip Development Team
