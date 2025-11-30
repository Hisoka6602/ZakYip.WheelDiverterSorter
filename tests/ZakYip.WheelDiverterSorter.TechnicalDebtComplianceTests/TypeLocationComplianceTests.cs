using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 事件载荷和枚举位置合规性测试
/// Tests to ensure event payloads and enums are in correct namespaces
/// </summary>
/// <remarks>
/// 根据规范：
/// 1. 所有事件载荷（*EventArgs）应该集中在 ZakYip.WheelDiverterSorter.Core.Events 命名空间下
/// 2. 所有枚举应该集中在 ZakYip.WheelDiverterSorter.Core.Enums 命名空间下
/// 
/// 这些测试目前作为审计报告生成，当遗留代码清理完成后可以启用强制检查。
/// </remarks>
public class TypeLocationComplianceTests
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
    /// 允许的事件载荷命名空间前缀
    /// </summary>
    private static readonly string[] AllowedEventNamespacePrefixes = 
    {
        "ZakYip.WheelDiverterSorter.Core.Events",
        "ZakYip.WheelDiverterSorter.Core.LineModel.Events",
        "ZakYip.WheelDiverterSorter.Core.Sorting.Events",
        "ZakYip.WheelDiverterSorter.Core.Hardware",
        "ZakYip.WheelDiverterSorter.Core.LineModel.Tracing",
        // 遗留位置 - 后续 PR 清理后移除
        "ZakYip.WheelDiverterSorter.Execution.Events",
        "ZakYip.WheelDiverterSorter.Execution.PathExecution",
        "ZakYip.WheelDiverterSorter.Ingress.Models",
        "ZakYip.WheelDiverterSorter.Communication.Models",
        "ZakYip.WheelDiverterSorter.Communication.Abstractions",
        "ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health",
    };

    /// <summary>
    /// 允许的枚举命名空间前缀
    /// </summary>
    private static readonly string[] AllowedEnumNamespacePrefixes = 
    {
        "ZakYip.WheelDiverterSorter.Core.Enums",
    };

    /// <summary>
    /// 生成事件载荷位置审计报告
    /// Generate event payload location audit report
    /// </summary>
    /// <remarks>
    /// 此测试列出所有事件载荷类型及其位置，帮助识别需要移动到 Core.Events 的类型。
    /// 当所有遗留代码清理完成后，此测试可以转换为强制检查。
    /// </remarks>
    [Fact]
    public void GenerateEventPayloadLocationAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allEventTypes = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventPayloadDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                allEventTypes.Add((eventType.TypeName, file, eventType.LineNumber, eventType.Namespace));
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# 事件载荷位置审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**总事件载荷数**: {allEventTypes.Count}\n");

        // 按命名空间分组
        var byNamespace = allEventTypes.GroupBy(e => e.Namespace).OrderBy(g => g.Key).ToList();

        report.AppendLine("## 按命名空间分组\n");
        foreach (var group in byNamespace)
        {
            var isAllowedLocation = AllowedEventNamespacePrefixes.Any(p => 
                group.Key.StartsWith(p, StringComparison.Ordinal));
            var marker = isAllowedLocation ? "✅" : "⚠️";
            report.AppendLine($"### {marker} {group.Key} ({group.Count()} 个)\n");
            foreach (var (typeName, filePath, lineNumber, _) in group.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"- `{typeName}` - {relativePath}:{lineNumber}");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD8 规范，所有新增的事件载荷必须满足以下要求：\n");
        report.AppendLine("1. **目标位置**: `src/Core/ZakYip.WheelDiverterSorter.Core/Events/`");
        report.AppendLine("2. **目标命名空间**: `ZakYip.WheelDiverterSorter.Core.Events`");
        report.AppendLine("3. **命名约定**: 类型名必须以 `EventArgs` 结尾");
        report.AppendLine("\n⚠️ 标记的命名空间是遗留位置，后续 PR 会逐步迁移到 Core.Events。");

        Console.WriteLine(report);
        Assert.True(true, "Audit report generated successfully");
    }

    /// <summary>
    /// 生成枚举位置审计报告
    /// Generate enum location audit report
    /// </summary>
    /// <remarks>
    /// 此测试列出所有枚举类型及其位置，帮助识别需要移动到 Core.Enums 的类型。
    /// 当所有遗留代码清理完成后，此测试可以转换为强制检查。
    /// </remarks>
    [Fact]
    public void GenerateEnumLocationAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allEnums = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var enums = ExtractEnumDefinitions(file);
            foreach (var enumType in enums)
            {
                allEnums.Add((enumType.TypeName, file, enumType.LineNumber, enumType.Namespace));
            }
        }

        var report = new StringBuilder();
        report.AppendLine("# 枚举位置审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**总枚举数**: {allEnums.Count}\n");

        // 分类统计
        var inCoreEnums = allEnums.Where(e => e.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Core.Enums")).ToList();
        var outsideCoreEnums = allEnums.Where(e => !e.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Core.Enums")).ToList();

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 在 Core.Enums 中: {inCoreEnums.Count}");
        report.AppendLine($"- 在其他位置: {outsideCoreEnums.Count}");
        report.AppendLine();

        // 按命名空间分组
        var byNamespace = allEnums.GroupBy(e => e.Namespace).OrderBy(g => g.Key).ToList();

        report.AppendLine("## 按命名空间分组\n");
        foreach (var group in byNamespace)
        {
            var isAllowedLocation = group.Key.StartsWith("ZakYip.WheelDiverterSorter.Core.Enums", StringComparison.Ordinal);
            var marker = isAllowedLocation ? "✅" : "⚠️";
            report.AppendLine($"### {marker} {group.Key} ({group.Count()} 个)\n");
            foreach (var (typeName, filePath, lineNumber, _) in group.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"- `{typeName}` - {relativePath}:{lineNumber}");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD8 规范，所有新增的枚举必须满足以下要求：\n");
        report.AppendLine("1. **目标位置**: `src/Core/ZakYip.WheelDiverterSorter.Core/Enums/[子目录]/`");
        report.AppendLine("2. **目标命名空间**: `ZakYip.WheelDiverterSorter.Core.Enums.[子命名空间]`");
        report.AppendLine("\n⚠️ 标记的命名空间是遗留位置，后续 PR 会逐步迁移到 Core.Enums。");

        Console.WriteLine(report);
        Assert.True(true, "Audit report generated successfully");
    }

    /// <summary>
    /// 验证 Core.Enums 目录结构存在
    /// Verify Core.Enums directory structure exists
    /// </summary>
    [Fact]
    public void CoreEnumsDirectoryShouldExist()
    {
        var solutionRoot = GetSolutionRoot();
        var enumsDir = Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "Enums");

        Assert.True(Directory.Exists(enumsDir), 
            $"Core.Enums 目录不存在: {enumsDir}\n" +
            "所有枚举应该集中在此目录下。");

        var enumFiles = Directory.GetFiles(enumsDir, "*.cs", SearchOption.AllDirectories);
        Assert.True(enumFiles.Length > 0,
            $"Core.Enums 目录中没有找到枚举文件。\n" +
            "至少应该有一些枚举定义在此目录中。");
    }

    /// <summary>
    /// 验证新增的枚举不在 Core.Enums 之外（强制检查 - 仅用于增量验证）
    /// Verify that new enums are not defined outside Core.Enums
    /// </summary>
    /// <remarks>
    /// 此测试用于增量验证：当前已有的遗留枚举会被白名单豁免，
    /// 但任何新增的枚举（不在白名单中）如果不在 Core.Enums 中将导致测试失败。
    /// </remarks>
    [Fact]
    public void NewEnumsShouldBeInCoreEnums()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 已知的遗留枚举白名单（在 Core.Enums 之外的现有枚举）
        // 这些枚举在后续 PR 中会被移动，但当前允许存在
        var legacyEnumsWhitelist = new HashSet<string>(StringComparer.Ordinal)
        {
            // 如果发现其他位置的枚举，在此添加白名单
            // 格式: "命名空间.枚举名"
        };

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var enums = ExtractEnumDefinitions(file);
            foreach (var enumType in enums)
            {
                // 检查是否在 Core.Enums 命名空间中
                if (enumType.Namespace.StartsWith("ZakYip.WheelDiverterSorter.Core.Enums", StringComparison.Ordinal))
                {
                    continue; // 正确位置
                }

                // 检查是否在白名单中
                var fullName = $"{enumType.Namespace}.{enumType.TypeName}";
                if (legacyEnumsWhitelist.Contains(fullName))
                {
                    continue; // 已知遗留枚举
                }

                violations.Add((enumType.TypeName, file, enumType.LineNumber, enumType.Namespace));
            }
        }

        // 当前所有枚举都在 Core.Enums 中，此测试应该通过
        // 如果有违规，说明有新枚举在错误位置
        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个枚举不在 Core.Enums 命名空间中:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"\n❌ {typeName}:");
                report.AppendLine($"   位置: {relativePath}:{lineNumber}");
                report.AppendLine($"   当前命名空间: {ns}");
                report.AppendLine($"   期望命名空间: ZakYip.WheelDiverterSorter.Core.Enums.*");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD8 规范:");
            report.AppendLine("  所有枚举必须定义在 ZakYip.WheelDiverterSorter.Core.Enums 命名空间下。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将枚举移动到 src/Core/ZakYip.WheelDiverterSorter.Core/Enums/[子目录]/ 下");
            report.AppendLine("  2. 更新命名空间为 ZakYip.WheelDiverterSorter.Core.Enums.[子命名空间]");
            report.AppendLine("  3. 更新所有引用的 using 语句");

            Assert.Fail(report.ToString());
        }
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    /// <summary>
    /// 从文件中提取事件载荷类型定义
    /// </summary>
    private static List<TypeLocationInfo> ExtractEventPayloadDefinitions(string filePath)
    {
        var types = new List<TypeLocationInfo>();

        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);

            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            var eventArgsPattern = new Regex(
                @"^\s*(?:public|internal)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<typeName>\w+EventArgs)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = eventArgsPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new TypeLocationInfo
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

    /// <summary>
    /// 从文件中提取枚举定义
    /// </summary>
    private static List<TypeLocationInfo> ExtractEnumDefinitions(string filePath)
    {
        var types = new List<TypeLocationInfo>();

        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);

            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            var enumPattern = new Regex(
                @"^\s*(?:public|internal)\s+enum\s+(?<typeName>\w+)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = enumPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new TypeLocationInfo
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
            Console.WriteLine($"Error extracting enums from {filePath}: {ex.Message}");
        }

        return types;
    }

    private class TypeLocationInfo
    {
        public required string TypeName { get; init; }
        public required int LineNumber { get; init; }
        public required string Namespace { get; init; }
    }

    #endregion
}
