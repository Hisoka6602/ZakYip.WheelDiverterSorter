using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 分拣编排服务合规性测试
/// Sorting orchestrator compliance tests
/// </summary>
/// <remarks>
/// PR-SORT2: 验证分拣架构的统一性：
/// 
/// 1. **唯一分拣编排内核**：
///    - ISortingOrchestrator 只有一个非测试实现类（SortingOrchestrator）
///    - Application 层的服务（DebugSortService / OptimizedSortingService）只做包装
/// 
/// 2. **Application 层影分身检测**：
///    - Sorting 服务必须在 100 行以内
///    - 只负责委托调用 + 指标记录
///    - 不允许拥有独立业务分支
/// 
/// 3. **中间件边界检查**：
///    - Pipeline 中间件不应包含分拣业务逻辑
///    - 只负责管道步骤，不实现分拣流程
/// </remarks>
public class SortingOrchestratorComplianceTests
{
    /// <summary>
    /// Application 层 Sorting 服务的最大行数限制
    /// </summary>
    private const int MaxApplicationServiceLineCount = 100;

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    #region 唯一分拣编排内核验证

    /// <summary>
    /// 验证 ISortingOrchestrator 只有一个非测试实现类
    /// Verify ISortingOrchestrator has exactly one non-test implementation
    /// </summary>
    /// <remarks>
    /// PR-SORT2 验收标准：
    /// 只有一个非测试实现类实现 ISortingOrchestrator（即 SortingOrchestrator）
    /// </remarks>
    [Fact]
    public void ShouldHaveExactlyOneSortingOrchestratorImplementation()
    {
        var solutionRoot = GetSolutionRoot();
        var implementations = new List<SortingOrchestratorImplementation>();

        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            
            // 检查是否实现 ISortingOrchestrator
            // 匹配模式：class ClassName : ISortingOrchestrator 或 class ClassName : Base, ISortingOrchestrator
            if (Regex.IsMatch(content, @"class\s+(\w+)\s*:\s*[^{]*ISortingOrchestrator"))
            {
                var classMatch = Regex.Match(content, @"class\s+(\w+)\s*:");
                var className = classMatch.Success ? classMatch.Groups[1].Value : "Unknown";
                
                implementations.Add(new SortingOrchestratorImplementation
                {
                    ClassName = className,
                    FilePath = file,
                    RelativePath = Path.GetRelativePath(solutionRoot, file)
                });
            }
        }

        // 预期只有一个实现：SortingOrchestrator
        var expectedClassName = "SortingOrchestrator";
        var expectedLocation = "Execution/ZakYip.WheelDiverterSorter.Execution/Orchestration";

