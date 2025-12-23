# 启动时路径布满包裹场景处理方案

> **文档类型**: 技术方案文档  
> **创建日期**: 2025-12-23  
> **维护团队**: ZakYip Development Team

---

## 问题描述

**场景**: 当分拣系统刚开启时，传送线上可能已经布满了包裹，导致传感器可能会出现异常触发模式。

**具体问题**:
```
正常情况：包裹依次通过 Position 1 → Position 2 → Position 3 → Position 4
异常情况：包裹从 Position 1 直接跳到 Position 4（跳过了 Position 2 和 3）
```

**问题来源**:
1. 系统启动前，线体已停止但包裹仍在线上
2. 启动瞬间，多个传感器可能同时检测到包裹
3. 某些位置的包裹可能已经越过了中间传感器

---

## 问题分析

### 1. 为什么会出现"跳点"现象

#### 1.1 正常流程（单个包裹进入空线体）

```
时刻 T0: 系统启动，线体为空
时刻 T1: 包裹A进入，触发 Position 1（传感器1）
时刻 T2: 包裹A移动，触发 Position 2（传感器2）
时刻 T3: 包裹A移动，触发 Position 3（传感器3）
时刻 T4: 包裹A移动，触发 Position 4（传感器4）

队列状态演变：
Position 1: [任务A] → []
Position 2: [] → [任务A] → []
Position 3: [] → [任务A] → []
Position 4: [] → [任务A] → []
```

✅ **正常**: 每个位置都有对应的任务，FIFO顺序正确

#### 1.2 异常流程（启动时线体布满包裹）

```
时刻 T0: 系统启动前，线体已有多个包裹
        包裹A位于 Position 3-4之间（已越过Position 1、2）
        包裹B位于 Position 2-3之间
        包裹C位于 Position 1-2之间

时刻 T1: 系统启动，开始扫描传感器
        传感器1被遮挡（包裹C在上方）→ 触发！创建包裹C
        传感器2被遮挡（包裹B在上方）→ 触发！创建包裹B
        传感器3被遮挡（包裹A在上方）→ 触发！创建包裹A
        传感器4未被遮挡 → 不触发

时刻 T2: 线体开始运行
        包裹C移动，离开传感器1 → 不触发（因为已触发过）
        包裹B移动，离开传感器2 → 不触发
        包裹A移动，到达传感器4 → 触发！

问题：
- 包裹A在系统中是第3个创建的（Position 3触发时）
- 但包裹A下一次触发的是 Position 4（跳过了中间状态）
- Position 4的队列中包裹A是第3个，但实际到达的是第1个
```

❌ **异常**: 队列顺序与实际到达顺序不一致！

### 2. 根本原因分析

#### 2.1 系统当前假设

系统的 Position-Index 队列机制基于以下假设：

1. **假设1**: 包裹按顺序进入系统（从 Position 0 开始）
2. **假设2**: 包裹依次通过所有 Position 点
3. **假设3**: IO触发顺序与包裹创建顺序一致
4. **假设4**: 队列FIFO顺序与实际物理到达顺序一致

#### 2.2 启动时违反的假设

启动时线体布满包裹的场景违反了以上**所有假设**：

- ❌ 假设1被违反：包裹不是从 Position 0 进入，而是已在线上
- ❌ 假设2被违反：某些包裹已越过部分 Position
- ❌ 假设3被违反：多个传感器同时触发，包裹创建顺序是随机的
- ❌ 假设4被违反：先创建的包裹可能后到达下一个 Position

---

## 解决方案

### 方案A：冷启动协议（推荐 ⭐）

**核心思想**: 系统启动时强制清空线体，确保从"空线体"状态开始。

#### A.1 实施步骤

```
步骤1: 系统启动，检测到运行模式从 Stopped → Ready
步骤2: 系统广播 "冷启动提示"（通过上游通知或面板显示）
步骤3: 操作员确认线体已清空（或自动延迟N秒）
步骤4: 系统清空所有队列和统计数据
步骤5: 系统状态转换到 Running
步骤6: 开始正常包裹分拣
```

#### A.2 代码实现

