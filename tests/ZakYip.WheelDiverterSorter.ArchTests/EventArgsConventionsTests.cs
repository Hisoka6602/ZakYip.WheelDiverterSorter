using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// PR-PERF-EVENTS01: 事件载荷规范 ArchTests
/// 确保所有 EventArgs 类型符合高性能事件规范
/// </summary>
/// <remarks>
/// 规则：
/// 1. 所有以 EventArgs 结尾的类型必须是 record 或 record struct
/// 2. 事件载荷位置必须在允许的命名空间（除白名单例外）
/// 3. 禁止在 Host/Drivers 层定义新的 *EventArgs 类型
/// </remarks>
public class EventArgsConventionsTests
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

    // 静态正则表达式，避免每次调用时重新编译
    private static readonly Regex EventArgsClassPattern = new(
        @"^\s*(?<modifiers>(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?)(?<kind>record\s+(?:class|struct)|record|class|struct)\s+(?<name>\w+EventArgs)\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    /// <summary>
    /// 白名单：允许在 Core.Events 之外定义的事件类型
    /// 这些通常是定义在接口文件中的内联事件参数
    /// </summary>
    private static readonly HashSet<string> WhitelistedEventTypes = new(StringComparer.Ordinal)
    {
        // 定义在 Communication.Abstractions 的事件参数（接口定义要求）
        "ClientConnectionEventArgs",
        "ParcelNotificationReceivedEventArgs",
        "ConnectionStateChangedEventArgs",
        
        // 定义在 Core/LineModel 的事件参数（健康监控）
        "NodeHealthChangedEventArgs",
        
        // 定义在 Execution 的事件参数（路径重规划）
        "ReroutingSucceededEventArgs",
        "ReroutingFailedEventArgs",
        
        // 仿真项目特有的事件参数
        "SimulatedParcelResultEventArgs",
        
        // 上游路由客户端接口中的事件参数
        "ChuteAssignmentEventArgs",
    };

    /// <summary>
    /// 允许的事件载荷目录前缀（物理路径）
    /// </summary>
    private static readonly string[] AllowedDirectoryPrefixes =
    [
        "Core/ZakYip.WheelDiverterSorter.Core/Events",
        "Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream",
        "Infrastructure/ZakYip.WheelDiverterSorter.Communication/Abstractions",
        "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Runtime/Health",
        "Execution/ZakYip.WheelDiverterSorter.Execution/PathExecution",
        "Simulation/ZakYip.WheelDiverterSorter.Simulation/Results",
    ];

    /// <summary>
    /// 禁止定义 EventArgs 的项目目录
    /// </summary>
    private static readonly string[] ForbiddenProjectDirectories =
    [
        "Host/",
        "Drivers/",
    ];

    [Fact]
    public void AllEventArgsShouldBeRecordOrRecordStruct()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string TypeName, string FilePath, int LineNumber, string CurrentDefinition)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventArgsDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                // 检查是否在白名单中
                if (WhitelistedEventTypes.Contains(eventType.TypeName))
                {
                    continue;
                }

                // 检查是否是 record 类型
                if (!eventType.IsRecord)
                {
                    violations.Add((eventType.TypeName, file, eventType.LineNumber, eventType.DefinitionKind));
                }
            }
        }

        if (violations.Any())
        {
            var message = "以下 EventArgs 类型不是 record 或 record struct:\n" +
                string.Join("\n", violations.Select(v => $"  - {v.TypeName} ({v.CurrentDefinition}) at {Path.GetRelativePath(solutionRoot, v.FilePath)}:{v.LineNumber}"));
            Assert.Fail(message);
        }
    }

    [Fact]
    public void EventArgsShouldBeInAllowedDirectories()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventArgsDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                // 检查白名单
                if (WhitelistedEventTypes.Contains(eventType.TypeName))
                {
                    continue;
                }

                // 获取相对路径
                var relativePath = Path.GetRelativePath(srcDir, file).Replace('\\', '/');

                // 检查是否在允许的目录
                var isAllowed = AllowedDirectoryPrefixes.Any(prefix =>
                    relativePath.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

                if (!isAllowed)
                {
                    violations.Add((eventType.TypeName, file));
                }
            }
        }

        if (violations.Any())
        {
            var message = "以下 EventArgs 类型不在允许的目录中:\n" +
                string.Join("\n", violations.Select(v => $"  - {v.TypeName} at {Path.GetRelativePath(solutionRoot, v.FilePath)}")) +
                $"\n\n允许的目录:\n{string.Join("\n", AllowedDirectoryPrefixes.Select(p => $"  - src/{p}"))}";
            Assert.Fail(message);
        }
    }

    [Fact]
    public void ForbiddenProjectsShouldNotDefineNewEventArgs()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            // 获取相对路径
            var relativePath = Path.GetRelativePath(srcDir, file).Replace('\\', '/');

            // 检查是否在禁止的项目目录
            var isForbidden = ForbiddenProjectDirectories.Any(dir =>
                relativePath.StartsWith(dir, StringComparison.OrdinalIgnoreCase));

            if (!isForbidden)
            {
                continue;
            }

            var eventTypes = ExtractEventArgsDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                // 检查白名单
                if (!WhitelistedEventTypes.Contains(eventType.TypeName))
                {
                    violations.Add((eventType.TypeName, file));
                }
            }
        }

        if (violations.Any())
        {
            var message = "以下项目中定义了 EventArgs 类型（应移至 Core.Events）:\n" +
                string.Join("\n", violations.Select(v => $"  - {v.TypeName} at {Path.GetRelativePath(solutionRoot, v.FilePath)}"));
            Assert.Fail(message);
        }
    }

    [Fact]
    public void GenerateEventArgsConventionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDir = Path.Combine(solutionRoot, "src");
        var allEventTypes = new List<(string TypeName, string FilePath, bool IsRecord, string DefinitionKind)>();

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar))
            .Where(f => !f.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractEventArgsDefinitions(file);
            foreach (var eventType in eventTypes)
            {
                allEventTypes.Add((eventType.TypeName, file, eventType.IsRecord, eventType.DefinitionKind));
            }
        }

        var report = new System.Text.StringBuilder();
        report.AppendLine("# EventArgs 规范审计报告");
        report.AppendLine($"\n**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"\n**EventArgs 类型总数**: {allEventTypes.Count}");

        var recordCount = allEventTypes.Count(e => e.IsRecord);
        var nonRecordCount = allEventTypes.Count(e => !e.IsRecord);

        report.AppendLine($"- record/record struct: {recordCount}");
        report.AppendLine($"- class/struct (需要迁移): {nonRecordCount}");

        // 按项目目录分组
        report.AppendLine("\n## 按项目分布\n");
        report.AppendLine("| 项目 | 类型数 | record类型 |");
        report.AppendLine("|------|--------|-----------|");

        var byProject = allEventTypes
            .GroupBy(e => GetProjectName(e.FilePath, srcDir))
            .OrderBy(g => g.Key);
        
        foreach (var group in byProject)
        {
            var total = group.Count();
            var records = group.Count(e => e.IsRecord);
            report.AppendLine($"| {group.Key} | {total} | {records} |");
        }

        report.AppendLine("\n## 详细类型列表\n");
        report.AppendLine("| 类型名 | 定义方式 | 文件位置 |");
        report.AppendLine("|--------|----------|----------|");

        foreach (var (typeName, filePath, isRecord, kind) in allEventTypes.OrderBy(e => e.TypeName))
        {
            var relativePath = Path.GetRelativePath(solutionRoot, filePath).Replace('\\', '/');
            var status = isRecord ? "✅" : "❌";
            report.AppendLine($"| {typeName} | {kind} {status} | {relativePath} |");
        }

        Console.WriteLine(report);
        Assert.True(true, "Report generated");
    }

    #region Helper Methods

    private record EventArgsTypeInfo(string TypeName, int LineNumber, string DefinitionKind, bool IsRecord);

    private static List<EventArgsTypeInfo> ExtractEventArgsDefinitions(string filePath)
    {
        var types = new List<EventArgsTypeInfo>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = EventArgsClassPattern.Match(lines[i]);
                if (match.Success)
                {
                    var typeName = match.Groups["name"].Value;
                    var kind = match.Groups["kind"].Value.Trim();
                    var isRecord = kind.StartsWith("record", StringComparison.OrdinalIgnoreCase);

                    types.Add(new EventArgsTypeInfo(typeName, i + 1, kind, isRecord));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting event types from {filePath}: {ex.Message}");
        }

        return types;
    }

    private static string GetProjectName(string filePath, string srcDir)
    {
        var relativePath = Path.GetRelativePath(srcDir, filePath).Replace('\\', '/');
        var parts = relativePath.Split('/');
        if (parts.Length >= 2)
        {
            return parts[0]; // Return project folder name
        }
        return "Unknown";
    }

    #endregion
}
