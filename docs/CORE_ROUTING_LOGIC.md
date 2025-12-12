# 包裹路由与位置索引队列机制（核心业务逻辑）

> **文档类型**: 核心业务逻辑规范  
> **优先级**: 🔴 **P0 - 最高优先级**  
> **变更控制**: ⚠️ **任何修改必须经过批准**  
> **生效日期**: 2025-12-12

---

## ⚠️ 重要警告

**本文档定义的业务逻辑为系统核心机制，任何PR如果违背本文档定义的逻辑原则，必须先获得明确批准才能合并。**

**变更流程**:
1. 识别可能影响本逻辑的代码修改
2. 在PR中明确标注"影响核心路由逻辑"
3. 等待明确批准后方可继续
4. 更新本文档以反映变更

---

## 一、核心概念

### 1.1 位置索引（Position Index）

**定义**: 每个摆轮节点在拓扑中的位置编号，用于标识包裹经过摆轮的顺序。

**特性**:
- 每个 `positionIndex` 对应一个独立的 FIFO 任务队列
- 队列中的任务按照包裹创建顺序排列
- 每个任务包含：包裹Id、摆轮动作、理论到达时间、超时容差、异常动作

**示例**:
```json
{
  "diverterId": 1,
  "positionIndex": 1,
  "frontSensorId": 2,
  "queue": [
    {
      "parcelId": "P1",
      "action": "Left",
      "expectedArrivalTime": "2025-12-12T20:30:00",
      "timeoutTolerance": 2000,
      "fallbackAction": "Straight"
    },
    {
      "parcelId": "P2",
      "action": "Right",
      "expectedArrivalTime": "2025-12-12T20:30:05",
      "timeoutTolerance": 2000,
      "fallbackAction": "Straight"
    }
  ]
}
```

### 1.2 触发点（Trigger Point）

**定义**: `frontSensorId` 对应的IO点，当包裹到达该传感器时触发摆轮动作。

**机制**:
- `positionIndex` 的触发点是绑定的 `frontSensorId` 的IO点
- 触发时从对应 `positionIndex` 的队列中取出第一个任务（FIFO）
- 执行任务中定义的摆轮动作

### 1.3 路径编排（Path Orchestration）

**定义**: 根据包裹的目标格口，计算需要经过的摆轮节点和每个节点的动作。

**规则**:
- 路径由一系列 `[摆轮Id, 动作]` 对组成
- 动作：Left（左转）、Right（右转）、Straight（直通）
- 路径计算基于拓扑结构和格口映射关系

---

## 二、拓扑结构示例

### 2.1 标准3摆轮6格口配置

```json
{
  "topologyId": "default",
  "topologyName": "标准格口路径拓扑",
  "description": "3摆轮6格口的标准配置",
  "entrySensorId": 1,
  "diverterNodes": [
    {
      "diverterId": 1,
      "diverterName": "摆轮D1",
      "positionIndex": 1,
      "segmentId": 1,
      "frontSensorId": 2,
      "leftChuteIds": [1],
      "rightChuteIds": [2]
    },
    {
      "diverterId": 2,
      "diverterName": "摆轮D2",
      "positionIndex": 2,
      "segmentId": 2,
      "frontSensorId": 4,
      "leftChuteIds": [3],
      "rightChuteIds": [4]
    }
  ],
  "exceptionChuteId": 999
}
```

### 2.2 拓扑关系

```
                  ┌──> 格口1 (Left)
   入口传感器1 ──> 摆轮D1 (frontSensor2) ─┼──> 格口2 (Right)
                  └──> 直通 ──> 摆轮D2 (frontSensor4) ─┼──> 格口3 (Left)
                                                        └──> 格口4 (Right)
```

---

## 三、核心业务流程

### 3.1 包裹路径编排流程

#### 步骤1: 包裹创建时计算路径

**输入**: 包裹Id、目标格口Id

**处理**:
1. 根据目标格口查找拓扑路径
2. 生成路径序列：`[(diverterId, action, positionIndex), ...]`
3. 为每个 `positionIndex` 创建队列任务