**文件**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`

```csharp
private void OnSystemStateChanged(object? sender, StateChangeEventArgs e)
{
    // 检测到从非运行状态进入 Running 状态
    if (e.NewState == SystemState.Running && 
        e.OldState is SystemState.Ready or SystemState.Stopped)
    {
        _logger.LogWarning(
            "[冷启动] 系统从 {OldState} 进入 Running 状态，执行冷启动清理",
            e.OldState);

        // 清空所有队列
        _queueManager?.ClearAllQueues();
        
        // 清空所有中位数统计
        _intervalTracker?.ClearAllStatistics();
        
        // 清空包裹创建记录
        _createdParcels.Clear();
        _parcelPaths.Clear();
        _parcelTargetChutes.Clear();
        
        _logger.LogInformation(
            "[冷启动] 冷启动清理完成，系统现在以空线体状态运行");
    }
    
    // ... 原有逻辑
}
```

#### A.3 优点与缺点

**优点** ✅:
- 实施简单，风险低
- 完全避免"跳点"问题
- 符合系统设计假设

**缺点** ❌:
- 需要人工干预（清空线体）
- 启动时间较长
- 可能造成生产停滞

#### A.4 适用场景

- 每日首次启动
- 长时间停机后重启
- 测试环境
- 对数据准确性要求高的场景

---

### 方案B：暖启动模式（智能识别）

**核心思想**: 系统启动时智能检测线体状态，仅对已存在的包裹执行"兜底分拣"。

#### B.1 实施步骤

```
步骤1: 系统启动，扫描所有传感器状态
步骤2: 检测到的包裹标记为 "预存在包裹"（Pre-existing Parcel）
步骤3: 为预存在包裹分配特殊路由策略：
       - 不记录到中位数统计
       - 强制路由到异常格口（或默认格口）
       - 不参与正常队列机制
步骤4: 等待所有预存在包裹离开线体
步骤5: 切换到正常运行模式
```

#### B.2 代码实现

**新增枚举**: `src/Core/.../Enums/Parcel/ParcelCreationType.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Core.Enums.Parcel;

/// <summary>
/// 包裹创建类型
/// </summary>
public enum ParcelCreationType
{
    /// <summary>
    /// 正常创建（从入口进入）
    /// </summary>
    Normal = 0,
    
    /// <summary>
    /// 预存在包裹（系统启动时已在线上）
    /// </summary>
    PreExisting = 1
}
```

**修改**: `src/Execution/.../Orchestration/SortingOrchestrator.cs`

```csharp
private readonly ConcurrentDictionary<long, ParcelCreationType> _parcelCreationTypes = new();

private async Task OnSensorTriggeredAsync(object? sender, SensorTriggeredEventArgs e)
{
    // 检测是否为系统刚启动后的第一批触发
    bool isSystemJustStarted = (DateTime.Now - _systemStartTime).TotalSeconds < 10;
    bool isFirstTriggerOnPosition = !_positionFirstTriggerRecorded.ContainsKey(e.PositionIndex);
    
    ParcelCreationType creationType = ParcelCreationType.Normal;
    
    if (isSystemJustStarted && isFirstTriggerOnPosition)
    {
        // 判定为预存在包裹
        creationType = ParcelCreationType.PreExisting;
        _positionFirstTriggerRecorded.TryAdd(e.PositionIndex, true);
        
        _logger.LogWarning(
            "[暖启动] 检测到预存在包裹：Position={Position}, 将路由到异常格口",
            e.PositionIndex);
    }
    
    var parcelId = GenerateParcelId();
    _parcelCreationTypes.TryAdd(parcelId, creationType);
    
    // 预存在包裹的特殊处理
    if (creationType == ParcelCreationType.PreExisting)
    {
        // 不调用 RecordParcelPosition（不记录中位数）
        // 直接路由到异常格口
        await RouteToExceptionChuteAsync(parcelId);
        return;
    }
    
    // 正常包裹处理
    await ProcessNormalParcelAsync(parcelId, e.PositionIndex);
}
```

#### B.3 优点与缺点

**优点** ✅:
- 无需人工干预
- 启动快速
- 生产连续性好

**缺点** ❌:
- 实施复杂
- 需要准确区分"预存在"与"正常"包裹
- 可能误判
- 增加系统复杂度

#### B.4 适用场景

- 频繁启停的生产环境
- 24/7 运行环境（短暂停机后重启）
- 无法清空线体的场景

---

### 方案C：混合模式（配置化选择）

**核心思想**: 提供配置选项，让用户根据实际场景选择启动模式。

#### C.1 配置模型

**新增**: `src/Core/.../Models/StartupModeConfiguration.cs`

```csharp
namespace ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;

/// <summary>
/// 系统启动模式配置
/// </summary>
public class StartupModeConfiguration
{
    public int Id { get; set; }
    public string ConfigName { get; set; } = "startup-mode";
    
    /// <summary>
    /// 启动模式
    /// </summary>
    /// <remarks>
    /// - Cold: 冷启动（要求空线体）
    /// - Warm: 暖启动（自动处理预存在包裹）
    /// </remarks>
    public StartupMode Mode { get; set; } = StartupMode.Cold;
    
