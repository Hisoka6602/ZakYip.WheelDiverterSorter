# 文档索引 (Documentation Index)

本文档提供项目所有文档的完整索引和导航。文档按照功能和用途分类，方便快速查找。

## 📚 文档分类

### 🎯 核心文档（必读）

这些文档包含系统的核心概念和架构设计：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [README.md](README.md) | 项目主文档，包含系统概述、运行流程、完成度和优化规划 | 所有人 |
| [RELATIONSHIP_WITH_RULEENGINE.md](RELATIONSHIP_WITH_RULEENGINE.md) | 与规则引擎的关系和集成方案 | 架构师、开发者 |
| [SYSTEM_SCOPE_CLARIFICATION.md](SYSTEM_SCOPE_CLARIFICATION.md) | 系统范围和边界说明 | 架构师、产品经理 |
| [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) | 路径失败检测和异常纠错机制详细说明 | 开发者、运维人员 |
| [DEFECT_ANALYSIS_REPORT.md](DEFECT_ANALYSIS_REPORT.md) | 系统缺陷分析报告 | 开发者、项目经理 |

### ⚙️ 配置文档

系统配置和部署相关文档：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [SYSTEM_CONFIG_GUIDE.md](SYSTEM_CONFIG_GUIDE.md) | 系统配置管理完整指南 | 运维人员、开发者 |
| [CONFIGURATION_API.md](CONFIGURATION_API.md) | 配置管理API接口文档 | 开发者、集成工程师 |
| [HARDWARE_DRIVER_CONFIG.md](HARDWARE_DRIVER_CONFIG.md) | 硬件驱动配置说明 | 硬件工程师、运维人员 |
| [PROTOCOL_CONFIGURATION_GUIDE.md](PROTOCOL_CONFIGURATION_GUIDE.md) | 通信协议配置指南 | 网络工程师、运维人员 |
| [UPSTREAM_CONNECTION_GUIDE.md](UPSTREAM_CONNECTION_GUIDE.md) | 上游连接配置指南（RuleEngine） | 集成工程师、运维人员 |
| [DYNAMIC_TTL_GUIDE.md](DYNAMIC_TTL_GUIDE.md) | 动态超时时间（TTL）配置指南 | 系统调优人员、运维人员 |

### 🔧 技术文档

深入的技术实现和原理说明：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [COMMUNICATION_INTEGRATION.md](COMMUNICATION_INTEGRATION.md) | 通信层集成文档（TCP/SignalR/MQTT/HTTP） | 开发者 |
| [CONCURRENCY_CONTROL.md](CONCURRENCY_CONTROL.md) | 并发控制机制实现 | 开发者 |
| [DRIVER_SENSOR_SEPARATION.md](DRIVER_SENSOR_SEPARATION.md) | 驱动器和传感器分离架构 | 架构师、开发者 |
| [EMC_DISTRIBUTED_LOCK.md](EMC_DISTRIBUTED_LOCK.md) | EMC硬件资源分布式锁使用指南 | 开发者、运维人员 |
| [API_USAGE_GUIDE.md](API_USAGE_GUIDE.md) | API使用教程和示例 | 开发者、集成工程师 |

### 💻 开发文档

开发人员需要的技术细节和开发指南：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [copilot-instructions.md](copilot-instructions.md) | GitHub Copilot编码规范和仿真运行指南 | 开发者、AI辅助工具 |
| [ZakYip.WheelDiverterSorter.Drivers/README.md](ZakYip.WheelDiverterSorter.Drivers/README.md) | 硬件驱动开发文档 | 驱动开发者 |
| [ZakYip.WheelDiverterSorter.Drivers/Leadshine/README.md](ZakYip.WheelDiverterSorter.Drivers/Leadshine/README.md) | 雷赛控制器驱动说明 | 驱动开发者 |
| [ZakYip.WheelDiverterSorter.Drivers/Leadshine/README_EMC_LOCK.md](ZakYip.WheelDiverterSorter.Drivers/Leadshine/README_EMC_LOCK.md) | 雷赛EMC锁实现说明 | 驱动开发者 |
| [ZakYip.WheelDiverterSorter.Drivers/S7/README.md](ZakYip.WheelDiverterSorter.Drivers/S7/README.md) | 西门子S7驱动说明 | 驱动开发者 |
| [ZakYip.WheelDiverterSorter.Ingress/README.md](ZakYip.WheelDiverterSorter.Ingress/README.md) | 入口管理和传感器模块 | 开发者 |
| [ZakYip.WheelDiverterSorter.Ingress/SENSOR_FACTORY.md](ZakYip.WheelDiverterSorter.Ingress/SENSOR_FACTORY.md) | 传感器工厂实现 | 开发者 |
| [ZakYip.WheelDiverterSorter.Communication/README.md](ZakYip.WheelDiverterSorter.Communication/README.md) | 通信层模块说明 | 开发者 |
| [SENSOR_IMPLEMENTATION_SUMMARY.md](SENSOR_IMPLEMENTATION_SUMMARY.md) | 传感器实现总结 | 开发者 |
| [PERFORMANCE_OPTIMIZATION.md](PERFORMANCE_OPTIMIZATION.md) | 性能优化指南 | 性能工程师、开发者 |