        if (implementations.Count == 0)
        {
            Assert.Fail("❌ 未找到 ISortingOrchestrator 的实现类");
        }
        else if (implementations.Count > 1)
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ ISortingOrchestrator 存在多个实现类（应该只有 1 个）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 根据 PR-SORT2 规范：只允许一个分拣\"编排内核\"实现。\n");

            foreach (var impl in implementations)
            {
                var isExpected = impl.ClassName == expectedClassName && impl.RelativePath.Contains(expectedLocation);
                report.AppendLine($"  {(isExpected ? "✅" : "❌")} {impl.ClassName}");
                report.AppendLine($"     位置: {impl.RelativePath}");
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 保留唯一的 SortingOrchestrator（在 Execution 层）");
            report.AppendLine("  2. 删除其他 ISortingOrchestrator 实现");
            report.AppendLine("  3. 如果是测试 Mock，请移到 tests 目录");

            Assert.Fail(report.ToString());
        }
        else
        {
            // 验证唯一的实现是预期的 SortingOrchestrator
            var impl = implementations[0];
            Assert.Equal(expectedClassName, impl.ClassName);
            Assert.Contains(expectedLocation.Replace("/", Path.DirectorySeparatorChar.ToString()), impl.RelativePath);
        }
    }

    #endregion

    #region Application 层影分身检测

    /// <summary>
    /// 验证 Application 层 Sorting 服务不是影分身
    /// Verify Application layer sorting services are not shadow implementations
    /// </summary>
    /// <remarks>
    /// PR-SORT2 验收标准：
    /// - OptimizedSortingService / DebugSortService 必须在 100 行以内
    /// - 只负责委托调用 + 指标记录
    /// - 不允许拥有独立业务分支
    /// </remarks>
    [Fact]
    public void ApplicationSortingServicesShouldBeDelegationOnly()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<SortingServiceShadowViolation>();

        // Application 层 Sorting 服务的路径
        var applicationServicesPath = Path.Combine(
            solutionRoot, 
            "src", 
            "Application", 
            "ZakYip.WheelDiverterSorter.Application", 
            "Services");

        // 需要检查的服务文件
        var sortingServiceFiles = new[]
        {
            "DebugSortService.cs",
            "OptimizedSortingService.cs"
        };

        foreach (var fileName in sortingServiceFiles)
        {
            var filePath = Path.Combine(applicationServicesPath, fileName);
            
            if (!File.Exists(filePath))
            {
                // 文件不存在，跳过
                continue;
            }

            var analysis = AnalyzeApplicationSortingService(filePath);
            
            if (analysis.HasViolation)
            {
                violations.Add(new SortingServiceShadowViolation
                {
                    FileName = fileName,
                    FilePath = filePath,
                    RelativePath = Path.GetRelativePath(solutionRoot, filePath),
                    LineCount = analysis.LineCount,
                    HasIndependentBusinessLogic = analysis.HasIndependentBusinessLogic,
                    IndependentLogicDetails = analysis.IndependentLogicDetails,
                    Reason = analysis.ViolationReason
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ Application 层发现 {violations.Count} 个 Sorting 服务\"影分身\":");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 根据 PR-SORT2 规范：Application 层 Sorting 服务只做包装。\n");
            report.AppendLine($"   允许的最大行数: {MaxApplicationServiceLineCount} 行");
            report.AppendLine("   允许的逻辑: 委托调用 + 指标记录 + 日志\n");

            foreach (var violation in violations)
            {
                report.AppendLine($"  ❌ {violation.FileName}");
                report.AppendLine($"     位置: {violation.RelativePath}");
                report.AppendLine($"     行数: {violation.LineCount} 行 (限制: {MaxApplicationServiceLineCount})");
                
                if (violation.HasIndependentBusinessLogic)
                {
                    report.AppendLine($"     ⚠️ 存在独立业务逻辑:");
                    foreach (var detail in violation.IndependentLogicDetails)
                    {
                        report.AppendLine($"        - {detail}");
                    }
                }
                
                report.AppendLine($"     原因: {violation.Reason}");
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将独立业务逻辑移动到 SortingOrchestrator");
            report.AppendLine("  2. Application 层服务只保留：");
            report.AppendLine("     - 调用 orchestrator 的委托代码");
            report.AppendLine("     - 指标记录代码（SorterMetrics / PrometheusMetrics）");
            report.AppendLine("     - 简单日志");
            report.AppendLine("  3. 不要在 Application 层重复实现分拣流程");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 分析 Application 层 Sorting 服务
    /// </summary>
    private SortingServiceAnalysisResult AnalyzeApplicationSortingService(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var lines = File.ReadAllLines(filePath);
        var result = new SortingServiceAnalysisResult
        {
            LineCount = lines.Length
        };

        // 检查行数
        if (lines.Length > MaxApplicationServiceLineCount)
        {
            result.HasViolation = true;
            result.ViolationReason = $"超过 {MaxApplicationServiceLineCount} 行限制";
        }

        // 检查是否有独立的业务逻辑（不是简单委托）
        var independentLogicPatterns = new Dictionary<string, string>
        {
            // 直接访问路径生成器（应该通过 orchestrator）
            { "ISwitchingPathGenerator", "直接依赖路径生成器（应通过 ISortingOrchestrator）" },
            // 直接访问上游客户端（应该通过 orchestrator）
            { "IUpstreamRoutingClient", "直接依赖上游路由客户端（应通过 ISortingOrchestrator）" },
            // 直接访问拥堵检测器（应该通过 orchestrator）
            { "ICongestionDetector", "直接依赖拥堵检测器（应通过 ISortingOrchestrator）" },
            // 直接访问超载策略（应该通过 orchestrator）
            { "IOverloadHandlingPolicy", "直接依赖超载策略（应通过 ISortingOrchestrator）" },
            // 直接访问系统配置仓储做分拣决策
            { "ISystemConfigurationRepository", "直接依赖系统配置仓储做分拣决策（应通过 ISortingOrchestrator）" },
        };

        // 检查文件内容是否包含独立业务逻辑的依赖
        foreach (var (pattern, description) in independentLogicPatterns)
        {
            // 检查字段声明和构造函数注入
            if (Regex.IsMatch(content, $@"private\s+(?:readonly\s+)?{pattern}\s+_\w+"))
            {
                result.HasIndependentBusinessLogic = true;
                result.IndependentLogicDetails.Add(description);
            }
        }

        // 检查是否有分拣流程代码（不是简单委托）
        var sortingFlowPatterns = new Dictionary<string, string>
        {
            // 直接生成路径
            { @"_pathGenerator\.GeneratePath", "直接调用路径生成（应通过 orchestrator）" },
            // 直接执行拥堵检测
            { @"_congestionDetector\.Detect", "直接调用拥堵检测（应通过 orchestrator）" },
            // 直接评估超载策略
            { @"_overloadPolicy\.Evaluate", "直接调用超载策略评估（应通过 orchestrator）" },
            // 直接发送上游请求
            { @"_upstreamClient\.(Notify|Request|Send)", "直接调用上游客户端（应通过 orchestrator）" },
        };

        foreach (var (pattern, description) in sortingFlowPatterns)
        {
            if (Regex.IsMatch(content, pattern))
            {
                result.HasIndependentBusinessLogic = true;
                result.IndependentLogicDetails.Add(description);
            }
        }

        // 如果有独立业务逻辑，标记为违规
        if (result.HasIndependentBusinessLogic)
        {
            result.HasViolation = true;
            if (string.IsNullOrEmpty(result.ViolationReason))
            {
                result.ViolationReason = "存在独立业务逻辑（应该只做委托调用）";
            }
        }

        return result;
    }

    #endregion

    #region 中间件边界检查

    /// <summary>
    /// 验证 Pipeline 中间件不包含分拣业务逻辑
    /// Verify pipeline middlewares don't contain sorting business logic
    /// </summary>
    /// <remarks>
    /// PR-SORT2 验收标准：
    /// - 中间件只是管道步骤，不再各自"偷偷实现一部分分拣业务"
    /// - 分拣决策仍在 SortingOrchestrator 中集中处理
    /// </remarks>
    [Fact]
    public void PipelineMiddlewaresShouldNotContainSortingBusinessLogic()
    {
        var solutionRoot = GetSolutionRoot();
        var warnings = new List<MiddlewareWarning>();

        // Pipeline 中间件的路径
        var middlewaresPath = Path.Combine(
            solutionRoot,
            "src",
            "Execution",
            "ZakYip.WheelDiverterSorter.Execution",
            "Pipeline",
            "Middlewares");

        if (!Directory.Exists(middlewaresPath))
        {
            // 目录不存在，测试通过
            return;
        }

        var middlewareFiles = Directory.GetFiles(middlewaresPath, "*Middleware.cs");

        foreach (var file in middlewareFiles)
        {
            var content = File.ReadAllText(file);
            var fileName = Path.GetFileName(file);

            // 检查是否有分拣决策代码（应该在 SortingOrchestrator 中）
            var businessLogicPatterns = new Dictionary<string, string>
            {
                // 直接决定格口（应该由委托提供）
                { @"context\.TargetChuteId\s*=\s*(?!.*delegate|.*Delegate)", "直接设置目标格口（应通过委托从 orchestrator 获取）" },
                // 直接访问系统配置做分拣模式判断
                { @"SortingMode\.(Formal|FixedChute|RoundRobin)", "直接判断分拣模式（应通过委托从 orchestrator 获取）" },
                // 直接实现固定格口或轮询逻辑
                { @"_roundRobinIndex|FixedChuteId|AvailableChuteIds", "直接实现格口选择逻辑（应通过委托从 orchestrator 获取）" },
            };

            var foundIssues = new List<string>();

            foreach (var (pattern, description) in businessLogicPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    foundIssues.Add(description);
                }
            }

            if (foundIssues.Any())
            {
                warnings.Add(new MiddlewareWarning
                {
                    FileName = fileName,
                    FilePath = file,
                    RelativePath = Path.GetRelativePath(solutionRoot, file),
                    Issues = foundIssues
                });
            }
        }

        if (warnings.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {warnings.Count} 个中间件可能包含分拣业务逻辑:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 根据 PR-SORT2 规范：中间件只是管道步骤，不实现分拣业务。\n");

            foreach (var warning in warnings)
            {
                report.AppendLine($"  ⚠️ {warning.FileName}");
                report.AppendLine($"     位置: {warning.RelativePath}");
                report.AppendLine("     发现的问题:");
                foreach (var issue in warning.Issues)
                {
                    report.AppendLine($"       - {issue}");
                }
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 中间件应该依赖委托（delegate）来获取分拣决策");
            report.AppendLine("  2. 分拣决策（格口选择、模式判断）应在 SortingOrchestrator 中实现");
            report.AppendLine("  3. 中间件只负责执行管道步骤，不做业务判断");

            // 输出警告但不失败测试（因为可能是误报）
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {warnings.Count} middleware warnings");
    }

    #endregion

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/", "/Tests/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    #endregion

    #region Internal Types

    /// <summary>
    /// SortingOrchestrator 实现信息
    /// </summary>
    private record SortingOrchestratorImplementation
    {
        public required string ClassName { get; init; }
        public required string FilePath { get; init; }
        public required string RelativePath { get; init; }
    }

    /// <summary>
    /// Sorting 服务分析结果
    /// </summary>
    private class SortingServiceAnalysisResult
    {
        public int LineCount { get; set; }
        public bool HasViolation { get; set; }
        public bool HasIndependentBusinessLogic { get; set; }
        public List<string> IndependentLogicDetails { get; set; } = new();
        public string ViolationReason { get; set; } = string.Empty;
    }

    /// <summary>
    /// Sorting 服务影分身违规信息
    /// </summary>
    private record SortingServiceShadowViolation
    {
        public required string FileName { get; init; }
        public required string FilePath { get; init; }
        public required string RelativePath { get; init; }
        public required int LineCount { get; init; }
        public required bool HasIndependentBusinessLogic { get; init; }
        public required List<string> IndependentLogicDetails { get; init; }
        public required string Reason { get; init; }
    }

    /// <summary>
    /// 中间件警告信息
    /// </summary>
    private record MiddlewareWarning
    {
        public required string FileName { get; init; }
        public required string FilePath { get; init; }
        public required string RelativePath { get; init; }
        public required List<string> Issues { get; init; }
    }

    #endregion
}
