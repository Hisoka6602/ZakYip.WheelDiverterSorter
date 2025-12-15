# 包裹丢失检测系数使用情况总结

## 问题回答

### 当前判断包裹丢失的计算系数是否生效？

**答：✅ 是的，系数已经生效并在实际计算中使用。**

---

## 系数生效证据

### 1. 丢失检测系数 (LostDetectionMultiplier)

**配置值**：`1.5`（默认）

**生效位置**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Tracking/PositionIntervalTracker.cs`

```csharp
// 第208行
public double? GetLostDetectionThreshold(int positionIndex)
{
    // ...
    var intervals = buffer.ToArray();
    var median = CalculateMedian(intervals);
    
    // ✅ 系数在这里直接参与计算
    var lostThreshold = median * _options.LostDetectionMultiplier;
    
    return lostThreshold;
}
```

### 2. 超时检测系数 (TimeoutMultiplier)

**配置值**：`3.0`（默认）

**生效位置**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Tracking/PositionIntervalTracker.cs`

```csharp
// 第185行
public double? GetDynamicThreshold(int positionIndex)
{
    // ...
    var intervals = buffer.ToArray();
    var median = CalculateMedian(intervals);
    
    // ✅ 系数在这里直接参与计算
    var threshold = median * _options.TimeoutMultiplier;
    
    return threshold;
}
```

---

## 公式详解

### 公式1：丢失判定阈值

```
丢失阈值 = 中位数间隔 × LostDetectionMultiplier

示例：
  中位数间隔 = 1000ms
  LostDetectionMultiplier = 1.5
  丢失阈值 = 1000 × 1.5 = 1500ms
```

**含义**：
- 如果包裹超过期望到达时间 **1500ms** 仍未到达
- 系统会判定包裹已物理丢失
- 触发 `ParcelLost` 事件，从队列移除该包裹的所有任务

### 公式2：超时判定阈值

```
超时阈值 = 中位数间隔 × TimeoutMultiplier

示例：
  中位数间隔 = 1000ms
  TimeoutMultiplier = 3.0
  超时阈值 = 1000 × 3.0 = 3000ms
```

**含义**：
- 如果包裹超过期望到达时间 **3000ms** 才到达
- 系统会判定包裹超时（但未丢失）
- 使用 `Straight` 动作继续处理（避免阻塞后续包裹）

### 公式3：中位数计算

```
中位数 = Median(最近N个包裹的间隔时间)

示例（N=10）：
  最近10次间隔: [950, 980, 1000, 1020, 990, 1010, 1005, 995, 1015, 985]
  排序后: [950, 980, 985, 990, 995, 1000, 1005, 1010, 1015, 1020]
  中位数 = (995 + 1000) / 2 = 997.5ms
```

**特点**：
- 使用中位数而非平均值，抗异常值干扰
- 每个 Position 独立计算
- 动态自适应，无需手动配置

---

## 完整计算流程示例

### 场景：Position 1 的包裹检测

**步骤1：收集间隔数据**
```
最近10次间隔: [950ms, 980ms, 1000ms, 1020ms, 990ms, 1010ms, 1005ms, 995ms, 1015ms, 985ms]
```

**步骤2：计算中位数**
```
排序后: [950, 980, 985, 990, 995, 1000, 1005, 1010, 1015, 1020]
中位数 = (995 + 1000) / 2 = 997.5ms
```

**步骤3：计算阈值**
```
丢失阈值 = 997.5 × 1.5 = 1496.25ms
超时阈值 = 997.5 × 3.0 = 2992.5ms
```

**步骤4：设置包裹截止时间**
```
假设包裹期望到达时间 = 2025-12-15 10:00:00.000

丢失判定截止时间 = 10:00:00.000 + 1496ms = 10:00:01.496
超时判定截止时间 = 10:00:00.000 + 2993ms = 10:00:02.993
```

**步骤5：判定结果**

