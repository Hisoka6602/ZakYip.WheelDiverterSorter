# PR-41: 混沌测试指南

## 概述

混沌工程 (Chaos Engineering) 是一种通过主动注入故障来测试系统韧性的方法。本指南介绍如何使用系统的混沌测试功能验证在故障情况下的系统行为。

## 混沌测试能力

### 支持的故障注入点

#### 1. 通讯层 (Communication Layer)
- **延迟注入**: 模拟网络延迟（100-2000ms）
- **异常注入**: 模拟连接失败、超时异常
- **断连注入**: 模拟连接中断

#### 2. 驱动层 (Driver Layer)
- **延迟注入**: 模拟硬件响应延迟（50-1500ms）
- **异常注入**: 模拟驱动器错误、状态异常

#### 3. IO 层 (IO Layer)
- **掉点注入**: 模拟传感器短暂失联
- **抖动注入**: 模拟传感器信号不稳定

## 混沌配置文件

系统提供三个预定义的混沌配置文件：

### Mild (轻度混沌)
**适用场景**: 常规韧性测试

```csharp
Communication:
  ExceptionProbability: 1%
  DelayProbability: 5%
  MinDelayMs: 50
  MaxDelayMs: 500
  DisconnectProbability: 0.5%

Driver:
  ExceptionProbability: 1%
  DelayProbability: 3%
  MinDelayMs: 50
  MaxDelayMs: 300

IO:
  DropoutProbability: 1%
```

### Moderate (中度混沌)
**适用场景**: 压力韧性测试

```csharp
Communication:
  ExceptionProbability: 5%
  DelayProbability: 10%
  MinDelayMs: 100
  MaxDelayMs: 1000
  DisconnectProbability: 2%

Driver:
  ExceptionProbability: 5%
  DelayProbability: 8%
  MinDelayMs: 100
  MaxDelayMs: 800

IO:
  DropoutProbability: 3%
```

### Heavy (重度混沌)
**适用场景**: 韧性极限测试

```csharp
Communication:
  ExceptionProbability: 10%
  DelayProbability: 20%
  MinDelayMs: 200
  MaxDelayMs: 2000
  DisconnectProbability: 5%

Driver:
  ExceptionProbability: 10%
  DelayProbability: 15%
  MinDelayMs: 200
  MaxDelayMs: 1500

IO:
  DropoutProbability: 8%
```

## 运行混沌测试

### 方法 1: 使用预定义场景

```bash
cd src/Simulation/ZakYip.WheelDiverterSorter.Simulation

# 场景 CH-1: 轻度混沌 5 分钟
dotnet run -- --scenario CH-1

# 场景 CH-2: 中度混沌 30 分钟
dotnet run -- --scenario CH-2

# 场景 CH-3: 重度混沌 2 小时
dotnet run -- --scenario CH-3

# 场景 CH-4: 生产级负载 4 小时
dotnet run -- --scenario CH-4

# 场景 CH-5: 极限韧性 30 分钟
dotnet run -- --scenario CH-5
```

### 方法 2: 自定义混沌配置

在 `appsettings.json` 中配置：

```json
{
  "Chaos": {
    "Enabled": true,
    "Communication": {
      "ExceptionProbability": 0.05,
      "DelayProbability": 0.10,
      "MinDelayMs": 100,
      "MaxDelayMs": 1000,
      "DisconnectProbability": 0.02
    },
    "Driver": {
      "ExceptionProbability": 0.05,
      "DelayProbability": 0.08,
      "MinDelayMs": 100,
      "MaxDelayMs": 800
    },
    "Io": {
      "DropoutProbability": 0.03
    },
    "Seed": 42
  }
}
```

### 方法 3: 编程方式启用

```csharp
// 在启动时配置混沌注入
services.AddSingleton<IChaosInjector>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<ChaosInjectionService>>();
    var chaos = new ChaosInjectionService(logger, ChaosProfiles.Moderate);
    return chaos;
});

// 运行时动态控制
var chaosInjector = serviceProvider.GetRequiredService<IChaosInjector>();

// 启用混沌测试
chaosInjector.Enable();

// 更改配置
chaosInjector.Configure(ChaosProfiles.Heavy);

// 禁用混沌测试
chaosInjector.Disable();
```

## 混沌测试场景说明

### CH-1: 轻度混沌短期测试 (5分钟)
**目的**: 验证系统基本韧性

**配置**:
- 轻度混沌注入
- 500 包裹/分钟
- 持续 5 分钟

**验收标准**:
- ✓ 系统无崩溃
- ✓ 所有混沌异常被 SafeExecutor 捕获
- ✓ 错误日志清晰标记 `[CHAOS]`
- ✓ 包裹成功率 > 90%

### CH-2: 中度混沌中期测试 (30分钟)
**目的**: 验证系统持续韧性

**配置**:
- 中度混沌注入
- 800 包裹/分钟
- 持续 30 分钟

**验收标准**:
- ✓ 系统无崩溃
- ✓ 内存无明显泄漏（增长 < 50MB）
- ✓ CPU 使用率稳定
- ✓ 包裹成功率 > 85%

### CH-3: 重度混沌长期测试 (2小时)
**目的**: 验证系统长期稳定性

