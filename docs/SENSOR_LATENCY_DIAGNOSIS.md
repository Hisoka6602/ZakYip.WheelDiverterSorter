# 传感器延迟诊断工具和分析

## 问题描述

用户反馈："很多时候包裹已经到了感应器位置，但是程序不知道"

这表明存在传感器检测延迟问题。包裹已物理到达传感器位置，但系统未能及时检测到。

## 可能的延迟原因

### 1. 传感器轮询间隔 ⚠️

**问题**：传感器使用轮询方式读取IO状态，默认轮询间隔为10ms

**代码位置**：`src/Ingress/.../Sensors/LeadshineSensor.cs`

```csharp
// 第154行：读取输入位状态
var currentState = await _inputPort.ReadAsync(_inputBit);

// 第211行：轮询间隔
await Task.Delay(_pollingIntervalMs, cancellationToken); // 默认10ms
```

**影响**：
- 最坏情况下，包裹到达传感器后需要等待10ms才能被检测到
- 如果包裹速度为1m/s，10ms的延迟意味着包裹已经移动了10mm
- 这可能导致摆轮动作延迟，影响分拣准确性

**诊断方法**：
```bash
# 查看传感器配置中的轮询间隔
# 检查 SensorVendorConfiguration 表中的 PollingIntervalMs 字段
sqlite3 your_database.db "SELECT SensorId, PollingIntervalMs FROM SensorVendorConfiguration;"
```

**优化建议**：
1. 将轮询间隔从10ms降低到5ms或更低（但不要低于1ms）
2. 考虑使用硬件中断方式代替轮询（如果IO卡支持）

### 2. IO驱动读取延迟 ⚠️

**问题**：`_inputPort.ReadAsync(_inputBit)` 可能存在延迟

**可能原因**：
- 通过网络通信读取IO卡状态（TCP/UDP/Modbus等）
- IO卡本身的响应延迟
- 驱动层的缓存机制

**诊断方法**：
创建性能测试，测量IO读取的实际耗时：

```csharp
var sw = Stopwatch.StartNew();
for (int i = 0; i < 1000; i++)
{
    var state = await _inputPort.ReadAsync(bitIndex);
}
sw.Stop();
var avgReadTimeMs = sw.Elapsed.TotalMilliseconds / 1000;
Console.WriteLine($"平均IO读取时间: {avgReadTimeMs} ms");
```

**优化建议**：
1. 如果使用网络IO，检查网络延迟
2. 考虑批量读取多个输入位，减少通信次数
3. 使用本地IO卡（PCI/PCIe）而非网络IO卡

### 3. 队列处理延迟 ⚠️

**问题**：传感器触发事件后，到队列出队并执行动作之间可能有延迟

**代码位置**：`src/Execution/.../Orchestration/SortingOrchestrator.cs`

```csharp
// 第1304行：窥视队列
var peekedTask = _queueManager!.PeekTask(positionIndex);

// 第1320-1372行：提前触发检测（如果启用）
if (enableEarlyTriggerDetection && ...)
{
    // 不出队，直接返回
    return;
}

// 第1375行：出队
var task = _queueManager!.DequeueTask(positionIndex);
```

**影响**：
- 如果启用了提前触发检测（EnableEarlyTriggerDetection=true），且包裹到达时间早于EarliestDequeueTime，则不会出队
- 这会导致包裹已到达传感器，但系统认为是"干扰信号"而不处理

**诊断方法**：
```bash
# 检查日志中的提前触发警告
grep "提前触发检测" logs/*.log

# 统计提前触发的次数
grep "提前触发检测" logs/*.log | wc -l
```

**优化建议**：
1. 检查 `SystemConfiguration.EnableEarlyTriggerDetection` 是否需要禁用
2. 检查 `ConveyorSegmentConfiguration.TimeToleranceMs` 是否配置合理
3. 如果提前触发频率很高（>5%），说明容差配置有问题

### 4. 状态变化忽略窗口 ⚠️