**示例**:
```csharp
// 包裹 P3 目标格口 3
var path = CalculatePath(chuteId: 3);
// 结果: 
// [
//   (diverterId: 1, action: Straight, positionIndex: 1),
//   (diverterId: 2, action: Left, positionIndex: 2)
// ]

// 为每个 positionIndex 创建任务
EnqueueTask(positionIndex: 1, new Task {
    ParcelId = "P3",
    Action = DiverterDirection.Straight,
    ExpectedArrivalTime = CalculateArrivalTime(segmentId: 1),
    TimeoutTolerance = GetTimeoutTolerance(segmentId: 1),
    FallbackAction = DiverterDirection.Straight
});

EnqueueTask(positionIndex: 2, new Task {
    ParcelId = "P3",
    Action = DiverterDirection.Left,
    ExpectedArrivalTime = CalculateArrivalTime(segmentId: 2),
    TimeoutTolerance = GetTimeoutTolerance(segmentId: 2),
    FallbackAction = DiverterDirection.Straight
});
```

#### 步骤2: 路径计算算法

```csharp
public List<(int DiverterId, DiverterDirection Action, int PositionIndex)> 
    CalculatePath(int targetChuteId)
{
    var path = new List<(int, DiverterDirection, int)>();
    
    // 遍历拓扑中的摆轮节点（按 positionIndex 排序）
    foreach (var node in diverterNodes.OrderBy(n => n.PositionIndex))
    {
        if (node.LeftChuteIds.Contains(targetChuteId))
        {
            // 目标格口在左侧
            path.Add((node.DiverterId, DiverterDirection.Left, node.PositionIndex));
            break; // 到达目标，停止
        }
        else if (node.RightChuteIds.Contains(targetChuteId))
        {
            // 目标格口在右侧
            path.Add((node.DiverterId, DiverterDirection.Right, node.PositionIndex));
            break; // 到达目标，停止
        }
        else
        {
            // 目标格口不在当前摆轮，直通到下一个摆轮
            path.Add((node.DiverterId, DiverterDirection.Straight, node.PositionIndex));
        }
    }
    
    return path;
}
```

### 3.2 触发执行流程

#### 触发时机

**条件**: 包裹到达 `frontSensorId` 对应的IO点

**处理流程**:
```csharp
public async Task OnSensorTriggered(int sensorId)
{
    // 1. 查找 sensorId 对应的 positionIndex
    var positionIndex = FindPositionIndexBySensorId(sensorId);
    if (positionIndex == null) return;
    
    // 2. 从队列中取出第一个任务（FIFO）
    var task = DequeueTask(positionIndex.Value);
    if (task == null)
    {
        _logger.LogWarning($"位置索引 {positionIndex} 队列为空，但传感器 {sensorId} 被触发");
        return;
    }
    
    // 3. 检查是否超时
    var now = _clock.LocalNow;
    var isTimeout = now > (task.ExpectedArrivalTime + task.TimeoutTolerance);
    
    // 4. 确定执行动作
    var action = isTimeout ? task.FallbackAction : task.Action;
    
    // 5. 执行摆轮动作
    var diverterId = GetDiverterIdByPositionIndex(positionIndex.Value);
    await ExecuteDiverterAction(diverterId, action);
    
    // 6. 如果是超时异常，需要在后续节点插入异常动作
    if (isTimeout && action == DiverterDirection.Straight)
    {
        InsertFallbackTasksForSubsequentNodes(task.ParcelId, positionIndex.Value);
    }
}
```

---

## 四、完整场景示例

### 4.1 场景定义

**包裹列表**:
- P1: 目标格口 1（需要：D1左转）
- P2: 目标格口 2（需要：D1右转）
- P3: 目标格口 3（需要：D1直通 → D2左转）

### 4.2 初始状态（包裹创建后）

**positionIndex 1 队列**:
```
[
  {parcelId: "P1", action: Left},
  {parcelId: "P2", action: Right},
  {parcelId: "P3", action: Straight}
]
```

**positionIndex 2 队列**:
```
[
  {parcelId: "P3", action: Left}
]
```

### 4.3 执行序列（正常情况）

#### 时刻 T1: P1 触发 frontSensorId2
```
触发: frontSensorId2 (positionIndex 1)
取出: {parcelId: "P1", action: Left}
执行: diverterId 1 → Left
结果: P1 → 格口1

positionIndex 1 队列剩余:
[
  {parcelId: "P2", action: Right},
  {parcelId: "P3", action: Straight}
]
```

#### 时刻 T2: P2 触发 frontSensorId2
```
触发: frontSensorId2 (positionIndex 1)
取出: {parcelId: "P2", action: Right}
执行: diverterId 1 → Right
结果: P2 → 格口2

positionIndex 1 队列剩余:
[
  {parcelId: "P3", action: Straight}
]
```

