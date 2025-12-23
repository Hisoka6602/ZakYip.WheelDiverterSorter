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
| [../.github/copilot-instructions.md](../.github/copilot-instructions.md) | Copilot 约束说明 |

## 使用指南

| 文档 | 说明 |
|------|------|
| [guides/API_USAGE_GUIDE.md](guides/API_USAGE_GUIDE.md) | API 使用指南 |
| [guides/SYSTEM_CONFIG_GUIDE.md](guides/SYSTEM_CONFIG_GUIDE.md) | 系统配置指南 |
| [guides/SENSOR_IO_POLLING_CONFIGURATION.md](guides/SENSOR_IO_POLLING_CONFIGURATION.md) | **感应IO轮询时间配置指南** |
| [guides/UPSTREAM_CONNECTION_GUIDE.md](guides/UPSTREAM_CONNECTION_GUIDE.md) | **上游连接配置（协议字段/时序/超时规则的唯一权威文档）** |
| [guides/VENDOR_EXTENSION_GUIDE.md](guides/VENDOR_EXTENSION_GUIDE.md) | 厂商扩展开发 |
| [PRODUCTION_SERVICE_STARTUP.md](PRODUCTION_SERVICE_STARTUP.md) | **生产环境服务启动说明（服务启动流程、配置加载、日志验证、故障排查）** |
| [../SELF_CONTAINED_DEPLOYMENT.md](../SELF_CONTAINED_DEPLOYMENT.md) | **自包含部署指南（无需安装 .NET Runtime）** |
| [SELF_CONTAINED_DEPLOYMENT_SUMMARY.md](SELF_CONTAINED_DEPLOYMENT_SUMMARY.md) | 自包含部署实施总结 |

## 故障排查与问题解答

| 文档 | 说明 |
|------|------|
| [UPSTREAM_NOTIFICATION_TROUBLESHOOTING.md](UPSTREAM_NOTIFICATION_TROUBLESHOOTING.md) | 上游通知故障排查指南（传感器触发vs测试端点对比） |
| [TIMEOUT_HANDLING_MECHANISM.md](TIMEOUT_HANDLING_MECHANISM.md) | **包裹超时处理机制说明（上游无响应时的超时兜底流程、配置参数、日志追踪）** |
| [PATH_EXECUTION_TARGET_CHUTE_ZERO_EXPLANATION.md](PATH_EXECUTION_TARGET_CHUTE_ZERO_EXPLANATION.md) | **"路径执行成功，到达目标格口: 0"日志说明（单段动作执行模式解释、常见疑问解答）** |
| [COMMUNICATION_LOGGING_VERIFICATION.md](COMMUNICATION_LOGGING_VERIFICATION.md) | **通信日志功能验证文档（日志文件说明、配置方法、验证步骤）** |
| [SERVER_MODE_DUAL_INSTANCE_ISSUE.md](SERVER_MODE_DUAL_INSTANCE_ISSUE.md) | **Server模式双实例问题分析与修复** |
| [POSITION_INTERVAL_FIX.md](POSITION_INTERVAL_FIX.md) | Position间隔追踪修复文档 |
| [WINDOWS_SERVICE_DEPLOYMENT.md](WINDOWS_SERVICE_DEPLOYMENT.md) | **Windows Service 部署指南（服务安装、管理、配置、故障排查）** |

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
