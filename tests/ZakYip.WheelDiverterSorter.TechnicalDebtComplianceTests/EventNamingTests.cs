using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD10: 事件载荷命名规范测试
/// Tests to ensure event payload types follow naming conventions
/// </summary>
/// <remarks>
/// 根据规范，所有事件载荷类型必须满足：
/// 1. 类型名必须以 EventArgs 结尾
/// 2. 必须使用 record 或 record struct 定义
/// </remarks>
public class EventNamingTests
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
    /// 白名单：允许不使用 record 的事件类型
    /// 这些通常是特殊用途的类型或遗留代码，需要在后续PR中迁移
    /// </summary>
    private static readonly HashSet<string> NonRecordEventArgsWhitelist = new(StringComparer.Ordinal)
    {
        // 遗留代码 - Core.Events.Communication
        "EmcLockEventArgs",                      // 需要从 class 迁移到 record
        
        // 遗留代码 - Core.LineModel.Runtime.Health
        "NodeHealthChangedEventArgs",            // 定义在接口文件中，需要从 class 迁移到 record
        
        // 遗留代码 - Execution.PathExecution
        "ReroutingSucceededEventArgs",           // 定义在实现类中，需要从 class 迁移到 record
        "ReroutingFailedEventArgs",              // 定义在实现类中，需要从 class 迁移到 record
        
        // 遗留代码 - Communication.Abstractions
        "ClientConnectionEventArgs",             // 定义在接口文件中，需要从 class 迁移到 record
        "ParcelNotificationReceivedEventArgs",   // 定义在接口文件中，需要从 class 迁移到 record
        "ConnectionStateChangedEventArgs",       // 定义在接口文件中，需要从 class 迁移到 record
    };

    /// <summary>
    /// PR-SD10: 事件载荷类型应以EventArgs结尾并使用record
    /// Event payload should end with EventArgs and use record
    /// </summary>
    /// <remarks>
    /// 检测所有以 EventArgs 结尾的类型，确保它们使用 record 定义。
    /// </remarks>
    [Fact]
    public void EventPayloadShouldEndWithEventArgsAndUseRecord()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<EventNamingViolation>();

        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectEventNamingViolations(file, solutionRoot);
            violations.AddRange(fileViolations);
        }

        // 过滤白名单
        violations = violations
            .Where(v => !NonRecordEventArgsWhitelist.Contains(v.TypeName))
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD10 违规: 发现 {violations.Count} 个事件载荷类型不符合命名规范:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"\n❌ {violation.TypeName}:");
                report.AppendLine($"   位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"   当前定义: {violation.CurrentDefinition}");
                report.AppendLine($"   问题: {violation.Issue}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD10 规范:");
            report.AppendLine("  所有事件载荷类型必须使用 record 或 record struct 定义，并以 EventArgs 结尾。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将 class 改为 record 或 record class");
            report.AppendLine("  2. 将 struct 改为 record struct");
            report.AppendLine("  3. 确保类型名以 EventArgs 结尾");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD10: 生成事件载荷命名审计报告
    /// Generate event payload naming audit report
    /// </summary>
    [Fact]
    public void GenerateEventPayloadNamingAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allEventTypes = new List<EventTypeDefinition>();

        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventTypeDefinitions(file, solutionRoot);
            allEventTypes.AddRange(eventTypes);
        }

        var report = new StringBuilder();
        report.AppendLine("# 事件载荷命名审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**总事件类型数**: {allEventTypes.Count}\n");

        // 分类统计
        var recordTypes = allEventTypes.Where(e => e.IsRecord).ToList();
        var nonRecordTypes = allEventTypes.Where(e => !e.IsRecord).ToList();

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 使用 record 定义: {recordTypes.Count}");
        report.AppendLine($"- 未使用 record 定义: {nonRecordTypes.Count}");
        report.AppendLine();

        if (recordTypes.Any())
        {
            report.AppendLine("## ✅ 使用 record 定义的事件载荷\n");
            report.AppendLine("| 类型名 | 定义方式 | 位置 |");
            report.AppendLine("|--------|----------|------|");
            foreach (var evt in recordTypes.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, evt.FilePath);
                report.AppendLine($"| {evt.TypeName} | {evt.DefinitionKind} | {relativePath}:{evt.LineNumber} |");
            }
            report.AppendLine();
        }

        if (nonRecordTypes.Any())
        {
            report.AppendLine("## ⚠️ 未使用 record 定义的事件载荷\n");
            report.AppendLine("| 类型名 | 定义方式 | 位置 |");
            report.AppendLine("|--------|----------|------|");
            foreach (var evt in nonRecordTypes.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, evt.FilePath);
                report.AppendLine($"| {evt.TypeName} | {evt.DefinitionKind} | {relativePath}:{evt.LineNumber} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD10 规范，所有事件载荷类型必须满足：\n");
        report.AppendLine("1. 类型名必须以 `EventArgs` 结尾");
        report.AppendLine("2. 必须使用 `record` 或 `record struct` 定义");

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

    /// <summary>
    /// 检测事件载荷命名违规
    /// </summary>
    private static List<EventNamingViolation> DetectEventNamingViolations(string filePath, string solutionRoot)
    {
        var violations = new List<EventNamingViolation>();

        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);

            // 匹配以 EventArgs 结尾的类型定义
            // 支持: class, struct, record, record class, record struct
            var eventArgsPattern = new Regex(
                @"^\s*(?<modifiers>(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?)(?<kind>record\s+(?:class|struct)|record|class|struct)\s+(?<name>\w+EventArgs)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = eventArgsPattern.Match(lines[i]);
                if (match.Success)
                {
                    var typeName = match.Groups["name"].Value;
                    var kind = match.Groups["kind"].Value.Trim();
                    var isRecord = kind.StartsWith("record", StringComparison.OrdinalIgnoreCase);

                    // 如果不是 record 类型，记录违规
                    if (!isRecord)
                    {
                        violations.Add(new EventNamingViolation
                        {
                            TypeName = typeName,
                            FilePath = filePath,
                            LineNumber = i + 1,
                            CurrentDefinition = kind,
                            Issue = $"使用了 '{kind}' 而不是 'record' 或 'record struct'"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting event naming violations from {filePath}: {ex.Message}");
        }

        return violations;
    }

    /// <summary>
    /// 提取事件类型定义
    /// </summary>
    private static List<EventTypeDefinition> ExtractEventTypeDefinitions(string filePath, string solutionRoot)
    {
        var types = new List<EventTypeDefinition>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            var eventArgsPattern = new Regex(
                @"^\s*(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?(?<kind>record\s+(?:class|struct)|record|class|struct)\s+(?<name>\w+EventArgs)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = eventArgsPattern.Match(lines[i]);
                if (match.Success)
                {
                    var typeName = match.Groups["name"].Value;
                    var kind = match.Groups["kind"].Value.Trim();
                    var isRecord = kind.StartsWith("record", StringComparison.OrdinalIgnoreCase);

                    types.Add(new EventTypeDefinition
                    {
                        TypeName = typeName,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        DefinitionKind = kind,
                        IsRecord = isRecord
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
}

/// <summary>
/// 事件命名违规信息
/// </summary>
public record EventNamingViolation
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
    /// 当前定义方式
    /// </summary>
    public required string CurrentDefinition { get; init; }

    /// <summary>
    /// 问题描述
    /// </summary>
    public required string Issue { get; init; }
}

/// <summary>
/// 事件类型定义信息
/// </summary>
public record EventTypeDefinition
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
    /// 定义方式
    /// </summary>
    public required string DefinitionKind { get; init; }

    /// <summary>
    /// 是否使用 record
    /// </summary>
    public required bool IsRecord { get; init; }
}