**配置**:
- 重度混沌注入
- 600 包裹/分钟
- 持续 2 小时
- 包含上游改口

**验收标准**:
- ✓ 系统无崩溃
- ✓ 内存增长 < 100MB
- ✓ 性能指标在可接受范围
- ✓ 包裹成功率 > 75%

### CH-4: 生产级负载压力测试 (4小时)
**目的**: 验证生产环境稳定性

**配置**:
- 轻度混沌注入（模拟真实故障）
- 1000 包裹/分钟
- 持续 4 小时
- 包含传感器故障

**验收标准**:
- ✓ 系统无崩溃
- ✓ 无资源泄漏
- ✓ 性能指标符合基线
- ✓ 包裹成功率 > 95%

### CH-5: 极限韧性测试 (30分钟)
**目的**: 测试系统韧性极限

**配置**:
- 极重度混沌注入
- 500 包裹/分钟
- 持续 30 分钟
- 所有故障类型

**验收标准**:
- ✓ 系统不崩溃（关键要求）
- ✓ 所有异常被妥善处理
- ✓ 系统能够恢复
- ✓ 包裹成功率 > 50%

## 混沌测试日志

混沌测试启用时，所有相关日志会带有 `[CHAOS]` 标记：

```log
[WARNING] ⚠️ CHAOS TESTING MODE ENABLED - System is running with chaos injection for resilience testing

[WARNING] [CHAOS] Injecting communication exception for operation: SendToRuleEngine
[DEBUG] [CHAOS] Injecting communication delay: 345ms
[WARNING] [CHAOS] Injecting driver exception for driver: WheelDiverter-D1
[WARNING] [CHAOS] Injecting IO dropout for sensor: Sensor-Entry
```

## 混沌测试指标

### Prometheus 指标

```promql
# 混沌测试状态
sorter_chaos_testing_active

# 混沌注入事件数
sorter_chaos_injection_total{layer="communication|driver|io", type="delay|exception|disconnect|dropout"}

# 示例查询：过去 5 分钟的混沌注入速率
rate(sorter_chaos_injection_total[5m])
```

### 监控面板

建议在 Grafana 中创建混沌测试监控面板，包括：

1. **混沌状态**: 显示是否启用混沌测试
2. **注入事件**: 按层级和类型分类的注入事件数
3. **系统韧性**: 成功率、异常捕获率
4. **性能影响**: 对比启用/禁用混沌时的性能指标

## 安全注意事项

### ⚠️ 重要警告

1. **生产环境禁用**: 混沌测试仅用于测试环境，严禁在生产环境启用
2. **明确标识**: 混沌测试日志会明确标记 `[CHAOS]`，避免与真实故障混淆
3. **可控性**: 混沌注入可以随时通过 API 或配置禁用
4. **隔离性**: 混沌测试应在隔离的测试环境中进行

### 启用/禁用控制

混沌测试的启用状态通过多个层级控制：

1. **配置文件**: `appsettings.json` 中的 `Chaos.Enabled`
2. **启动参数**: `--enable-chaos` / `--disable-chaos`
3. **运行时 API**: 通过 `IChaosInjector.Enable()` / `Disable()`
4. **环境变量**: `CHAOS_TESTING_ENABLED=true/false`

## 结果分析

### 成功的混沌测试应该满足

1. **无崩溃**: 系统在整个测试期间保持运行
2. **异常处理**: 所有混沌异常都被正确捕获和记录
3. **性能可控**: 虽然有故障，但性能指标在可接受范围
4. **自动恢复**: 系统能够从临时故障中自动恢复
5. **资源稳定**: 无内存泄漏、连接泄漏等资源问题

### 如果混沌测试失败

1. **收集日志**: 保存完整的日志文件
2. **检查异常**: 查看是否有未捕获的异常
3. **分析性能**: 检查性能指标是否超出阈值
4. **重现问题**: 使用相同的 Seed 重现问题
5. **修复问题**: 改进异常处理、添加重试、优化性能

## 最佳实践

1. **渐进式测试**: 从轻度混沌开始，逐步增加强度
2. **定期执行**: 在 CI/CD 流程中定期运行混沌测试
3. **监控指标**: 密切关注性能和韧性指标
4. **保存基线**: 保存混沌测试结果作为基线对比
5. **文档记录**: 记录每次测试的配置和结果

## 故障排查

### 问题: 混沌测试未生效

**检查**:
1. `Chaos.Enabled` 是否为 `true`
2. 日志中是否有 "CHAOS TESTING MODE ENABLED" 警告
3. `IChaosInjector.IsEnabled` 属性值

### 问题: 混沌注入过于频繁

**解决**:
1. 降低概率参数
2. 使用更温和的配置文件（Mild）
3. 调整 Seed 值改变随机序列

### 问题: 系统在混沌测试中崩溃

**调查**:
1. 检查崩溃时的日志
2. 确认是否所有异常处理路径都已覆盖
3. 验证 SafeExecutor 是否正确配置
4. 检查是否有资源泄漏导致的崩溃

## 参考资料

- [Principles of Chaos Engineering](https://principlesofchaos.org/)
- [Performance Baseline Documentation](./PR41_PERFORMANCE_BASELINE.md)
- [System Error Handling Guide](../ERROR_CORRECTION_MECHANISM.md)
