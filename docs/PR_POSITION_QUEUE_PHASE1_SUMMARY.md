# Position-Index Queue System 实施 - Phase 1 总结

> **PR状态**: Phase 1 & Phase 7 已完成  
> **日期**: 2025-12-12  
> **分支**: `copilot/update-sensor-configuration-api`

---

## 📋 任务背景

根据 Issue 要求，系统需要进行以下重大重构：

### 1. API 配置清理
- 移除 `/api/hardware/leadshine/sensors` 中的业务逻辑字段（boundWheelDiverterId、boundChuteId、deduplicationWindowMs）
- 确认 `/api/config/conveyor-segments` 的计算字段不可手动设置

### 2. 完整重写分拣编排
实现基于 positionIndex 的任务队列系统：
- 每个 positionIndex 一个独立的 FIFO 队列
- 队列元素包含：包裹ID、摆轮动作、理论到达时间、超时容差、异常动作
- 所有执行严格基于 IO 触发点
- 面板控制（停止/急停/复位）清空所有队列

### 3. 包裹丢失检测文档
提供完整的超时检测、丢失识别和处理流程文档

---

## ✅ Phase 1 完成内容

### 1.1 SensorConfiguration 配置清理

**移除的字段**：
- ❌ `BoundWheelDiverterId` - WheelFront 传感器与摆轮的绑定
- ❌ `BoundChuteId` - ChuteLock 传感器与格口的绑定
- ❌ `DeduplicationWindowMs` - 单个传感器的防抖时间窗口

**新的绑定机制**：
- WheelFront 传感器现在通过 **拓扑配置** 的 `DiverterPathNode.FrontSensorId` 绑定到摆轮
- 防抖时间统一使用全局配置 `ParcelDetectionOptions.DeduplicationWindowMs`

**优势**：
- ✅ 清晰的职责分离：硬件配置 vs 拓扑配置
- ✅ 更灵活的拓扑结构配置
- ✅ 简化配置管理

**示例配置对比**：

```json
// ❌ 旧方式（已移除）
{
  "sensorId": 2,
  "sensorName": "摆轮1前感应IO",
  "ioType": "WheelFront",
  "bitNumber": 1,
  "boundWheelDiverterId": 1,  // ← 已移除
  "deduplicationWindowMs": 1000,  // ← 已移除
  "isEnabled": true
}

// ✅ 新方式（v2.0）
{
  "sensorId": 2,
  "sensorName": "摆轮1前感应IO",
  "ioType": "WheelFront",
  "bitNumber": 1,
  "pollingIntervalMs": 10,
  "isEnabled": true
}

// 绑定关系在拓扑配置中定义：
{
  "diverterId": 1,
  "diverterName": "摆轮D1",
  "positionIndex": 1,
  "frontSensorId": 2,  // ← 在这里绑定
  "segmentId": 1
}
```

### 1.2 ConveyorSegmentConfiguration 字段确认

**确认结果**: ✅ 当前实现已正确

- `CalculatedTransitTimeMs` 和 `CalculatedTimeoutThresholdMs` 是 **只读计算字段**
- 在 **Response DTO** 中返回（用于显示计算结果）
- **不在 Request DTO** 中存在（不可手动设置）
- 通过以下方法自动计算：
  ```csharp
  public double CalculateTransitTimeMs() => (LengthMm / (double)SpeedMmps) * 1000;
  public double CalculateTimeoutThresholdMs() => CalculateTransitTimeMs() + TimeToleranceMs;
  ```

**API行为**：
```json
// POST /api/config/conveyor-segments
// Request (不包含计算字段):
{
  "segmentId": 1,
  "segmentName": "入口到摆轮D1",
  "lengthMm": 5000,
  "speedMmps": 1000,
  "timeToleranceMs": 500,
  "enableLossDetection": true
}

// Response (自动计算并返回):
{
  "segmentId": 1,
  "lengthMm": 5000,
  "speedMmps": 1000,
  "timeToleranceMs": 500,
  "calculatedTransitTimeMs": 5000,  // ← 自动计算
  "calculatedTimeoutThresholdMs": 5500,  // ← 自动计算
  "createdAt": "2025-12-12T...",
  "updatedAt": "2025-12-12T..."
}
```

### 1.3 代码变更总结

**修改的文件**：
1. `src/Core/.../LineModel/Configuration/Models/SensorConfiguration.cs`
   - 移除字段定义
   - 移除验证逻辑
   - 更新注释说明绑定机制

2. `src/Ingress/.../Services/ParcelDetectionService.cs`
   - 简化 `GetDeduplicationWindowForSensor()` 方法
   - 统一使用全局配置

3. `src/Host/.../Controllers/HardwareConfigController.cs`
   - 更新 Swagger 文档注释
   - 说明 v2.0 变更

### 1.4 文档输出

**新增文档**：`docs/guides/PARCEL_LOSS_DETECTION.md`

内容包括：
- ✅ 包裹生命周期与检测点
- ✅ 超时检测机制（判定公式、检测时机、处理流程）
- ✅ 包裹丢失识别规则（丢失判定条件、主动/被动检测）
- ✅ 异常场景处理（超时未丢失、完全丢失、队列为空触发、多包裹并发超时）
- ✅ 队列状态恢复（面板控制、清空队列、恢复策略）
- ✅ 监控与告警（关键指标、日志记录、告警规则）
- ✅ 配置建议（TimeToleranceMs、EnableLossDetection、队列监控间隔）
- ✅ 故障排查流程
- ✅ 核心数据结构定义

---

## 🚧 Phase 2-6 待实施内容

