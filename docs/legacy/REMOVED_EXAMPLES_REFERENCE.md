# 已删除的示例和演示代码参考

本文档记录了在 PR-7 中删除的示例和演示代码的关键思路，供未来参考。

## 1. EMC 分布式锁使用模式

**原文件**: `src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/Leadshine/EmcDistributedLockUsageExample.cs`（已删除）

### 核心思路

EMC 分布式锁用于协调多个实例对同一个 EMC 控制卡的访问，避免冲突。主要场景：

1. **基本互斥访问**:
   ```csharp
   using var resourceLock = new EmcNamedMutexLock(logger, "CardNo_0");
   
   if (await resourceLock.TryAcquireAsync(TimeSpan.FromSeconds(30)))
   {
       // 执行需要独占访问的操作
       await PerformColdReset();
   }
   finally
   {
       resourceLock.Release();
   }
   ```

2. **协调控制器包装**:
   ```csharp
   var coordinatedController = new CoordinatedEmcController(
       logger,
       underlyingEmcController,
       resourceLock
   );
   
   // 调用方法时自动管理锁
   await coordinatedController.ColdResetAsync();
   ```

3. **多实例场景**:
   - 多个应用实例可能同时运行
   - 使用 Windows 命名互斥锁（`Global\ZakYip_EMC_CardNo_{CardNo}`）
   - 超时机制避免无限等待
   - 进程崩溃时系统自动释放锁

### 关键设计决策

- 使用命名互斥锁而非 TCP 锁：简单、可靠、由操作系统管理
- 超时时间设为 30 秒：平衡响应速度与操作时间
- 锁粒度为控制卡级别：确保同一卡的所有操作串行化

---

## 2. 拓扑配置使用模式

**原文件**: `src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Topology/TopologyUsageExample.cs`（已删除）

### 核心思路

演示如何使用 `SorterTopology` 和 `DiverterNode` 描述摆轮分拣系统的拓扑结构。

### 基本用法

```csharp
// 获取拓扑配置
var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

// 访问节点信息
foreach (var node in topology.Nodes)
{
    Console.WriteLine($"节点: {node.NodeName} ({node.NodeId})");
    Console.WriteLine($"支持的动作: {string.Join(", ", node.SupportedActions)}");
    
    // 查看格口映射
    foreach (var (side, chutes) in node.ChuteMapping)
    {
        Console.WriteLine($"{side}: {string.Join(", ", chutes)}");
    }
}
```

### 关键设计决策

- 拓扑配置与路由配置分离：拓扑描述物理结构，路由描述业务规则
- 节点包含格口映射：每个摆轮节点知道它可以分拣到哪些格口
- 支持动态加载：可以从配置文件或数据库加载拓扑

---

## 3. 策略实验框架

**原文件**: `src/Simulation/ZakYip.WheelDiverterSorter.Simulation/Demo/StrategyExperimentDemo.cs`（已删除）

### 核心思路

演示如何使用策略实验框架进行 A/B/N 对比测试，用于比较不同分拣策略的性能。

### 实验配置

```csharp
var config = new StrategyExperimentConfig
{
    Profiles = new List<StrategyProfile>
    {
        new() { Name = "FIFO", Strategy = OverloadStrategy.FirstInFirstOut },
        new() { Name = "Priority", Strategy = OverloadStrategy.PriorityBased },
        new() { Name = "LoadBalance", Strategy = OverloadStrategy.LoadBalancing }
    },
    ParcelCount = 1000,
    ReleaseInterval = TimeSpan.FromMilliseconds(300)
};

var results = await experimentRunner.RunExperimentAsync(config);
```

### 评估指标

- 吞吐量（Throughput）
- 平均延迟（Average Latency）
- 失败率（Failure Rate）
- 负载均衡度（Load Balance Score）

### 关键设计决策

- 使用独立的策略工厂：便于扩展新策略
- 支持并行实验：多个策略同时运行，减少总测试时间
- 自动生成对比报告：Markdown 格式，易于分享

---

## 总结

这些示例代码的核心思路已经融入到系统的设计和实现中：

- **分布式锁**: `CoordinatedEmcController` 已在生产使用
- **拓扑配置**: `DefaultSorterTopologyProvider` 提供标准配置
- **策略实验**: 可通过仿真系统和指标收集进行性能对比

如需详细了解这些功能，请参考：
- 架构文档: `docs/ARCHITECTURE_OVERVIEW.md`
- 通信层文档: `docs/COMMUNICATION_DEVELOPER_COURSE.md`
- 仿真指南: `docs/STRATEGY_EXPERIMENT_GUIDE.md`
