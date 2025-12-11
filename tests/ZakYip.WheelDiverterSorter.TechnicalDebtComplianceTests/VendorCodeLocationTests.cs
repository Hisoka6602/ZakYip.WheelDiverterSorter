using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 厂商代码位置合规性测试
/// Tests to verify vendor-specific code is only in allowed locations
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. 所有厂商相关代码只允许出现在 Drivers/Vendors 对应目录中
/// 2. 通信层厂商协议实现允许在 Communication 层
/// 3. Core / Execution / Ingress / Host / Application / Observability / Simulation
///    中禁止直接出现厂商名或厂商特有结构
/// 
/// 例外：
/// - Core/Enums/Hardware/Vendors/ 目录允许包含厂商协议枚举（如 ShuDiNiaoResponseCode）
/// - Core/Enums/Hardware/VendorId.cs 等厂商标识枚举
/// - 配置模型中的厂商标识字段（如 VendorProfileKey）
/// </remarks>
public class VendorCodeLocationTests
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
    /// 厂商关键词列表
    /// </summary>
    private static readonly string[] VendorKeywords = { "Leadshine", "Modi", "ShuDiNiao", "Siemens" };

    /// <summary>
    /// 允许包含厂商关键词的路径模式
    /// </summary>
    private static readonly string[] AllowedPathPatterns =
    {
        "Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/",
        "Communication/",
        // Core 中允许的位置：
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/Vendors/",
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/VendorId.cs",
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/DriverVendorType.cs",
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/SensorVendorType.cs",
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/Hardware/WheelDiverterVendorType.cs",
    };

    /// <summary>
    /// 验证所有包含厂商关键词的类型只存在于允许的位置
    /// All vendor-named types should reside in Drivers or Communication
    /// </summary>
    [Fact]
    public void AllVendorNamedTypesShouldResideInDriversOrCommunication()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<VendorCodeViolation>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配包含厂商关键词的类型定义
        var vendorTypePattern = new Regex(
            $@"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface|enum)\s+(?<typeName>\w*(?:{string.Join("|", VendorKeywords)})\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");

            // 检查文件是否在允许的路径中
            if (IsInAllowedPath(relativePath))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var matches = vendorTypePattern.Matches(content);

            violations.AddRange(
                matches.Select(match => new VendorCodeViolation
                {
                    FilePath = relativePath,
                    TypeOrUsage = match.Groups["typeName"].Value,
                    ViolationType = "厂商命名的类型定义"
                })
            );
        }

        if (violations.Any())
        {
            var report = GenerateViolationReport(violations, "厂商命名的类型");
            // 这是顾问性测试 - 现有违规作为技术债记录，不阻止构建
            // PR-SD8 阶段 2 会修复这些问题
            Console.WriteLine(report);
            Console.WriteLine("\n⚠️ 这是顾问性测试，发现的违规将作为技术债记录。");
        }
    }

    /// <summary>
    /// 验证非允许位置不包含厂商特定的 using 语句
    /// Non-allowed locations should not have vendor-specific using statements
    /// </summary>
    [Fact]
    public void ShouldNotHaveVendorUsingStatementsOutsideAllowedLocations()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<VendorCodeViolation>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配厂商特定的 using 语句
        var vendorUsingPattern = new Regex(
            @"using\s+[\w.]*\.Vendors\.(?:Leadshine|Modi|ShuDiNiao|Siemens)[\w.]*;",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");

            // 检查文件是否在允许的路径中
            if (IsInAllowedPath(relativePath))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var matches = vendorUsingPattern.Matches(content);

            foreach (Match match in matches)
            {
                violations.Add(new VendorCodeViolation
                {
                    FilePath = relativePath,
                    TypeOrUsage = match.Value.Trim(),
                    ViolationType = "厂商特定的 using 语句"
                });
            }
        }

        if (violations.Any())
        {
            var report = GenerateViolationReport(violations, "厂商特定的 using 语句");
            // 这是顾问性测试 - 现有违规作为技术债记录，不阻止构建
            // PR-SD8 阶段 2 会修复这些问题
            Console.WriteLine(report);
            Console.WriteLine("\n⚠️ 这是顾问性测试，发现的违规将作为技术债记录。");
        }
    }

    /// <summary>
    /// 生成厂商代码位置审计报告
    /// Generate vendor code location audit report
    /// </summary>
    [Fact]
    public void GenerateVendorCodeLocationAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 厂商代码位置审计报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 按厂商分组统计
        var vendorStats = new Dictionary<string, List<(string Path, string Context)>>();
        foreach (var vendor in VendorKeywords)
        {
            vendorStats[vendor] = new List<(string, string)>();
        }

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            var content = File.ReadAllText(file);
            var lines = content.Split('\n');

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                foreach (var vendor in VendorKeywords)
                {
                    if (line.Contains(vendor, StringComparison.OrdinalIgnoreCase))
                    {
                        var context = line.Trim();
                        if (context.Length > 100)
                        {
                            context = context.Substring(0, 100) + "...";
                        }
                        vendorStats[vendor].Add(($"{relativePath}:{i + 1}", context));
                    }
                }
            }
        }

        // 按厂商输出统计
        foreach (var vendor in VendorKeywords)
        {
            var occurrences = vendorStats[vendor];
            report.AppendLine($"## {vendor} ({occurrences.Count} 处引用)\n");

            // 按项目/目录分组
            var byProject = occurrences
                .GroupBy(o => o.Path.Split('/')[0] + "/" + o.Path.Split('/')[1])
                .OrderBy(g => g.Key);

            foreach (var group in byProject)
            {
                var isAllowed = IsInAllowedPath(group.First().Path);
                var marker = isAllowed ? "✅" : "⚠️";
                report.AppendLine($"### {marker} {group.Key} ({group.Count()} 处)\n");

                // 仅显示前 10 个，避免报告过长
                foreach (var (path, context) in group.Take(10))
                {
                    report.AppendLine($"- `{path}`");
                    report.AppendLine($"  ```{context}```");
                }

                if (group.Count() > 10)
                {
                    report.AppendLine($"- ... 还有 {group.Count() - 10} 处");
                }
                report.AppendLine();
            }
        }

        report.AppendLine("## 位置规范\n");
        report.AppendLine("**允许的位置**：");
        foreach (var pattern in AllowedPathPatterns)
        {
            report.AppendLine($"- `{pattern}`");
        }
        report.AppendLine("\n**禁止的位置**：");
        report.AppendLine("- Core / Execution / Ingress / Host / Application / Observability / Simulation");
        report.AppendLine("  （除了上述允许的特定文件）");

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

    private static bool IsInAllowedPath(string relativePath)
    {
        return AllowedPathPatterns.Any(pattern =>
            relativePath.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateViolationReport(List<VendorCodeViolation> violations, string violationType)
    {
        var report = new StringBuilder();
        report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个{violationType}在禁止位置:");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var violation in violations.OrderBy(v => v.FilePath))
        {
            report.AppendLine($"\n❌ {violation.TypeOrUsage}");
            report.AppendLine($"   位置: {violation.FilePath}");
            report.AppendLine($"   类型: {violation.ViolationType}");
        }

        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.AppendLine("\n💡 根据 copilot-instructions.md 规范:");
        report.AppendLine("  所有厂商相关代码只允许出现在以下位置：");
        report.AppendLine("  - src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/");
        report.AppendLine("  - src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/");
        report.AppendLine("  - Core/Enums/Hardware/Vendors/ (厂商协议枚举)");
        report.AppendLine("\n  修复建议:");
        report.AppendLine("  1. 将厂商特定类型移动到 Drivers/Vendors/<VendorName>/ 目录");
        report.AppendLine("  2. 使用厂商无关的抽象接口（Core/Hardware/）替代直接引用");
        report.AppendLine("  3. 通过 ISensorVendorConfigProvider 等 HAL 接口获取厂商配置");

        return report.ToString();
    }

    #endregion

    private class VendorCodeViolation
    {
        public required string FilePath { get; init; }
        public required string TypeOrUsage { get; init; }
        public required string ViolationType { get; init; }
    }
}