#### 时刻 T3: P3 触发 frontSensorId2
```
触发: frontSensorId2 (positionIndex 1)
取出: {parcelId: "P3", action: Straight}
执行: diverterId 1 → Straight
结果: P3 直通，继续前往 D2

positionIndex 1 队列剩余: []
```

#### 时刻 T4: P3 触发 frontSensorId4
```
触发: frontSensorId4 (positionIndex 2)
取出: {parcelId: "P3", action: Left}
执行: diverterId 2 → Left
结果: P3 → 格口3

positionIndex 2 队列剩余: []
```

### 4.4 执行序列（超时异常情况）

#### 假设: P2 在 T2 时刻超时

```
时刻 T2: P2 触发 frontSensorId2（但已超时）

触发: frontSensorId2 (positionIndex 1)
取出: {parcelId: "P2", action: Right, fallbackAction: Straight}
检查: now > (expectedArrivalTime + timeoutTolerance) → 超时
执行: diverterId 1 → Straight (异常动作)
结果: P2 直通（未按计划右转）

positionIndex 1 队列剩余:
[
  {parcelId: "P3", action: Straight}
]

补偿操作:
因为 P2 会比 P3 先到达 positionIndex 2，需要在 positionIndex 2 队列前插入异常任务：
positionIndex 2 队列变为:
[
  {parcelId: "P2", action: Straight, isCompensation: true},  // 新插入
  {parcelId: "P3", action: Left}
]
```

---

## 五、关键要点与约束

### 5.1 以触发为操作起点

⚠️ **强制约束**: 所有的创建包裹、执行摆轮动作的判断、动作都以触发IO为操作起点，在没有触发之前只能等待触发。

**含义**:
- ❌ 禁止：定时扫描包裹并主动执行动作
- ❌ 禁止：在包裹创建时立即执行摆轮动作
- ✅ 正确：仅在IO点触发时才执行动作
- ✅ 正确：包裹创建时仅入队，不执行

**实施**:
```csharp
// ❌ 错误：包裹创建时执行动作
public async Task CreateParcel(CreateParcelRequest request)
{
    var parcel = new Parcel { Id = request.ParcelId, ChuteId = request.ChuteId };
    await _repository.SaveAsync(parcel);
    
    // ❌ 禁止在此执行摆轮动作
    var path = CalculatePath(request.ChuteId);
    await ExecutePath(path);  // ❌ 错误！
}

// ✅ 正确：包裹创建时仅入队
public async Task CreateParcel(CreateParcelRequest request)
{
    var parcel = new Parcel { Id = request.ParcelId, ChuteId = request.ChuteId };
    await _repository.SaveAsync(parcel);
    
    // ✅ 仅计算路径并入队
    var path = CalculatePath(request.ChuteId);
    EnqueueTasks(parcel.Id, path);
}

// ✅ 正确：仅在IO触发时执行
public async Task OnSensorTriggered(int sensorId)
{
    var positionIndex = FindPositionIndexBySensorId(sensorId);
    var task = DequeueTask(positionIndex);
    await ExecuteDiverterAction(task.DiverterId, task.Action);
}
```

### 5.2 FIFO队列机制

⚠️ **强制约束**: 每个 `positionIndex` 的队列必须严格遵循FIFO（先进先出）原则。

**含义**:
- ❌ 禁止：跳过队列中的任务
- ❌ 禁止：根据优先级重新排序
- ✅ 正确：始终取出队首任务
- ✅ 例外：超时补偿时可在队首插入异常任务

### 5.3 清空队列时机

⚠️ **强制约束**: 当面板IO按下停止、急停、复位时，必须清空所有队列和任务。

**实施**:
```csharp
public async Task OnPanelButtonPressed(PanelButtonType buttonType)
{
    if (buttonType == PanelButtonType.Stop || 
        buttonType == PanelButtonType.EmergencyStop ||
        buttonType == PanelButtonType.Reset)
    {
        // 清空所有 positionIndex 队列
        ClearAllQueues();
        
        _logger.LogWarning($"面板按钮 {buttonType} 被按下，所有位置索引队列已清空");
    }
}
```

### 5.4 超时处理机制

⚠️ **强制约束**: 超时时必须执行异常动作（默认直通），并在后续节点插入补偿任务。

**计算公式**:
```
超时 = 当前时间 > (理论到达时间 + 超时容差时间)
```

