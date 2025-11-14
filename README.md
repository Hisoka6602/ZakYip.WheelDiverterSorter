# ZakYip.WheelDiverterSorter

[![.NET Build and Test](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml/badge.svg)](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/actions/workflows/dotnet.yml)
[![codecov](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter/branch/main/graph/badge.svg)](https://codecov.io/gh/Hisoka6602/ZakYip.WheelDiverterSorter)

直线摆轮分拣系统 - 基于方向控制的包裹自动分拣解决方案

## 系统概述

本项目是一个基于直线摆轮（Wheel Diverter）的包裹自动分拣系统。包裹通过传感器检测进入系统，在输送线上单向移动，经过配置的摆轮节点时，根据转向方向分流到目标格口。

### 系统拓扑图

```
      格口B     格口D     格口F
        ↑         ↑         ↑
入口 → 摆轮D1 → 摆轮D2 → 摆轮D3 → 末端(默认异常口)
  ↓     ↓         ↓         ↓
传感器  格口A      格口C     格口E
```

**拓扑说明：**
- **入口传感器**：检测包裹到达，触发系统创建包裹记录
- **摆轮D1/D2/D3**：每个摆轮前有IO传感器，确认包裹到达并触发转向动作
- **格口A-F**：6个分拣格口，分别位于3个摆轮的左右两侧
- **末端异常口**：包裹直行通过所有摆轮后到达，用于收集异常包裹

### 核心特点

- ✅ **方向控制模式**：摆轮使用左/右/直行方向控制，不依赖具体角度
- ✅ **传感器驱动**：入口和每个摆轮前配置IO传感器，实时跟踪包裹位置
- ✅ **动态配置**：通过LiteDB存储配置，支持运行时热更新
- ✅ **多协议通信**：支持TCP/SignalR/MQTT/HTTP与上游规则引擎通信
- ✅ **完整的异常处理机制**：包裹超时、设备异常、上游超时等自动路由到异常格口

## 运行流程与逻辑

### 系统工作流程图

```
┌─────────────────────────────────────────────────────────────────┐
│                     包裹分拣完整流程                              │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  1. 包裹入口      │
                    │  入口传感器检测   │
                    └──────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  创建包裹ID      │
                    │  (基于时间戳)    │
                    └──────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  2. 请求格口分配  │
                    │  通知RuleEngine  │
                    └──────────────────┘
                              │
                ┌─────────────┴─────────────┐
                │                           │
                ▼                           ▼
     ┌────────────────────┐      ┌──────────────────┐
     │  等待格口分配       │      │   连接失败？      │
     │  (10秒超时)        │      │   是 → 异常格口   │
     └────────────────────┘      └──────────────────┘
                │
    ┌───────────┴──────────────┐
    │                          │
    ▼                          ▼
┌─────────┐           ┌────────────────┐
│ 超时？   │           │  收到格口ID     │
│ 是→异常口│           │  (正常流程)     │
└─────────┘           └────────────────┘
                              │
                              ▼
                    ┌──────────────────┐
                    │  3. 路径生成      │
                    │  查询摆轮配置     │
                    └──────────────────┘
                              │
                 ┌────────────┴────────────┐
                 │                         │
                 ▼                         ▼
      ┌────────────────┐        ┌──────────────────┐
      │  路径生成失败？ │        │  路径生成成功     │
      │  是 → 异常格口  │        │  (正常流程)      │
      └────────────────┘        └──────────────────┘
                                          │
                                          ▼
                                ┌──────────────────┐
                                │  4. 路径执行      │
                                │  控制摆轮转向     │
                                └──────────────────┘
                                          │
                       ┌──────────────────┴──────────────────┐
                       │                                     │
                       ▼                                     ▼
            ┌──────────────────┐              ┌──────────────────────┐
            │  执行成功         │              │  执行失败？           │
            │  到达目标格口     │              │  设备异常/超时        │
            └──────────────────┘              │  → 触发纠错机制       │
                       │                      │  → 记录失败           │
                       │                      │  → 路由到异常格口     │
                       │                      └──────────────────────┘
                       │                                     │
                       └─────────────┬───────────────────────┘
                                     ▼
                            ┌──────────────────┐
                            │  5. 完成分拣      │
                            └──────────────────┘
```

### 详细流程说明

#### 1. 包裹入口
- 入口传感器检测包裹物理到达
- 生成唯一包裹ID（基于时间戳）
- 触发格口分配请求

#### 2. 请求格口分配
- 通过TCP/SignalR/MQTT向RuleEngine请求格口号
- **异常处理**：
  - **连接失败** → 包裹自动路由到异常格口
  - **等待超时（默认10秒）** → 包裹自动路由到异常格口

#### 3. 路径生成
- 根据目标格口ID生成摆轮转向路径
- 查询LiteDB配置获取格口到摆轮的映射关系
- **异常处理**：
  - **格口未配置** → 包裹自动路由到异常格口
  - **路径生成失败** → 包裹自动路由到异常格口

#### 4. 路径执行
- 按顺序执行每个路径段（摆轮转向）
- 每段有TTL（超时时间）限制
- 传感器确认包裹到达每个摆轮位置
- **异常处理**：
  - **摆轮控制失败** → 触发纠错机制，路由到异常格口
  - **段执行超时** → 触发纠错机制，路由到异常格口
  - **传感器故障** → 触发告警，记录异常

#### 5. 完成分拣
- 包裹成功到达目标格口
- 记录分拣结果

## 异常纠错机制

系统实现了完整的异常纠错机制，确保在任何异常情况下，包裹都能安全到达异常格口，**不会错分到其他格口**。

### 纠错触发场景

| 异常场景 | 触发时机 | 处理策略 |
|---------|---------|----------|
| **RuleEngine连接失败** | 尝试通知包裹检测时 | 立即路由到异常格口，不等待响应 |
| **格口分配超时** | 等待超过配置时间（默认10秒） | 自动路由到异常格口 |
| **格口未配置** | 路径生成时发现目标格口不存在 | 自动路由到异常格口 |
| **路径生成失败** | 无法生成到目标格口的路径 | 自动路由到异常格口 |
| **摆轮控制失败** | 摆轮硬件故障或通信失败 | 触发PathFailureHandler，记录失败，路由到异常格口 |
| **路径执行超时** | 包裹在TTL时间内未到达下一摆轮 | 触发PathFailureHandler，记录超时，路由到异常格口 |
| **传感器故障** | 传感器健康监控检测到异常 | 记录告警，通知运维人员 |
| **重复触发异常** | 包裹在短时间内多次触发同一传感器 | 自动路由到异常格口 |

### 纠错机制实现

系统通过 `PathFailureHandler` 实现统一的异常处理：

```csharp
// 路径执行失败时的处理逻辑
if (!executionResult.IsSuccess)
{
    // 1. 记录失败详情
    _pathFailureHandler.HandlePathFailure(
        parcelId,
        originalPath,
        executionResult.FailureReason,
        executionResult.FailedSegment);
    
    // 2. 计算备用路径（到异常格口）
    var backupPath = _pathFailureHandler.CalculateBackupPath(originalPath);
    
    // 3. 触发事件通知
    // - SegmentExecutionFailed: 段级别失败
    // - PathExecutionFailed: 路径级别失败
    // - PathSwitched: 路径切换到异常格口
    
    // 4. 记录日志用于追踪和分析
}
```

**关键特性：**
- ✅ **实时检测**：在包裹当前位置检测异常
- ✅ **立即响应**：检测到异常立即触发纠错
- ✅ **防止错分**：所有异常都路由到统一的异常格口，不会错分到其他格口
- ✅ **完整日志**：记录失败原因、位置、时间等详细信息
- ✅ **事件通知**：通过事件机制通知监控系统

## 系统架构

```
ZakYip.WheelDiverterSorter/
├── ZakYip.WheelDiverterSorter.Core/           # 核心业务逻辑
│   ├── 路径生成器 (ISwitchingPathGenerator)
│   ├── 配置管理 (LiteDB Repository)
│   └── 事件定义
├── ZakYip.WheelDiverterSorter.Execution/      # 执行层
│   ├── 路径执行器 (ISwitchingPathExecutor)
│   └── 异常处理 (IPathFailureHandler)
├── ZakYip.WheelDiverterSorter.Drivers/        # 硬件驱动层
│   ├── Leadshine/ (雷赛控制器)
│   └── S7/ (西门子，部分实现)
├── ZakYip.WheelDiverterSorter.Ingress/        # 入口管理
│   ├── 传感器驱动
│   ├── 包裹检测
│   └── 健康监控
├── ZakYip.WheelDiverterSorter.Communication/  # 通信层
│   ├── TCP客户端
│   ├── SignalR客户端
│   ├── MQTT客户端
│   └── HTTP客户端
├── ZakYip.WheelDiverterSorter.Host/           # Web API主机
│   ├── 配置管理API
│   ├── 调试接口
│   └── 编排服务 (ParcelSortingOrchestrator)
└── ZakYip.WheelDiverterSorter.Observability/  # 可观测性
    └── 指标收集
```

## 项目完成度

### 整体完成度：约 92%

| 模块 | 完成度 | 说明 |
|-----|--------|------|
| 核心路径生成 | 100% | 基于格口到摆轮映射的路径生成，支持LiteDB动态配置 |
| 异常纠错机制 | 100% | 完整的异常检测和处理，所有异常路由到异常格口 |
| 配置管理系统 | 100% | LiteDB存储，支持热更新和API管理 |
| 执行器层 | 100% | 模拟执行器和硬件执行器完整实现 |
| 通信层 | 100% | TCP/SignalR/MQTT/HTTP全部实现，支持推送模型 |
| 并发控制 | 100% | 摆轮资源锁、包裹队列管理、限流保护 |
| 硬件驱动层 | 80% | 雷赛控制器完整支持，其他厂商部分实现 |
| 传感器系统 | 85% | 雷赛传感器驱动、健康监控、故障检测 |
| 可观测性 | 50% | 基础日志和指标收集，缺少可视化仪表板 |
| 测试覆盖 | 15% | 基准性能测试，单元测试覆盖率严重不足 |

## 存在的缺陷

### 🔴 高优先级缺陷

1. **测试覆盖率严重不足（14.04%）**
   - 当前仅有基准测试和部分集成测试
   - Communication层、Execution.Concurrency、Observability层无测试
   - 2个集成测试失败未修复
   - **影响**：代码质量无法保证，重构风险高

2. **API安全性缺失**
   - 所有API端点无认证机制
   - 敏感配置端点完全开放
   - 无操作审计日志
   - **影响**：生产环境安全风险高

3. **多厂商硬件支持不足**
   - 仅支持雷赛控制器
   - 缺少西门子、三菱、欧姆龙等主流PLC驱动
   - **影响**：市场竞争力受限

### 🟡 中优先级缺陷

4. **可观测性功能不完整**
   - 缺少Grafana仪表板
   - 无完整的告警系统（钉钉/邮件/短信）
   - 无分布式追踪
   - **影响**：运维效率低，故障响应慢

5. **路径算法功能受限**
   - 不支持基于拓扑图的动态路径搜索
   - 不考虑设备实时状态
   - 无负载均衡
   - **影响**：设备故障时容错能力差

6. **性能验证不充分**
   - 未在高负载场景（500-1000包裹/分钟）下充分测试
   - 无长时间运行稳定性验证
   - **影响**：生产环境性能不确定

## 未完善的逻辑

1. **包裹状态持久化缺失**
   - 当前包裹路径存储在内存中
   - 系统重启后无法恢复中断的分拣流程
   - **建议**：持久化包裹状态到数据库

2. **RuleEngine连接重试机制简单**
   - 连接失败立即路由到异常格口
   - 缺少智能重试和熔断器模式
   - **建议**：实现指数退避重试和熔断保护

3. **传感器自动校准缺失**
   - 传感器参数需要手动配置
   - 无自动学习和优化机制
   - **建议**：实现传感器自适应校准

4. **Web管理界面缺失**
   - 仅有RESTful API端点
   - 配置管理需要使用命令行工具
   - **建议**：开发Web UI管理界面

## 未来优化规划（PR单位）

### 第一阶段：质量保障（2-3周）

#### PR-1: 修复集成测试失败 ⚠️ **P0**
- 修复`RouteConfigControllerTests`的2个失败测试
- 配置JSON枚举序列化选项
- 预计时间：2-4小时

#### PR-2: 核心模块单元测试补充 ⚠️ **P0**
- Communication层测试（目标覆盖率>80%）
- Execution.Concurrency测试
- Observability测试
- 预计时间：1周
- 目标：代码覆盖率从14% → 60%

#### PR-3: API安全性增强 ⚠️ **P1**
- 实现JWT Bearer认证
- 基于角色的访问控制（RBAC）
- 敏感数据加密
- 操作审计日志
- 预计时间：1-2周

### 第二阶段：功能扩展（2-3个月）

#### PR-4: 多厂商硬件支持 ⚠️ **P2**
- 驱动插件化架构设计
- 西门子S7驱动实现
- 三菱FX/Q驱动实现
- 欧姆龙CP/CJ驱动实现
- 预计时间：4-6周

#### PR-5: 可观测性增强 ⚠️ **P2**
- Prometheus Exporter集成
- Grafana监控仪表板
- 告警系统（钉钉/邮件/短信）
- OpenTelemetry分布式追踪
- 预计时间：2-3周

#### PR-6: 智能路径算法 ⚠️ **P3**
- 拓扑图建模
- Dijkstra/A*路径搜索算法
- 设备状态感知
- 负载均衡
- 预计时间：3-4周

### 第三阶段：体验优化（1-2个月）

#### PR-7: 性能验证和优化 ⚠️ **P3**
- 高负载场景测试（500-1000包裹/分钟）
- 压力测试和稳定性测试
- 性能瓶颈分析和优化
- CI/CD集成性能测试
- 预计时间：2-3周

#### PR-8: Web管理界面 ⚠️ **P4**
- 前端项目搭建（Vue 3 + Element Plus）
- 仪表板、配置管理、系统监控页面
- 用户权限管理
- 预计时间：3-4周

#### PR-9: 容错和恢复机制完善 ⚠️ **P4**
- 包裹状态持久化
- 自动重试机制（Polly库）
- 熔断器模式
- 配置备份恢复
- 预计时间：1周

## 快速开始

### 运行项目

```bash
cd ZakYip.WheelDiverterSorter.Host
dotnet run
```

默认监听端口：5000（HTTP）

### 测试分拣功能

```bash
# 测试格口A（D1右转）
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG001", "targetChuteId": "CHUTE_A"}'

# 测试格口E（D1直行→D2直行→D3右转）
curl -X POST http://localhost:5000/api/debug/sort \
  -H "Content-Type: application/json" \
  -d '{"parcelId": "PKG002", "targetChuteId": "CHUTE_E"}'
```

## 文档导航

### 核心文档
- [系统架构和关系说明](RELATIONSHIP_WITH_RULEENGINE.md)
- [系统范围明确说明](SYSTEM_SCOPE_CLARIFICATION.md)
- [异常纠错机制详细说明](PATH_FAILURE_DETECTION_GUIDE.md)
- [缺陷分析报告](DEFECT_ANALYSIS_REPORT.md)

### 配置文档
- [系统配置管理指南](SYSTEM_CONFIG_GUIDE.md)
- [配置管理API文档](CONFIGURATION_API.md)
- [硬件驱动配置](HARDWARE_DRIVER_CONFIG.md)
- [通信协议配置指南](PROTOCOL_CONFIGURATION_GUIDE.md)
- [上游连接配置指南](UPSTREAM_CONNECTION_GUIDE.md)
- [动态TTL配置指南](DYNAMIC_TTL_GUIDE.md)

### 技术文档
- [通信层集成文档](COMMUNICATION_INTEGRATION.md)
- [并发控制机制](CONCURRENCY_CONTROL.md)
- [驱动和传感器分离](DRIVER_SENSOR_SEPARATION.md)
- [EMC分布式锁使用指南](EMC_DISTRIBUTED_LOCK.md)
- [API使用教程](API_USAGE_GUIDE.md)

### 开发文档
- [硬件驱动开发](ZakYip.WheelDiverterSorter.Drivers/README.md)
- [传感器实现总结](SENSOR_IMPLEMENTATION_SUMMARY.md)
- [性能优化总结](PERFORMANCE_OPTIMIZATION.md)

### 测试和质量保证
- [测试文档](TESTING.md)
- [测试实施状态](TESTING_IMPLEMENTATION_STATUS.md)
- [E2E测试总结](E2E_TESTING_SUMMARY.md)
- [可观测性测试](OBSERVABILITY_TESTING.md)
- [API测试和代码覆盖率完成报告](API_TESTING_AND_CODECOV_COMPLETION_REPORT.md)

### 可观测性和运维
- [Prometheus指标指南](PROMETHEUS_GUIDE.md)
- [Prometheus实现总结](PROMETHEUS_IMPLEMENTATION_SUMMARY.md)
- [告警规则](ALARM_RULES.md)

### 项目总结文档
- [实现完成报告](IMPLEMENTATION_COMPLETE.md)
- [实现总结](IMPLEMENTATION_SUMMARY.md)
- [推送模型实现总结](IMPLEMENTATION_SUMMARY_PUSH_MODEL.md)
- [并发控制实现总结](IMPLEMENTATION_SUMMARY_CONCURRENCY.md)
- [性能优化总结](PERFORMANCE_SUMMARY.md)
- [重构总结](REFACTORING_SUMMARY.md)
- [任务完成总结](TASK_COMPLETION_SUMMARY.md)

### CI/CD和DevOps
- [CI/CD设置](CI_CD_SETUP.md)

## 技术栈

- **.NET 8.0**: 核心框架
- **ASP.NET Core Minimal API**: Web API框架
- **LiteDB**: 配置数据存储
- **SignalR/MQTT/TCP**: 通信协议
- **雷赛LTDMC**: 硬件控制器驱动

## 贡献指南

1. Fork本仓库
2. 创建特性分支 (`git checkout -b feature/AmazingFeature`)
3. 提交更改 (`git commit -m 'Add some AmazingFeature'`)
4. 推送到分支 (`git push origin feature/AmazingFeature`)
5. 创建Pull Request

---

**文档版本：** v3.0  
**最后更新：** 2025-11-14  
**维护团队：** ZakYip Development Team
