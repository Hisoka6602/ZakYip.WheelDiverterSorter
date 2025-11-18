# PR-14: 故障自愈与降级模式实现总结

## Implementation Summary - Node-level Degradation and Self-Healing

### 概述 (Overview)

本PR实现了节点级别的降级和自愈功能，当部分摆轮、基站或IO节点故障时，系统不会完全停止，而是自动进入降级运行模式，将受影响的包裹路由到异常口，保持系统部分可用性。

This PR implements node-level degradation and self-healing. When some diverters, stations, or IO nodes fail, the system doesn't completely stop. Instead, it automatically enters degraded operation mode, routing affected parcels to the exception chute while maintaining partial system availability.

---

## 核心实现 (Core Implementation)

### 1. 核心模型 (Core Models)

#### NodeHealthStatus (节点健康状态)
```csharp
public record struct NodeHealthStatus
{
    public int NodeId { get; init; }              // 节点ID
    public bool IsHealthy { get; init; }          // 是否健康
    public string? ErrorCode { get; init; }       // 错误代码
    public string? ErrorMessage { get; init; }    // 错误消息（中文）
    public string? NodeType { get; init; }        // 节点类型（摆轮/基站/IO）
    public DateTimeOffset CheckedAt { get; init; } // 检查时间
}
```

#### DegradationMode (降级模式)
```csharp
public enum DegradationMode
{
    None = 0,           // 正常模式
    NodeDegraded = 1,   // 节点降级（部分节点不可用）
    LineDegraded = 2    // 线体降级（多个关键节点不可用）
}
```

**降级判断逻辑:**
- 所有节点健康 → `None`
- <30% 节点不健康 → `NodeDegraded`
- ≥30% 节点不健康 → `LineDegraded`

#### OverloadReason (超载原因枚举)
新增 `NodeDegraded` 原因，用于结构化追踪节点降级导致的异常路由。

---

### 2. 节点健康管理 (Node Health Management)

#### INodeHealthRegistry (节点健康注册表接口)
```csharp
public interface INodeHealthRegistry
{
    void UpdateNodeHealth(NodeHealthStatus status);
    NodeHealthStatus? GetNodeHealth(int nodeId);
    IReadOnlyList<NodeHealthStatus> GetAllNodeHealth();
    IReadOnlyList<NodeHealthStatus> GetUnhealthyNodes();
    bool IsNodeHealthy(int nodeId);
    DegradationMode GetDegradationMode();
    event EventHandler<NodeHealthChangedEventArgs>? NodeHealthChanged;
}
```

**特性:**
- 线程安全（使用 ConcurrentDictionary）
- 事件通知机制（健康状态变更时触发事件）
- 默认假设：未注册的节点视为健康

#### NodeHealthRegistry (实现类)
- 单例模式注册
- 自动计算降级模式
- 记录健康状态变更日志

---

### 3. 路径健康检查 (Path Health Checking)

#### PathHealthChecker
在路径规划后、执行前检查路径是否经过不健康节点。

```csharp
public PathHealthResult ValidatePath(SwitchingPath path)
{
    // 检查路径中的每个摆轮节点
    // 返回是否健康及不健康节点列表
}
```

**集成位置:** `ParcelSortingOrchestrator.ProcessSortingAsync()`

**处理流程:**
1. 生成路径后立即检查节点健康
2. 如果路径经过不健康节点：
   - 记录警告日志（包含不健康节点ID列表）
   - 重新生成到异常格口的路径
   - 记录 Trace 日志（Stage=OverloadDecision, Source=NodeHealthCheck）
   - 增加 `NodeDegraded` 指标计数
   - 标记为超载异常

---

### 4. 系统自检集成 (Self-Test Integration)

#### SystemSelfTestCoordinator
扩展自检协调器，将驱动健康状态转换为节点健康状态。

**映射逻辑:**
- NodeId = Hash(DriverName) % 10000
- NodeType 根据驱动名称判断：
  - 包含 "Diverter" → "摆轮"
  - 包含 "Station" 或 "基站" → "基站"
  - 包含 "IO" → "IO设备"
  - 其他 → "驱动器"

**更新时机:**
- 系统启动自检时
- 定期健康检查时

---

### 5. 可观测性 (Observability)

#### Health Endpoint 扩展
`GET /health/line` 新增字段：
```json
{
  "degradationMode": "NodeDegraded",
  "degradedNodesCount": 2,
  "degradedNodes": [
    {
      "nodeId": 101,
      "nodeType": "摆轮",
      "isHealthy": false,
      "errorCode": "COMM_TIMEOUT",
      "errorMessage": "通信超时",
      "checkedAt": "2025-11-18T05:30:00Z"
    }
  ]
}
```

#### Prometheus 指标
```
# 降级节点总数
sorting_degraded_nodes_total{} 2

# 降级模式 (0=None, 1=NodeDegraded, 2=LineDegraded)
sorting_degraded_mode{} 1

# 超载包裹计数（按原因分类）
sorting_overload_parcels_total{reason="NodeDegraded"} 42
```

#### NodeHealthMonitorService
后台服务，每10秒更新一次Prometheus指标，并在节点健康状态变更时立即更新。

---

## 使用场景 (Usage Scenarios)

### 场景1：摆轮故障降级
```
1. 摆轮节点101通信超时
2. SystemSelfTestCoordinator 检测到故障，更新 NodeHealthRegistry
3. NodeHealthRegistry 计算降级模式 = NodeDegraded
4. Prometheus 指标更新
5. 下一个包裹分拣时：
   - PathGenerator 生成经过节点101的路径
   - PathHealthChecker 检测到节点101不健康
   - 重新生成到异常格口的路径
   - 包裹成功分拣到异常口，系统继续运行
```