    /// <summary>
    /// 预存在包裹检测窗口（秒）
    /// </summary>
    /// <remarks>
    /// 系统启动后N秒内检测到的包裹判定为预存在包裹
    /// 仅在 Warm 模式下生效
    /// </remarks>
    public int PreExistingDetectionWindowSeconds { get; set; } = 10;
    
    /// <summary>
    /// 预存在包裹路由策略
    /// </summary>
    public PreExistingParcelRoutingStrategy RoutingStrategy { get; set; } 
        = PreExistingParcelRoutingStrategy.ExceptionChute;
    
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public enum StartupMode
{
    /// <summary>冷启动</summary>
    Cold = 0,
    
    /// <summary>暖启动</summary>
    Warm = 1
}

public enum PreExistingParcelRoutingStrategy
{
    /// <summary>路由到异常格口</summary>
    ExceptionChute = 0,
    
    /// <summary>路由到默认格口（如格口1）</summary>
    DefaultChute = 1,
    
    /// <summary>丢弃（不执行任何动作）</summary>
    Discard = 2
}
```

#### C.2 API 端点

```
GET /api/config/startup-mode
POST /api/config/startup-mode
```

#### C.3 优点与缺点

**优点** ✅:
- 灵活性最高
- 适应不同生产环境
- 用户可根据实际情况选择

**缺点** ❌:
- 实施最复杂
- 配置选项多，用户可能困惑
- 需要完善的文档支持

---

## 推荐方案

### 短期方案：方案A（冷启动协议）⭐

**理由**:
1. 实施简单，风险低
2. 完全符合当前系统设计
3. 无需复杂的状态管理
4. 适合当前阶段快速上线

**实施优先级**: 高
**预计工作量**: 2-4小时

### 长期方案：方案C（混合模式）

**理由**:
1. 满足不同客户需求
2. 提升系统适应性
3. 增强产品竞争力

**实施优先级**: 中
**预计工作量**: 2-3天

---

## 实施路线图

### 阶段1: 短期快速修复（立即）

```
✅ 1. 在 SortingOrchestrator.OnSystemStateChanged 中添加冷启动清理逻辑
✅ 2. 添加日志记录
✅ 3. 更新相关文档
✅ 4. 测试验证
```

### 阶段2: 配置化支持（2-4周）

```
☑ 1. 设计并实现 StartupModeConfiguration 模型
☑ 2. 实现暖启动逻辑（预存在包裹检测与处理）
☑ 3. 添加 API 端点支持配置切换
☑ 4. 完善 Swagger 文档
☑ 5. 编写用户指南
☑ 6. 全面测试（包括边缘场景）
```

### 阶段3: 智能优化（1-2个月）

```
☐ 1. 基于机器学习的包裹检测
☐ 2. 自动识别启动场景
☐ 3. 动态调整检测窗口
☐ 4. 性能优化
```

---

## 测试场景

### 测试场景1: 冷启动（空线体）

```
前置条件: 线体完全为空
操作步骤:
1. 系统从 Stopped → Ready → Running
2. 发送第一个包裹
预期结果:
- 包裹正常通过所有 Position
- 队列顺序正确
- 中位数统计正常
```

### 测试场景2: 暖启动（线体有1个包裹）

```
前置条件: 包裹A停留在 Position 2-3之间
操作步骤:
1. 系统启动
2. 传感器2触发（检测到包裹A）
3. 线体运行，包裹A继续移动
预期结果:
- 包裹A被识别为预存在包裹
- 包裹A路由到异常格口（或配置的处理策略）
- 不影响后续正常包裹
```

### 测试场景3: 暖启动（线体布满包裹）

```
前置条件: 
- 包裹A在 Position 3-4之间
- 包裹B在 Position 2-3之间
- 包裹C在 Position 1-2之间
操作步骤:
1. 系统启动
2. 传感器1、2、3同时触发
3. 线体运行
预期结果:
- 所有包裹都被识别为预存在包裹
- 所有包裹路由到异常格口
- 清空完成后，系统进入正常状态
```

---

## 相关文档

- [边缘场景处理机制](./EDGE_CASE_HANDLING.md) - 包裹转向失败场景
- [核心路由逻辑](./CORE_ROUTING_LOGIC.md) - Position-Index 队列机制
- [包裹丢失检测方案](./PARCEL_LOSS_DETECTION_SOLUTION.md) - 中位数自适应超时检测

---

## 附录：当前系统行为

**当前系统行为**（无特殊处理）:

```
启动时线体有包裹 → 多个传感器同时触发 → 创建多个包裹 → 队列顺序错乱 → 可能导致错误分拣
```

**建议**:
- **立即实施方案A**（冷启动协议）避免问题发生
- **规划实施方案C**（混合模式）提升系统灵活性

---

**文档版本**: 1.0  
**最后更新**: 2025-12-23  
**维护团队**: ZakYip Development Team
