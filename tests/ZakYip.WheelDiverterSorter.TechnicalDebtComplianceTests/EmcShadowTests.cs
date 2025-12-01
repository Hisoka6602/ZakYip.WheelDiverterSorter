using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: EMC 控制器影分身检测测试
/// Tests to detect EMC controller shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. IEmcController 接口仅在 Core.Hardware 中定义
/// 2. 具体实现及厂商特殊逻辑只允许出现在 Drivers/Vendors 下
/// 3. Execution 应该只依赖 IEmcController 接口，不声明具体实现类型
/// </remarks>
public class EmcShadowTests
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
    /// 验证 Execution 层不声明 EmcController 具体类型
    /// Execution should not declare EmcController concrete types
    /// </summary>
    [Fact]
    public void ExecutionShouldNotDeclareEmcControllerConcreteTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var executionPath = Path.Combine(solutionRoot, "src", "Execution");

        if (!Directory.Exists(executionPath))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(executionPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = new List<(string TypeName, string FilePath)>();

        // 匹配 EmcController 类型定义
        var emcControllerPattern = new Regex(
            @"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct)\s+(?<typeName>\w*Emc\w*Controller\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations2 = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = emcControllerPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (match.Groups["typeName"].Value, relativePath));
            })
            .ToList();
        violations.AddRange(violations2);

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 在 Execution 层发现 {violations.Count} 个 EmcController 具体类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  Execution 层应该只依赖 IEmcController 接口：");
            report.AppendLine("  - 删除 Execution 中的具体 EmcController 类型");
            report.AppendLine("  - 改为通过依赖注入获取 IEmcController");
            report.AppendLine("  - 具体实现移动到 Drivers/Vendors/ 目录");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 IEmcController 只在 Core/Hardware 中定义
    /// IEmcController should only be defined in Core/Hardware
    /// </summary>
    [Fact]
    public void IEmcControllerShouldOnlyBeDefinedInCoreHardware()
    {
        var solutionRoot = GetSolutionRoot();
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/Hardware";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var interfacePattern = new Regex(
            @"(?:public|internal)\s+interface\s+IEmcController\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var normalizedPath = file.Replace("\\", "/");

            // 如果文件在允许的路径中，跳过
            if (normalizedPath.Contains(allowedPath))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            if (interfacePattern.IsMatch(content))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                violations.Add(relativePath);
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现在 Core/Hardware 目录外定义的 IEmcController 接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var filePath in violations)
            {
                report.AppendLine($"  📄 {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  IEmcController 接口必须定义在 Core/Hardware/ 目录下。");
            report.AppendLine("  请删除其他位置的 IEmcController 定义。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成 EMC 类型分布报告
    /// </summary>
    [Fact]
    public void GenerateEmcTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: EMC 类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配包含 Emc 的类型定义
        var emcPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*Emc\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = emcPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (TypeName: match.Groups["typeName"].Value, FilePath: relativePath));
            })
            .ToList();

        if (foundTypes.Count == 0)
        {
            report.AppendLine("未发现任何 EMC 相关类型。");
            Console.WriteLine(report);
            Assert.True(true);
            return;
        }

        // 按项目分组
        var byProject = foundTypes
            .GroupBy(t =>
            {
                var parts = t.FilePath.Split('/');
                return parts.Length >= 3 ? parts[1] : "Unknown";
            })
            .OrderBy(g => g.Key);

        foreach (var group in byProject)
        {
            report.AppendLine($"## {group.Key}\n");
            report.AppendLine("| 类型名称 | 位置 |");
            report.AppendLine("|----------|------|");

            foreach (var (typeName, filePath) in group.OrderBy(t => t.TypeName))
            {
                report.AppendLine($"| {typeName} | {filePath} |");
            }
            report.AppendLine();
        }

        report.AppendLine("## 规范说明\n");
        report.AppendLine("根据 PR-SD8 规范：");
        report.AppendLine("- IEmcController 接口只能在 Core/Hardware/ 定义");
        report.AppendLine("- 具体实现只能在 Drivers/Vendors/ 定义");
        report.AppendLine("- Execution 只能通过依赖注入使用 IEmcController");

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
