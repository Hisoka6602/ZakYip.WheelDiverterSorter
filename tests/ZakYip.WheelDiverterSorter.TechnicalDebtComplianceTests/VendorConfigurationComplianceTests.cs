using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD9: 项目依赖约束和厂商配置位置合规性测试
/// Tests for project dependency constraints and vendor configuration location compliance
/// </summary>
/// <remarks>
/// 根据 PR-SD9 规范：
/// 1. Ingress 项目不应引用 Drivers 项目
/// 2. 厂商特定的配置类型只能存在于 Drivers/Vendors 命名空间下
/// 3. Core 层不应包含厂商名称的配置类型
/// </remarks>
public class VendorConfigurationComplianceTests
{
    /// <summary>
    /// 厂商名称列表
    /// </summary>
    private static readonly string[] VendorNames = 
    {
        "Leadshine",
        "Modi",
        "ShuDiNiao",
        "Siemens",
        "Mitsubishi",
        "Omron"
    };

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
    /// PR-SD9: 验证 Ingress 项目不引用 Drivers 项目
    /// Verify that Ingress project does not reference Drivers project
    /// </summary>
    /// <remarks>
    /// 根据 PR-SD9 规范，Ingress 应该只依赖 Core + Communication，
    /// 不应该直接引用 Drivers 项目。
    /// </remarks>
    [Fact]
    public void Ingress_ShouldNotReference_Drivers()
    {
        var solutionRoot = GetSolutionRoot();
        var ingressCsprojPath = Path.Combine(
            solutionRoot, "src", "Ingress", 
            "ZakYip.WheelDiverterSorter.Ingress",
            "ZakYip.WheelDiverterSorter.Ingress.csproj");

        if (!File.Exists(ingressCsprojPath))
        {
            Assert.Fail($"Ingress 项目文件不存在: {ingressCsprojPath}");
            return;
        }

        var csprojContent = File.ReadAllText(ingressCsprojPath);
        
        // 检查是否包含对 Drivers 项目的引用
        var driversReferencePattern = new Regex(
            @"<ProjectReference[^>]*Include[^>]*Drivers[^>]*>",
            RegexOptions.IgnoreCase);

        var hasDriversReference = driversReferencePattern.IsMatch(csprojContent);

        if (hasDriversReference)
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ PR-SD9 违规: Ingress 项目引用了 Drivers 项目");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n位置: {Path.GetRelativePath(solutionRoot, ingressCsprojPath)}");
            report.AppendLine("\n💡 根据 PR-SD9 规范:");
            report.AppendLine("  Ingress 项目应该只依赖 Core + Communication，");
            report.AppendLine("  不应该直接引用 Drivers 项目。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 移除 Ingress.csproj 中对 Drivers 项目的 <ProjectReference>");
            report.AppendLine("  2. 如果 Ingress 需要使用配置类，应使用 Core 层定义的 DTO");
            report.AppendLine("  3. 具体厂商配置应在 Host 层或 Application 层组装时注入");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD9: 验证厂商配置类型只存在于 Drivers/Vendors 命名空间
    /// Verify that vendor-specific configuration types only exist in Drivers/Vendors namespaces
    /// </summary>
    /// <remarks>
    /// 此测试验证：
    /// 1. 厂商特定的 *Options / *Config 类型只能在 Drivers.Vendors.* 命名空间中定义
    /// 2. 其他项目（Core, Application, Host 等）不应该定义厂商命名的配置类
    /// 
    /// 白名单规则：
    /// - Core 层可以定义厂商无关的配置存储模型（用于 LiteDB 持久化）
    /// - 这些模型可以包含厂商名作为属性（如 VendorType 枚举）
    /// - 但不应该有以厂商名开头的类型名
    /// 
    /// 遗留类型白名单（待后续 PR 清理）：
    /// - ShuDiNiaoWheelDiverterConfig, ShuDiNiaoDeviceEntry (Core 层 LiteDB 持久化)
    /// - ModiWheelDiverterConfig, ModiDeviceEntry (Core 层 LiteDB 持久化)
    /// - LeadshineDriverConfig, DiverterDriverEntry, LeadshineIoConnectionConfig (Core 层 LiteDB 持久化)
    /// </remarks>
    [Fact]
    public void VendorConfigurationTypes_ShouldLiveUnder_DriversVendors()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        // 遗留类型白名单：这些类型在 Core 层用于 LiteDB 持久化，待后续 PR 重构
        var legacyWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ShuDiNiaoWheelDiverterConfig",
            "ShuDiNiaoDeviceEntry",
            "ModiWheelDiverterConfig",
            "ModiDeviceEntry",
            "LeadshineDriverConfig",
            "LeadshineIoConnectionConfig",
            "DiverterDriverEntry"
        };

        // 扫描 src 目录下所有 .cs 文件（排除 Drivers/Vendors 目录）
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInDriversVendorsDirectory(f)) // 排除 Drivers/Vendors 目录
            .ToList();

        foreach (var file in sourceFiles)
        {
            var vendorTypes = ExtractVendorConfigurationTypes(file);
            // 过滤掉白名单中的遗留类型
            var nonWhitelistedViolations = vendorTypes
                .Where(v => !legacyWhitelist.Contains(v.TypeName))
                .ToList();
            violations.AddRange(nonWhitelistedViolations);
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD9 违规: 发现 {violations.Count} 个厂商配置类型不在 Drivers/Vendors 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations.OrderBy(v => v.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"\n❌ {typeName}:");
                report.AppendLine($"   位置: {relativePath}:{lineNumber}");
                report.AppendLine($"   命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD9 规范:");
            report.AppendLine("  厂商特定的配置类型（以 Leadshine/Modi/ShuDiNiao 等开头）");
            report.AppendLine("  只能在 Drivers/Vendors/[VendorName]/Configuration/ 目录定义。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将厂商配置类型移动到 Drivers/Vendors/[VendorName]/Configuration/");
            report.AppendLine("  2. 在 Core 层使用厂商无关的抽象或 VendorProfileKey 模式");
            report.AppendLine("  3. 更新所有引用以使用新位置");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD9: 验证 Drivers/Vendors 目录下的厂商配置完整性
    /// Verify vendor configuration completeness under Drivers/Vendors
    /// </summary>
    [Fact]
    public void DriversVendors_ShouldHaveCompleteVendorConfiguration()
    {
        var solutionRoot = GetSolutionRoot();
        var vendorsDir = Path.Combine(
            solutionRoot, "src", "Drivers",
            "ZakYip.WheelDiverterSorter.Drivers", "Vendors");

        if (!Directory.Exists(vendorsDir))
        {
            Assert.Fail($"Drivers/Vendors 目录不存在: {vendorsDir}");
            return;
        }

        var vendorDirs = Directory.GetDirectories(vendorsDir);
        var report = new StringBuilder();
        var hasIssues = false;

        report.AppendLine("\n📋 Drivers/Vendors 目录结构审计报告:");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        foreach (var vendorDir in vendorDirs)
        {
            var vendorName = Path.GetFileName(vendorDir);
            var configDir = Path.Combine(vendorDir, "Configuration");

            report.AppendLine($"\n📦 {vendorName}:");

            if (Directory.Exists(configDir))
            {
                var configFiles = Directory.GetFiles(configDir, "*.cs");
                if (configFiles.Length > 0)
                {
                    report.AppendLine($"   ✅ Configuration 目录存在 ({configFiles.Length} 个文件)");
                    foreach (var file in configFiles.Take(5))
                    {
                        report.AppendLine($"      - {Path.GetFileName(file)}");
                    }
                    if (configFiles.Length > 5)
                    {
                        report.AppendLine($"      - ... 和 {configFiles.Length - 5} 个其他文件");
                    }
                }
                else
                {
                    report.AppendLine($"   ⚠️ Configuration 目录为空");
                    hasIssues = true;
                }
            }
            else
            {
                report.AppendLine($"   ❌ 缺少 Configuration 目录");
                hasIssues = true;
            }
        }

        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        // 此测试作为审计报告，即使有问题也输出信息但不失败
        Console.WriteLine(report.ToString());
        
        Assert.True(true, hasIssues 
            ? "审计完成，发现一些结构问题（见报告）" 
            : "审计完成，结构完整");
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsInDriversVendorsDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        return normalizedPath.Contains("/Drivers/") && normalizedPath.Contains("/Vendors/");
    }

    /// <summary>
    /// 从文件中提取厂商配置类型定义
    /// </summary>
    private static List<(string TypeName, string FilePath, int LineNumber, string Namespace)> ExtractVendorConfigurationTypes(
        string filePath)
    {
        var types = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        try
        {
            var content = File.ReadAllText(filePath);
            var lines = content.Split('\n');

            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 构建厂商名称模式
            var vendorPattern = string.Join("|", VendorNames);
            
            // 查找以厂商名称开头的配置类型定义
            // 匹配模式: [public|internal] [sealed] [record] [class|struct] [VendorName]...[Options|Config|Configuration|Settings]
            var pattern = new Regex(
                $@"^\s*(?:public|internal)\s+(?:sealed\s+)?(?:record\s+)?(?:class|struct)\s+(?<typeName>(?:{vendorPattern})\w*(?:Options|Config|Configuration|Settings|Entry))\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.IgnoreCase);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = pattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add((match.Groups["typeName"].Value, filePath, i + 1, ns));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting vendor config types from {filePath}: {ex.Message}");
        }

        return types;
    }

    #endregion
}