### 场景2：多节点故障线体降级
```
1. 3个摆轮节点（共10个节点的30%）同时故障
2. DegradationMode 自动切换为 LineDegraded
3. /health/line 显示 degradationMode="LineDegraded"
4. Prometheus alerting 可基于此指标触发告警
5. 运维人员收到通知，进行维护
```

---

## 依赖注入配置 (DI Configuration)

在 `Program.cs` 中自动注册：
```csharp
builder.Services.AddNodeHealthServices();
```

包含的服务：
- `INodeHealthRegistry` → `NodeHealthRegistry` (Singleton)
- `PathHealthChecker` (Singleton)
- `NodeHealthMonitorService` (HostedService)

---

## 测试覆盖 (Test Coverage)

### 单元测试 (10个测试，全部通过)
- `NodeHealthRegistryTests`
  - ✓ UpdateNodeHealth_AddsNewNode
  - ✓ UpdateNodeHealth_UpdatesExistingNode
  - ✓ GetNodeHealth_ReturnsNullForNonexistentNode
  - ✓ IsNodeHealthy_ReturnsTrueForNonexistentNode
  - ✓ IsNodeHealthy_ReturnsFalseForUnhealthyNode
  - ✓ GetUnhealthyNodes_ReturnsOnlyUnhealthyNodes
  - ✓ GetDegradationMode_ReturnsNoneWhenAllNodesHealthy
  - ✓ GetDegradationMode_ReturnsNodeDegradedWhenFewNodesUnhealthy
  - ✓ GetDegradationMode_ReturnsLineDegradedWhenManyNodesUnhealthy
  - ✓ NodeHealthChanged_FiresEventOnHealthStatusChange

---

## 验收标准达成 (Acceptance Criteria Met)

✅ **手动让某个节点驱动持续失败**
- 通过 SystemSelfTest 标记节点不健康

✅ **节点被标记不健康，路径规划统一打异常口**
- PathHealthChecker 在 ParcelSortingOrchestrator 中集成
- 检测到不健康节点自动重定向到异常口

✅ **系统状态为 LineDegraded 而不是 Faulted**
- DegradationMode 在 SystemSelfTestReport 中记录
- 通过 /health/line 暴露

✅ **/health/line 能看见降级信息**
- 新增 degradationMode, degradedNodesCount, degradedNodes 字段

✅ **Prometheus 能看到对应指标变化**
- sorting_degraded_nodes_total
- sorting_degraded_mode

---

## 后续改进建议 (Future Improvements)

1. **运行时动态节点注册**: 当前节点ID通过自检时映射，可考虑支持运行时动态添加/移除节点
2. **节点恢复策略**: 实现自动重试机制，定期检查不健康节点是否恢复
3. **分级降级策略**: 根据节点重要性（关键路径 vs 备用路径）制定不同的降级策略
4. **预测性维护**: 基于节点健康历史数据预测即将故障的节点
5. **负载均衡**: 在部分节点不可用时，自动调整剩余节点的负载分配

---

## 文件清单 (Files Modified/Created)

### Core Layer
- ✨ `ZakYip.WheelDiverterSorter.Core/Runtime/Health/NodeHealthStatus.cs`
- ✨ `ZakYip.WheelDiverterSorter.Core/Runtime/Health/DegradationMode.cs`
- ✨ `ZakYip.WheelDiverterSorter.Core/Runtime/Health/INodeHealthRegistry.cs`
- 📝 `ZakYip.WheelDiverterSorter.Core/Runtime/Health/SystemSelfTestReport.cs`
- ✨ `ZakYip.Sorting.Core/Overload/OverloadReason.cs`
- 📝 `ZakYip.Sorting.Core/Overload/OverloadDecision.cs`

### Execution Layer
- ✨ `ZakYip.WheelDiverterSorter.Execution/Health/NodeHealthRegistry.cs`
- ✨ `ZakYip.WheelDiverterSorter.Execution/Health/PathHealthChecker.cs`
- ✨ `ZakYip.WheelDiverterSorter.Execution/Health/NodeHealthMonitorService.cs`
- ✨ `ZakYip.WheelDiverterSorter.Execution/NodeHealthServiceExtensions.cs`
- 📝 `ZakYip.WheelDiverterSorter.Execution/SelfTest/SystemSelfTestCoordinator.cs`

### Host Layer
- 📝 `ZakYip.WheelDiverterSorter.Host/Controllers/HealthController.cs`
- 📝 `ZakYip.WheelDiverterSorter.Host/Services/ParcelSortingOrchestrator.cs`
- 📝 `ZakYip.WheelDiverterSorter.Host/Program.cs`

### Observability Layer
- 📝 `ZakYip.WheelDiverterSorter.Observability/PrometheusMetrics.cs`

### Tests
- ✨ `ZakYip.WheelDiverterSorter.Execution.Tests/Health/NodeHealthRegistryTests.cs`

**图例:** ✨ 新增文件 | 📝 修改文件

---

## 总结 (Conclusion)

本PR成功实现了节点级降级和自愈功能，使系统具备了更强的容错能力。当部分节点故障时，系统能够自动降级运行，保持核心功能可用，避免完全停机。通过完善的可观测性支持，运维人员可以及时发现问题并采取行动。

This PR successfully implements node-level degradation and self-healing, giving the system stronger fault tolerance. When some nodes fail, the system can automatically degrade and continue operating, maintaining core functionality and avoiding complete shutdown. With comprehensive observability support, operations teams can quickly identify issues and take action.
