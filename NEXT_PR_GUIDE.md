# 下一个 PR 工作指南

> 本文档指导如何完成拓扑驱动分拣流程的剩余50%工作（Issue #4 的 Phase 2c + Phase 3）

## 前置条件

在开始之前，请确保已阅读以下文档：

1. **`TOPOLOGY_IMPLEMENTATION_PLAN.md`** - 完整的拓扑驱动实现计划和架构设计
2. **`docs/RepositoryStructure.md`** - 第 5 章节技术债索引（TD-062）
3. **`docs/TechnicalDebtLog.md`** - TD-062 详细说明

## 技术债标识

**ID**: TD-062  
**标题**: 完成拓扑驱动分拣流程集成（Issue #4 剩余50%）  
**状态**: ❌ 未开始  
**估计工作量**: 2-3 小时

## 已完成的工作（当前 PR）

### Phase 1 - 基础架构 ✅
- ✅ `PendingParcelQueue` 实现（线程安全 FIFO 队列）
- ✅ 按摆轮节点分组
- ✅ 超时检测机制

### Phase 2a - 配置结构 ✅
- ✅ `TopologyDrivenSortingOptions` 配置类
- ✅ `PendingParcelTimeoutMonitor` 后台服务骨架
- ✅ 移除 Immediate 模式的配置准备

### Phase 2b - 设计规范 ✅
- ✅ 事件驱动 Timer 设计（替代轮询间隔）
- ✅ 超时从 `calculatedTimeoutThresholdMs` 计算
- ✅ `frontSensorId` 映射规范
- ✅ 方向从 `leftChuteIds`/`rightChuteIds` 判断
- ✅ 完整实现文档

## 待完成的工作（下一个 PR）

### Phase 2c - Orchestrator 集成

**文件**: `src/Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration/SortingOrchestrator.cs`

**任务清单**:

1. **添加依赖注入**
   ```csharp
   private readonly IPendingParcelQueue _pendingQueue;
   private readonly ITopologyConfigurationService _topologyService;
   private readonly ILineSegmentService _segmentService;
   
   // 在构造函数中注入
   ```

2. **修改 ProcessParcelAsync 方法**
   - 路由成功后，不立即执行分拣
   - 查询拓扑配置获取摆轮节点信息：
     ```csharp
     var topology = await _topologyService.GetTopologyAsync();
     var diverterNode = topology.DiverterNodes.FirstOrDefault(n => 
         n.LeftChuteIds.Contains(targetChuteId) || 
         n.RightChuteIds.Contains(targetChuteId));
     ```
   - 查询线体段配置获取超时时间：
     ```csharp
     var segment = await _segmentService.GetSegmentAsync(diverterNode.SegmentId);
     var timeoutMs = segment.CalculatedTimeoutThresholdMs;
     ```
   - 将包裹加入 PendingQueue：
     ```csharp
     await _pendingQueue.EnqueueAsync(
         parcelId, 
         targetChuteId, 
         diverterNode.DiverterId, 
         TimeSpan.FromMilliseconds(timeoutMs));
     ```
   - 记录日志：包裹已入队，等待 WheelFront 传感器触发

3. **订阅 WheelFront 传感器事件**
   - 在 Orchestrator 初始化时订阅 `ParcelDetectionService.ParcelDetected` 事件
   - 过滤 `SensorType.WheelFront` 的传感器事件
   - 事件处理逻辑：
     ```csharp
     private async Task OnWheelFrontSensorTriggered(object sender, ParcelDetectedEventArgs e)
     {
         // 1. 根据 sensorId 查找对应的 diverterId
         var topology = await _topologyService.GetTopologyAsync();
         var diverterNode = topology.DiverterNodes.FirstOrDefault(n => 
             n.FrontSensorId == e.SensorId);
         
         if (diverterNode == null)
         {
             _logger.LogWarning($"未找到传感器 {e.SensorId} 对应的摆轮节点");
             return;
         }
         
         // 2. 从队列取出该摆轮的第一个包裹
         var parcel = await _pendingQueue.DequeueByWheelNodeAsync(diverterNode.DiverterId);
         
         if (parcel == null)
         {
             _logger.LogWarning($"摆轮 {diverterNode.DiverterId} 前传感器触发，但队列中无等待包裹");
             return;
         }
         
         // 3. 执行分拣
         await ExecuteSortingAsync(parcel.ParcelId, parcel.TargetChuteId);
         
         _logger.LogInformation($"包裹 {parcel.ParcelId} 触发摆轮 {diverterNode.DiverterId}，执行分拣到格口 {parcel.TargetChuteId}");
     }
     ```

4. **实现超时包裹处理**
   - 在 `PendingParcelTimeoutMonitor` 中实现定时检查逻辑
   - 调用 `_pendingQueue.GetTimedOutParcelsAsync()` 获取超时包裹
   - 对每个超时包裹：
     ```csharp
     foreach (var parcel in timedOutParcels)
     {
         _logger.LogWarning($"包裹 {parcel.ParcelId} 超时未到达摆轮，路由到异常格口");
         
         // 路由到异常格口（999）
         await _orchestrator.RouteFallbackAsync(parcel.ParcelId, ExceptionChuteId);
     }
     ```