### Phase 2: Position-Index 队列系统核心实现

**需要新增**：

1. **PositionQueueItem 模型** (`src/Core/.../Execution/Models/PositionQueueItem.cs`)
   ```csharp
   public record class PositionQueueItem
   {
       public required string ParcelId { get; init; }
       public required long DiverterId { get; init; }
       public required DiverterDirection DiverterAction { get; init; }
       public required DateTime ExpectedArrivalTime { get; init; }
       public required long TimeoutThreshold { get; init; }
       public DiverterDirection FallbackAction { get; init; } = DiverterDirection.Straight;
       public DateTime CreatedAt { get; init; }
   }
   ```

2. **IPositionIndexQueueManager 接口** (`src/Core/.../Execution/Interfaces/`)
   ```csharp
   public interface IPositionIndexQueueManager
   {
       void EnqueueTask(int positionIndex, PositionQueueItem task);
       PositionQueueItem? DequeueTask(int positionIndex);
       PositionQueueItem? PeekTask(int positionIndex);
       void ClearAllQueues();
       QueueStatus GetQueueStatus(int positionIndex);
       Dictionary<int, QueueStatus> GetAllQueueStatuses();
   }
   ```

3. **PositionIndexQueueManager 实现** (`src/Execution/.../Queue/PositionIndexQueueManager.cs`)
   - 使用 `ConcurrentDictionary<int, ConcurrentQueue<PositionQueueItem>>`
   - 线程安全的入队/出队
   - 完整的日志记录

### Phase 3: 路径生成器重构

**需要修改**：
- `ISwitchingPathGenerator` 接口 - 添加 positionIndex 和时间计算
- 实现类 - 从拓扑读取 segmentId，计算理论到达时间

### Phase 4: 包裹创建与任务入队

**需要修改**：
- 包裹创建流程（Parcel-First）
- 路由请求逻辑
- 任务入队逻辑

### Phase 5: IO 触发器与队列执行

**需要重写**：
- `SortingOrchestrator` 或创建新的 `PositionBasedExecutor`
- frontSensorId 触发处理
- 超时检测逻辑
- 超时包裹的后续位置插入逻辑

### Phase 6: 面板控制集成

**需要修改**：
- 停止/急停/复位事件处理
- 调用 `ClearAllQueues()`

---

## 📊 实施建议

### 分阶段PR策略

**推荐将剩余工作拆分为多个PR**：

1. **PR-1 (已完成)**: API 配置清理 + 丢失检测文档
   - ✅ Phase 1: SensorConfiguration 字段移除
   - ✅ Phase 7: PARCEL_LOSS_DETECTION.md

2. **PR-2 (下一步)**: 队列管理器核心实现
   - Phase 2: PositionQueueItem + IPositionIndexQueueManager + PositionIndexQueueManager
   - 单元测试

3. **PR-3**: 路径生成器重构
   - Phase 3: ISwitchingPathGenerator 接口修改
   - 实现新的路径生成逻辑
   - 单元测试

4. **PR-4**: 执行逻辑重写
   - Phase 4: 包裹创建与任务入队
   - Phase 5: IO 触发器与队列执行
   - Phase 6: 面板控制集成
   - 集成测试

5. **PR-5**: 测试与文档完善
   - Phase 8: E2E 测试、性能测试
   - Phase 9: 文档更新

### 关键注意事项

1. **保持现有功能可用**：
   - 在重写过程中，确保系统仍可运行
   - 可以考虑使用功能开关（Feature Flag）逐步切换

2. **测试覆盖**：
   - 每个 Phase 都需要对应的单元测试
   - 重写后运行完整的回归测试

3. **文档同步**：
   - 更新 `docs/RepositoryStructure.md`
   - 更新 API 文档（Swagger）
   - 更新架构图

---

## 🔍 验证方法

### Phase 1 验证清单

- [x] 代码编译通过
- [x] SensorConfiguration 不再包含 boundWheelDiverterId、boundChuteId、deduplicationWindowMs
- [x] ParcelDetectionService 使用全局配置
- [x] HardwareConfigController Swagger 文档已更新
- [x] PARCEL_LOSS_DETECTION.md 文档已创建

### Phase 2-6 验证清单（待完成）

- [ ] PositionIndexQueueManager 单元测试通过
- [ ] 路径生成器测试通过
- [ ] 多包裹并发测试通过
- [ ] 超时场景测试通过
- [ ] 面板控制清空队列测试通过
- [ ] 现有E2E测试全部通过

---

## 📝 相关文档

- [PARCEL_LOSS_DETECTION.md](./guides/PARCEL_LOSS_DETECTION.md) - 包裹丢失检测指南（新增）
- [UPSTREAM_CONNECTION_GUIDE.md](./guides/UPSTREAM_CONNECTION_GUIDE.md) - 上游连接配置
- [TOPOLOGY_LINEAR_N_DIVERTERS.md](./TOPOLOGY_LINEAR_N_DIVERTERS.md) - N 摆轮线性拓扑模型

---

## 🎯 下一步行动

**立即行动**：
1. Review 当前 PR，确认 Phase 1 变更无误
2. 合并 PR-1（如果满意）
3. 开始 PR-2: 实现 PositionIndexQueueManager

**长期规划**：
- 按照上述 PR 拆分策略逐步实施
- 每个 PR 独立测试和审查
- 确保系统始终可用

---

**维护团队**: ZakYip Development Team  
**PR 分支**: `copilot/update-sensor-configuration-api`  
**联系方式**: 请通过 GitHub Issues 或 PR 评论反馈
