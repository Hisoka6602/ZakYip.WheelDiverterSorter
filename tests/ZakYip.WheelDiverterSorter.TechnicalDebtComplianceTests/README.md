# Technical Debt Compliance Tests

## 概述 (Overview)

本测试项目实现了一套自动化的技术债务检测和护栏机制，确保代码库符合项目的技术规范，并在每次 PR 提交前自动检测潜在违规。

This test project implements an automated technical debt detection and guardrail mechanism to ensure the codebase complies with project technical specifications and automatically detects potential violations before each PR submission.

## 测试类别 (Test Categories)

### 1. DateTime 使用规范测试 (DateTime Usage Compliance)

**目的**: 确保整个项目不使用 UTC 时间，所有时间操作必须通过 `ISystemClock.LocalNow`

**检测内容**:
- ❌ 禁止直接使用 `DateTime.Now`
- ❌ 禁止直接使用 `DateTime.UtcNow`
- ❌ 禁止直接使用 `DateTimeOffset.UtcNow`
- ⚠️ 禁止使用 `ISystemClock.UtcNow`（除非在 SystemClock 实现类中）

**当前状态**: ⚠️ **154 个违规** 需要修复

**测试类**: `DateTimeUsageComplianceTests`

### 2. SafeExecution 覆盖率测试 (SafeExecution Coverage)

**目的**: 确保所有 BackgroundService 的 ExecuteAsync 方法都通过 ISafeExecutionService 包裹

**检测内容**:
- 扫描所有继承自 `BackgroundService` 的类
- 验证 `ExecuteAsync` 方法是否使用了 SafeExecution
- 生成覆盖率报告

**当前状态**: ✅ **100% 覆盖** (6/6 服务)

**测试类**: `SafeExecutionCoverageTests`

### 3. 线程安全集合测试 (Thread-Safe Collections)

**目的**: 检测高风险命名空间中的非线程安全集合使用

**检测内容**:
- 扫描 Execution、Communication、Observability、Simulation 层
- 查找 `Dictionary<>`, `List<>`, `HashSet<>`, `Queue<>`, `Stack<>` 等非线程安全集合
- 识别未标记 `[SingleThreadedOnly]` 的字段

**当前状态**: ⚠️ **11 个潜在问题** 需要人工审查

**测试类**: `ThreadSafeCollectionTests`

### 4. 文档一致性测试 (Documentation Consistency)

**目的**: 验证技术债务计划文档与实际代码状态的一致性

**检测内容**:
- 对比文档声称的完成度与实际扫描结果
- 生成全面的修复计划
- 估算修复工作量

**当前状态**: ✅ 持续同步

**测试类**: `DocumentationConsistencyTests`

## 如何使用 (How to Use)

### 本地开发 (Local Development)

**运行所有合规性测试**:
```bash
cd /path/to/ZakYip.WheelDiverterSorter
dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/
```

**只运行 DateTime 检测**:
```bash
dotnet test --filter "FullyQualifiedName~DateTimeUsageComplianceTests"
```

**只运行 SafeExecution 检测**:
```bash
dotnet test --filter "FullyQualifiedName~SafeExecutionCoverageTests"
```

**只运行线程安全检测**:
```bash
dotnet test --filter "FullyQualifiedName~ThreadSafeCollectionTests"
```

### PR 提交前检查 (Pre-PR Checklist)

在提交 PR 之前，**必须**运行合规性测试：

```bash
# 1. 运行合规性测试
dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/

# 2. 检查结果
# - 如果有 Failed 测试 → 必须修复违规后才能提交 PR
# - 如果所有测试 Passed → 可以提交 PR
```

### 查看详细报告 (View Detailed Reports)

测试运行后会在 `/tmp/` 目录生成详细报告：

```bash
# DateTime 违规报告
cat /tmp/datetime_violations_report.md

# BackgroundService 覆盖率报告
cat /tmp/background_service_coverage_report.md

# 线程安全集合报告
cat /tmp/thread_safe_collection_report.md

# 文档一致性报告
cat /tmp/documentation_consistency_report.md

# 全面修复计划
cat /tmp/remediation_plan.md
```

## CI/CD 集成 (CI/CD Integration)

### GitHub Actions 配置

在 `.github/workflows/` 中添加合规性检查步骤：

```yaml
name: Technical Debt Compliance Check

on:
  pull_request:
    branches: [ main, develop ]

jobs:
  compliance-check:
    runs-on: ubuntu-latest
    
    steps:
    - uses: actions/checkout@v3
    
    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '9.0.x'
    
    - name: Restore dependencies
      run: dotnet restore
    
    - name: Build
      run: dotnet build --no-restore
    
    - name: Run Compliance Tests
      run: |
        dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/ \
          --no-build \
          --logger "trx;LogFileName=compliance-results.trx" \
          --logger "console;verbosity=detailed"
    
    - name: Upload Test Results
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: compliance-test-results
        path: '**/compliance-results.trx'
    
    - name: Upload Compliance Reports
      if: always()
      uses: actions/upload-artifact@v3
      with:
        name: compliance-reports
        path: |
          /tmp/datetime_violations_report.md
          /tmp/background_service_coverage_report.md
          /tmp/thread_safe_collection_report.md
          /tmp/remediation_plan.md
```

### 本地 Git Hook (推荐)

在 `.git/hooks/pre-push` 添加自动检查：

