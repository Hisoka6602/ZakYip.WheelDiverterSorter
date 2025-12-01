using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 日志配置影分身检测测试
/// Tests to detect logging configuration shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. Core 配置模型已有统一的 LoggingConfiguration
/// 2. Application 层存在 ILoggingConfigService / LoggingConfigService
/// 3. Host 层有 LoggingConfigController
/// 4. 不允许额外的 *LoggingConfig* 影分身类型
/// </remarks>
public class LoggingConfigShadowTests
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
    /// 允许的 LoggingConfiguration 相关类型
    /// </summary>
    private static readonly HashSet<string> AllowedLoggingTypes = new(StringComparer.Ordinal)
    {
        // Core
        "LoggingConfiguration",
        "ILoggingConfigurationRepository",
        "LiteDbLoggingConfigurationRepository",
        // Application
        "ILoggingConfigService",
        "LoggingConfigService",
        "LoggingConfigUpdateResult",
        "UpdateLoggingConfigCommand",
        // Host
        "LoggingConfigController",
        "LoggingConfigRequest",
        "LoggingConfigResponse",
        // Observability
        "LogCleanupOptions",
    };

    /// <summary>
    /// 验证使用单一的 LoggingConfiguration 模型
    /// Should use single LoggingConfiguration model
    /// </summary>
    [Fact]
    public void ShouldUseSingleLoggingConfigurationModel()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配 LoggingConfig 或 LogConfig 相关类型
        var loggingConfigPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:LoggingConfig|LogConfig|LogOptions)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = loggingConfigPattern.Matches(content);
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");

            violations.AddRange(
                matches.Cast<Match>()
                    .Select(match => match.Groups["typeName"].Value)
                    .Where(typeName => !AllowedLoggingTypes.Contains(typeName))
                    .Select(typeName => (typeName, relativePath))
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个重复的日志配置模型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"\n❌ {typeName}");
                report.AppendLine($"   位置: {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n允许的日志配置类型:");
            foreach (var allowedType in AllowedLoggingTypes.OrderBy(t => t))
            {
                report.AppendLine($"  - {allowedType}");
            }
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  1. 删除重复的日志配置模型");
            report.AppendLine("  2. 使用 Core 层的 LoggingConfiguration 模型");
            report.AppendLine("  3. 使用 Application 层的 ILoggingConfigService 服务");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成日志配置类型分布报告
    /// </summary>
    [Fact]
    public void GenerateLoggingConfigTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 日志配置类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var loggingPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Logging|Log)(?:Config|Options|Settings)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = loggingPattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match =>
                    {
                        var typeName = match.Groups["typeName"].Value;
                        var isAllowed = AllowedLoggingTypes.Contains(typeName);
                        return (TypeName: typeName, FilePath: relativePath, IsAllowed: isAllowed);
                    });
            })
            .ToList();

        report.AppendLine("## 发现的日志配置类型\n");
        report.AppendLine("| 类型名称 | 位置 | 状态 |");
        report.AppendLine("|----------|------|------|");

        foreach (var (typeName, filePath, isAllowed) in foundTypes.OrderBy(t => t.FilePath))
        {
            var status = isAllowed ? "✅ 允许" : "❌ 未授权";
            report.AppendLine($"| {typeName} | {filePath} | {status} |");
        }

        report.AppendLine("\n## 规范说明\n");
        report.AppendLine("根据 PR-SD8 规范，日志配置只允许以下位置：");
        report.AppendLine("- **Core**: LoggingConfiguration 模型");
        report.AppendLine("- **Application**: ILoggingConfigService 服务");
        report.AppendLine("- **Host**: LoggingConfigController API");

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
