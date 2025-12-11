using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD10: 仿真事件命名规范测试
/// Tests to ensure simulation event types follow naming conventions
/// </summary>
/// <remarks>
/// 根据规范，Simulation 程序集内的事件载荷类型，名称要以 Simulated 开头。
/// 这样可以避免与 Ingress/Execution 层的真实事件类型同名。
/// </remarks>
public class SimulationEventTests
{
    private const string SimulatedPrefix = "Simulated";
    
    // 静态正则表达式，避免每次调用时重新编译
    private static readonly Regex EventPattern = new(
        @"^\s*(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)|record|class|struct)\s+(?<name>\w+(?:EventArgs|Event))\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

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
    /// 白名单：允许不以 Simulated 开头的仿真事件类型
    /// 这些通常是特殊用途的类型或需要后续迁移的遗留代码
    /// </summary>
    private static readonly HashSet<string> NonSimulatedPrefixWhitelist = new(StringComparer.Ordinal)
    {
        // 如有需要，在此添加白名单项
    };

    /// <summary>
    /// PR-SD10: 仿真事件类型应以Simulated开头
    /// Simulation events should be prefixed with Simulated
    /// </summary>
    /// <remarks>
    /// 检测 Simulation 程序集内的事件载荷类型，确保它们以 Simulated 开头。
    /// </remarks>
    [Fact]
    public void SimulationEventsShouldBePrefixedWithSimulated()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<SimulationEventViolation>();

        // 只扫描 Simulation 项目目录下的文件
        var simulationDir = Path.Combine(solutionRoot, "src", "Simulation");
        if (!Directory.Exists(simulationDir))
        {
            Assert.True(true, "Simulation directory not found, skipping test");
            return;
        }

        var sourceFiles = Directory.GetFiles(
            simulationDir,
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectSimulationEventViolations(file, solutionRoot);
            violations.AddRange(fileViolations);
        }

        // 过滤白名单
        violations = violations
            .Where(v => !NonSimulatedPrefixWhitelist.Contains(v.TypeName))
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD10 违规: 发现 {violations.Count} 个仿真事件类型未使用 Simulated 前缀:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"\n❌ {violation.TypeName}:");
                report.AppendLine($"   位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"   建议名称: {violation.SuggestedName}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD10 规范:");
            report.AppendLine("  Simulation 程序集内的事件载荷类型，名称必须以 Simulated 开头。");
            report.AppendLine("  例如: ParcelDetectedEventArgs -> SimulatedParcelDetectedEventArgs");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将类型重命名为 Simulated* 前缀");
            report.AppendLine("  2. 更新所有引用该类型的代码");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD10: 生成仿真事件命名审计报告
    /// Generate simulation event naming audit report
    /// </summary>
    [Fact]
    public void GenerateSimulationEventNamingAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allSimulationEventTypes = new List<SimulationEventTypeInfo>();

        // 只扫描 Simulation 项目目录下的文件
        var simulationDir = Path.Combine(solutionRoot, "src", "Simulation");
        if (!Directory.Exists(simulationDir))
        {
            Console.WriteLine("Simulation directory not found");
            Assert.True(true, "Simulation directory not found, skipping test");
            return;
        }

        var sourceFiles = Directory.GetFiles(
            simulationDir,
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            var eventTypes = ExtractSimulationEventTypes(file, solutionRoot);
            allSimulationEventTypes.AddRange(eventTypes);
        }

        var report = new StringBuilder();
        report.AppendLine("# 仿真事件命名审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");
        report.AppendLine($"**仿真事件类型数**: {allSimulationEventTypes.Count}\n");

        // 分类统计
        var correctlyPrefixed = allSimulationEventTypes.Where(e => e.HasSimulatedPrefix).ToList();
        var incorrectlyPrefixed = allSimulationEventTypes.Where(e => !e.HasSimulatedPrefix).ToList();

        report.AppendLine("## 统计摘要\n");
        report.AppendLine($"- 使用 Simulated 前缀: {correctlyPrefixed.Count}");
        report.AppendLine($"- 未使用 Simulated 前缀: {incorrectlyPrefixed.Count}");
        report.AppendLine();

        if (correctlyPrefixed.Any())
        {
            report.AppendLine("## ✅ 使用 Simulated 前缀的事件类型\n");
            report.AppendLine("| 类型名 | 位置 |");
            report.AppendLine("|--------|------|");
            foreach (var evt in correctlyPrefixed.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, evt.FilePath);
                report.AppendLine($"| {evt.TypeName} | {relativePath}:{evt.LineNumber} |");
            }
            report.AppendLine();
        }

