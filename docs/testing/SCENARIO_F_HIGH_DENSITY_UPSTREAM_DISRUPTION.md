# 场景 F：高密度流量 + 上游连接抖动
# Scenario F: High-Density Traffic with Upstream Connection Disruption

## 场景概述 / Scenario Overview

场景 F 是一个复杂仿真场景，用于测试系统在**高密度包裹流量**和**上游连接间歇性中断**同时存在的情况下的鲁棒性和自动恢复能力。

Scenario F is a complex simulation scenario designed to test system robustness and automatic recovery under **high-density parcel traffic** combined with **intermittent upstream connection disruptions**.

## 场景参数 / Scenario Parameters

| 参数 | 值 | 说明 |
|------|-----|------|
| **包裹频率** | 500-1000 件/分钟 | 高密度流量（间隔 1.2s 到 120ms） |
| **上游连接模式** | 间歇性断开/恢复 | 模拟网络抖动 |
| **断开周期** | 每 30 秒断开 5 秒 | 模拟现实网络不稳定 |
| **重连策略** | 无限重试 + 指数退避 | 最大退避 2 秒（硬编码上限） |
| **摩擦因子范围** | 0.9 - 1.1 | 中等摩擦变化（±10%） |
| **掉包概率** | 5% | 轻微掉包率 |
| **线速** | 1000 mm/s | 标准传送带速度 |
| **异常口** | 999 | 默认异常格口ID |

## 测试目标 / Test Objectives

该场景验证系统在复杂环境下的以下特性：

### 1. 客户端无限重连逻辑正确性 ✅

**验证点**：
- 上游连接断开时，系统自动触发重连
- 使用指数退避策略（200ms → 400ms → 800ms → 1600ms → 2000ms）
- 最大退避时间不超过 2 秒（硬编码上限）
- 无限重试，不会放弃连接

**预期行为**：
- 日志记录连接失败和重试间隔
- 每次重试使用新的退避时间
- 达到 2 秒上限后保持 2 秒退避
- 连接恢复后立即恢复正常工作

### 2. 连接断开期间异常格口路由 🔄

**验证点**：
- 连接不可用时，新包裹自动路由到异常格口（999）
- 不会阻塞系统，不会积压包裹
- 包裹状态正确标记为 `RuleEngineTimeout` 或 `ExceptionPath`

**预期行为**：
- 连接断开期间到达的包裹：
  - 不等待上游响应
  - 立即路由到异常格口
  - 记录原因（连接不可用）

### 3. 连接恢复后正常工作 ✅

**验证点**：
- 上游连接恢复后，系统自动恢复正常分拣流程
- 新包裹正常请求格口分配
- 路径生成和执行正常

**预期行为**：
- 连接恢复后的包裹：
  - 正常通知 RuleEngine
  - 接收格口分配
  - 按路径正确分拣

### 4. 零错分保证 ✅

**验证点**：
- 无论连接状态如何，`SortedToWrongChute` 计数必须为 0
- 所有成功分拣的包裹 `FinalChuteId == TargetChuteId`
- 异常包裹正确路由到异常格口

### 5. 日志去重生效 📝

**验证点**：
- 连接失败日志不会刷屏
- 1 秒时间窗口内重复日志被抑制
- 日志可读性良好

**预期行为**：
- 首次连接失败：记录日志
- 1 秒内重复失败：抑制日志
- 1 秒后再次失败：记录日志

### 6. 高密度流量处理能力 🚀

**验证点**：
- 系统能够稳定处理 500-1000 件/分钟的包裹流量
- 吞吐量不受连接抖动明显影响
- 延迟在可接受范围内（P95 < 180s, P99 < 200s）

## 预期结果 / Expected Results

### 成功率 / Success Rate

| 阶段 | 预期成功率 | 说明 |
|------|-----------|------|
| 连接正常时段 | > 90% | 正常业务流程 |
| 连接断开时段 | ~5% | 大部分路由到异常口（预期） |
| 整体成功率 | > 75% | 取决于断开时长占比 |

### 统计指标 / Statistics

**必须满足**：
- ✅ `SortedToWrongChute` = 0（零错分）
- ✅ `simulation_mis_sort_total` = 0（Prometheus 指标）

**预期范围**：
- `SortedToTargetChute`：70-85%（连接正常时的包裹）
- `RuleEngineTimeout`：10-25%（连接断开时的包裹）
- `Timeout`：0-5%（摩擦或掉包导致）
- `Dropped`：3-7%（5% 掉包率）

## 如何运行 / How to Run

### 方法一：使用启动脚本（推荐）

