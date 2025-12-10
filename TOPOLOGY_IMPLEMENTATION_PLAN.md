# 拓扑驱动分拣流程完整实现计划

## 用户需求明确

根据用户提供的配置结构，所有信息已在拓扑和线体配置中：

### 1. 拓扑配置 (TopologyConfiguration)
```json
{
  "diverterNodes": [
    {
      "diverterId": 1,
      "segmentId": 1,           // → 用于查询线体配置获取时间
      "frontSensorId": 2,        // → 摆轮前传感器ID  
      "leftChuteIds": [2, 3],    // → 左转格口列表
      "rightChuteIds": [1, 4]    // → 右转格口列表
    }
  ]
}
```

### 2. 线体配置 (LineSegment)
```json
{
  "segmentId": 1,
  "lengthMm": 5000,              // 段长度
  "speedMmps": 1000,             // 线速  
  "timeToleranceMs": 500,        // 容差
  "calculatedTransitTimeMs": 5000,        // = lengthMm / speedMmps * 1000
  "calculatedTimeoutThresholdMs": 5500    // = transitTime + tolerance
}
```

## 核心逻辑

### 流程
```
1. ParcelCreation传感器触发 
   → 创建包裹

2. 请求上游路由
   → 获得targetChuteId

3. 查询拓扑配置
   → 根据targetChuteId找到对应的DiverterNode
   → 获取segmentId和frontSensorId

4. 查询线体配置  
   → 根据segmentId获取calculatedTimeoutThresholdMs

5. 加入PendingQueue
   → Enqueue(parcelId, targetChuteId, frontSensorId, timeoutMs)
   → 立即注册Timer（事件驱动，无需轮询）

6. 等待WheelFront传感器触发
   → 传感器ID == frontSensorId时
   → Dequeue(frontSensorId)
   → 取消Timer
   → 执行分拣（根据leftChuteIds/rightChuteIds判断方向）

7. 超时处理
   → Timer触发ParcelTimedOut事件
   → 自动路由到异常格口(999)
```

### 方向判断逻辑
```csharp
Direction GetDirection(long targetChuteId, DiverterNode node)
{
    if (node.LeftChuteIds.Contains(targetChuteId))
        return Direction.Left;
    if (node.RightChuteIds.Contains(targetChuteId))
        return Direction.Right;
    // 默认直通或抛出异常
    return Direction.Straight;
}
```

## 已完成的工作

✅ Phase 1 (commit d63c1c3):
- PendingParcelQueue基础结构
- FIFO队列、线程安全

✅ Phase 2a (commit 41e60c6):  
- TopologyDrivenSortingOptions配置（现已简化为空类）
- PendingParcelTimeoutMonitor后台服务
- 移除Feature flag

✅ Phase 2b (当前开发中):
- 事件驱动的超时监控（Timer替代轮询）
- ParcelTimedOut事件
- 超时时间从配置计算（移除硬编码）

## 待完成的工作

### Phase 2c - Orchestrator集成 (预计2小时)

1. **修改ProcessParcelAsync**:
```csharp
// 旧流程（删除）:
// CreateParcel → RequestRouting → ExecutePath → Complete

// 新流程:
public async Task<SortingResult> ProcessParcelAsync(string parcelId)
{
    // 1. 创建包裹
    var parcel = await CreateParcelAsync(parcelId);
    
    // 2. 请求路由
    var targetChuteId = await RequestRoutingAsync(parcelId);
    
    // 3. 查询拓扑配置（新增）
    var diverterNode = await _topologyService.GetDiverterNodeByChuteId(targetChuteId);
    
    // 4. 查询线体配置（新增）
    var segment = await _lineSegmentRepo.GetByIdAsync(diverterNode.SegmentId);
    var timeoutMs = segment.CalculatedTimeoutThresholdMs;
    
    // 5. 加入待执行队列（新增）
    _pendingQueue.Enqueue(
        parcelId, 
        targetChuteId, 
        diverterNode.FrontSensorId.ToString(), 
        timeoutMs);
    
    // 6. 返回（不再立即执行分拣）
    return SortingResult.Pending(parcelId, targetChuteId);
}
```

2. **订阅WheelFront传感器事件**:
```csharp
private void OnSensorTriggered(object? sender, SensorTriggeredEventArgs e)
{
    // 检查传感器类型
    var sensorConfig = _sensorConfigRepo.GetById(e.SensorId);
    if (sensorConfig.IoType != SensorIoType.WheelFront)
        return;
    
    // 从队列取出包裹
    var parcel = _pendingQueue.DequeueByWheelNode(e.SensorId.ToString());
    if (parcel == null)
    {
        _logger.LogWarning("WheelFront传感器 {SensorId} 触发，但队列中无对应包裹", e.SensorId);
        return;
    }
    
    // 执行分拣
    await ExecuteSortingAsync(parcel);
}
```

3. **实现超时包裹处理**:
```csharp
public async Task ProcessTimedOutParcelAsync(long parcelId, CancellationToken ct)
{
    _logger.LogWarning("包裹 {ParcelId} 超时，路由到异常格口", parcelId);
    
    var exceptionChuteId = await _systemConfigRepo.GetExceptionChuteIdAsync();
    var path = _pathGenerator.GeneratePath(exceptionChuteId);
    
    await _pathExecutor.ExecuteAsync(path, ct);
}
```

### Phase 3 - DI注册和测试 (预计1小时)

1. **DI注册**:
```csharp
// Register PendingQueue with event-driven timeout
services.AddSingleton<IPendingParcelQueue, PendingParcelQueue>();

// Register timeout monitor  
services.AddHostedService<PendingParcelTimeoutMonitor>();

// No configuration needed (topology-driven)
```

2. **单元测试**:
- `PendingParcelQueueTests` - 事件驱动超时测试
- `TopologyDrivenSortingIntegrationTests` - 完整流程测试

## 配置示例

不再需要配置文件！所有时间从拓扑和线体配置自动计算。

## 预计总时间

- Phase 2c: 2小时
- Phase 3: 1小时
- **总计**: 3小时完成

## 关键改进

相比之前的设计：
- ✅ 移除硬编码的超时时间配置
- ✅ 移除定期轮询，改为事件驱动
- ✅ 所有时间从拓扑/线体配置计算
- ✅ frontSensorId直接从拓扑获取
- ✅ 方向通过leftChuteIds/rightChuteIds自动判断
