using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 纯转发类型检测测试
/// Tests to detect pure forwarding Facade/Adapter/Wrapper/Proxy types
/// </summary>
/// <remarks>
/// PR-S2: 验证代码库中不存在"纯转发"的影分身类型。
/// 
/// 判断标准（满足以下条件判定为影分身）：
/// 1. 类型以 *Facade / *Adapter / *Wrapper / *Proxy 结尾
/// 2. 只持有 1~2 个服务接口字段
/// 3. 方法体只做直接调用另一个服务的方法，没有：
///    - 额外的输入验证
///    - 数据聚合或转换
///    - 重试策略
///    - 领域编排
///    - 最多只有简单日志
/// 
/// 合法的 Adapter/Facade 应该：
/// - 有明确的类型转换逻辑
/// - 有协议适配逻辑
/// - 有事件订阅/转发机制
/// - 有状态跟踪
/// </remarks>
public class PureForwardingTypeDetectionTests
{
    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    /// <summary>
    /// 检测是否存在纯转发的 Facade/Adapter/Wrapper/Proxy 类型
    /// Detect pure forwarding Facade/Adapter/Wrapper/Proxy types
    /// </summary>
    /// <remarks>
    /// 根据 copilot-instructions.md 规范：
    /// 禁止创建"只为转发调用而存在"的 Facade/Adapter，
    /// 除非是 Decorator 并且职责写清楚。
    /// </remarks>
    [Fact]
    public void ShouldNotHavePureForwardingFacadeAdapterTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<PureForwardingViolation>();
        
        // 获取 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 高危命名模式
        var highRiskPatterns = new[] { "Facade", "Adapter", "Wrapper", "Proxy" };

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            
            // 检查文件名是否匹配高危模式
            var matchedPattern = highRiskPatterns.FirstOrDefault(p => fileName.EndsWith($"{p}.cs"));
            if (matchedPattern == null)
            {
                continue;
            }

            // 分析文件内容
            var analysis = AnalyzeFile(file);
            