```bash
#!/bin/bash

echo "Running technical debt compliance checks..."

dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/ \
  --filter "FullyQualifiedName~DateTimeUsageComplianceTests.ShouldNotUseUtcTimeInBusinessLogic" \
  --logger "console;verbosity=minimal"

if [ $? -ne 0 ]; then
  echo "❌ Compliance check failed! UTC time usage detected."
  echo "Please fix DateTime violations before pushing."
  echo "Run: dotnet test tests/ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/"
  exit 1
fi

echo "✅ Compliance check passed!"
```

## 修复指南 (Remediation Guide)

### DateTime 违规修复

**错误示例**:
```csharp
// ❌ 错误
public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
public DateTime UpdatedAt { get; init; } = DateTime.Now;
```

**正确示例**:
```csharp
// ✅ 正确
public class MyService
{
    private readonly ISystemClock _clock;
    
    public MyService(ISystemClock clock)
    {
        _clock = clock;
    }
    
    public MyRecord CreateRecord()
    {
        return new MyRecord
        {
            Timestamp = _clock.LocalNowOffset,  // 使用 LocalNowOffset
            CreatedAt = _clock.LocalNow,        // 使用 LocalNow
            UpdatedAt = _clock.LocalNow         // 使用 LocalNow
        };
    }
}
```

### SafeExecution 违规修复

**错误示例**:
```csharp
// ❌ 错误
public class MyWorker : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await DoWorkAsync();
            await Task.Delay(1000, stoppingToken);
        }
    }
}
```

**正确示例**:
```csharp
// ✅ 正确
public class MyWorker : BackgroundService
{
    private readonly ISafeExecutionService _safeExecutor;
    
    public MyWorker(ISafeExecutionService safeExecutor)
    {
        _safeExecutor = safeExecutor;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _safeExecutor.ExecuteAsync(
            async () =>
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    await DoWorkAsync();
                    await Task.Delay(1000, stoppingToken);
                }
            },
            operationName: "MyWorkerLoop",
            cancellationToken: stoppingToken
        );
    }
}
```

### 线程安全集合修复

**选项 1: 使用并发集合**
```csharp
// ❌ 错误
private readonly Dictionary<string, int> _counters = new();

// ✅ 正确
private readonly ConcurrentDictionary<string, int> _counters = new();
```

**选项 2: 使用不可变集合**
```csharp
// ❌ 错误
private readonly List<string> _items = new();

// ✅ 正确（只读初始化后不变）
private readonly ImmutableList<string> _items = ImmutableList<string>.Empty;
```

**选项 3: 添加显式锁**
```csharp
// ✅ 正确（需要可变且顺序重要）
private readonly object _lock = new();
private readonly List<string> _items = new();

public void Add(string item)
{
    lock (_lock)
    {
        _items.Add(item);
    }
}
```

**选项 4: 标记为单线程**
```csharp
// ✅ 正确（确认单线程使用）
[SingleThreadedOnly]
private readonly List<string> _items = new();
```

## 测试架构 (Test Architecture)

```
ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests/
├── Utilities/
│   └── CodeScanner.cs          # 代码扫描工具
├── DateTimeUsageComplianceTests.cs      # DateTime 规范测试
├── SafeExecutionCoverageTests.cs        # SafeExecution 覆盖率测试
├── ThreadSafeCollectionTests.cs         # 线程安全集合测试
└── DocumentationConsistencyTests.cs     # 文档一致性测试
```

### CodeScanner 工具

`CodeScanner` 提供以下功能：

- `GetAllSourceFiles(pattern)` - 获取所有源代码文件
- `FindDateTimeViolations(file)` - 查找 DateTime 违规
- `ScanAllDateTimeViolations()` - 扫描所有 DateTime 违规
- `FindBackgroundServices()` - 查找 BackgroundService 实现
- `FindNonThreadSafeCollections()` - 查找非线程安全集合

## 常见问题 (FAQ)

### Q: 为什么测试会失败？
A: 测试失败意味着代码中存在违规。这是**预期行为**，目的是防止不符合规范的代码被合并。

### Q: 我可以跳过这些测试吗？
A: **不可以**。这些测试是代码质量的护栏，必须通过才能合并 PR。

### Q: 如何快速定位我的违规？
A: 查看测试输出或生成的报告文件（在 `/tmp/` 目录）。报告会明确指出文件路径和行号。

### Q: 所有 UTC 时间使用都必须删除吗？
A: **是的**。根据最新规范，整个项目任何地方都不能使用 UTC 时间。所有时间必须使用 `ISystemClock.LocalNow` 或 `ISystemClock.LocalNowOffset`。

### Q: 如果我的集合确实是单线程使用怎么办？
A: 在字段声明前添加 `[SingleThreadedOnly]` 特性标记，测试将会忽略该字段。

## 维护 (Maintenance)

### 更新白名单

如果需要添加白名单类（例如新的 SystemClock 实现），编辑 `Utilities/CodeScanner.cs`:

```csharp
var isWhitelisted = fileContent.Contains("class LocalSystemClock") || 
                   fileContent.Contains("class SystemClock") ||
                   fileContent.Contains("class TestSystemClock") ||
                   fileContent.Contains("class MockSystemClock") ||
                   fileContent.Contains("class YourNewSystemClock");  // 添加新的
```

### 添加新的检测规则

1. 在 `Utilities/CodeScanner.cs` 添加新的扫描方法
2. 创建新的测试类（例如 `MyNewComplianceTests.cs`）
3. 实现检测逻辑和报告生成
4. 更新本 README 文档

## 版本历史 (Version History)

- **v1.0** (2025-11-21)
  - 初始实现
  - DateTime 使用规范检测
  - SafeExecution 覆盖率检测
  - 线程安全集合检测
  - 文档一致性验证

## 许可证 (License)

本测试框架遵循项目主许可证。