**问题**：为了处理镂空包裹，传感器设置了"状态变化忽略窗口"

**代码位置**：`src/Ingress/.../Sensors/LeadshineSensor.cs`

```csharp
// 第160-180行：状态变化忽略窗口逻辑
if (_stateChangeIgnoreWindowMs > 0 && _lastRisingEdgeTime.HasValue)
{
    var timeSinceLastRisingEdge = (now - _lastRisingEdgeTime.Value).TotalMilliseconds;
    if (timeSinceLastRisingEdge < _stateChangeIgnoreWindowMs)
    {
        // 忽略状态变化
        _lastState = currentState;
        await Task.Delay(_pollingIntervalMs, cancellationToken);
        continue; // ← 不触发事件！
    }
}
```

**影响**：
- 如果 `StateChangeIgnoreWindowMs` 设置过大（如500ms），包裹到达传感器后的500ms内，后续的状态变化会被忽略
- 这可能导致包裹被检测到，但后续的离开信号被忽略，或者下一个包裹的到达信号被忽略

**诊断方法**：
```bash
# 查看传感器配置中的忽略窗口
sqlite3 your_database.db "SELECT SensorId, StateChangeIgnoreWindowMs FROM SensorVendorConfiguration;"
```

**优化建议**：
1. 如果不需要处理镂空包裹，将 `StateChangeIgnoreWindowMs` 设置为0
2. 如果需要处理镂空包裹，设置为合理值（如50-100ms）

### 5. 事件处理延迟 ⚠️

**问题**：从传感器触发事件到业务逻辑处理之间的延迟

**事件处理链路**：
```
传感器触发 → SensorTriggered事件 
           → ParcelDetectionService.OnSensorEvent 
           → ParcelDetected事件 
           → SortingOrchestrator.OnParcelDetected
           → 出队并执行摆轮动作
```

**可能延迟点**：
1. 事件订阅者太多，依次调用耗时
2. 事件处理中有同步IO操作
3. 事件处理中有数据库查询

**诊断方法**：
在每个环节添加性能日志：

```csharp
var sw = Stopwatch.StartNew();
// 执行业务逻辑
sw.Stop();
_logger.LogDebug("步骤X耗时: {ElapsedMs}ms", sw.Elapsed.TotalMilliseconds);
```

### 6. 系统资源瓶颈 ⚠️

**问题**：CPU、内存、磁盘IO等系统资源不足

**可能原因**：
- 运行在虚拟机或低配置设备上
- 同时运行了其他资源密集型程序
- 频繁的GC（垃圾回收）导致暂停

**诊断方法**：
1. 监控CPU使用率
2. 监控内存使用情况
3. 使用dotMemory检查是否有内存泄漏
4. 检查GC日志

## 诊断工具

### 快速诊断脚本

创建以下脚本来收集诊断信息：

```bash
#!/bin/bash
# sensor_latency_diagnosis.sh

echo "===================================================="
echo "传感器延迟诊断工具"
echo "===================================================="
echo ""

# 1. 检查传感器轮询间隔
echo "【1. 传感器轮询间隔配置】"
sqlite3 your_database.db "SELECT SensorId, PollingIntervalMs FROM SensorVendorConfiguration;"
echo ""

# 2. 检查状态变化忽略窗口
echo "【2. 状态变化忽略窗口配置】"
sqlite3 your_database.db "SELECT SensorId, StateChangeIgnoreWindowMs FROM SensorVendorConfiguration;"
echo ""

# 3. 检查提前触发检测开关
echo "【3. 提前触发检测开关】"
sqlite3 your_database.db "SELECT EnableEarlyTriggerDetection FROM SystemConfiguration;"
echo ""

# 4. 统计日志中的提前触发事件
echo "【4. 提前触发事件统计】"
if [ -d logs ]; then
    echo "提前触发总数: $(grep '提前触发检测' logs/*.log 2>/dev/null | wc -l)"
    echo "最近10条提前触发："
    grep '提前触发检测' logs/*.log 2>/dev/null | tail -10
else
    echo "未找到logs目录"
fi
echo ""

# 5. 统计干扰信号
echo "【5. 干扰信号统计】"
if [ -d logs ]; then
    echo "干扰信号总数: $(grep '干扰信号' logs/*.log 2>/dev/null | wc -l)"
    echo "最近10条干扰信号："
    grep '干扰信号' logs/*.log 2>/dev/null | tail -10
else
    echo "未找到logs目录"
fi
echo ""

# 6. 检查容差配置
echo "【6. 输送段容差配置】"
sqlite3 your_database.db "SELECT SegmentId, LengthMm, SpeedMmps, TimeToleranceMs FROM ConveyorSegmentConfiguration;"
echo ""

echo "===================================================="
echo "诊断完成"
echo "===================================================="
```

