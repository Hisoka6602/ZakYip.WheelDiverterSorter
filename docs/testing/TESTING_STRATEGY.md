# 测试策略文档 / Testing Strategy

## 文档版本 / Document Version
- **版本 / Version**: 1.0
- **创建日期 / Created**: 2025-11-20
- **最后更新 / Last Updated**: 2025-11-20

## 概述 / Overview

本文档详细说明了 ZakYip.WheelDiverterSorter 项目的测试策略、测试项目组织结构、测试覆盖率目标以及如何运行和分析测试。

This document details the testing strategy for the ZakYip.WheelDiverterSorter project, test project organization, coverage goals, and how to run and analyze tests.

## 测试项目结构 / Test Project Structure

项目包含 9 个测试项目，按照功能和层次组织：

The project contains 9 test projects, organized by function and layer:

### 1. 核心层测试 / Core Layer Tests

#### **ZakYip.WheelDiverterSorter.Core.Tests**
- **职责 / Responsibility**: 测试核心领域模型、配置类、并发控制基础设施
- **主要测试内容 / Key Test Areas**:
  - 配置验证和序列化
  - DiverterResourceLock 并发控制
  - DiverterResourceLockManager 资源管理
  - 死锁检测和超时机制
- **测试文件数量 / Test Files**: 15+

### 2. 驱动层测试 / Drivers Layer Tests

#### **ZakYip.WheelDiverterSorter.Drivers.Tests**
- **职责 / Responsibility**: 测试硬件驱动接口实现、模拟驱动、厂商特定驱动
- **主要测试内容 / Key Test Areas**:
  - 驱动接口行为（IWheelDiverterDriver, IIoLinkageDriver 等）
  - 驱动异常处理（with SafeExecutor）
  - 雷赛（Leadshine）驱动实现
  - 西门子 S7 驱动实现
  - 模拟驱动（Simulated drivers）
  - IO 映射和端口读写
  - EMC 锁机制
- **新增测试 / New Tests** (PR-39):
  - `DriverExceptionHandlingTests.cs` - 驱动异常处理测试
- **测试文件数量 / Test Files**: 12+

### 3. 执行层测试 / Execution Layer Tests

#### **ZakYip.WheelDiverterSorter.Execution.Tests**
- **职责 / Responsibility**: 测试分拣执行逻辑、并发控制、管线处理、健康检查
- **主要测试内容 / Key Test Areas**:
  - SortingPipeline 管线处理
  - 多包裹并发执行
  - 异常检测（AnomalyDetector）
  - 路径重路由服务
  - 传送带段协调
  - 节点健康注册表
  - 并发队列（PriorityParcelQueue, MonitoredParcelQueue）
  - DiverterResourceLock 高级场景
- **新增测试 / New Tests** (PR-39):
  - `Pipeline/MultiParcelPipelineTests.cs` - 多包裹管线测试
- **测试文件数量 / Test Files**: 15+

### 4. 入口层测试 / Ingress Layer Tests

#### **ZakYip.WheelDiverterSorter.Ingress.Tests**
- **职责 / Responsibility**: 测试传感器管理、包裹检测、IO 处理
- **主要测试内容 / Key Test Areas**:
  - 传感器工厂和配置
  - 包裹检测逻辑
  - IO 输入处理
  - 传感器状态管理
- **新增测试 / New Tests** (PR-39):
  - `IoSimulationTests.cs` - IO 仿真测试（抖动、高负载、配置错误）
- **测试文件数量 / Test Files**: 8+

### 5. 通信层测试 / Communication Layer Tests

#### **ZakYip.WheelDiverterSorter.Communication.Tests**
- **职责 / Responsibility**: 测试上游通信、协议适配器、连接管理
- **主要测试内容 / Key Test Areas**:
  - RuleEngine 客户端（TCP, SignalR, MQTT, HTTP）
  - 连接管理和重连策略
  - 消息序列化和反序列化
  - 协议适配器
- **测试文件数量 / Test Files**: 10+

### 6. 可观测性测试 / Observability Tests

#### **ZakYip.WheelDiverterSorter.Observability.Tests**
- **职责 / Responsibility**: 测试日志、指标、健康检查、安全执行
- **主要测试内容 / Key Test Areas**:
  - SafeExecutionService 异常包裹
  - LogDeduplicator 日志去重
  - SystemClock 时间抽象
  - HealthCheck 服务
  - Prometheus 指标导出
- **测试文件数量 / Test Files**: 8+