            if (analysis.IsPureForwarding)
            {
                violations.Add(new PureForwardingViolation
                {
                    FilePath = file,
                    FileName = fileName,
                    TypeName = analysis.TypeName,
                    Pattern = matchedPattern,
                    DependencyCount = analysis.DependencyCount,
                    Reason = analysis.Reason
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个纯转发的 Facade/Adapter/Wrapper/Proxy 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 根据 copilot-instructions.md 规范：禁止创建\"只为转发调用而存在\"的 Facade/Adapter。\n");

            foreach (var violation in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"  ❌ {violation.TypeName} ({violation.Pattern})");
                report.AppendLine($"     文件: {relativePath}");
                report.AppendLine($"     依赖数: {violation.DependencyCount}");
                report.AppendLine($"     原因: {violation.Reason}");
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 如果类型只做简单转发，考虑直接使用被包装的服务接口");
            report.AppendLine("  2. 如果有必要的横切逻辑（日志、统计），移动到被调用服务或使用装饰器模式");
            report.AppendLine("  3. 如果是协议适配，确保有明确的类型转换或协议映射逻辑");
            report.AppendLine("  4. 删除纯转发类型后，调整 DI 注册与调用方直接使用真正的服务接口");
            report.AppendLine("\n有效的 Adapter 应该具有：");
            report.AppendLine("  - 类型转换/协议映射逻辑");
            report.AppendLine("  - 事件订阅/转发机制");
            report.AppendLine("  - 状态跟踪");
            report.AppendLine("  - 批量操作聚合");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 分析文件判断是否为纯转发类型
    /// </summary>
    private FileAnalysisResult AnalyzeFile(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var lines = File.ReadAllLines(filePath);
        var result = new FileAnalysisResult();

        // 提取类名
        var classMatch = Regex.Match(content, @"(?:public|internal)\s+(?:sealed\s+)?class\s+(\w+)");
        if (classMatch.Success)
        {
            result.TypeName = classMatch.Groups[1].Value;
        }

        // 统计私有字段依赖（包括所有接口类型，不再排除 ILogger）
        var fieldPattern = new Regex(
            @"private\s+(?:readonly\s+)?(?<type>I\w+)\s+_\w+",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        var allFields = fieldPattern.Matches(content)
            .Cast<Match>()
            .Select(m => m.Groups["type"].Value)
            .Distinct()
            .ToList();
        
        // 排除 Logger 计算服务依赖数量（用于显示）
        var serviceFields = allFields.Where(t => !t.Contains("Logger") && !t.Contains("ILogger")).ToList();
        result.DependencyCount = serviceFields.Count;

        // 如果只有一个依赖（包含 Logger），进一步分析方法体
        if (allFields.Count == 1)
        {
            var singleDependency = allFields[0];
            var hasTypeConversion = HasTypeConversionLogic(content);
            var hasEventSubscription = HasEventSubscriptionLogic(content);
            var hasStateTracking = HasStateTrackingLogic(content);
            var hasBatchOperations = HasBatchOperationLogic(content);
            var hasValidationLogic = HasValidationLogic(content);
            var hasRetryOrCircuitBreaker = HasRetryOrCircuitBreakerLogic(content);

            // 如果没有任何附加值逻辑，判定为纯转发
            if (!hasTypeConversion && !hasEventSubscription && !hasStateTracking && 
                !hasBatchOperations && !hasValidationLogic && !hasRetryOrCircuitBreaker)
            {
                // 额外检查：方法是否都是简单转发
                var isPureOneLineForwarding = CheckIfPureOneLineForwarding(content, singleDependency);
                if (isPureOneLineForwarding)
                {
                    result.IsPureForwarding = true;
                    result.Reason = $"只依赖单一服务 {singleDependency}，所有方法都是一行转发调用";
                }
            }
        }
        else if (serviceFields.Count == 1 && allFields.Count <= 2)
        {
            // 只有一个服务依赖 + 可选的 Logger
            var singleService = serviceFields[0];
            var hasTypeConversion = HasTypeConversionLogic(content);
            var hasEventSubscription = HasEventSubscriptionLogic(content);
            var hasStateTracking = HasStateTrackingLogic(content);
            var hasBatchOperations = HasBatchOperationLogic(content);
            var hasValidationLogic = HasValidationLogic(content);
            var hasRetryOrCircuitBreaker = HasRetryOrCircuitBreakerLogic(content);

            // 如果没有任何附加值逻辑，判定为纯转发
            if (!hasTypeConversion && !hasEventSubscription && !hasStateTracking && 
                !hasBatchOperations && !hasValidationLogic && !hasRetryOrCircuitBreaker)
            {
                result.IsPureForwarding = true;
                result.Reason = $"只依赖单一服务 {singleService}（+日志），方法体仅做转发调用";
            }
        }
        else if (allFields.Count == 0)
        {
            // 可能是静态工具类或其他形式，需要进一步分析
            result.IsPureForwarding = false;
            result.Reason = "没有检测到服务依赖";
        }
        else
        {
            // 有多个依赖，可能是有意义的聚合
            result.IsPureForwarding = false;
            result.Reason = $"依赖 {serviceFields.Count} 个服务，可能是有意义的聚合";
        }

        return result;
    }

    /// <summary>
    /// 检查是否所有公开方法都是简单的一行转发
    /// </summary>
    private bool CheckIfPureOneLineForwarding(string content, string dependencyType)
    {
        // 匹配公开方法
        var methodPattern = new Regex(
            @"public\s+(?:async\s+)?(?:Task<?\w*>?|\w+)\s+(?<methodName>\w+)\s*\([^)]*\)\s*\{(?<body>[^}]*)\}",
            RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.ExplicitCapture);
        
        var methods = methodPattern.Matches(content);
        
        if (methods.Count == 0)
        {
            return false;
        }

        foreach (Match method in methods)
        {
            var methodBody = method.Groups["body"].Value.Trim();
            var lines = methodBody.Split('\n')
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrEmpty(l) && !l.StartsWith("//"))
                .ToList();
            
            // 允许最多2行：参数验证 + 转发调用
            if (lines.Count > 2)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 检查是否有类型转换逻辑
    /// </summary>
    private bool HasTypeConversionLogic(string content)
    {
        // 检查 LINQ Select 投影、new 对象初始化器、Map 方法等
        return Regex.IsMatch(content, @"\.Select\s*\(\s*\w+\s*=>") ||
               Regex.IsMatch(content, @"new\s+\w+\s*\{.*=") ||
               Regex.IsMatch(content, @"MapTo|MapFrom|Convert|Transform");
    }

    /// <summary>
    /// 检查是否有事件订阅/转发逻辑
    /// </summary>
    private bool HasEventSubscriptionLogic(string content)
    {
        return content.Contains("+=") && content.Contains("EventHandler") ||
               content.Contains("event ") ||
               Regex.IsMatch(content, @"\.\w+\s*\+=");
    }

    /// <summary>
    /// 检查是否有状态跟踪逻辑
    /// </summary>
    private bool HasStateTrackingLogic(string content)
    {
        // 检查私有状态字段（非只读、非服务依赖）
        return Regex.IsMatch(content, @"private\s+(?!readonly)(?!static)\w+\s+_\w+State") ||
               Regex.IsMatch(content, @"private\s+(?!readonly)(?!static)\w+\s+_last\w+") ||
               Regex.IsMatch(content, @"private\s+(?!readonly)(?!static)\w+\s+_current\w+");
    }

    /// <summary>
    /// 检查是否有批量操作逻辑
    /// </summary>
    private bool HasBatchOperationLogic(string content)
    {
        return content.Contains("Batch") ||
               content.Contains("foreach") && content.Contains("await") ||
               Regex.IsMatch(content, @"Task\.WhenAll");
    }

    /// <summary>
    /// 检查是否有验证逻辑
    /// </summary>
    private bool HasValidationLogic(string content)
    {
        return content.Contains("ArgumentNullException.ThrowIfNull") ||
               content.Contains("throw new ArgumentException") ||
               content.Contains("Validate") ||
               Regex.IsMatch(content, @"if\s*\([^)]+==\s*null\)");
    }

    /// <summary>
    /// 检查是否有重试或断路器逻辑
    /// </summary>
    private bool HasRetryOrCircuitBreakerLogic(string content)
    {
        return content.Contains("Retry") ||
               content.Contains("CircuitBreaker") ||
               content.Contains("Polly") ||
               Regex.IsMatch(content, @"for\s*\(\s*int\s+\w+\s*=\s*0\s*;\s*\w+\s*<\s*\d+\s*;");
    }

    /// <summary>
    /// 检查文件是否在排除目录中
    /// </summary>
    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    /// <summary>
    /// 生成 Facade/Adapter 类型审计报告
    /// </summary>
    [Fact]
    public void GenerateFacadeAdapterAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# Facade/Adapter/Wrapper/Proxy 审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var highRiskPatterns = new[] { "Facade", "Adapter", "Wrapper", "Proxy" };
        var foundTypes = new List<(string Pattern, string FileName, FileAnalysisResult Analysis)>();

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            var matchedPattern = highRiskPatterns.FirstOrDefault(p => fileName.EndsWith($"{p}.cs"));
            
            if (matchedPattern != null)
            {
                var analysis = AnalyzeFile(file);
                foundTypes.Add((matchedPattern, file, analysis));
            }
        }

        // 按模式分组
        foreach (var pattern in highRiskPatterns)
        {
            var typesWithPattern = foundTypes.Where(t => t.Pattern == pattern).ToList();
            
            report.AppendLine($"## *{pattern} 类型 ({typesWithPattern.Count} 个)\n");
            
            if (typesWithPattern.Any())
            {
                report.AppendLine("| 类型名称 | 依赖数 | 状态 | 说明 |");
                report.AppendLine("|----------|--------|------|------|");
                
                foreach (var (_, filePath, analysis) in typesWithPattern)
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                    var status = analysis.IsPureForwarding ? "❌ 纯转发" : "✅ 有附加值";
                    report.AppendLine($"| {analysis.TypeName} | {analysis.DependencyCount} | {status} | {analysis.Reason} |");
                }
            }
            else
            {
                report.AppendLine("_没有发现此类型_\n");
            }
            
            report.AppendLine();
        }

        report.AppendLine("## 判断标准\n");
        report.AppendLine("**纯转发类型**（应该删除）：");
        report.AppendLine("- 只持有 1~2 个服务接口字段");
        report.AppendLine("- 方法体只做直接调用另一个服务的方法");
        report.AppendLine("- 没有类型转换、事件订阅、状态跟踪、批量操作、验证、重试等逻辑\n");
        report.AppendLine("**有附加值的类型**（应该保留）：");
        report.AppendLine("- 有明确的类型转换/协议适配逻辑");
        report.AppendLine("- 有事件订阅/转发机制");
        report.AppendLine("- 有状态跟踪");
        report.AppendLine("- 有批量操作聚合");
        report.AppendLine("- 有验证或重试逻辑");

        Console.WriteLine(report.ToString());
        
        var reportPath = Path.Combine(Path.GetTempPath(), "facade_adapter_audit_report.md");
        File.WriteAllText(reportPath, report.ToString());
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");

        Assert.True(true, "Audit report generated successfully");
    }

    /// <summary>
    /// 文件分析结果
    /// </summary>
    private class FileAnalysisResult
    {
        public string TypeName { get; set; } = "Unknown";
        public int DependencyCount { get; set; }
        public bool IsPureForwarding { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

/// <summary>
/// 纯转发类型违规信息
/// </summary>
public record PureForwardingViolation
{
    /// <summary>
    /// 文件完整路径
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// 文件名
    /// </summary>
    public required string FileName { get; init; }
    
    /// <summary>
    /// 类型名称
    /// </summary>
    public required string TypeName { get; init; }
    
    /// <summary>
    /// 匹配的模式（Facade/Adapter/Wrapper/Proxy）
    /// </summary>
    public required string Pattern { get; init; }
    
    /// <summary>
    /// 依赖的服务数量
    /// </summary>
    public required int DependencyCount { get; init; }
    
    /// <summary>
    /// 判定原因
    /// </summary>
    public required string Reason { get; init; }
}
