using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 摆轮控制器影分身检测测试
/// Tests to detect wheel diverter controller shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. HAL 接口统一在 Core/Hardware 目录下定义
/// 2. 所有摆轮实现必须命名为 WheelDiverterDriver 或 WheelDiverterDevice
/// 3. 禁止使用 *DiverterController 命名
/// 4. 禁止存在 IDiverterController 接口
/// </remarks>
public class WheelDiverterShadowTests
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
    /// 验证不存在 DiverterController 影分身类型
    /// Should not have DiverterController shadow types
    /// </summary>
    /// <remarks>
    /// PR-SD8: 断言没有类型名包含 DiverterController 或包含 Wheel + Controller
    /// 且不在 Drivers 中实现 HAL。
    /// </remarks>
    [Fact]
    public void ShouldNotHaveDiverterControllerShadows()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配以 DiverterController 结尾的类型定义（包括接口）
        // 但排除 Swagger/文档相关的 Controller
        var diverterControllerPattern = new Regex(
            @"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*DiverterController)\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = diverterControllerPattern.Matches(content);
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");

            violations.AddRange(
                matches.Cast<Match>()
                    .Select(match => match.Groups["typeName"].Value)
                    .Where(typeName =>
                        !typeName.Contains("DocumentFilter") &&
                        !typeName.Contains("Swagger") &&
                        !typeName.Contains("Api"))
                    .Select(typeName => (typeName, relativePath))
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个禁止的 *DiverterController 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  所有摆轮实现必须统一命名为：");
            report.AppendLine("  - <VendorName>WheelDiverterDriver（实现 IWheelDiverterDriver）");
            report.AppendLine("  - <VendorName>WheelDiverterDevice（实现 IWheelDiverterDevice）");
            report.AppendLine("  禁止使用 *DiverterController 命名。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证不存在 IDiverterController 接口（禁止存在）
    /// Should not have IDiverterController interface
    /// </summary>
    [Fact]
    public void ShouldNotHaveIDiverterControllerInterface()
    {
        var solutionRoot = GetSolutionRoot();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var interfacePattern = new Regex(
            @"(?:public|internal)\s+interface\s+IDiverterController\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
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
            report.AppendLine("\n❌ 发现禁止的 IDiverterController 接口定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var filePath in violations)
            {
                report.AppendLine($"  📄 {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  HAL 接口已统一到 Core/Hardware/：");
            report.AppendLine("  - IWheelDiverterDevice（Core/Hardware/）- 基于命令的设备接口");
            report.AppendLine("  - IWheelDiverterDriver（Core/Hardware/Devices/）- 基于方向的驱动接口");
            report.AppendLine("  请删除 IDiverterController 接口并使用上述接口。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成摆轮类型分布报告
    /// Generate wheel diverter type distribution report
    /// </summary>
    [Fact]
    public void GenerateWheelDiverterTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 摆轮类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配包含 WheelDiverter 或 Diverter 的类型定义
        var wheelDiverterPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:WheelDiverter|Diverter)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = wheelDiverterPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (TypeName: match.Groups["typeName"].Value, FilePath: relativePath));
            })
            .ToList();

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
        report.AppendLine("- HAL 接口只能在 Core/Hardware/ 定义");
        report.AppendLine("- 厂商实现只能在 Drivers/Vendors/ 定义");
        report.AppendLine("- 禁止使用 *DiverterController 命名");

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