**补偿逻辑**:
```csharp
if (isTimeout && action == DiverterDirection.Straight)
{
    // 包裹因超时而直通，需要在后续所有计划节点前插入直通任务
    var subsequentNodes = GetSubsequentPositionIndexes(currentPositionIndex);
    foreach (var nodeIndex in subsequentNodes)
    {
        // 在队首插入补偿任务（因为超时包裹会比正常包裹先到）
        InsertTaskAtFront(nodeIndex, new Task {
            ParcelId = task.ParcelId,
            Action = DiverterDirection.Straight,
            IsCompensation = true
        });
    }
}
```

---

## 六、架构映射

### 6.1 核心接口与类

**路径计算**:
- `ISwitchingPathGenerator` - 路径生成器接口
- 实现类应根据拓扑结构计算路径

**队列管理**:
- `IPositionIndexQueueManager` - 位置索引队列管理器（需新增）
- 管理所有 `positionIndex` 的任务队列

**触发处理**:
- `ISensorEventHandler` - 传感器事件处理器
- 监听IO触发，执行队列任务

**摆轮执行**:
- `IWheelDiverterDevice` - 摆轮设备接口
- 执行具体的摆轮动作

### 6.2 数据模型

**队列任务模型**:
```csharp
public record PositionIndexTask
{
    /// <summary>包裹Id</summary>
    public required string ParcelId { get; init; }
    
    /// <summary>摆轮动作</summary>
    public required DiverterDirection Action { get; init; }
    
    /// <summary>理论到达时间</summary>
    public required DateTimeOffset ExpectedArrivalTime { get; init; }
    
    /// <summary>超时容差（毫秒）</summary>
    public required int TimeoutToleranceMs { get; init; }
    
    /// <summary>异常动作（默认直通）</summary>
    public DiverterDirection FallbackAction { get; init; } = DiverterDirection.Straight;
    
    /// <summary>是否为补偿任务</summary>
    public bool IsCompensation { get; init; } = false;
}
```

---

## 七、变更控制流程

### 7.1 识别影响

以下类型的PR可能影响本核心逻辑，必须特别审查：

1. **路径计算相关**:
   - 修改 `ISwitchingPathGenerator` 或其实现
   - 修改拓扑结构模型
   - 修改格口映射逻辑

2. **队列管理相关**:
   - 新增或修改队列管理组件
   - 修改入队/出队逻辑
   - 修改队列清空逻辑

3. **触发机制相关**:
   - 修改传感器事件处理流程
   - 修改IO触发逻辑
   - 新增自动触发机制

4. **超时处理相关**:
   - 修改超时判断逻辑
   - 修改异常动作执行
   - 修改补偿任务插入逻辑

### 7.2 审批流程

**步骤1**: PR创建者在PR描述中明确标注：
```markdown
## ⚠️ 影响核心路由逻辑

本PR修改了以下核心机制：
- [ ] 路径计算
- [ ] 队列管理
- [ ] 触发机制
- [ ] 超时处理

**变更说明**: [详细描述修改内容和原因]

**兼容性**: [说明是否保持与本文档定义逻辑的兼容性]
```

**步骤2**: Code Review时重点审查：
- 是否违背"以触发为起点"原则
- 是否破坏FIFO队列机制
- 是否影响超时处理逻辑
- 是否缺少队列清空处理

**步骤3**: 获得明确批准后方可合并

**步骤4**: 合并后更新本文档

---

## 八、测试要求

### 8.1 单元测试

必须覆盖以下场景：

1. **路径计算**:
   - 不同目标格口的路径计算正确性
   - 多级摆轮的路径计算

2. **队列管理**:
   - 入队顺序正确性
   - 出队FIFO顺序
   - 队列清空功能

3. **触发执行**:
   - 正常触发执行正确动作
   - 超时触发执行异常动作
   - 队列为空时触发的处理

4. **超时补偿**:
   - 超时时插入补偿任务
   - 补偿任务在正确位置
   - 多包裹超时的处理

### 8.2 集成测试

必须覆盖以下场景：

1. **完整流程测试**:
   - 3个包裹按示例场景完整执行
   - 验证每个包裹到达正确格口

2. **超时异常测试**:
   - 模拟P2超时
   - 验证异常动作和补偿逻辑

3. **队列清空测试**:
   - 模拟面板按钮按下
   - 验证所有队列被清空

---

## 九、参考文档

- `docs/guides/UPSTREAM_CONNECTION_GUIDE.md` - 上游协议（路由请求相关）
- `docs/RepositoryStructure.md` - 仓库结构
- `.github/copilot-instructions.md` - 编码规范

---

**文档版本**: 1.0  
**最后更新**: 2025-12-12  
**维护团队**: ZakYip Development Team  
**批准人**: Hisoka6602
