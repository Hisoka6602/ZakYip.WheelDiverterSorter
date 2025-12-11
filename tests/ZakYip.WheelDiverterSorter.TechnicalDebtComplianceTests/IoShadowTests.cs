using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: IO/传感器影分身检测测试
/// Tests to detect IO/Sensor shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. IO/HAL 统一入口在 Core/Hardware/Ports：IInputPort, IOutputPort
/// 2. ISensorVendorConfigProvider 接口在 Core/Hardware/Providers/
/// 3. 厂商 SDK 调用只能在 Drivers 层
/// </remarks>
public class IoShadowTests
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
    /// 厂商 SDK 命名空间前缀（用于检测非 Drivers 项目中的直接引用）
    /// 只检测 using 语句，避免误报注释和文档
    /// </summary>
    private static readonly string[] VendorSdkNamespaces =
    {
        "using LeadShine",      // 雷赛 SDK
        "using DMC1380",        // 雷赛板卡
        "using LTDMC",          // 雷赛运动控制
        "using Siemens.S7",     // 西门子 S7 SDK
        "using S7.Net",         // S7 通信库
    };

    /// <summary>
    /// 验证非 Drivers 项目不直接引用厂商 IO API
    /// Non-Drivers projects should not reference vendor IO APIs
    /// </summary>
    [Fact]
    public void NonDriversProjectsShouldNotReferenceVendorIoApis()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string FilePath, string UsagePattern)>();

        // 排除 Drivers 项目
        var nonDriversProjects = new[] { "Core", "Execution", "Ingress", "Host", "Application", "Observability", "Simulation" };

        foreach (var project in nonDriversProjects)
        {
            var projectPath = Path.Combine(solutionRoot, "src", project);
            if (!Directory.Exists(projectPath))
            {
                continue;
            }

            var sourceFiles = Directory.GetFiles(projectPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsInExcludedDirectory(f))
                .ToList();

            violations.AddRange(
                sourceFiles
                    .SelectMany(file =>
                    {
                        var content = File.ReadAllText(file);
                        var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                        return VendorSdkNamespaces
                            .Where(sdkNamespace => content.Contains(sdkNamespace, StringComparison.OrdinalIgnoreCase))
                            .Select(sdkNamespace => (relativePath, $"引用厂商 SDK: {sdkNamespace}"));
                    })
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 处非 Drivers 项目中的厂商 IO API 引用:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (filePath, usagePattern) in violations.Take(20))
            {
                report.AppendLine($"\n⚠️ {filePath}");
                report.AppendLine($"   {usagePattern}");
            }

            if (violations.Count > 20)
            {
                report.AppendLine($"\n... 还有 {violations.Count - 20} 处");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  厂商 SDK 调用必须收敛到 Drivers 层：");
            report.AppendLine("  1. 将直接 SDK 调用移动到 Drivers/Vendors/<VendorName>/");
            report.AppendLine("  2. 实现 IInputPort 或 ISensorInputReader 接口");
            report.AppendLine("  3. 其他层通过 HAL 接口获取 IO 状态");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 IO 相关 HAL 接口只在 Core/Hardware 中定义
    /// IO HAL interfaces should only be defined in Core/Hardware
    /// </summary>
    [Fact]
    public void IoHalInterfacesShouldOnlyBeDefinedInCoreHardware()
    {
        var solutionRoot = GetSolutionRoot();
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/Hardware";

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // IO HAL 接口定义模式
        var ioHalInterfacePattern = new Regex(
            @"(?:public|internal)\s+interface\s+(?<interfaceName>IInputPort|IOutputPort|ISensorInputReader|IIoLinkageDriver)\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = sourceFiles
            .Where(file => !file.Replace("\\", "/").Contains(allowedPath))
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = ioHalInterfacePattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match => (match.Groups["interfaceName"].Value, relativePath));
            })
            .ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个在 Core/Hardware 目录外定义的 IO HAL 接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (interfaceName, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {interfaceName}");
                report.AppendLine($"     {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 修复建议:");
            report.AppendLine("  IO HAL 接口必须统一定义在 Core/Hardware/ 目录下：");
            report.AppendLine("  - IInputPort（Core/Hardware/Ports/）");
            report.AppendLine("  - IOutputPort（Core/Hardware/Ports/）");
            report.AppendLine("  - ISensorInputReader（Core/Hardware/Providers/）");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成 IO/传感器类型分布报告
    /// </summary>
    [Fact]
    public void GenerateIoSensorTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: IO/传感器类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配包含 Sensor, Input, Output, Port, Io 的接口和类型
        var ioPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>I?(?:Sensor|Input|Output)(?:Port|Reader|Writer|Provider|Driver|Factory)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = ioPattern.Matches(content);
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
        report.AppendLine("- IO HAL 接口只能在 Core/Hardware/ 定义");
        report.AppendLine("- 厂商 SDK 调用只能在 Drivers/Vendors/ 中");
        report.AppendLine("- ISensorVendorConfigProvider 在 Core/Hardware/Providers/");

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
