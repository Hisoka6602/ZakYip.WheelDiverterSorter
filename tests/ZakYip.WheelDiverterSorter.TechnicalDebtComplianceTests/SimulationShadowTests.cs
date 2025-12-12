using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 仿真影分身检测测试
/// Tests to detect simulation shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. 仿真主体集中在 ZakYip.WheelDiverterSorter.Simulation（Library）
/// 2. 命令行入口在 Simulation.Cli
/// 3. Host 层只存在 SimulationConfigController / SimulationController
/// 4. 禁止在 Host/Execution 中实现重复的"轻量仿真"
/// </remarks>
public class SimulationShadowTests
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
    /// 允许的仿真类型命名模式及其允许位置
    /// </summary>
    private static readonly Dictionary<string, string[]> AllowedSimulationTypeLocations = new()
    {
        // Simulation 项目中允许的类型
        ["SimulationRunner"] = new[] { "Simulation/" },
        ["SimulationScenarioRunner"] = new[] { "Simulation/" },
        ["ISimulationScenarioRunner"] = new[] { "Simulation/", "Application/" },
        ["Simulator"] = new[] { "Simulation/", "Drivers/Vendors/Simulated/" },
        ["SimulationEngine"] = new[] { "Simulation/" },
        ["SimulatedParcelResultEventArgs"] = new[] { "Simulation/" },
        // Host 中允许的 Controller
        ["SimulationConfigController"] = new[] { "Host/" },
        ["SimulationController"] = new[] { "Host/" },
        // Host 中允许的 DTO
        ["SimulationStatus"] = new[] { "Host/" },
        ["TopologySimulationResult"] = new[] { "Host/" },
        ["SimulationStep"] = new[] { "Host/" },
        // Application 中允许的服务
        ["SimulationModeProvider"] = new[] { "Application/" },
        // Observability 中允许的接口
        ["ISimulationReportWriter"] = new[] { "Observability/" },
        // Drivers/Simulated 中允许的仿真驱动
        ["SimulatedWheelDiverterDevice"] = new[] { "Drivers/Vendors/Simulated/" },
        ["SimulatedConveyorSegmentDriver"] = new[] { "Drivers/Vendors/Simulated/" },
        ["SimulatedSensor"] = new[] { "Drivers/Vendors/Simulated/", "Ingress/" },
        ["SimulatedSensorFactory"] = new[] { "Drivers/Vendors/Simulated/", "Ingress/" },
    };

    /// <summary>
    /// 验证非 Simulation 项目不定义仿真引擎类型
    /// Non-Simulation projects should not define simulation engines
    /// </summary>
    [Fact]
    public void NonSimulationProjectsShouldNotDefineSimulationEngines()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath, string Reason)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配仿真相关类型
        var simulationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Simulation|Simulator|FakeSorter|DryRun|InlineSimulation)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            var content = File.ReadAllText(file);
            var matches = simulationPattern.Matches(content);

            violations.AddRange(
                matches.Cast<Match>()
                    .Select(match => match.Groups["typeName"].Value)
                    .Where(typeName =>
                        // 检查是否在允许列表中且在正确位置，或在 Simulation 项目中
                        !(AllowedSimulationTypeLocations.TryGetValue(typeName, out var allowedPaths) &&
                          allowedPaths.Any(p => relativePath.Contains(p, StringComparison.OrdinalIgnoreCase)))
                        && !relativePath.Contains("Simulation/")
                        && !relativePath.Contains("Drivers/Vendors/Simulated/")
                        // 排除一些常见的非仿真类型
                        && !(typeName.Contains("Request") || typeName.Contains("Response") ||
                             typeName.Contains("Config") || typeName.Contains("Options") ||
                             typeName.Contains("DTO") || typeName.Contains("Dto"))
                    )
                    .Select(typeName => (typeName, relativePath, "仿真引擎/服务应该在 Simulation 项目中"))
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个非法位置的仿真类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, reason) in violations.Take(20))
            {
                report.AppendLine($"\n❌ {typeName}");
                report.AppendLine($"   位置: {filePath}");
                report.AppendLine($"   原因: {reason}");
            }

            if (violations.Count > 20)
            {
                report.AppendLine($"\n... 还有 {violations.Count - 20} 处");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  仿真引擎的单一事实源在 Simulation 项目中：");
            report.AppendLine("  1. 将仿真逻辑移动到 ZakYip.WheelDiverterSorter.Simulation");
            report.AppendLine("  2. Host/Application 只通过接口调用仿真服务");
            report.AppendLine("  3. 仿真驱动放在 Drivers/Vendors/Simulated/");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成仿真类型分布报告
    /// </summary>
    [Fact]
    public void GenerateSimulationTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 仿真类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var simulationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Simulation|Simulator|Simulated)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = simulationPattern.Matches(content);
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
            var isSimulationProject = group.Key.Contains("Simulation") || group.Key.Contains("Simulated");
            var marker = isSimulationProject ? "✅" : "⚠️";
            report.AppendLine($"## {marker} {group.Key}\n");
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
        report.AppendLine("- 仿真引擎只能在 Simulation 项目中定义");
        report.AppendLine("- 仿真驱动只能在 Drivers/Vendors/Simulated/ 中定义");
        report.AppendLine("- Host 只有 SimulationConfigController 和 SimulationController");
        report.AppendLine("- Application 只有 SimulationModeProvider 和接口");

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
