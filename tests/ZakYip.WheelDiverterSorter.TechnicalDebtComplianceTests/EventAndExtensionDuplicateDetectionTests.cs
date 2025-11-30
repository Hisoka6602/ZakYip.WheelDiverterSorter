using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 事件类型与 DI 扩展类重名检测测试
/// Tests to detect duplicate event types and service collection extension classes
/// </summary>
/// <remarks>
/// PR-S6: 事件 &amp; DI 扩展影分身清理
/// 
/// 检测策略：
/// 1. 事件类型不应跨层重名（*Event/*EventArgs 在 Core/Ingress/Simulation 等多个项目中）
///    - 仿真侧可通过 Simulated 前缀规避
/// 2. 每个项目最多一个 *ServiceCollectionExtensions 类
/// 3. 同一 *ServiceCollectionExtensions 类名不得在多个项目中重复
/// </remarks>
public class EventAndExtensionDuplicateDetectionTests
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
    /// PR-S6: 事件类型不应跨层重名
    /// Event types should not be duplicated across layers
    /// </summary>
    /// <remarks>
    /// 若同名 *Event / *EventArgs 同时出现在 Core、Ingress、Simulation 等多个项目，
    /// 则视为影分身（仿真侧可通过明确前缀 Simulated 规避）。
    /// </remarks>
    [Fact]
    public void EventTypesShouldNotBeDuplicatedAcrossLayers()
    {
        var solutionRoot = GetSolutionRoot();
        var eventTypesByName = new Dictionary<string, List<EventTypeInfo>>(StringComparer.OrdinalIgnoreCase);
        
        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有事件类型定义（*Event 或 *EventArgs）
        eventTypesByName = sourceFiles
            .SelectMany(file => ExtractEventTypeDefinitions(file, solutionRoot))
            .GroupBy(eventType => eventType.TypeName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase
            );

        // 构建所有事件类型名的集合，便于查找对应的非 Simulated 类型
        var allEventTypeNames = new HashSet<string>(eventTypesByName.Keys, StringComparer.OrdinalIgnoreCase);

        // 查找跨项目重复的事件类型
        var duplicates = eventTypesByName
            .Where(kvp => kvp.Value.Count > 1)
            // 只有当在多个不同项目中定义时才算重复
            .Where(kvp => kvp.Value.Select(t => t.ProjectName).Distinct().Count() > 1)
            // 排除 file-scoped 类型
            .Where(kvp => !kvp.Value.All(t => t.IsFileScoped))
            // 仅当 Simulated* 有对应非 Simulated 类型时才豁免，否则仍检测重复
            .Where(kvp =>
                !(
                    kvp.Key.StartsWith("Simulated", StringComparison.OrdinalIgnoreCase)
                    && allEventTypeNames.Contains(kvp.Key.Substring("Simulated".Length))
                )
            )
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-S6 违规: 发现 {duplicates.Count} 个事件类型存在跨项目重复定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, locations) in duplicates.OrderBy(d => d.Key))
            {
                report.AppendLine($"\n❌ {typeName}:");
                foreach (var loc in locations.OrderBy(l => l.ProjectName))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, loc.FilePath);
                    report.AppendLine($"   - 项目: {loc.ProjectName}");
                    report.AppendLine($"     位置: {relativePath}:{loc.LineNumber}");
                    report.AppendLine($"     命名空间: {loc.Namespace}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-S6 规范:");
            report.AppendLine("  同名的 *Event / *EventArgs 类型不能在多个项目中定义。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 保留一个权威定义（通常在 Core 或原始定义位置）");
            report.AppendLine("  2. 重命名其他定义（仿真侧使用 Simulated 前缀）");
            report.AppendLine("  3. 更新所有引用以使用唯一定义");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-S6: ServiceCollectionExtensions 类名不应跨项目重复
    /// ServiceCollectionExtensions class names should not be duplicated across projects
    /// </summary>
    /// <remarks>
    /// 同一 *ServiceCollectionExtensions 类名不得在多个项目中重复。
    /// Application 层的 WheelDiverterSorterServiceCollectionExtensions 应为唯一该名称类型，
    /// Host 层应使用 WheelDiverterSorterHostServiceCollectionExtensions。
    /// 
    /// 注意：多个厂商特定的扩展类在同一项目（如 Drivers）是允许的，
    /// 因为它们有不同的类名（如 LeadshineIoServiceCollectionExtensions、ModiWheelServiceCollectionExtensions）。
    /// </remarks>
    [Fact]
    public void ServiceCollectionExtensionsShouldBeUniquePerProject()
    {
        var solutionRoot = GetSolutionRoot();
        var extensionsByName = new Dictionary<string, List<ExtensionTypeInfo>>(StringComparer.OrdinalIgnoreCase);
        
        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有 *ServiceCollectionExtensions 类型定义
        foreach (var file in sourceFiles)
        {
            var extensionTypes = ExtractServiceCollectionExtensionTypes(file, solutionRoot);
            foreach (var extType in extensionTypes)
            {
                if (!extensionsByName.ContainsKey(extType.TypeName))
                {
                    extensionsByName[extType.TypeName] = new List<ExtensionTypeInfo>();
                }
                extensionsByName[extType.TypeName].Add(extType);
            }
        }

        // 查找跨项目重复的 ServiceCollectionExtensions 类名
        var duplicates = extensionsByName
            .Where(kvp => kvp.Value.Count > 1)
            // 只有当在多个不同项目中定义时才算重复
            .Where(kvp => kvp.Value.Select(t => t.ProjectName).Distinct().Count() > 1)
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-S6 违规: 发现 {duplicates.Count} 个 ServiceCollectionExtensions 类名存在跨项目重复:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, locations) in duplicates.OrderBy(d => d.Key))
            {
                report.AppendLine($"\n❌ {typeName}:");
                foreach (var loc in locations.OrderBy(l => l.ProjectName))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, loc.FilePath);
                    report.AppendLine($"   - 项目: {loc.ProjectName}");
                    report.AppendLine($"     位置: {relativePath}:{loc.LineNumber}");
                    report.AppendLine($"     命名空间: {loc.Namespace}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-S6 规范:");
            report.AppendLine("  同一 *ServiceCollectionExtensions 类名不能在多个项目中定义。");
            report.AppendLine("  例如：Application 层使用 WheelDiverterSorterServiceCollectionExtensions，");
            report.AppendLine("       Host 层应使用 WheelDiverterSorterHostServiceCollectionExtensions。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 为 Host 层的扩展类使用更明确的名称（如 *HostServiceCollectionExtensions）");
            report.AppendLine("  2. 更新 Program.cs 中的引用");

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
    /// 从文件中提取事件类型定义（*Event 或 *EventArgs）
    /// </summary>
    private static List<EventTypeInfo> ExtractEventTypeDefinitions(string filePath, string solutionRoot)
    {
        var types = new List<EventTypeInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 提取项目名
            var projectName = ExtractProjectName(filePath, solutionRoot);

            // 查找以 Event 或 EventArgs 结尾的类型定义
            // 支持: class, struct, record, record class, record struct
            var eventPattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<typeName>\w+(?:Event|EventArgs))\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = eventPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new EventTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns,
                        ProjectName = projectName,
                        IsFileScoped = match.Groups["fileScoped"].Success
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
    /// 从文件中提取 *ServiceCollectionExtensions 类型定义
    /// </summary>
    private static List<ExtensionTypeInfo> ExtractServiceCollectionExtensionTypes(string filePath, string solutionRoot)
    {
        var types = new List<ExtensionTypeInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 提取项目名
            var projectName = ExtractProjectName(filePath, solutionRoot);

            // 查找以 ServiceCollectionExtensions 结尾的静态类定义
            var extensionPattern = new Regex(
                @"^\s*(?:public|internal)\s+static\s+class\s+(?<typeName>\w+ServiceCollectionExtensions)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = extensionPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new ExtensionTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns,
                        ProjectName = projectName
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting extension types from {filePath}: {ex.Message}");
        }

        return types;
    }

    /// <summary>
    /// 从文件路径提取项目名
    /// </summary>
    private static string ExtractProjectName(string filePath, string solutionRoot)
    {
        var relativePath = Path.GetRelativePath(solutionRoot, filePath);
        var parts = relativePath.Replace('\\', '/').Split('/');
        
        // 查找项目目录名
        // 路径格式通常为: src/[Layer]/[ProjectName]/[SubDirs]/[File].cs
        // 例如: src/Core/ZakYip.WheelDiverterSorter.Core/Sorting/Policies/UpstreamConnectionOptions.cs
        if (parts.Length >= 3 && parts[0] == "src")
        {
            return parts[2]; // 返回项目目录名
        }
        
        return Path.GetFileName(Path.GetDirectoryName(filePath) ?? "Unknown");
    }

    #endregion
}

/// <summary>
/// 事件类型位置信息
/// </summary>
public record EventTypeInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
    public required string ProjectName { get; init; }
    public bool IsFileScoped { get; init; }
}

/// <summary>
/// DI 扩展类型位置信息
/// </summary>
public record ExtensionTypeInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
    public required string ProjectName { get; init; }
}
