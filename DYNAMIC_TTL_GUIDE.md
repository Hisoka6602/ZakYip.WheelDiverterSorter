# 动态TTL计算指南 (Dynamic TTL Calculation Guide)

## 概述

本文档说明了路径段（Path Segment）TTL的动态计算机制及其配置方法。

## 什么是段TTL？

段TTL（Time To Live，生存时间）是指包裹通过单个摆轮段的最大允许时间。如果包裹在TTL时间内未能通过该段，系统将认为该包裹丢失或卡滞，并触发异常处理流程。

## 动态TTL计算

### 计算公式

```
段TTL (毫秒) = (段长度(米) / 段速度(米/秒)) × 1000 + 容差时间(毫秒)
```

### 参数说明

| 参数 | 说明 | 默认值 | 单位 |
|------|------|--------|------|
| **SegmentLengthMeter** | 从上一个摆轮到本摆轮的距离 | 5.0 | 米 |
| **SegmentSpeedMeterPerSecond** | 本段输送带的运行速度 | 1.0 | 米/秒 |
| **SegmentToleranceTimeMs** | 允许的时间误差范围 | 2000 | 毫秒 |

### 计算示例

#### 示例1：单段路径
- 段长度：5.0米
- 段速度：1.5米/秒
- 容差时间：2000毫秒

计算：
```
理论时间 = 5.0 / 1.5 = 3.333秒 = 3333毫秒
段TTL = 3333 + 2000 = 5333毫秒 (向上取整为 5334毫秒)
```

#### 示例2：多段路径
格口3需要经过摆轮1（直行）和摆轮2（右转）：

**段1：入口 → 摆轮1**
- 长度：5.0米，速度：1.5米/秒，容差：2000ms
- TTL = (5.0 / 1.5) × 1000 + 2000 = 5334ms

**段2：摆轮1 → 摆轮2**
- 长度：5.0米，速度：1.5米/秒，容差：2000ms
- TTL = (5.0 / 1.5) × 1000 + 2000 = 5334ms

每个段独立计算其TTL，互不影响。

## 容差时间配置指南

### 容差时间的作用

容差时间用于应对以下情况：
1. **传感器响应延迟**：传感器检测包裹需要时间
2. **速度波动**：输送带速度可能有小幅波动
3. **包裹尺寸差异**：不同尺寸的包裹通过时间略有不同
4. **机械误差**：摆轮动作和位置检测的时间误差

### 容差时间验证规则

**关键原则：容差时间必须小于包裹间隔时间的一半**

```
容差时间 < 包裹间隔时间 / 2
```

#### 为什么需要这个规则？

假设两个包裹A和B：
- 包裹A在时刻T到达摆轮
- 包裹B在时刻T+间隔到达摆轮

包裹A的超时检测窗口为：`[T - 容差, T + 容差]`
包裹B的超时检测窗口为：`[T + 间隔 - 容差, T + 间隔 + 容差]`

为了避免两个窗口重叠，需要：
```
T + 容差 < T + 间隔 - 容差
即：2 × 容差 < 间隔
即：容差 < 间隔 / 2
```

### 推荐配置

| 包裹间隔 | 推荐容差上限 | 示例配置 |
|----------|--------------|----------|
| 2000ms | < 1000ms | 800ms |
| 1000ms | < 500ms | 400ms |
| 500ms | < 250ms | 200ms |

### 验证方法

使用 `DefaultSwitchingPathGenerator.ValidateToleranceTime()` 方法验证配置：

```csharp
var segmentConfig = new DiverterConfigurationEntry
{
    DiverterId = 1,
    TargetDirection = DiverterDirection.Straight,
    SequenceNumber = 1,
    SegmentLengthMeter = 5.0,
    SegmentSpeedMeterPerSecond = 1.5,
    SegmentToleranceTimeMs = 800  // 容差时间
};

int parcelIntervalMs = 2000;  // 包裹间隔2秒

bool isValid = DefaultSwitchingPathGenerator.ValidateToleranceTime(
    segmentConfig, 
    parcelIntervalMs
);

// isValid = true，因为 800 < 2000/2 (1000)
```

