# 测试文档 (Testing Documentation)

## 概述 (Overview)

本项目包含全面的单元测试和集成测试，用于确保代码质量和系统稳定性。

This project includes comprehensive unit and integration tests to ensure code quality and system stability.

## 测试结构 (Test Structure)

### 单元测试项目 (Unit Test Projects)

- **ZakYip.WheelDiverterSorter.Core.Tests**: 核心路径生成逻辑的单元测试
  - Tests for core path generation logic
  - Target coverage: >80%
  
- **ZakYip.WheelDiverterSorter.Drivers.Tests**: 硬件驱动程序的单元测试（使用Mock设备）
  - Tests for hardware drivers with mock devices
  - Tests error handling and timeout scenarios

## 运行测试 (Running Tests)

### 运行所有测试 (Run All Tests)

```bash
dotnet test
```

### 运行特定项目的测试 (Run Tests for Specific Project)

```bash
# Core tests
dotnet test ZakYip.WheelDiverterSorter.Core.Tests/ZakYip.WheelDiverterSorter.Core.Tests.csproj

# Driver tests
dotnet test ZakYip.WheelDiverterSorter.Drivers.Tests/ZakYip.WheelDiverterSorter.Drivers.Tests.csproj
```

### 运行测试并生成覆盖率报告 (Run Tests with Coverage)

```bash
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults
```

### 查看详细输出 (Verbose Output)

```bash
dotnet test --verbosity normal
```

## 代码覆盖率 (Code Coverage)

项目使用 coverlet 进行代码覆盖率收集。

The project uses coverlet for code coverage collection.

### 覆盖率目标 (Coverage Targets)

- **核心模块 (Core Module)**: >80%
- **驱动程序模块 (Drivers Module)**: >70%
- **总体 (Overall)**: >75%

### 生成覆盖率报告 (Generate Coverage Report)

```bash
# 安装 ReportGenerator 工具
dotnet tool install -g dotnet-reportgenerator-globaltool

# 运行测试并收集覆盖率
dotnet test --collect:"XPlat Code Coverage" --results-directory ./TestResults

# 生成 HTML 报告
reportgenerator -reports:"./TestResults/**/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

打开 `coveragereport/index.html` 查看详细报告。

Open `coveragereport/index.html` to view the detailed report.

## 测试类别 (Test Categories)

### 1. 路径生成测试 (Path Generation Tests)

测试 `DefaultSwitchingPathGenerator` 类：
- ✅ 有效格口ID生成路径
- ✅ 空或无效输入处理
- ✅ 配置不存在时的处理
- ✅ 段顺序排序
- ✅ TTL默认值设置

### 2. 硬件执行器测试 (Hardware Executor Tests)

测试 `HardwareSwitchingPathExecutor` 类（使用Mock设备）：
- ✅ 成功执行完整路径
- ✅ 设备未找到错误处理
- ✅ 设备失败错误处理
- ✅ 取消操作处理
- ✅ 异常处理
- ✅ 段顺序执行验证
- ✅ 首个失败段停止执行

## CI/CD 自动化测试 (CI/CD Automation)

GitHub Actions 工作流在每次推送和PR时自动运行测试：

GitHub Actions workflow automatically runs tests on every push and PR:

- 构建解决方案 (Build solution)
- 运行所有测试 (Run all tests)
- 生成覆盖率报告 (Generate coverage report)
- 检查覆盖率阈值 (80%) (Check coverage threshold)
- 上传覆盖率报告为构建artifacts (Upload coverage reports as build artifacts)

查看 `.github/workflows/dotnet.yml` 了解详情。

See `.github/workflows/dotnet.yml` for details.

## 添加新测试 (Adding New Tests)

### 单元测试示例 (Unit Test Example)

```csharp
using Xunit;
using Moq;

namespace ZakYip.WheelDiverterSorter.YourModule.Tests;

public class YourClassTests
{
    [Fact]
    public void YourMethod_WithValidInput_ReturnsExpectedResult()
    {
        // Arrange
        var mockDependency = new Mock<IDependency>();
        mockDependency.Setup(d => d.DoSomething()).Returns(true);
        var sut = new YourClass(mockDependency.Object);

        // Act
        var result = sut.YourMethod();

        // Assert
        Assert.True(result);
        mockDependency.Verify(d => d.DoSomething(), Times.Once);
    }
}
```

### 测试命名约定 (Test Naming Convention)

使用 `MethodName_Scenario_ExpectedBehavior` 格式：

Use the format `MethodName_Scenario_ExpectedBehavior`:

- `GeneratePath_WithValidChuteId_ReturnsValidPath`
- `ExecuteAsync_WhenDiverterFails_ReturnsFailure`
- `CreateRoute_WithEmptyChuteId_ReturnsBadRequest`

## 最佳实践 (Best Practices)

1. **每个测试应该独立运行** - Each test should run independently
2. **使用Arrange-Act-Assert模式** - Use the Arrange-Act-Assert pattern
3. **测试一个概念** - Test one concept per test
4. **使用描述性的测试名称** - Use descriptive test names
5. **保持测试简洁** - Keep tests concise
6. **使用Mock对象隔离依赖** - Use mocks to isolate dependencies
7. **测试边界条件** - Test edge cases
8. **保持高覆盖率** - Maintain high coverage (>80%)

## 故障排除 (Troubleshooting)

### 测试失败 (Tests Failing)

1. 确保所有依赖已安装: `dotnet restore`
2. 清理并重新构建: `dotnet clean && dotnet build`
3. 检查测试输出查看详细错误信息

### 覆盖率工具问题 (Coverage Tool Issues)

如果覆盖率收集失败，尝试:
```bash
dotnet tool update -g coverlet.console
```

## 相关资源 (Related Resources)

- [xUnit Documentation](https://xunit.net/)
- [Moq Documentation](https://github.com/moq/moq4)
- [Coverlet Documentation](https://github.com/coverlet-coverage/coverlet)
- [.NET Testing Best Practices](https://docs.microsoft.com/en-us/dotnet/core/testing/unit-testing-best-practices)