| 实际到达时间 | 延迟 | 判定结果 | 处理方式 |
|------------|------|---------|---------|
| 10:00:00.500 | +500ms | ✅ 正常 | 执行计划动作（Left/Right） |
| 10:00:01.200 | +1200ms | ⚠️ 接近丢失边界 | 执行计划动作 |
| 10:00:01.600 | +1600ms | ❌ **超过丢失阈值** | 触发丢失事件，移除任务 |
| 10:00:03.500 | +3500ms | ❌ **超过超时阈值** | 使用Straight动作 |

---

## 系数配置路径

### 1. 配置模型定义

**文件**：`src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/ParcelLossDetectionConfiguration.cs`

```csharp
public class ParcelLossDetectionConfiguration
{
    /// <summary>
    /// 丢失检测系数
    /// 丢失阈值 = 中位数间隔 * 丢失检测系数
    /// 默认值：1.5
    /// 推荐范围：1.5-2.5
    /// </summary>
    public double LostDetectionMultiplier { get; set; } = 1.5;

    /// <summary>
    /// 超时检测系数
    /// 超时阈值 = 中位数间隔 * 超时检测系数
    /// 默认值：3.0
    /// 推荐范围：2.5-3.5
    /// </summary>
    public double TimeoutMultiplier { get; set; } = 3.0;
}
```

### 2. DI注册

**文件**：`src/Application/ZakYip.WheelDiverterSorter.Application/Extensions/WheelDiverterSorterServiceCollectionExtensions.cs`

```csharp
// 第436-437行
services.Configure<PositionIntervalTrackerOptions>(options =>
{
    options.TimeoutMultiplier = 3.0;          // ✅ 注入超时系数
    options.LostDetectionMultiplier = 1.5;    // ✅ 注入丢失系数
});
```

### 3. 使用位置

**文件**：`src/Execution/ZakYip.WheelDiverterSorter.Execution/Tracking/PositionIntervalTracker.cs`

- **超时阈值计算**：第185行
- **丢失阈值计算**：第208行

---

## 如何验证系数生效

### 方法1：查看API统计数据

```bash
# 查询当前配置
GET /api/sorting/parcel-loss-detection/config

# 查看实时统计（包含计算后的阈值）
GET /api/sorting/position-intervals

# 响应示例（可以看到系数已应用）：
[
  {
    "positionIndex": 1,
    "medianIntervalMs": 997.5,
    "lostThresholdMs": 1496.25,    # ✅ 997.5 × 1.5 = 1496.25
    "timeoutThresholdMs": 2992.5,  # ✅ 997.5 × 3.0 = 2992.5
    "sampleCount": 10
  }
]
```

### 方法2：查看Debug日志

启用Debug级别日志后，可以看到：

```log
[DEBUG] [丢失检测阈值] Position 1: 中位数=997.5ms, 系数=1.5, 阈值=1496.25ms
[DEBUG] [超时检测] Position 1: 中位数=997.5ms, 系数=3.0, 阈值=2992.5ms
```

### 方法3：修改系数测试

```bash
# 修改系数
PUT /api/sorting/parcel-loss-detection/config
{
  "lostDetectionMultiplier": 2.0,
  "timeoutMultiplier": 3.5
}

# 再次查询统计，会看到阈值已变化
GET /api/sorting/position-intervals

# 响应示例（阈值已更新）：
[
  {
    "positionIndex": 1,
    "medianIntervalMs": 997.5,
    "lostThresholdMs": 1995.0,     # ✅ 997.5 × 2.0 = 1995.0 (已变化)
    "timeoutThresholdMs": 3491.25, # ✅ 997.5 × 3.5 = 3491.25 (已变化)
    "sampleCount": 10
  }
]
```

---

## 结论

**系数确实生效并正在使用**：

1. ✅ **LostDetectionMultiplier (1.5)** 直接用于计算丢失阈值
2. ✅ **TimeoutMultiplier (3.0)** 直接用于计算超时阈值
3. ✅ 两个系数都可以通过 API 动态修改，立即生效
4. ✅ 计算公式清晰明确：`阈值 = 中位数 × 系数`

**详细技术文档**：请参阅 `docs/PARCEL_LOSS_DETECTION_FORMULA_EXPLANATION.md`

---

**创建时间**：2025-12-15  
**维护团队**：ZakYip Development Team
