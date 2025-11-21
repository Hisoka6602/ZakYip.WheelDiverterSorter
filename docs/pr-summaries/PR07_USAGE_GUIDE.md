# PR-07: 使用示例 - 路径重规划功能集成指南

## 快速开始

### 1. 在 DI 容器中注册服务

在 `Program.cs` 或服务配置文件中添加以下代码：

```csharp
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Observability;

// ... existing services ...

// 注册路径重规划服务
builder.Services.AddSingleton<IPathReroutingService, PathReroutingService>();

// 注册增强的路径失败处理器（支持重规划）
builder.Services.AddSingleton<IPathFailureHandler>(sp =>
{
    var pathGenerator = sp.GetRequiredService<ISwitchingPathGenerator>();
    var logger = sp.GetRequiredService<ILogger<EnhancedPathFailureHandler>>();
    var reroutingService = sp.GetService<IPathReroutingService>(); // 可选
    var metrics = sp.GetService<PrometheusMetrics>();               // 可选
    
    return new EnhancedPathFailureHandler(
        pathGenerator,
        logger,
        reroutingService,
        metrics
    );
});
```

### 2. 在 ParcelSortingOrchestrator 中使用

现有的 `ParcelSortingOrchestrator` 已经支持 `IPathFailureHandler`，无需修改：

```csharp
// 这段代码已经存在于 ParcelSortingOrchestrator.cs
var orchestrator = new ParcelSortingOrchestrator(
    parcelDetectionService,
    ruleEngineClient,
    pathGenerator,
    pathExecutor,
    options,
    systemConfigRepository,
    logger,
    pathFailureHandler,  // 自动使用 EnhancedPathFailureHandler
    stateService
);
```

### 3. 订阅重规划事件（可选）

如果需要监控重规划过程，可以订阅事件：

```csharp
if (pathFailureHandler is EnhancedPathFailureHandler enhancedHandler)
{
    // 订阅重规划成功事件
    enhancedHandler.ReroutingSucceeded += (sender, args) =>
    {
        Console.WriteLine($"✅ 包裹 {args.ParcelId} 重规划成功！");
        Console.WriteLine($"   失败节点: {args.FailedNodeId}");
        Console.WriteLine($"   新路径段数: {args.NewPath.Segments.Count}");
    };
    
    // 订阅重规划失败事件
    enhancedHandler.ReroutingFailed += (sender, args) =>
    {
        Console.WriteLine($"❌ 包裹 {args.ParcelId} 重规划失败");
        Console.WriteLine($"   失败节点: {args.FailedNodeId}");
        Console.WriteLine($"   原因: {args.FailureReason}");
    };
}
```

## 配置选项

### 使用原有的失败处理器（不启用重规划）

如果希望保持原有行为，注册原来的 `PathFailureHandler`：

```csharp
builder.Services.AddSingleton<IPathFailureHandler>(sp =>
    new PathFailureHandler(
        sp.GetRequiredService<ISwitchingPathGenerator>(),
        sp.GetRequiredService<ILogger<PathFailureHandler>>()
    )
);
```

### 只记录指标，不实际重规划

```csharp
builder.Services.AddSingleton<IPathFailureHandler>(sp =>
    new EnhancedPathFailureHandler(
        sp.GetRequiredService<ISwitchingPathGenerator>(),
        sp.GetRequiredService<ILogger<EnhancedPathFailureHandler>>(),
        reroutingService: null,  // 不提供重规划服务
        sp.GetRequiredService<PrometheusMetrics>()  // 但仍记录指标
    )
);
```

## 监控和观测

### Prometheus 指标

重规划功能提供了三个新指标：

#### 1. 路径失败总数（按原因分类）

```promql
# 查看各类失败原因的分布
sum by (reason) (sorting_path_failures_total)

# 查看最近 5 分钟的失败速率
rate(sorting_path_failures_total[5m])

# 查看特定失败类型
sorting_path_failures_total{reason="SensorTimeout"}
```

#### 2. 重规划尝试总数

```promql
# 查看重规划尝试次数
sorting_path_reroutes_total

# 查看重规划速率
rate(sorting_path_reroutes_total[5m])
```

#### 3. 重规划成功总数

```promql
# 查看重规划成功次数
sorting_reroute_success_total

# 计算重规划成功率
sorting_reroute_success_total / sorting_path_reroutes_total * 100

# 查看最近 1 小时的成功率
rate(sorting_reroute_success_total[1h]) / rate(sorting_path_reroutes_total[1h]) * 100
```

### Grafana 仪表板示例

创建 Grafana 面板监控重规划效果：

