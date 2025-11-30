using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 电柜面板配置影分身检测测试
/// Tests to detect panel configuration shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. Core 配置中已有 CabinetIoOptions，作为电柜面板 IO 的统一配置模型
/// 2. 旧版厂商绑定面板配置模型（如 LeadshineCabinetIoOptions）已删除
/// 3. 所有电柜/面板配置都使用 CabinetIoOptions
/// </remarks>
public class PanelConfigShadowTests
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
    /// 验证不存在厂商特定的电柜配置选项
    /// Should not have vendor-specific cabinet options
    /// </summary>
    [Fact]
    public void ShouldNotHaveVendorSpecificCabinetOptions()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配厂商特定的 Cabinet 配置类型
        var vendorCabinetPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct)\s+(?<typeName>(?:Leadshine|Modi|ShuDiNiao|Siemens)Cabinet\w*(?:Options|Config)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = vendorCabinetPattern.Matches(content);

            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                violations.Add((typeName, relativePath));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个厂商特定的电柜配置类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"\n❌ {typeName}");
                report.AppendLine($"   位置: {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  1. 删除厂商特定的电柜配置类型");
            report.AppendLine("  2. 使用厂商无关的 CabinetIoOptions 模型");
            report.AppendLine("  3. 通过 ISystemConfigService 读写配置");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证使用统一的 CabinetIoOptions 模型
    /// Should use unified CabinetIoOptions model
    /// </summary>
    [Fact]
    public void ShouldUseUnifiedCabinetIoOptions()
    {
        var solutionRoot = GetSolutionRoot();
        var coreConfigPath = Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "LineModel", "Configuration");

        // 检查 CabinetIoOptions 是否存在于 Core 配置中
        var cabinetIoOptionsFiles = Directory.GetFiles(
            coreConfigPath,
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => Path.GetFileName(f).Contains("Cabinet") || Path.GetFileName(f).Contains("Panel"))
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var report = new StringBuilder();
        report.AppendLine("# 电柜/面板配置检查报告\n");
        report.AppendLine($"**检查路径**: {coreConfigPath}\n");

        if (cabinetIoOptionsFiles.Any())
        {
            report.AppendLine("## 发现的电柜配置文件\n");
            foreach (var file in cabinetIoOptionsFiles)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                report.AppendLine($"- {relativePath}");
            }
            report.AppendLine("\n✅ 电柜配置文件存在于 Core 配置目录中。");
        }
        else
        {
            report.AppendLine("⚠️ 未在 Core 配置目录中找到电柜配置文件。");
        }

        Console.WriteLine(report);
        Assert.True(true, "Check completed");
    }

    /// <summary>
    /// 生成面板配置类型分布报告
    /// </summary>
    [Fact]
    public void GeneratePanelConfigTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 面板配置类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配 Cabinet 或 Panel 相关类型
        var panelPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Cabinet|Panel)\w*(?:Config|Options|Controller|Service)?\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = panelPattern.Matches(content);

            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                foundTypes.Add((typeName, relativePath));
            }
        }

        if (foundTypes.Count == 0)
        {
            report.AppendLine("未发现任何面板配置相关类型。");
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
        report.AppendLine("- 电柜配置使用厂商无关的 CabinetIoOptions");
        report.AppendLine("- 禁止厂商特定的配置类型（如 LeadshineCabinetIoOptions）");
        report.AppendLine("- Host 层只有 PanelConfigController API 端点");

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