### Phase 3 - DI 注册和测试

**文件**: `src/Application/ZakYip.WheelDiverterSorter.Application/Extensions/WheelDiverterSorterServiceCollectionExtensions.cs`

**任务清单**:

1. **注册服务**
   ```csharp
   // 在 AddWheelDiverterSorter() 方法中添加
   services.AddSingleton<IPendingParcelQueue, PendingParcelQueue>();
   services.AddHostedService<PendingParcelTimeoutMonitor>();
   ```

2. **添加单元测试**
   - `PendingParcelQueueTests.cs`:
     - 测试 Enqueue/Dequeue 基本操作
     - 测试 FIFO 顺序
     - 测试按摆轮节点分组
     - 测试超时检测
     - 测试并发安全性
   
   - `PendingParcelTimeoutMonitorTests.cs`:
     - 测试超时包裹识别
     - 测试超时处理逻辑
     - 测试 Timer 调度

3. **添加集成测试**
   - `TopologyDrivenSortingFlowTests.cs`:
     - 测试完整流程：ParcelCreation → Route → Queue → WheelFront → Execute
     - 测试超时场景：包裹超时自动路由到异常格口
     - 测试多包裹并发场景
     - 测试拓扑配置缺失场景

## 实现步骤建议

### 第 1 步：理解当前架构（30分钟）
1. 阅读 `TOPOLOGY_IMPLEMENTATION_PLAN.md`
2. 查看 `PendingParcelQueue.cs` 实现
3. 查看 `SortingOrchestrator.cs` 当前流程
4. 了解拓扑配置API结构

### 第 2 步：实现 Orchestrator 集成（1小时）
1. 添加依赖注入（5分钟）
2. 修改 `ProcessParcelAsync` 方法（30分钟）
3. 实现 WheelFront 事件订阅（25分钟）

### 第 3 步：实现超时监控（30分钟）
1. 完善 `PendingParcelTimeoutMonitor` 实现
2. 集成到 Orchestrator 的超时回调

### 第 4 步：DI 注册（10分钟）
1. 注册 `IPendingParcelQueue`
2. 注册 `PendingParcelTimeoutMonitor`
3. 验证启动无错误

### 第 5 步：测试（1小时）
1. 编写单元测试（30分钟）
2. 编写集成测试（30分钟）
3. 运行所有测试验证

### 第 6 步：文档更新（10分钟）
1. 更新 `RepositoryStructure.md` 标记 TD-062 为 ✅
2. 更新 `TechnicalDebtLog.md` 添加完成记录
3. 删除 `NEXT_PR_GUIDE.md`（本文档）

## 验收标准

下一个 PR 必须满足以下条件才能合并：

1. ✅ 构建成功（0 errors, 0 warnings）
2. ✅ 所有现有测试通过
3. ✅ 新增测试全部通过
4. ✅ E2E 测试验证完整流程：
   - ParcelCreation 传感器触发
   - 包裹加入 PendingQueue
   - WheelFront 传感器触发
   - 包裹从队列取出并执行分拣
   - 超时包裹路由到异常格口
5. ✅ 日志完整记录每个步骤
6. ✅ 技术债 TD-062 标记为已解决

## 配置要求

完成后，系统运行需要以下配置：

```json
// 拓扑配置 (GET /api/topology/config)
{
  "diverterNodes": [
    {
      "diverterId": 1,
      "segmentId": 1,           // 关联线体段
      "frontSensorId": 2,        // WheelFront 传感器
      "leftChuteIds": [2, 3],    // 左转格口
      "rightChuteIds": [1, 4]    // 右转格口
    }
  ]
}

// 线体段配置 (GET /api/topology/segments)
{
  "segments": [
    {
      "segmentId": 1,
      "calculatedTimeoutThresholdMs": 5500  // 超时阈值（毫秒）
    }
  ]
}
```

## ⚠️ 注意事项

1. **BREAKING CHANGE**: 完成后，旧的 Immediate 立即执行模式将完全失效
2. **必需的配置**: 系统启动前必须配置完整的拓扑和线体段信息
3. **传感器映射**: 所有摆轮的 `frontSensorId` 必须正确配置
4. **格口映射**: 所有格口必须在某个摆轮的 `leftChuteIds` 或 `rightChuteIds` 中

## 参考资料

- **架构设计**: `TOPOLOGY_IMPLEMENTATION_PLAN.md`
- **技术债详情**: `docs/TechnicalDebtLog.md` - TD-062
- **拓扑配置API**: `docs/guides/TOPOLOGY_CONFIGURATION_API.md`（如存在）
- **编码规范**: `.github/copilot-instructions.md`

## 联系方式

如有问题，请参考：
- Issue #4 的原始需求和讨论
- PR 评论中的澄清说明
- 拓扑配置示例JSON

---

**创建时间**: 2025-12-10  
**预计完成时间**: 2-3 小时  
**优先级**: 高（用户明确要求完成）