### 性能测试代码

添加以下性能测试来测量各个环节的耗时：

```csharp
// 文件位置：tests/ZakYip.WheelDiverterSorter.Benchmarks/SensorLatencyBenchmarks.cs

using BenchmarkDotNet.Attributes;
using System.Diagnostics;

namespace ZakYip.WheelDiverterSorter.Benchmarks;

[MemoryDiagnoser]
public class SensorLatencyBenchmarks
{
    // TODO: 添加实际的测试代码
    // 1. 测试IO读取延迟
    // 2. 测试队列出队延迟
    // 3. 测试事件处理延迟
}
```

## 建议的优化步骤

### 步骤1：收集基线数据

1. 运行诊断脚本，收集当前配置
2. 在日志中添加性能计时：
   ```csharp
   _logger.LogDebug("[性能] 传感器触发: SensorId={SensorId}, Time={Time:HH:mm:ss.fff}", 
       sensorId, _clock.LocalNow);
   _logger.LogDebug("[性能] 队列出队: ParcelId={ParcelId}, Time={Time:HH:mm:ss.fff}", 
       parcelId, _clock.LocalNow);
   _logger.LogDebug("[性能] 摆轮执行: ParcelId={ParcelId}, Time={Time:HH:mm:ss.fff}", 
       parcelId, _clock.LocalNow);
   ```
3. 运行系统，收集一批包裹的性能数据
4. 计算每个环节的平均延迟

### 步骤2：根据数据优化

**如果传感器轮询间隔是瓶颈（占比>50%）**：
- 将 `PollingIntervalMs` 从10ms降低到5ms

**如果IO读取延迟是瓶颈（占比>30%）**：
- 检查IO卡的通信方式（网络 vs 本地）
- 优化驱动层的读取逻辑

**如果提前触发检测误判率高（>5%）**：
- 调整 `TimeToleranceMs` 配置
- 或禁用 `EnableEarlyTriggerDetection`

**如果状态变化忽略窗口影响检测**：
- 降低或禁用 `StateChangeIgnoreWindowMs`

### 步骤3：验证优化效果

1. 应用优化配置
2. 重新运行系统并收集性能数据
3. 对比优化前后的延迟改善
4. 确保没有引入新问题（如包裹错位）

## 预期结果

**理想情况下的延迟分布**：
- 传感器轮询延迟：<5ms
- IO读取延迟：<2ms
- 队列出队延迟：<0.01ms（已验证）
- 事件处理延迟：<1ms
- **总延迟：<10ms**

**如果总延迟超过50ms，说明存在明显问题，需要重点排查。**

## 相关文档

- [QUEUE_PERFORMANCE_REPORT.md](../tests/ZakYip.WheelDiverterSorter.Benchmarks/QUEUE_PERFORMANCE_REPORT.md) - 队列性能分析
- [CORE_ROUTING_LOGIC.md](CORE_ROUTING_LOGIC.md) - 核心路由逻辑
- [SYSTEM_CONFIG_GUIDE.md](guides/SYSTEM_CONFIG_GUIDE.md) - 系统配置指南

---

**文档日期**：2025-12-27
**状态**：待实施诊断