### 7. Host 集成测试 / Host Integration Tests

#### **ZakYip.WheelDiverterSorter.Host.IntegrationTests**
- **职责 / Responsibility**: 测试 Host 层服务集成、API 端点、启动流程
- **主要测试内容 / Key Test Areas**:
  - API 控制器集成测试
  - 后台服务（Workers）测试
  - 配置管理 API
  - 健康检查端点
  - 启动和关闭流程
- **新增测试 / New Tests** (PR-39):
  - `StartupSimulationTests.cs` - 启动仿真测试（冷启动、失败重试、健康转换）
- **测试文件数量 / Test Files**: 12+

### 8. 端到端测试 / End-to-End Tests

#### **ZakYip.WheelDiverterSorter.E2ETests**
- **职责 / Responsibility**: 完整系统端到端测试
- **主要测试内容 / Key Test Areas**:
  - 完整分拣流程
  - 多场景仿真
  - 性能和负载测试
  - 异常场景测试
- **测试文件数量 / Test Files**: 8+

### 9. 基准测试 / Benchmarks

#### **ZakYip.WheelDiverterSorter.Benchmarks**
- **职责 / Responsibility**: 性能基准测试
- **主要测试内容 / Key Test Areas**:
  - 并发性能基准
  - 内存分配基准
  - 吞吐量测试
- **测试文件数量 / Test Files**: 6+

## 测试覆盖率目标 / Coverage Goals

### 整体目标 / Overall Goals
- **当前基线 / Current Baseline**: ~70% (PR-38 后)
- **PR-39 目标 / PR-39 Target**: ≥ 80%
- **长期目标 / Long-term Goal**: 85%+

### 按层覆盖率目标 / Coverage Goals by Layer

| 层 / Layer | 目标 / Target | 优先级 / Priority |
|-----------|--------------|------------------|
| Core | 85%+ | High |
| Drivers | 80%+ | High |
| Execution | 85%+ | High |
| Ingress | 80%+ | Medium |
| Communication | 75%+ | Medium |
| Observability | 80%+ | High |
| Host | 70%+ | Medium |
| Simulation | 60%+ | Low |

## 测试类型和策略 / Test Types and Strategies

### 1. 单元测试 / Unit Tests
- **目的 / Purpose**: 测试单个类或方法的行为
- **特点 / Characteristics**: 
  - 快速执行
  - 隔离依赖（使用 Moq）
  - 确定性结果
- **示例 / Examples**:
  - `SimulatedIoLinkageDriverTests`
  - `DiverterResourceLockTests`
  - `AnomalyDetectorTests`

### 2. 集成测试 / Integration Tests
- **目的 / Purpose**: 测试多个组件协作
- **特点 / Characteristics**:
  - 真实依赖或测试替身
  - 较慢执行
  - 验证组件交互
- **示例 / Examples**:
  - `StartupSimulationTests`
  - `AlertFlowIntegrationTests`
  - Host API 集成测试

### 3. 行为测试 / Behavior Tests (PR-39 重点)
- **目的 / Purpose**: 验证驱动和执行层在异常情况下的行为
- **特点 / Characteristics**:
  - 测试异常处理路径
  - 验证 SafeExecutor 包裹
  - 确保系统不崩溃
- **新增测试 / New Tests**:
  - `DriverExceptionHandlingTests` - 驱动异常行为
  - `MultiParcelPipelineTests` - 多包裹状态机

### 4. 仿真测试 / Simulation Tests (PR-39 重点)
- **目的 / Purpose**: 模拟复杂场景和边界条件
- **特点 / Characteristics**:
  - 复杂场景模拟（启动、IO 抖动）
  - 高负载和并发测试
  - 长时间运行稳定性
- **新增测试 / New Tests**:
  - `StartupSimulationTests` - 启动过程仿真
  - `IoSimulationTests` - IO 复杂场景仿真

### 5. 端到端测试 / End-to-End Tests
- **目的 / Purpose**: 验证完整业务流程
- **特点 / Characteristics**:
  - 完整系统启动
  - 真实场景模拟
  - 最慢执行
- **示例 / Examples**:
  - E2E 场景测试
  - 性能测试脚本

## 如何运行测试 / How to Run Tests

### 运行所有测试 / Run All Tests

```bash
cd /path/to/ZakYip.WheelDiverterSorter
dotnet test
```

### 运行特定测试项目 / Run Specific Test Project

