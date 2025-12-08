using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 面板 IO 影分身检测测试
/// Tests to detect Panel IO shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. IPanelInputReader 接口在 Core/LineModel/Bindings/
/// 2. IPanelIoCoordinator 接口在 Core/LineModel/Bindings/
/// 3. 实现类必须在 Core 或 Drivers/Vendors/Simulated（仿真）
/// 4. 禁止在其他项目中重复定义面板相关接口或实现
/// </remarks>
public class PanelIoShadowTests
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
    /// 验证 IPanelInputReader 接口只在 Core/LineModel/Bindings 中定义
    /// IPanelInputReader interface should only be defined in Core/LineModel/Bindings
    /// </summary>
    [Fact]
    public void IPanelInputReaderShouldOnlyBeDefinedInCoreLineModelBindings()
    {
        var solutionRoot = GetSolutionRoot();
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // IPanelInputReader 接口定义模式
        var panelInputReaderPattern = new Regex(
            @"(?:public|internal)\s+interface\s+IPanelInputReader\b",
            RegexOptions.Compiled);

        var violations = sourceFiles
            .Where(file => !file.Replace("\\", "/").Contains(allowedPath))
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = panelInputReaderPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(_ => relativePath);
            })
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 违规: 发现 {violations.Count} 个在权威位置外定义的 IPanelInputReader 接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var filePath in violations)
            {
                report.AppendLine($"  ⚠️ {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  IPanelInputReader 接口必须统一定义在：");
            report.AppendLine("  - Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 IPanelIoCoordinator 接口只在 Core/LineModel/Bindings 中定义
    /// IPanelIoCoordinator interface should only be defined in Core/LineModel/Bindings
    /// </summary>
    [Fact]
    public void IPanelIoCoordinatorShouldOnlyBeDefinedInCoreLineModelBindings()
    {
        var solutionRoot = GetSolutionRoot();
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // IPanelIoCoordinator 接口定义模式
        var panelIoCoordinatorPattern = new Regex(
            @"(?:public|internal)\s+interface\s+IPanelIoCoordinator\b",
            RegexOptions.Compiled);

        var violations = sourceFiles
            .Where(file => !file.Replace("\\", "/").Contains(allowedPath))
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = panelIoCoordinatorPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(_ => relativePath);
            })
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 违规: 发现 {violations.Count} 个在权威位置外定义的 IPanelIoCoordinator 接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var filePath in violations)
            {
                report.AppendLine($"  ⚠️ {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  IPanelIoCoordinator 接口必须统一定义在：");
            report.AppendLine("  - Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 IPanelInputReader 实现只在允许的位置
    /// IPanelInputReader implementations should only be in allowed locations
    /// </summary>
    [Fact]
    public void IPanelInputReaderImplementationsShouldOnlyBeInAllowedLocations()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 允许的实现位置：
        // 1. Core/LineModel/Bindings（默认实现）
        // 2. Drivers/Vendors/Simulated（仿真实现）
        // 3. Drivers/Vendors/<VendorName>（厂商实现）
        var allowedPatterns = new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings",
            "Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors"
        };

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // IPanelInputReader 实现类模式
        var implementationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?class\s+(?<className>\w+)\s*:\s*.*IPanelInputReader",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = sourceFiles
            .Where(file =>
            {
                var normalizedPath = file.Replace("\\", "/");
                return !allowedPatterns.Any(pattern => normalizedPath.Contains(pattern));
            })
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = implementationPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (ClassName: match.Groups["className"].Value, FilePath: relativePath));
            })
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 违规: 发现 {violations.Count} 个在非允许位置的 IPanelInputReader 实现:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (className, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {className}");
                report.AppendLine($"     {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  IPanelInputReader 实现必须放在以下位置之一：");
            report.AppendLine("  - Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings/（默认实现）");
            report.AppendLine("  - Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/<VendorName>/（厂商实现）");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 IPanelIoCoordinator 实现只在 Core/LineModel/Bindings 中
    /// IPanelIoCoordinator implementations should only be in Core/LineModel/Bindings
    /// </summary>
    [Fact]
    public void IPanelIoCoordinatorImplementationsShouldOnlyBeInCore()
    {
        var solutionRoot = GetSolutionRoot();
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // IPanelIoCoordinator 实现类模式
        var implementationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?class\s+(?<className>\w+)\s*:\s*.*IPanelIoCoordinator",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = sourceFiles
            .Where(file => !file.Replace("\\", "/").Contains(allowedPath))
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = implementationPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (ClassName: match.Groups["className"].Value, FilePath: relativePath));
            })
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 违规: 发现 {violations.Count} 个在非允许位置的 IPanelIoCoordinator 实现:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (className, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {className}");
                report.AppendLine($"     {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  IPanelIoCoordinator 实现必须统一定义在：");
            report.AppendLine("  - Core/ZakYip.WheelDiverterSorter.Core/LineModel/Bindings/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成面板 IO 类型分布报告
    /// Generate Panel IO type distribution report
    /// </summary>
    [Fact]
    public void GeneratePanelIoTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# 面板 IO 类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配面板相关的接口和类型
        var panelPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>I?(?:Panel|SignalTower)\w+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = panelPattern.Matches(content);
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
        report.AppendLine("根据架构规范：");
        report.AppendLine("- IPanelInputReader 接口在 Core/LineModel/Bindings/");
        report.AppendLine("- IPanelIoCoordinator 接口在 Core/LineModel/Bindings/");
        report.AppendLine("- 实现类在 Core 或 Drivers/Vendors/");

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