```bash
# Linux/macOS
./performance-tests/run-scenario-f-high-density-upstream-disruption.sh \
  --parcels=1000 \
  --density=high \
  --duration=300

# Windows PowerShell
.\performance-tests\run-scenario-f-high-density-upstream-disruption.ps1 `
  -Parcels 1000 `
  -Density high `
  -Duration 300
```

**参数说明**：
- `--parcels`: 包裹总数（默认 1000）
- `--density`: 流量密度（low=500/min, medium=750/min, high=1000/min）
- `--duration`: 运行时长（秒，默认 300 = 5 分钟）

### 方法二：手动运行仿真程序

```bash
cd ZakYip.WheelDiverterSorter.Simulation
dotnet run -c Release -- \
  --Simulation:ParcelCount=1000 \
  --Simulation:ParcelInterval=00:00:00.120 \
  --Simulation:UpstreamDisruption:Enabled=true \
  --Simulation:UpstreamDisruption:DisruptionIntervalSeconds=30 \
  --Simulation:UpstreamDisruption:DisruptionDurationSeconds=5 \
  --Simulation:IsEnableRandomFriction=true \
  --Simulation:FrictionModel:MinFactor=0.9 \
  --Simulation:FrictionModel:MaxFactor=1.1 \
  --Simulation:IsEnableRandomDropout=true \
  --Simulation:DropoutModel:DropoutProbabilityPerSegment=0.05 \
  --Simulation:ExceptionChuteId=999 \
  --Simulation:IsPauseAtEnd=false
