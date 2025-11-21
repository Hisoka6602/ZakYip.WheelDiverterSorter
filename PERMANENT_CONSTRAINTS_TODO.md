# 永久约束规则待办事项 (Permanent Constraint Rules TODO)

本文档记录需要在整个代码库中系统性应用的永久约束规则。

## 约束规则 1: 线程安全集合 (Thread-Safe Collections)

**规则**: 所有的锁、数组、集合都需要使用线程安全的声明（如 ConcurrentDictionary）

**状态**: 🔴 未完成
- 发现 85+ 处使用非线程安全集合（Dictionary, List, HashSet, Queue, Stack）
- 需要评估每个使用场景，判断是否需要线程安全

**建议方案**:
1. 审查每个 `new Dictionary<>`, `new List<>`, `new HashSet<>` 等
2. 如果多线程访问，替换为：
   - `ConcurrentDictionary<>` 替代 `Dictionary<>`
   - `ConcurrentBag<>` 或 `ConcurrentQueue<>` 替代 `List<>`（根据使用模式）
   - 如果是只读集合，使用 `ImmutableList<>`, `ImmutableDictionary<>` 等
3. 如果确定单线程访问，添加注释说明原因

**高优先级文件**（共享状态）:
- src/Observability/ZakYip.WheelDiverterSorter.Observability/AlarmService.cs
- src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/各种缓存和状态管理类

## 约束规则 2: 使用本地时间 (Use Local Time)

**规则**: 时间不能使用 UTC 时间，必须使用本地时间（通过 ISystemClock.LocalNow）

**状态**: 🔴 未完成
- 发现 211+ 处直接使用 `DateTime.UtcNow` 或 `DateTimeOffset.UtcNow`
- ISystemClock 基础设施已存在

**建议方案**:
1. 在所有类的构造函数中注入 `ISystemClock`
2. 将所有 `DateTime.UtcNow` 替换为 `_systemClock.LocalNow`
3. 将所有 `DateTimeOffset.UtcNow` 替换为 `_systemClock.LocalNowOffset`
4. 保留 `ISystemClock.UtcNow` 仅用于与外部系统交互时的时间转换

**高优先级文件**:
- src/Observability/ZakYip.WheelDiverterSorter.Observability/Runtime/RuntimePerformanceCollector.cs (多处)
- src/Observability/ZakYip.WheelDiverterSorter.Observability/AlarmService.cs (多处)
- src/Observability/ZakYip.WheelDiverterSorter.Observability/AlarmEvent.cs
- src/Observability/ZakYip.WheelDiverterSorter.Observability/Tracing/*.cs
- src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/ 下的所有文件
- src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/*.cs (所有配置类)

**注意**: 
- Configuration 类的默认值（如 `DateTime.UtcNow`）需要特别注意
- 可能需要在服务注册时设置正确的时间值

## 约束规则 3: 异常安全隔离 (Exception Safety Isolation)

**规则**: 所有有概率异常的方法都需要使用安全隔离器（ISafeExecutionService），保证程序任何地方异常都只记录，不崩溃

**状态**: 🟡 部分完成
- SafeExecutionService 基础设施已存在
- 需要系统性地应用到所有可能抛异常的方法

**建议方案**:
1. 识别所有可能抛异常的操作：
   - I/O 操作（文件、网络、数据库）
   - 外部系统调用（RuleEngine 通信）
   - 硬件驱动器操作
   - 反序列化操作
   - 任何第三方库调用
2. 使用 ISafeExecutionService 包装这些操作
3. 对于 ASP.NET Core Controller，考虑：
   - 使用 global exception filter 或 middleware
   - 或者在每个 Action 中使用 SafeExecutionService

**高优先级区域**:
- 所有 Controller 的 Action 方法
- 所有 BackgroundService/HostedService 的后台任务
- 所有硬件驱动器调用
- 所有网络通信代码
- 所有文件I/O操作

**示例用法**:
```csharp
// Before
public async Task DoSomethingAsync()
{
    await riskyOperation();
}

// After
public async Task DoSomethingAsync()
{
    await _safeExecutor.ExecuteAsync(
        async () => await riskyOperation(),
        "DoSomething",
        _cancellationToken);
}
```

## 实施计划

### Phase 1: 基础设施准备（已完成 ✅）
- [x] SafeExecutionService 已实现
- [x] ISystemClock 已实现
- [x] Program.cs 中已注册相关服务

### Phase 2: 高优先级修复（建议下一个 PR）
1. **修复 Configuration 类的时间使用** (快速)
   - 影响：配置持久化和默认值
   - 工作量：中等（需要修改默认值和初始化逻辑）

2. **修复 AlarmService 和 Observability 的时间使用** (快速)
   - 影响：告警时间记录、性能监控时间戳
   - 工作量：中等

3. **为所有 Controller 添加异常处理** (快速)
   - 影响：API 稳定性，避免 500 错误
   - 工作量：大（但可以使用 global filter 简化）

### Phase 3: 系统性修复（后续多个 PR）
1. **审查和修复线程安全问题**
   - 需要逐个评估场景
   - 工作量：大

2. **Communication 层的时间使用修复**
   - 影响：与 RuleEngine 的通信时间戳
   - 工作量：中等

3. **硬件驱动器的异常安全**
   - 影响：硬件故障恢复能力
   - 工作量：大

## 验收标准

- [ ] 零 `DateTime.UtcNow` 或 `DateTimeOffset.UtcNow` 在业务代码中（除了 ISystemClock 实现）
- [ ] 零非线程安全集合在共享状态场景中
- [ ] 所有 I/O、外部调用、硬件操作都使用 SafeExecutionService
- [ ] 所有单元测试通过
- [ ] 集成测试通过
- [ ] CodeQL 扫描无新增告警

## 参考资源

- SafeExecutionService: `src/Observability/.../Utilities/SafeExecutionService.cs`
- ISystemClock: `src/Observability/.../Utilities/ISystemClock.cs`
- 服务注册: `src/Host/.../Program.cs` 中的 `AddInfrastructureServices()`

## 注意事项

1. **向后兼容性**: 时间格式的改变可能影响：
   - 日志文件名（如 `alerts-{DateTime.UtcNow:yyyyMMdd}.log`）
   - 数据库中存储的时间戳
   - API 响应中的时间字段
   - 与外部系统的时间协议

2. **性能影响**: 
   - 使用 SafeExecutionService 会有轻微性能开销
   - 线程安全集合通常比非线程安全版本慢
   - 需要在关键路径上进行性能测试

3. **测试策略**:
   - 为 ISystemClock 创建 mock，用于单元测试
   - 测试异常场景，确保 SafeExecutionService 正常工作
   - 压力测试以验证线程安全性

---

最后更新: 2025-11-21
创建者: GitHub Copilot
状态: 待实施