## 配置方法

### 方法1：通过API配置

```http
POST /api/config/routes
Content-Type: application/json

{
  "chuteId": 1,
  "chuteName": "格口1",
  "diverterConfigurations": [
    {
      "diverterId": 1,
      "targetDirection": "Right",
      "sequenceNumber": 1,
      "segmentLengthMeter": 5.0,
      "segmentSpeedMeterPerSecond": 1.5,
      "segmentToleranceTimeMs": 800
    }
  ],
  "beltSpeedMeterPerSecond": 1.5,
  "beltLengthMeter": 5.0,
  "toleranceTimeMs": 2000,
  "isEnabled": true
}
```

### 方法2：通过代码配置

```csharp
var routeConfig = new ChuteRouteConfiguration
{
    ChuteId = 1,
    ChuteName = "格口1",
    DiverterConfigurations = new List<DiverterConfigurationEntry>
    {
        new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Right,
            SequenceNumber = 1,
            SegmentLengthMeter = 5.0,
            SegmentSpeedMeterPerSecond = 1.5,
            SegmentToleranceTimeMs = 800
        }
    },
    BeltSpeedMeterPerSecond = 1.5,
    BeltLengthMeter = 5.0,
    ToleranceTimeMs = 2000,
    IsEnabled = true
};

repository.Upsert(routeConfig);
```

## 最小TTL保证

系统强制要求每个段的TTL不能小于1000毫秒（1秒），即使计算结果小于这个值，也会被调整为1000毫秒。这是为了：
1. 避免过于严格的超时检测
2. 给予机械系统足够的响应时间
3. 减少误报率

## 实施建议

### 1. 测量实际参数
在生产环境中：
- 使用卷尺或激光测距仪精确测量段长度
- 使用计时器测量包裹实际通过时间，计算实际速度
- 记录多次测量结果，取平均值

### 2. 逐步调整容差
1. 从较大的容差值开始（如2000ms）
2. 观察系统运行情况，记录误报和漏报
3. 根据实际表现逐步减小容差值
4. 确保容差 < 包裹间隔/2

### 3. 监控和优化
- 监控每个段的超时率
- 记录实际通过时间与理论时间的偏差
- 定期重新评估和调整配置

## 常见问题

### Q1: 为什么计算的TTL和预期不符？
**A:** 检查以下几点：
1. 段长度单位是米，不是毫米
2. 速度单位是米/秒，不是毫米/秒
3. 容差时间单位是毫秒
4. 注意向上取整的影响

### Q2: 容差设置多大合适？
**A:** 建议根据以下因素综合考虑：
1. 包裹间隔时间（容差 < 间隔/2）
2. 传感器精度（一般100-500ms）
3. 速度波动范围（±5-10%）
4. 实际测试结果

### Q3: 多段路径的TTL如何计算？
**A:** 每个段独立计算，总的超时时间是所有段TTL的累加。例如：
- 段1 TTL: 5334ms
- 段2 TTL: 5334ms
- 总超时时间: 10668ms（约10.7秒）

### Q4: 可以为不同的段设置不同的容差吗？
**A:** 可以。每个段的配置是独立的，可以根据该段的特点设置不同的参数。例如：
- 第一段（入口）可能需要较大容差（包裹加速阶段）
- 中间段可以使用标准容差
- 最后段（格口前）可以使用较小容差（速度稳定）

## 参考资料

- [DefaultSwitchingPathGenerator.cs](ZakYip.WheelDiverterSorter.Core/DefaultSwitchingPathGenerator.cs)
- [DiverterConfigurationEntry.cs](ZakYip.WheelDiverterSorter.Core/Configuration/DiverterConfigurationEntry.cs)
- [DefaultSwitchingPathGeneratorTests.cs](ZakYip.WheelDiverterSorter.Core.Tests/DefaultSwitchingPathGeneratorTests.cs)