### 🧪 测试和质量保证

测试相关的文档和报告：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [TESTING_STRATEGY.md](TESTING_STRATEGY.md) | 测试策略和项目组织完整指南 🆕 | 测试工程师、开发者 |
| [TESTING.md](TESTING.md) | 测试文档和策略 | 测试工程师、开发者 |
| [TESTING_IMPLEMENTATION_STATUS.md](TESTING_IMPLEMENTATION_STATUS.md) | 测试实施状态报告 | 项目经理、测试工程师 |
| [E2E_TESTING_SUMMARY.md](E2E_TESTING_SUMMARY.md) | 端到端测试总结（包含35个测试场景） | 测试工程师 |
| [PR41_E2E_SIMULATION_SUMMARY.md](PR41_E2E_SIMULATION_SUMMARY.md) | **PR-41: 电柜面板启动→分拣落格端到端仿真环境** 🆕 | 测试工程师、开发者 |
| [OBSERVABILITY_TESTING.md](OBSERVABILITY_TESTING.md) | 可观测性测试 | 测试工程师、运维人员 |
| [API_TESTING_AND_CODECOV_COMPLETION_REPORT.md](API_TESTING_AND_CODECOV_COMPLETION_REPORT.md) | API测试和代码覆盖率完成报告 | 项目经理、测试工程师 |
| [performance-tests/README.md](performance-tests/README.md) | 性能测试文档 | 性能工程师 |

### 📊 可观测性和运维

监控、日志和运维相关文档：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [PROMETHEUS_GUIDE.md](PROMETHEUS_GUIDE.md) | Prometheus指标收集和监控指南 | 运维人员、监控工程师 |
| [PROMETHEUS_IMPLEMENTATION_SUMMARY.md](PROMETHEUS_IMPLEMENTATION_SUMMARY.md) | Prometheus实现总结 | 开发者、运维人员 |
| [ALARM_RULES.md](ALARM_RULES.md) | 告警规则配置 | 运维人员 |

### 📋 项目总结文档

项目各阶段的实现总结和报告：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [IMPLEMENTATION_COMPLETE.md](IMPLEMENTATION_COMPLETE.md) | 实现完成报告 | 项目经理、技术负责人 |
| [IMPLEMENTATION_SUMMARY.md](IMPLEMENTATION_SUMMARY.md) | 项目实现总结 | 项目经理、开发者 |
| [IMPLEMENTATION_SUMMARY_PUSH_MODEL.md](IMPLEMENTATION_SUMMARY_PUSH_MODEL.md) | 推送模型实现总结 | 开发者 |
| [IMPLEMENTATION_SUMMARY_CONCURRENCY.md](IMPLEMENTATION_SUMMARY_CONCURRENCY.md) | 并发控制实现总结 | 开发者 |
| [PERFORMANCE_SUMMARY.md](PERFORMANCE_SUMMARY.md) | 性能优化总结 | 性能工程师、开发者 |
| [REFACTORING_SUMMARY.md](REFACTORING_SUMMARY.md) | 重构总结 | 开发者、架构师 |
| [TASK_COMPLETION_SUMMARY.md](TASK_COMPLETION_SUMMARY.md) | 任务完成总结 | 项目经理 |

### 🚀 CI/CD和DevOps

持续集成、持续部署相关文档：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [CI_CD_SETUP.md](CI_CD_SETUP.md) | CI/CD设置和配置 | DevOps工程师、开发者 |

### 🎮 仿真运行与场景测试 (Simulation & Scenario Testing)