```json
{
  "title": "路径重规划监控",
  "panels": [
    {
      "title": "重规划成功率",
      "targets": [
        {
          "expr": "rate(sorting_reroute_success_total[5m]) / rate(sorting_path_reroutes_total[5m]) * 100"
        }
      ],
      "type": "gauge"
    },
    {
      "title": "失败原因分布",
      "targets": [
        {
          "expr": "sum by (reason) (sorting_path_failures_total)"
        }
      ],
      "type": "pie"
    },
    {
      "title": "重规划趋势",
      "targets": [
        {
          "expr": "rate(sorting_path_reroutes_total[5m])",
          "legendFormat": "尝试"
        },
        {
          "expr": "rate(sorting_reroute_success_total[5m])",
          "legendFormat": "成功"
        }
      ],
      "type": "graph"
    }
  ]
}
```

## 测试和验证

### 单元测试

运行 PathReroutingService 的单元测试：

```bash
cd ZakYip.WheelDiverterSorter.Execution.Tests
dotnet test --filter "FullyQualifiedName~PathReroutingServiceTests"
```

预期输出：
```
Passed!  - Failed:     0, Passed:     7, Skipped:     0, Total:     7
```

### 手动测试场景

#### 场景 1: 中间节点失败，后续节点可以补救

**设置**：
- 目标格口需要节点 [1, 2, 3, 4]
- 节点 2 失败
- 剩余节点 [3, 4] 足以到达目标

**预期结果**：
- ✅ 重规划成功
- ✅ 包裹通过节点 [3, 4] 到达目标格口
- ✅ `sorting_path_reroutes_total` +1
- ✅ `sorting_reroute_success_total` +1

#### 场景 2: 中间节点失败，无法补救

**设置**：
- 目标格口需要节点 [1, 2, 3, 4]
- 节点 3 失败
- 剩余节点 [4] 不足以到达目标（缺少关键节点）

**预期结果**：
- ❌ 重规划失败
- ❌ 包裹进入异常格口
- ✅ `sorting_path_reroutes_total` +1
- ❌ `sorting_reroute_success_total` 不变

#### 场景 3: 物理约束失败

**设置**：
- 失败类型为 `PathFailureReason.PhysicalConstraint`

**预期结果**：
- ⏭️ 跳过重规划
- ❌ 直接进入异常格口
- ❌ `sorting_path_reroutes_total` 不变

## 故障排查

### 重规划总是失败

**可能原因**：
1. 路由配置不完整 - 检查 `IRouteConfigurationRepository`
2. 失败节点位置过晚 - 后续节点不足
3. 拓扑配置问题 - 节点 ID 不匹配

**诊断步骤**：
1. 启用 Debug 日志查看详细信息
2. 检查 Prometheus 指标中的失败原因
3. 查看 `ReroutingFailed` 事件的详细消息

### 指标未显示

**可能原因**：
1. PrometheusMetrics 未注册
2. EnhancedPathFailureHandler 未使用 metrics 参数

**解决方法**：
```csharp
// 确保 PrometheusMetrics 已注册
builder.Services.AddSingleton<PrometheusMetrics>();

// 确保 EnhancedPathFailureHandler 使用 metrics
builder.Services.AddSingleton<IPathFailureHandler>(sp =>
    new EnhancedPathFailureHandler(
        sp.GetRequiredService<ISwitchingPathGenerator>(),
        sp.GetRequiredService<ILogger<EnhancedPathFailureHandler>>(),
        sp.GetRequiredService<IPathReroutingService>(),
        sp.GetRequiredService<PrometheusMetrics>()  // 必须提供
    )
);
```

## 性能考虑

### 内存使用

- 每次重规划创建新的 `SwitchingPath` 对象
- 路径段列表是只读的，避免不必要的复制
- 事件参数使用 record class，减少内存开销

### CPU 使用

- 重规划算法复杂度: O(n*m)，其中 n 是剩余段数，m 是所需摆轮数
- 对于典型场景（<10个节点），性能影响可忽略
- 失败原因解析使用字典查找，O(1) 复杂度

### 网络/IO

- 重规划过程不涉及网络调用
- 只查询内存中的路由配置
- Prometheus 指标更新是异步的，不阻塞主流程

## 最佳实践

1. **总是提供 PrometheusMetrics**：即使不使用重规划，也应记录失败指标
2. **监控重规划成功率**：如果成功率过低，考虑优化路由配置
3. **定期审查失败原因**：根据失败分布优化系统配置
4. **订阅重规划事件**：用于实时监控和告警
5. **在测试环境验证**：先在模拟环境测试重规划效果

## 相关文档

- [PR07_IMPLEMENTATION_SUMMARY.md](PR07_IMPLEMENTATION_SUMMARY.md) - 完整实现总结
- [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) - 原有失败检测机制
- [PROMETHEUS_GUIDE.md](PROMETHEUS_GUIDE.md) - Prometheus 指标指南