```bash
# Core tests
dotnet test tests/ZakYip.WheelDiverterSorter.Core.Tests

# Driver tests
dotnet test tests/ZakYip.WheelDiverterSorter.Drivers.Tests

# Execution tests
dotnet test tests/ZakYip.WheelDiverterSorter.Execution.Tests

# Integration tests
dotnet test tests/ZakYip.WheelDiverterSorter.Host.IntegrationTests
```

### 运行特定测试类 / Run Specific Test Class

```bash
dotnet test --filter "FullyQualifiedName~DriverExceptionHandlingTests"
dotnet test --filter "FullyQualifiedName~StartupSimulationTests"
dotnet test --filter "FullyQualifiedName~IoSimulationTests"
```

### 运行特定测试方法 / Run Specific Test Method

```bash
dotnet test --filter "FullyQualifiedName~WheelDiverterDriver_ThrowsException_SafeExecutorCatchesIt"
```

## 代码覆盖率 / Code Coverage

### 生成覆盖率报告 / Generate Coverage Report

```bash
# 使用 coverlet 收集覆盖率
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# 查看覆盖率文件
ls -la TestResults/*/coverage.cobertura.xml
```

### 使用 ReportGenerator 生成 HTML 报告 / Generate HTML Report with ReportGenerator

```bash
# 安装 ReportGenerator (如果还没有安装)
dotnet tool install -g dotnet-reportgenerator-globaltool

# 生成 HTML 报告
reportgenerator \
  -reports:"TestResults/*/coverage.cobertura.xml" \
  -targetdir:"TestResults/CoverageReport" \
  -reporttypes:Html

# 查看报告
open TestResults/CoverageReport/index.html  # macOS
xdg-open TestResults/CoverageReport/index.html  # Linux
```

### 查看覆盖率摘要 / View Coverage Summary

```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=lcov /p:CoverletOutput=./coverage/
```

### CI/CD 覆盖率集成 / CI/CD Coverage Integration

项目使用 Codecov 进行覆盖率跟踪和报告。配置文件：`codecov.yml`

The project uses Codecov for coverage tracking and reporting. Configuration file: `codecov.yml`

- **目标覆盖率 / Target Coverage**: 80%
- **Patch 覆盖率 / Patch Coverage**: 60%
- **自动报告 / Automatic Reporting**: 每次 PR 提交

## 测试最佳实践 / Testing Best Practices

### 1. 命名约定 / Naming Conventions

```csharp
// 单元测试命名模式
[Fact]
public void MethodName_StateUnderTest_ExpectedBehavior()

// 示例
[Fact]
public void TurnLeft_DriverConnected_ExecutesSuccessfully()

[Fact]
public void ReadIoPoint_DeviceDisconnected_ThrowsException()
```

### 2. Arrange-Act-Assert 模式 / AAA Pattern

```csharp
[Fact]
public async Task TestMethod()
{
    // Arrange - 准备测试数据和依赖
    var mockDriver = new Mock<IWheelDiverterDriver>();
    mockDriver.Setup(d => d.TurnLeftAsync()).ReturnsAsync(true);

    // Act - 执行被测试的方法
    var result = await mockDriver.Object.TurnLeftAsync();

    // Assert - 验证结果
    Assert.True(result);
}
```

### 3. 使用 Mock 隔离依赖 / Use Mocks to Isolate Dependencies

```csharp
// 好的做法 - 隔离外部依赖
var mockDriver = new Mock<IWheelDiverterDriver>();
mockDriver.Setup(d => d.IsConnected).Returns(true);

// 避免 - 直接使用真实依赖在单元测试中
// var driver = new LeadshineDiverterDriver(...); // 不推荐
```

### 4. 测试异常情况 / Test Exception Scenarios

```csharp
[Fact]
public async Task Operation_WhenError_HandledBySafeExecutor()
{
    // Arrange
    var mockDriver = new Mock<IWheelDiverterDriver>();
    mockDriver.Setup(d => d.TurnLeftAsync())
        .ThrowsAsync(new InvalidOperationException("Error"));

    // Act & Assert
    await Assert.ThrowsAsync<InvalidOperationException>(
        () => mockDriver.Object.TurnLeftAsync()
    );
}
```

### 5. 并发测试 / Concurrency Tests

```csharp
[Fact]
public async Task ConcurrentOperations_ThreadSafe()
{
    // Arrange
    var service = new ThreadSafeService();
    var tasks = new List<Task>();

    // Act - 并发执行
    for (int i = 0; i < 100; i++)
    {
        tasks.Add(Task.Run(() => service.DoWork()));
    }
    
    await Task.WhenAll(tasks);

    // Assert - 验证数据一致性
    Assert.Equal(100, service.CompletedCount);
}
```