```

### 方法三：集成测试

```bash
cd ZakYip.WheelDiverterSorter.E2ETests
dotnet test --filter "DisplayName~ScenarioF"
```

## 监控与可观测性 / Monitoring & Observability

### Prometheus 指标 / Prometheus Metrics

仿真过程中会暴露以下指标：

| 指标名称 | 类型 | 说明 |
|---------|------|------|
| `sorting_total_parcels` | Counter | 总处理包裹数 |
| `sorting_success_parcels_total` | Counter | 成功分拣包裹数 |
| `sorting_failed_parcels_total{reason}` | Counter | 失败包裹数（按原因分类） |
| `sorting_mis_sort_total` | Counter | 错分包裹数（必须为0） |
| `upstream_connection_state` | Gauge | 上游连接状态（0=断开，1=连接） |
| `upstream_reconnect_attempts_total` | Counter | 重连尝试次数 |
| `log_deduplication_suppressed_total` | Counter | 日志去重抑制次数 |

### 日志监控 / Log Monitoring

**关键日志模式**：

```
[{LocalTime}] Connection to RuleEngine failed. Will retry in {BackoffMs}ms
[{LocalTime}] Connection to RuleEngine restored
[{LocalTime}] Parcel {ParcelId} routed to exception chute (reason: upstream unavailable)
```

**预期日志频率**：
- 连接失败日志：每次断开记录一次（去重生效）
- 重连成功日志：每次恢复记录一次
- 异常格口路由日志：连接断开期间每个包裹一次

### Grafana 仪表板查询 / Grafana Dashboard Queries

**成功率趋势**：
```promql
rate(sorting_success_parcels_total[1m]) 
/ 
rate(sorting_total_parcels[1m])
```

**重连频率**：
```promql
rate(upstream_reconnect_attempts_total[1m])
```

**异常格口使用率**：
```promql
rate(sorting_failed_parcels_total{reason="RuleEngineTimeout"}[1m])
```

## 与其他场景的对比 / Comparison with Other Scenarios

| 场景 | 包裹频率 | 上游连接 | 摩擦 | 掉包 | 特点 |
|------|----------|---------|------|------|------|
| A (基线) | 120/min | 稳定 | ±5% | 0% | 理想环境，无异常 |
| B (高摩擦) | 120/min | 稳定 | ±30% | 0% | 只有摩擦变化 |
| C (中等摩擦+小掉包) | 120/min | 稳定 | ±10% | 5% | 轻微异常 |
| D (极端压力) | 120/min | 稳定 | ±40% | 20% | 最严苛环境 |
| E (高摩擦有丢失) | 120/min | 稳定 | ±30% | 10% | 现实复杂场景 |
| **F (高密度+抖动)** | **500-1000/min** | **间歇性断开** | **±10%** | **5%** | **高负载+网络不稳定** |

## 应用场景 / Use Cases

场景 F 模拟的是生产环境中常见的复杂情况：

### 1. 高峰时段流量 🚀
- 电商促销活动（双11、618）
- 仓库处理高峰时段
- 需要验证系统在高负载下的稳定性

### 2. 网络不稳定环境 🌐
- 跨机房通信（网络延迟和丢包）
- 无线网络环境（WiFi 信号不稳定）
- 上游系统重启或维护

### 3. 混合异常场景 🔀
- 高流量 + 网络抖动 + 设备摩擦 + 偶发掉包
- 最接近真实生产环境的压力测试
- 验证系统在多重压力下的表现

## 故障排查 / Troubleshooting

### 问题 1：重连失败次数过多

**症状**：`upstream_reconnect_attempts_total` 持续增长

**可能原因**：
- 上游服务真的不可用（非仿真断开）
- 配置的上游地址错误
- 防火墙阻止连接

**排查步骤**：
1. 检查上游服务是否运行：`netstat -an | grep {port}`
2. 检查日志中的连接错误详情
3. 验证配置的上游地址和端口

### 问题 2：成功率异常低

**症状**：整体成功率 < 50%

**可能原因**：
- 断开时间过长（应该是 5 秒/30 秒 ≈ 16.7%）
- 摩擦或掉包导致额外失败
- TTL 配置过短

**排查步骤**：
1. 查看 `sorting_failed_parcels_total{reason}` 按原因分布
2. 检查 TTL 配置是否合理
3. 查看摩擦和掉包参数是否过高

### 问题 3：日志刷屏

**症状**：连接失败日志大量重复

**可能原因**：
- 日志去重未生效
- LogDeduplicator 未正确注入

**排查步骤**：
1. 检查 `ILogDeduplicator` 是否注册到 DI 容器
2. 检查日志代码是否使用 `ShouldLog()` 判断
3. 查看 `log_deduplication_suppressed_total` 指标

## 验收标准 / Acceptance Criteria

✅ **必须满足**：
- `SortedToWrongChuteCount == 0`：无错分
- `simulation_mis_sort_total == 0`：Prometheus 指标验证
- 总包裹数 = 各状态之和（数据一致性）
- 成功分拣的包裹 `FinalChuteId == TargetChuteId`

✅ **预期行为**：
- 整体成功率在 70%-85% 之间
- 连接断开期间包裹路由到异常口
- 连接恢复后系统正常工作
- 日志去重生效，无刷屏
- P95 延迟 < 180s, P99 延迟 < 200s

✅ **性能要求**：
- 系统能够稳定处理 500-1000 件/分钟
- 无内存泄漏（内存增长稳定）
- CPU 使用率合理（< 80%）

## 相关文件 / Related Files

- 场景定义：`ZakYip.WheelDiverterSorter.Simulation/Scenarios/ScenarioDefinitions.cs::CreateScenarioF()`
- 单元测试：`ZakYip.WheelDiverterSorter.E2ETests/SimulationScenariosTests.cs::ScenarioF_*`
- 启动脚本：`performance-tests/run-scenario-f-high-density-upstream-disruption.sh`
- 仿真配置：`ZakYip.WheelDiverterSorter.Simulation/appsettings.Simulation.json`
- 上游连接管理：`ZakYip.WheelDiverterSorter.Communication/Infrastructure/UpstreamConnectionManager.cs`

## 技术实现细节 / Technical Implementation

### 1. 上游连接抖动模拟

使用 `SimulatedUpstreamDisruptionService` 在后台周期性地：
- 断开上游连接（模拟网络中断）
- 等待指定时长（默认 5 秒）
- 恢复上游连接

### 2. 重连逻辑验证

通过 `UpstreamConnectionManager` 实现：
```csharp
// 指数退避策略
currentBackoffMs = Math.Min(
    currentBackoffMs * 2,
    MaxBackoffMs  // 硬编码为 2000ms
);
```

### 3. 日志去重验证

通过 `ILogDeduplicator` 实现：
```csharp
if (_logDeduplicator.ShouldLog(LogLevel.Error, errorMessage, exceptionType))
{
    _logger.LogError(ex, errorMessage);
    _logDeduplicator.RecordLog(LogLevel.Error, errorMessage, exceptionType);
}
```

## 维护建议 / Maintenance Recommendations

- **定期运行**：每次重大变更后运行场景 F 验证系统稳定性
- **调整参数**：根据实际生产环境调整包裹频率、断开周期等参数
- **监控指标**：在生产环境持续监控重连次数、异常格口使用率
- **日志分析**：定期分析日志去重效果，调整时间窗口

---

**场景版本：** v1.0  
**创建日期：** 2025-11-19  
**适用版本：** >= PR-39  
**依赖特性：** PR-37 (基础设施), PR-38 (上游连接管理)
