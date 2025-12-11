using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 事件载荷位置合规性测试
/// Tests to ensure all event payload classes are in Core.Events namespace
/// </summary>
/// <remarks>
/// 根据规范，所有事件载荷（*EventArgs, *Event）必须集中在 ZakYip.WheelDiverterSorter.Core.Events 命名空间下。
/// 
/// 规则：
/// 1. 所有以 EventArgs 结尾的类型必须在 Core.Events 命名空间
/// 2. 所有以 Event 结尾且继承自 EventArgs 的类型必须在 Core.Events 命名空间
/// 3. 不允许在其他项目/命名空间定义事件载荷类型
/// 
/// 例外：
/// - System.EventArgs 基类本身
/// - 第三方库中的事件类型
/// - 测试项目中的事件类型
/// </remarks>
public class EventPayloadLocationComplianceTests
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
    /// 允许的事件载荷命名空间
    /// </summary>
    private const string AllowedEventsNamespace = "ZakYip.WheelDiverterSorter.Core.Events";

    /// <summary>
    /// 白名单：允许在其他位置定义的事件类型
    /// 这些通常是特殊用途的类型，需要经过架构评审才能添加
    /// </summary>
    private static readonly HashSet<string> WhitelistedEventTypes = new(StringComparer.Ordinal)
    {
        // 定义在接口文件中的事件参数（后续PR清理）
        "ClientConnectionEventArgs",           // IRuleEngineServer.cs
        "ParcelNotificationReceivedEventArgs", // IRuleEngineServer.cs
        "ConnectionStateChangedEventArgs",     // IUpstreamConnectionManager.cs
        "NodeHealthChangedEventArgs",          // INodeHealthRegistry.cs
        "ChuteAssignmentEventArgs",            // IUpstreamRoutingClient.cs
        
        // 定义在实现类中的事件参数（后续PR清理）
        "ReroutingSucceededEventArgs",         // EnhancedPathFailureHandler.cs
        "ReroutingFailedEventArgs",            // EnhancedPathFailureHandler.cs
        
        // 仿真项目特有的事件参数
        "SimulatedParcelResultEventArgs",      // ParcelSimulationResult.cs
        
        // 数递鸟厂商特定事件参数（Drivers层，符合架构原则：vendor-specific concerns in Drivers）
        "DeviceStatusEventArgs",               // ShuDiNiao/Events/DeviceStatusEventArgs.cs
        "DeviceConnectionEventArgs",           // ShuDiNiao/Events/DeviceConnectionEventArgs.cs
    };

    /// <summary>
    /// PR-SD8: 验证所有事件载荷类型都在 Core.Events 命名空间中
    /// Verify all event payload types are in Core.Events namespace
    /// </summary>
    [Fact]
    public void AllEventPayloadsShouldBeInCoreEventsNamespace()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<EventPayloadViolation>();

        // 扫描 src 目录下所有 .cs 文件（排除 Core/Events 目录）
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInAllowedEventsDirectory(f, solutionRoot))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventPayloadDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                // 检查是否在白名单中
                if (WhitelistedEventTypes.Contains(eventType.TypeName))
                {
                    continue;
                }

                // 检查命名空间是否以 Core.Events 开头（允许子命名空间如 Core.Events.Sorting）
                if (eventType.Namespace.StartsWith(AllowedEventsNamespace, StringComparison.Ordinal))
                {
                    continue;
                }

                violations.Add(new EventPayloadViolation
                {
                    TypeName = eventType.TypeName,
                    FilePath = file,
                    LineNumber = eventType.LineNumber,
                    CurrentNamespace = eventType.Namespace,
                    ExpectedNamespace = AllowedEventsNamespace + ".*"
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个事件载荷类型不在 Core.Events 命名空间中:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"\n❌ {violation.TypeName}:");
                report.AppendLine($"   位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"   当前命名空间: {violation.CurrentStateamespace}");
                report.AppendLine($"   期望命名空间: {violation.ExpectedNamespace}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD8 规范:");
            report.AppendLine("  所有事件载荷（*EventArgs）必须集中在 ZakYip.WheelDiverterSorter.Core.Events 命名空间下。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将事件载荷类型移动到 src/Core/ZakYip.WheelDiverterSorter.Core/Events/ 目录");
            report.AppendLine("  2. 更新命名空间为 ZakYip.WheelDiverterSorter.Core.Events");
            report.AppendLine("  3. 更新所有引用的 using 语句");
            report.AppendLine("  4. 如果是特殊用途类型，需要经过架构评审后添加到白名单");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 Core.Events 目录存在且包含事件载荷
    /// Verify Core.Events directory exists and contains event payloads
    /// </summary>
    [Fact]
    public void CoreEventsDirectoryShouldExistAndContainEventPayloads()
    {
        var solutionRoot = GetSolutionRoot();
        var eventsDir = Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "Events");

        Assert.True(Directory.Exists(eventsDir), 
            $"Core.Events 目录不存在: {eventsDir}\n" +
            "所有事件载荷应该集中在此目录下。");

        // Search in all subdirectories since events are organized by category
        var eventFiles = Directory.GetFiles(eventsDir, "*EventArgs.cs", SearchOption.AllDirectories);
        Assert.True(eventFiles.Length > 0,
            $"Core.Events 目录中没有找到事件载荷文件 (*EventArgs.cs)。\n" +
            "至少应该有一些事件载荷定义在此目录中。");
    }

    /// <summary>
    /// 生成事件载荷位置审计报告
    /// Generate event payload location audit report
    /// </summary>
    [Fact]
    public void GenerateEventPayloadLocationAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allEventTypes = new List<(string TypeName, string FilePath, int LineNumber, string Namespace, bool IsInCorrectLocation)>();

        // 扫描所有 src 目录
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventPayloadDefinitions(file);
            var isInCorrectLocation = IsInAllowedEventsDirectory(file, solutionRoot);
            
            foreach (var eventType in eventTypes)
            {
                allEventTypes.Add((eventType.TypeName, file, eventType.LineNumber, eventType.Namespace, isInCorrectLocation));
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# 事件载荷位置审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var correctCount = allEventTypes.Count(e => e.IsInCorrectLocation);
        var incorrectCount = allEventTypes.Count(e => !e.IsInCorrectLocation);

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 事件载荷总数: {allEventTypes.Count}");
        report.AppendLine($"- 在正确位置 (Core.Events): {correctCount}");
        report.AppendLine($"- 在错误位置: {incorrectCount}");
        report.AppendLine();

        // 按位置分组
        if (correctCount > 0)
        {
            report.AppendLine("## ✅ 在 Core.Events 中的事件载荷\n");
            report.AppendLine("| 类型名 | 命名空间 |");
            report.AppendLine("|--------|----------|");
            foreach (var (typeName, _, _, ns, isCorrect) in allEventTypes.Where(e => e.IsInCorrectLocation).OrderBy(e => e.TypeName))
            {
                report.AppendLine($"| {typeName} | {ns} |");
            }
            report.AppendLine();
        }

        if (incorrectCount > 0)
        {
            report.AppendLine("## ❌ 不在 Core.Events 中的事件载荷（需要移动）\n");
            report.AppendLine("| 类型名 | 当前位置 | 当前命名空间 |");
            report.AppendLine("|--------|----------|--------------|");
            foreach (var (typeName, filePath, _, ns, isCorrect) in allEventTypes.Where(e => !e.IsInCorrectLocation).OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"| {typeName} | {relativePath} | {ns} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD8 规范，所有事件载荷必须满足以下要求：\n");
        report.AppendLine("1. **位置**: 必须在 `src/Core/ZakYip.WheelDiverterSorter.Core/Events/` 目录下");
        report.AppendLine("2. **命名空间**: 必须是 `ZakYip.WheelDiverterSorter.Core.Events`");
        report.AppendLine("3. **命名约定**: 类型名必须以 `EventArgs` 结尾");

        Console.WriteLine(report);
        Assert.True(true, "Audit report generated successfully");
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsInAllowedEventsDirectory(string filePath, string solutionRoot)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var allowedDir = Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "Events")
            .Replace('\\', '/');
        return normalizedPath.StartsWith(allowedDir, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 从文件中提取事件载荷类型定义
    /// </summary>
    private static List<EventPayloadTypeInfo> ExtractEventPayloadDefinitions(string filePath)
    {
        var types = new List<EventPayloadTypeInfo>();

        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);

            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 查找 *EventArgs 类型定义
            // 支持: class, struct, record, record class, record struct, readonly record struct
            var eventArgsPattern = new Regex(
                @"^\s*(?:public|internal)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<typeName>\w+EventArgs)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = eventArgsPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new EventPayloadTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        LineNumber = i + 1,
                        Namespace = ns
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting event types from {filePath}: {ex.Message}");
        }

        return types;
    }

    #endregion

    /// <summary>
    /// 事件载荷类型信息
    /// </summary>
    private class EventPayloadTypeInfo
    {
        public required string TypeName { get; init; }
        public required int LineNumber { get; init; }
        public required string Namespace { get; init; }
    }
}

/// <summary>
/// 事件载荷位置违规信息
/// </summary>
public record EventPayloadViolation
{
    /// <summary>
    /// 类型名称
    /// </summary>
    public required string TypeName { get; init; }

    /// <summary>
    /// 文件路径
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// 行号
    /// </summary>
    public required int LineNumber { get; init; }

    /// <summary>
    /// 当前命名空间
    /// </summary>
    public required string CurrentNamespace { get; init; }

    /// <summary>
    /// 期望命名空间
    /// </summary>
    public required string ExpectedNamespace { get; init; }
}
