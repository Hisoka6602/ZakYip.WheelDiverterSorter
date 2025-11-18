# 策略对比实验报告
# Strategy Comparison Experiment Report

生成时间 / Generated at: 2025-11-18 07:29:35

## 整体对比 / Overall Comparison

| Profile | 描述 / Description | 成功率 / Success | 异常率 / Exception | Overload 次数 / Events | 平均延迟(ms) / Avg Latency | 最大延迟(ms) / Max Latency |
|---------|-------------------|-----------------|-------------------|----------------------|---------------------------|---------------------------|
| Baseline | 基线策略（生产默认配置） | 95.37 % | 4.63 % | 38 | 525.00 | 1304.00 |
| AggressiveOverload | 更激进的超载策略（更低阈值，更早触发异常） | 96.72 % | 3.28 % | 74 | 380.00 | 1421.00 |
| Conservative | 更保守的策略（更高阈值，更少触发异常） | 91.77 % | 8.23 % | 77 | 398.00 | 1556.00 |

## 详细配置 / Detailed Configuration

### Baseline

**描述 / Description**: 基线策略（生产默认配置）

**Overload 策略配置 / Overload Policy Configuration**:

- 启用 / Enabled: True
- 严重拥堵强制异常 / Force Exception on Severe: True
- 超容量强制异常 / Force Exception on Over Capacity: False
- 超时强制异常 / Force Exception on Timeout: True
- 窗口不足强制异常 / Force Exception on Window Miss: False
- 最大在途包裹数 / Max In Flight Parcels: 无限制 / Unlimited
- 最小所需 TTL / Min Required TTL: 500ms
- 最小到达窗口 / Min Arrival Window: 200ms

**统计结果 / Statistics**:

- 总包裹数 / Total Parcels: 500
- 成功落格 / Success Parcels: 476 (95.37 %)
- 异常口 / Exception Parcels: 24 (4.63 %)
- Overload 事件 / Overload Events: 38
- 平均延迟 / Average Latency: 525.00ms
- 最大延迟 / Max Latency: 1304.00ms

**Overload 原因分布 / Overload Reason Distribution**:

- WindowMiss: 10 次
- Timeout: 5 次
- CapacityExceeded: 1 次

### AggressiveOverload

**描述 / Description**: 更激进的超载策略（更低阈值，更早触发异常）

**Overload 策略配置 / Overload Policy Configuration**:

- 启用 / Enabled: True
- 严重拥堵强制异常 / Force Exception on Severe: True
- 超容量强制异常 / Force Exception on Over Capacity: True
- 超时强制异常 / Force Exception on Timeout: True
- 窗口不足强制异常 / Force Exception on Window Miss: True
- 最大在途包裹数 / Max In Flight Parcels: 50
- 最小所需 TTL / Min Required TTL: 800ms
- 最小到达窗口 / Min Arrival Window: 300ms

**统计结果 / Statistics**:

- 总包裹数 / Total Parcels: 500
- 成功落格 / Success Parcels: 483 (96.72 %)
- 异常口 / Exception Parcels: 17 (3.28 %)
- Overload 事件 / Overload Events: 74
- 平均延迟 / Average Latency: 380.00ms
- 最大延迟 / Max Latency: 1421.00ms

**Overload 原因分布 / Overload Reason Distribution**:

- WindowMiss: 21 次
- CapacityExceeded: 18 次
- Timeout: 8 次

### Conservative

**描述 / Description**: 更保守的策略（更高阈值，更少触发异常）

**Overload 策略配置 / Overload Policy Configuration**:

- 启用 / Enabled: True
- 严重拥堵强制异常 / Force Exception on Severe: False
- 超容量强制异常 / Force Exception on Over Capacity: False
- 超时强制异常 / Force Exception on Timeout: False
- 窗口不足强制异常 / Force Exception on Window Miss: False
- 最大在途包裹数 / Max In Flight Parcels: 无限制 / Unlimited
- 最小所需 TTL / Min Required TTL: 300ms
- 最小到达窗口 / Min Arrival Window: 100ms

**统计结果 / Statistics**:

- 总包裹数 / Total Parcels: 500
- 成功落格 / Success Parcels: 458 (91.77 %)
- 异常口 / Exception Parcels: 42 (8.23 %)
- Overload 事件 / Overload Events: 77
- 平均延迟 / Average Latency: 398.00ms
- 最大延迟 / Max Latency: 1556.00ms

**Overload 原因分布 / Overload Reason Distribution**:

- Timeout: 22 次
- CapacityExceeded: 21 次
- WindowMiss: 11 次