        if (incorrectlyPrefixed.Any())
        {
            report.AppendLine("## ⚠️ 未使用 Simulated 前缀的事件类型\n");
            report.AppendLine("| 类型名 | 建议名称 | 位置 |");
            report.AppendLine("|--------|----------|------|");
            foreach (var evt in incorrectlyPrefixed.OrderBy(e => e.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, evt.FilePath);
                var suggestedName = GetSuggestedSimulatedName(evt.TypeName);
                report.AppendLine($"| {evt.TypeName} | {suggestedName} | {relativePath}:{evt.LineNumber} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD10 规范，Simulation 程序集内的事件载荷类型必须满足：\n");
        report.AppendLine("1. 类型名必须以 `Simulated` 开头");
        report.AppendLine("2. 避免与 Ingress/Execution 层的真实事件类型同名");

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
    /// 生成建议的仿真事件名称（添加 Simulated 前缀）
    /// </summary>
    private static string GetSuggestedSimulatedName(string typeName)
    {
        return $"{SimulatedPrefix}{typeName}";
    }

    /// <summary>
    /// 检测仿真事件命名违规
    /// </summary>
    private static List<SimulationEventViolation> DetectSimulationEventViolations(string filePath, string solutionRoot)
    {
        var violations = new List<SimulationEventViolation>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = EventPattern.Match(lines[i]);
                if (match.Success)
                {
                    var typeName = match.Groups["name"].Value;
                    
                    // 检查是否以 Simulated 开头
                    if (!typeName.StartsWith(SimulatedPrefix, StringComparison.Ordinal))
                    {
                        violations.Add(new SimulationEventViolation
                        {
                            TypeName = typeName,
                            FilePath = filePath,
                            LineNumber = i + 1,
                            SuggestedName = GetSuggestedSimulatedName(typeName)
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error detecting simulation event violations from {filePath}: {ex.Message}");
        }

        return violations;
    }

    /// <summary>
    /// 提取仿真事件类型信息
    /// </summary>
    private static List<SimulationEventTypeInfo> ExtractSimulationEventTypes(string filePath, string solutionRoot)
    {
        var types = new List<SimulationEventTypeInfo>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = EventPattern.Match(lines[i]);
                if (match.Success)
                {
                    var typeName = match.Groups["name"].Value;
                    var hasSimulatedPrefix = typeName.StartsWith(SimulatedPrefix, StringComparison.Ordinal);

                    types.Add(new SimulationEventTypeInfo
                    {
                        TypeName = typeName,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        HasSimulatedPrefix = hasSimulatedPrefix
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting simulation event types from {filePath}: {ex.Message}");
        }

        return types;
    }

    #endregion
}

/// <summary>
/// 仿真事件命名违规信息
/// </summary>
public record SimulationEventViolation
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
    /// 建议的名称（包含 Simulated 前缀）
    /// </summary>
    public required string SuggestedName { get; init; }
}

/// <summary>
/// 仿真事件类型信息
/// </summary>
public record SimulationEventTypeInfo
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
    /// 是否有 Simulated 前缀
    /// </summary>
    public required bool HasSimulatedPrefix { get; init; }
}