## PR-39 新增测试重点 / PR-39 New Test Focus

### 1. Execution/Drivers 行为测试

- **DriverExceptionHandlingTests.cs**
  - 驱动抛异常，SafeExecutor 捕获
  - 多个驱动同时失败
  - EMC 连接丢失处理
  - IO 端口读写失败恢复

### 2. 启动过程仿真

- **StartupSimulationTests.cs**
  - 冷启动场景（设备逐步上线）
  - 启动失败重试（指数退避）
  - 健康检查状态转换（Unhealthy → Healthy）
  - 启动异常日志去重

### 3. IO 复杂仿真

- **IoSimulationTests.cs**
  - 传感器抖动和去抖处理
  - IO 配置错误检测
  - 高负载并发 IO 测试
  - 多摆轮同时 IO 控制无干扰

### 4. 多包裹管线测试

- **MultiParcelPipelineTests.cs**
  - 多包裹并发处理
  - 不同路径同时执行
  - 包裹状态机转换
  - 异常包裹隔离

## 特定场景测试指南 / Specific Scenario Testing Guide

### 启动场景测试 / Startup Scenario Testing

```bash
# 运行启动仿真测试
dotnet test --filter "FullyQualifiedName~StartupSimulationTests"
```

### IO 场景测试 / IO Scenario Testing

```bash
# 运行 IO 仿真测试
dotnet test --filter "FullyQualifiedName~IoSimulationTests"
```

### 高负载场景 / High Load Scenarios

参考现有的仿真场景脚本：
- `performance-tests/run-scenario-f-high-density-upstream-disruption.sh`
- `performance-tests/run-scenario-g-multi-vendor-mixed.sh`
- `performance-tests/run-scenario-h-long-run-stability.sh`

## 测试覆盖率分析 / Coverage Analysis

### 查看未覆盖代码 / View Uncovered Code

使用 ReportGenerator HTML 报告：
1. 打开 `TestResults/CoverageReport/index.html`
2. 点击项目名称查看详细覆盖率
3. 红色标记的行表示未覆盖代码
4. 优先为高风险模块（Execution, Drivers, Host）添加测试

### 识别覆盖率缺口 / Identify Coverage Gaps

重点关注：
- **Execution 层**: 路径执行、并发控制、异常处理
- **Drivers 层**: 驱动接口实现、异常恢复
- **Host 层**: 启动流程、后台服务、API 端点
- **IO 处理**: 传感器读取、去抖、错误恢复

## 持续改进 / Continuous Improvement

### 定期审查 / Regular Reviews

- **每月审查**: 测试覆盖率报告
- **每季度审查**: 测试策略和最佳实践
- **每次 PR**: 新代码必须包含测试

### 测试质量指标 / Test Quality Metrics

- **覆盖率 / Coverage**: ≥ 80%
- **测试通过率 / Pass Rate**: 100%
- **测试执行时间 / Execution Time**: < 5 分钟（单元测试）
- **Flaky 测试率 / Flaky Test Rate**: < 1%

## 故障排查 / Troubleshooting

### 测试失败常见原因 / Common Test Failure Reasons

1. **并发竞态条件 / Race Conditions**: 使用适当的同步机制
2. **Mock 配置错误 / Mock Setup Issues**: 验证 Mock 设置正确
3. **异步超时 / Async Timeouts**: 增加超时时间或优化测试
4. **资源清理 / Resource Cleanup**: 使用 `IDisposable` 和 `using` 语句

### 调试测试 / Debugging Tests

```bash
# 在 VS Code 中调试
# 1. 在测试方法上设置断点
# 2. 右键点击测试方法
# 3. 选择 "Debug Test"

# 使用命令行调试
dotnet test --logger:"console;verbosity=detailed"
```

## 参考资料 / References

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [ReportGenerator](https://github.com/danielpalme/ReportGenerator)

## 更新日志 / Change Log

| 日期 / Date | 版本 / Version | 变更内容 / Changes |
|------------|----------------|-------------------|
| 2025-11-20 | 1.0 | 初始版本，包含 PR-39 新增测试策略 |

---

**维护团队 / Maintained by**: ZakYip Development Team  
**联系方式 / Contact**: [GitHub Issues](https://github.com/Hisoka6602/ZakYip.WheelDiverterSorter/issues)
