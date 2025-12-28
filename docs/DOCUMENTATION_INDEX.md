# 文档索引 (Documentation Index)

本文档提供项目所有文档的完整索引。

## 核心文档

| 文档 | 说明 |
|------|------|
| [../README.md](../README.md) | 项目主文档 |
| [RepositoryStructure.md](RepositoryStructure.md) | 仓库结构、技术债索引 |
| [TechnicalDebtLog.md](TechnicalDebtLog.md) | 技术债详细日志 |

## 架构与规范

| 文档 | 说明 |
|------|------|
| [ARCHITECTURE_PRINCIPLES.md](ARCHITECTURE_PRINCIPLES.md) | 架构原则 |
| [CODING_GUIDELINES.md](CODING_GUIDELINES.md) | 编码规范 |
| [TOPOLOGY_LINEAR_N_DIVERTERS.md](TOPOLOGY_LINEAR_N_DIVERTERS.md) | N 摆轮线性拓扑模型（PR-TOPO02） |
| [CORE_ROUTING_LOGIC.md](CORE_ROUTING_LOGIC.md) | **核心路由逻辑（包裹路由与位置索引队列机制）** |
| [EARLY_ARRIVAL_HANDLING.md](EARLY_ARRIVAL_HANDLING.md) | **早到包裹处理机制说明（提前触发检测详解）** |
| [../.github/copilot-instructions.md](../.github/copilot-instructions.md) | Copilot 约束说明 |

## 系统能力与技术分析

| 文档 | 说明 |
|------|------|
| [SYSTEM_CAPABILITIES_ANALYSIS.md](SYSTEM_CAPABILITIES_ANALYSIS.md) | **系统能力现状分析**（防抖、镂空包裹识别、队列错误防护） |
| [RACE_CONDITION_ANALYSIS.md](RACE_CONDITION_ANALYSIS.md) | **竞态条件分析**（UpdateAffectedParcelsToStraight 的竞态场景与修复方案） |
| [../POSITION_INTERVAL_PERFORMANCE_ANALYSIS.md](../POSITION_INTERVAL_PERFORMANCE_ANALYSIS.md) | **Position 间隔性能分析**（阻塞点分析与 fire-and-forget 优化） |
| [../PACKET_LOSS_DETECTION_ISSUE.md](../PACKET_LOSS_DETECTION_ISSUE.md) | **包裹丢失检测问题排查**（配置热更新验证、故障排查步骤） |
| [../CONFIGURATION_HOT_RELOAD_MECHANISM.md](../CONFIGURATION_HOT_RELOAD_MECHANISM.md) | **配置热更新机制**（缓存刷新机制、配置立即生效保证） |
| [../CONFIGURATION_CACHE_AUDIT.md](../CONFIGURATION_CACHE_AUDIT.md) | **配置缓存一致性审计**（7个配置服务审计报告、缓存刷新覆盖率） |

## 使用指南

| 文档 | 说明 |
|------|------|
| [guides/API_USAGE_GUIDE.md](guides/API_USAGE_GUIDE.md) | API 使用指南 |
| [guides/SYSTEM_CONFIG_GUIDE.md](guides/SYSTEM_CONFIG_GUIDE.md) | 系统配置指南 |
| [guides/SENSOR_IO_POLLING_CONFIGURATION.md](guides/SENSOR_IO_POLLING_CONFIGURATION.md) | **感应IO轮询时间配置指南** |
| [guides/UPSTREAM_CONNECTION_GUIDE.md](guides/UPSTREAM_CONNECTION_GUIDE.md) | **上游连接配置（协议字段/时序/超时规则的唯一权威文档）** |
| [guides/VENDOR_EXTENSION_GUIDE.md](guides/VENDOR_EXTENSION_GUIDE.md) | 厂商扩展开发 |
| [guides/POSITION_INTERVAL_CALCULATION.md](guides/POSITION_INTERVAL_CALCULATION.md) | **Position 间隔时间计算详解（从 Position 2 到 Position 3 的时间计算原理）** |
| [PRODUCTION_SERVICE_STARTUP.md](PRODUCTION_SERVICE_STARTUP.md) | **生产环境服务启动说明（服务启动流程、配置加载、日志验证、故障排查）** |
| [../SELF_CONTAINED_DEPLOYMENT.md](../SELF_CONTAINED_DEPLOYMENT.md) | **自包含部署指南（无需安装 .NET Runtime）** |
| [SELF_CONTAINED_DEPLOYMENT_SUMMARY.md](SELF_CONTAINED_DEPLOYMENT_SUMMARY.md) | 自包含部署实施总结 |

## 上游协议相关文档

> **单一权威说明**：所有上游协议字段定义、示例 JSON、时序说明、超时/丢失规则只在 `guides/UPSTREAM_CONNECTION_GUIDE.md` 中维护，其他文档只做高层引用。

| 文档 | 说明 |
|------|------|
| [guides/UPSTREAM_CONNECTION_GUIDE.md](guides/UPSTREAM_CONNECTION_GUIDE.md) | **权威文档**：字段表、示例 JSON、超时规则、配置说明 |
| [UPSTREAM_SEQUENCE_FIREFORGET.md](UPSTREAM_SEQUENCE_FIREFORGET.md) | 时序图详解（引用权威文档） |

## 项目级文档

| 文档 | 说明 |
|------|------|
| [../monitoring/README.md](../monitoring/README.md) | 监控栈部署 |
| [../performance-tests/README.md](../performance-tests/README.md) | 性能测试 |
| [../src/Drivers/ZakYip.WheelDiverterSorter.Drivers/README.md](../src/Drivers/ZakYip.WheelDiverterSorter.Drivers/README.md) | 驱动开发 |
| [../src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/README.md](../src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/README.md) | 通信层 |
| [../src/Ingress/ZakYip.WheelDiverterSorter.Ingress/README.md](../src/Ingress/ZakYip.WheelDiverterSorter.Ingress/README.md) | 入口层 |
| [../src/Simulation/ZakYip.WheelDiverterSorter.Simulation/README.md](../src/Simulation/ZakYip.WheelDiverterSorter.Simulation/README.md) | 仿真 |

---

**维护团队：** ZakYip Development Team
