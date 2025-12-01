using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 路径生成单一事实源测试
/// Tests to ensure single source of truth for switching path generation
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. ISwitchingPathGenerator 只有两个实现：
///    - DefaultSwitchingPathGenerator（Core）
///    - CachedSwitchingPathGenerator（Application 层 Decorator）
/// 2. Execution/Simulation 中只允许使用 ISwitchingPathGenerator 作为依赖
/// 3. 禁止在 Execution 中直接从 ChutePathTopologyConfig 读取并拼装摆轮指令
/// </remarks>
public class SwitchingPathGenerationTests
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
    /// 允许的 ISwitchingPathGenerator 实现
    /// </summary>
    private static readonly HashSet<string> AllowedImplementations = new(StringComparer.Ordinal)
    {
        "DefaultSwitchingPathGenerator",
        "CachedSwitchingPathGenerator",
    };

    /// <summary>
    /// 验证 ISwitchingPathGenerator 只有允许的实现
    /// Should have single source of truth for ISwitchingPathGenerator
    /// </summary>
    [Fact]
    public void ShouldHaveSingleSourceOfTruth()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配实现 ISwitchingPathGenerator 的类型
        var implementationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<typeName>\w+)\s*:\s*[^{]*\bISwitchingPathGenerator\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = implementationPattern.Matches(content);
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");

            violations.AddRange(
                matches.Cast<Match>()
                    .Select(match => match.Groups["typeName"].Value)
                    .Where(typeName => !AllowedImplementations.Contains(typeName))
                    .Select(typeName => (typeName, relativePath))
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个未经授权的 ISwitchingPathGenerator 实现:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"\n❌ {typeName}");
                report.AppendLine($"   位置: {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD8 规范:");
            report.AppendLine("  ISwitchingPathGenerator 只允许以下实现：");
            foreach (var impl in AllowedImplementations)
            {
                report.AppendLine($"  - {impl}");
            }
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 删除未经授权的实现类");
            report.AppendLine("  2. 改为使用 ISwitchingPathGenerator 接口依赖注入");
            report.AppendLine("  3. 如果需要装饰器，继承 CachedSwitchingPathGenerator 或创建新的 Decorator");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 DefaultSwitchingPathGenerator 只存在于 Core 层
    /// DefaultSwitchingPathGenerator should only exist in Core
    /// </summary>
    [Fact]
    public void DefaultSwitchingPathGeneratorShouldOnlyExistInCore()
    {
        var solutionRoot = GetSolutionRoot();
        var corePathPattern = "Core/ZakYip.WheelDiverterSorter.Core/";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "DefaultSwitchingPathGenerator.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = sourceFiles
            .Select(f => Path.GetRelativePath(solutionRoot, f).Replace("\\", "/"))
            .Where(p => !p.Contains(corePathPattern))
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ DefaultSwitchingPathGenerator 不应该存在于 Core 层之外:");
            foreach (var path in violations)
            {
                report.AppendLine($"  - {path}");
            }
            report.AppendLine("\n期望位置: Core/ZakYip.WheelDiverterSorter.Core/LineModel/Topology/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 CachedSwitchingPathGenerator 只存在于 Application 层
    /// CachedSwitchingPathGenerator should only exist in Application
    /// </summary>
    [Fact]
    public void CachedSwitchingPathGeneratorShouldOnlyExistInApplication()
    {
        var solutionRoot = GetSolutionRoot();
        var applicationPathPattern = "Application/ZakYip.WheelDiverterSorter.Application/";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "CachedSwitchingPathGenerator.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = sourceFiles
            .Select(f => Path.GetRelativePath(solutionRoot, f).Replace("\\", "/"))
            .Where(p => !p.Contains(applicationPathPattern))
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ CachedSwitchingPathGenerator 不应该存在于 Application 层之外:");
            foreach (var path in violations)
            {
                report.AppendLine($"  - {path}");
            }
            report.AppendLine("\n期望位置: Application/ZakYip.WheelDiverterSorter.Application/Services/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成路径生成器实现分布报告
    /// Generate switching path generator implementation distribution report
    /// </summary>
    [Fact]
    public void GenerateSwitchingPathGeneratorDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 路径生成器实现分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 搜索包含 PathGenerator 或实现 ISwitchingPathGenerator 的类型
        var pathGeneratorPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<typeName>\w*PathGenerator\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = pathGeneratorPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                var implementsInterface = content.Contains("ISwitchingPathGenerator");
                return matches.Cast<Match>()
                    .Select(match => (TypeName: match.Groups["typeName"].Value, FilePath: relativePath, ImplementsInterface: implementsInterface));
            })
            .ToList();

        report.AppendLine("## 发现的 PathGenerator 类型\n");
        report.AppendLine("| 类型名称 | 位置 | 实现 ISwitchingPathGenerator | 状态 |");
        report.AppendLine("|----------|------|------------------------------|------|");

        foreach (var (typeName, filePath, implementsInterface) in foundTypes.OrderBy(t => t.FilePath))
        {
            var isAllowed = AllowedImplementations.Contains(typeName);
            var status = isAllowed ? "✅ 允许" : (implementsInterface ? "❌ 未授权" : "⚠️ 检查");
            report.AppendLine($"| {typeName} | {filePath} | {(implementsInterface ? "是" : "否")} | {status} |");
        }

        report.AppendLine("\n## 规范说明\n");
        report.AppendLine("**允许的实现**：");
        foreach (var impl in AllowedImplementations)
        {
            report.AppendLine($"- `{impl}`");
        }
        report.AppendLine("\n**位置要求**：");
        report.AppendLine("- `DefaultSwitchingPathGenerator` → Core/LineModel/Topology/");
        report.AppendLine("- `CachedSwitchingPathGenerator` → Application/Services/");

        Console.WriteLine(report);
        Assert.True(true, "Report generated successfully");
    }

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }
}