仿真模式相关的文档和指南：

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [LONG_RUN_SIMULATION_IMPLEMENTATION.md](LONG_RUN_SIMULATION_IMPLEMENTATION.md) | 长跑仿真模式实施总结 | 测试工程师、开发者 |
| [HIGH_LOAD_PERFORMANCE_TESTING.md](HIGH_LOAD_PERFORMANCE_TESTING.md) | 高负载性能测试指南（500-1000包裹/分钟） | 性能工程师、测试工程师 |
| [SCENARIO_E_DOCUMENTATION.md](SCENARIO_E_DOCUMENTATION.md) | 场景 E：高摩擦有丢失 | 测试工程师 |
| [SCENARIO_F_HIGH_DENSITY_UPSTREAM_DISRUPTION.md](SCENARIO_F_HIGH_DENSITY_UPSTREAM_DISRUPTION.md) | 场景 F：高密度流量 + 上游连接抖动 🆕 | 测试工程师、运维人员 |
| [SCENARIO_G_MULTI_VENDOR_MIXED.md](SCENARIO_G_MULTI_VENDOR_MIXED.md) | 场景 G：多厂商混合驱动仿真 🆕 | 架构师、开发者 |
| [SCENARIO_H_LONG_RUN_STABILITY.md](SCENARIO_H_LONG_RUN_STABILITY.md) | 场景 H：长时间运行稳定性（增强版）🆕 | 性能工程师、运维人员 |
| [SENSOR_FAULT_SIMULATION_IMPLEMENTATION.md](SENSOR_FAULT_SIMULATION_IMPLEMENTATION.md) | 传感器故障仿真实施总结 | 测试工程师、开发者 |
| [copilot-instructions.md#simulation--仿真运行目标](copilot-instructions.md#10-simulation--仿真运行目标) | 仿真运行目标、实现约束和架构指南 | 开发者、AI辅助工具 |
| [copilot-instructions.md#仿真运行物理约束](copilot-instructions.md#104-仿真运行物理约束) | 仿真运行的物理假设和不变量约束 | 开发者、AI辅助工具 |

### 📝 其他文档

| 文档 | 说明 | 适用人群 |
|------|------|----------|
| [copilot-instructions.md](copilot-instructions.md) | GitHub Copilot编码规范和开发指南 | 开发者 |

## 🗺️ 学习路径推荐

### 新手入门路径

1. 阅读 [README.md](README.md) - 了解系统整体概览
2. 阅读 [SYSTEM_SCOPE_CLARIFICATION.md](SYSTEM_SCOPE_CLARIFICATION.md) - 明确系统范围
3. 阅读 [RELATIONSHIP_WITH_RULEENGINE.md](RELATIONSHIP_WITH_RULEENGINE.md) - 理解系统架构
4. 阅读 [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) - 掌握异常处理机制
5. 阅读 [API_USAGE_GUIDE.md](API_USAGE_GUIDE.md) - 学习API使用

### 开发者路径

1. 完成新手入门路径
2. 阅读 [COMMUNICATION_INTEGRATION.md](COMMUNICATION_INTEGRATION.md) - 理解通信层
3. 阅读 [CONCURRENCY_CONTROL.md](CONCURRENCY_CONTROL.md) - 理解并发控制
4. 阅读 [ZakYip.WheelDiverterSorter.Drivers/README.md](ZakYip.WheelDiverterSorter.Drivers/README.md) - 学习驱动开发
5. 阅读 [TESTING.md](TESTING.md) - 了解测试策略

### 运维人员路径

1. 阅读 [README.md](README.md) - 了解系统整体
2. 阅读 [SYSTEM_CONFIG_GUIDE.md](SYSTEM_CONFIG_GUIDE.md) - 学习系统配置
3. 阅读 [HARDWARE_DRIVER_CONFIG.md](HARDWARE_DRIVER_CONFIG.md) - 配置硬件驱动
4. 阅读 [UPSTREAM_CONNECTION_GUIDE.md](UPSTREAM_CONNECTION_GUIDE.md) - 配置上游连接
5. 阅读 [PROMETHEUS_GUIDE.md](PROMETHEUS_GUIDE.md) - 配置监控告警

### 架构师路径

1. 阅读所有核心文档
2. 阅读所有技术文档
3. 阅读 [DEFECT_ANALYSIS_REPORT.md](DEFECT_ANALYSIS_REPORT.md) - 了解系统缺陷
4. 阅读所有项目总结文档
5. 阅读 [SCENARIO_G_MULTI_VENDOR_MIXED.md](SCENARIO_G_MULTI_VENDOR_MIXED.md) - 理解多厂商架构设计
6. 制定系统优化和演进计划

### 仿真测试路径 🆕

1. 阅读 [README.md](README.md) - 了解系统基本概念
2. 阅读 [LONG_RUN_SIMULATION_IMPLEMENTATION.md](LONG_RUN_SIMULATION_IMPLEMENTATION.md) - 理解仿真框架
3. 阅读 [PR41_E2E_SIMULATION_SUMMARY.md](PR41_E2E_SIMULATION_SUMMARY.md) - 学习端到端仿真测试（从配置到分拣）🆕
4. 阅读 [SCENARIO_E_DOCUMENTATION.md](SCENARIO_E_DOCUMENTATION.md) - 学习基础场景（场景 E）
5. 阅读 [SCENARIO_F_HIGH_DENSITY_UPSTREAM_DISRUPTION.md](SCENARIO_F_HIGH_DENSITY_UPSTREAM_DISRUPTION.md) - 高密度 + 上游抖动测试
6. 阅读 [SCENARIO_G_MULTI_VENDOR_MIXED.md](SCENARIO_G_MULTI_VENDOR_MIXED.md) - 多厂商混合测试
7. 阅读 [SCENARIO_H_LONG_RUN_STABILITY.md](SCENARIO_H_LONG_RUN_STABILITY.md) - 长时间稳定性测试
8. 阅读 [HIGH_LOAD_PERFORMANCE_TESTING.md](HIGH_LOAD_PERFORMANCE_TESTING.md) - 性能测试工具和方法
9. 运行实际仿真场景，分析结果

## 📌 文档更新说明

- **文档版本：** v1.3 🆕
- **最后更新：** 2025-11-20
- **本次更新内容（PR-41）：**
  - 新增 PR41_E2E_SIMULATION_SUMMARY.md - 电柜面板启动→分拣落格端到端仿真环境
  - 新增 PanelStartupToSortingE2ETests - 3个端到端仿真场景
  - 新增 InMemoryLogCollector - 日志级别验证工具
  - 更新 E2E_TESTING_SUMMARY.md - 添加Panel E2E测试信息
  - 所有测试通过：3/3场景，零Error日志
- **上次更新内容（PR-39）：**
  - 新增 TESTING_STRATEGY.md - 完整测试策略和项目组织文档
  - 新增驱动异常处理测试（DriverExceptionHandlingTests）
  - 新增启动仿真测试（StartupSimulationTests）
  - 新增 IO 复杂仿真测试（IoSimulationTests）
  - 新增多包裹管线测试（MultiParcelPipelineTests）
  - 更新测试文档索引
  - 测试覆盖率目标：80%+
- **维护规则：**
  - 每次添加新文档时，必须在此索引中添加对应条目
  - 每次重大功能更新时，应更新相关文档
  - 每月审查一次文档的准确性和完整性
  - 过时文档应标记为"已弃用"并说明替代文档

## 🔍 快速查找

### 按主题查找

- **配置相关**：查看"配置文档"分类
- **开发相关**：查看"技术文档"和"开发文档"分类
- **运维相关**：查看"配置文档"和"可观测性和运维"分类
- **测试相关**：查看"测试和质量保证"分类
- **仿真相关**：查看"仿真运行与场景测试"分类 🆕
- **故障排查**：参考 [PATH_FAILURE_DETECTION_GUIDE.md](PATH_FAILURE_DETECTION_GUIDE.md) 和 [DEFECT_ANALYSIS_REPORT.md](DEFECT_ANALYSIS_REPORT.md)

### 按角色查找

- **项目经理**：核心文档 + 项目总结文档
- **架构师**：核心文档 + 技术文档 + 开发文档 + 场景 G（多厂商架构）
- **开发者**：技术文档 + 开发文档 + 测试文档
- **测试工程师**：测试和质量保证分类 + 仿真场景文档（F/G/H）🆕
- **性能工程师**：高负载测试 + 场景 F（高密度）+ 场景 H（长跑稳定性）🆕
- **运维人员**：配置文档 + 可观测性和运维分类 + 场景 F/H（稳定性监控）🆕
- **硬件工程师**：硬件驱动配置 + 驱动开发文档 + 场景 G（多厂商）🆕

## 📧 反馈和贡献

如果发现文档有误或需要补充，请：
1. 提交Issue说明问题
2. 或直接提交PR修改文档
3. 联系项目维护团队

---

**维护团队：** ZakYip Development Team  
**联系方式：** [GitHub Issues](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/issues)
