using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-CONFIG-HOTRELOAD01: 配置缓存影分身检测测试
/// Tests to detect configuration cache shadow types
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 和 RepositoryStructure.md 6.1 单一权威实现表：
/// 1. 配置缓存唯一权威位置：Application/Services/Caching/ (ISlidingConfigCache, SlidingConfigCache)
/// 2. 所有配置服务统一使用 ISlidingConfigCache
/// 3. 禁止在以下位置出现配置缓存实现：
///    - Configuration.Persistence 层（不允许自带缓存）
///    - Host/Controllers 层（不允许自定义缓存）
///    - Core/Execution/Drivers/Ingress 层（不允许实现配置缓存）
/// 4. 禁止出现的类型模式：*ConfigCache, *OptionsProvider, *Cached*Repository
/// </remarks>
public class ConfigCacheShadowTests
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
    /// 允许的配置缓存相关类型（仅在 Application/Services/Caching/）
    /// </summary>
    private static readonly HashSet<string> AllowedConfigCacheTypes = new(StringComparer.Ordinal)
    {
        // Application/Services/Caching/ - 权威实现
        "ISlidingConfigCache",
        "SlidingConfigCache",
        "CachedSwitchingPathGenerator",  // 路径生成装饰器（允许）
        "InMemoryRoutePlanRepository",   // 路由计划内存仓储（允许）
    };

    /// <summary>
    /// 验证配置缓存只存在于权威位置
    /// Should only have config cache in Application/Services/Caching/
    /// </summary>
    [Fact]
    public void ConfigCache_Should_Only_Exist_In_Application_Services_Caching()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath, string Pattern)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 禁止的类型模式
        var forbiddenPatterns = new[]
        {
            (@"\w*ConfigCache\w*", "ConfigCache"),
            (@"\w*OptionsProvider\w*", "OptionsProvider"),
            (@"Cached\w*Repository\w*", "Cached*Repository")
        };

        var configCachePattern = new Regex(
            string.Join("|", forbiddenPatterns.Select(p => $"(?:{p.Item1})")),
            RegexOptions.Compiled);

        var typeDeclarationPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            
            // 检查是否在权威位置
            var isInAuthorityLocation = relativePath.Contains("Application/ZakYip.WheelDiverterSorter.Application/Services/Caching/");

            var typeMatches = typeDeclarationPattern.Matches(content);

            foreach (Match typeMatch in typeMatches)
            {
                var typeName = typeMatch.Groups["typeName"].Value;
                
                // 检查是否匹配禁止的模式
                if (configCachePattern.IsMatch(typeName))
                {
                    // 如果不在权威位置，且不在允许列表中，则为违规
                    if (!isInAuthorityLocation || !AllowedConfigCacheTypes.Contains(typeName))
                    {
                        var matchedPattern = forbiddenPatterns.FirstOrDefault(p => Regex.IsMatch(typeName, p.Item1));
                        violations.Add((typeName, relativePath, matchedPattern.Item2 ?? "Unknown"));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-CONFIG-HOTRELOAD01 违规: 发现 {violations.Count} 个配置缓存影分身:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, pattern) in violations)
            {
                report.AppendLine($"\n❌ {typeName} (匹配模式: {pattern})");
                report.AppendLine($"   位置: {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n✅ 权威实现位置: Application/Services/Caching/");
            report.AppendLine("   - ISlidingConfigCache (接口)");
            report.AppendLine("   - SlidingConfigCache (实现)");
            report.AppendLine("\n❌ 禁止出现的模式:");
            report.AppendLine("   - *ConfigCache");
            report.AppendLine("   - *OptionsProvider");
            report.AppendLine("   - Cached*Repository");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除重复的配置缓存实现");
            report.AppendLine("  2. 统一使用 ISlidingConfigCache");
            report.AppendLine("  3. 配置服务通过 DI 注入 ISlidingConfigCache");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 Configuration.Persistence 层不包含缓存字段
    /// Configuration.Persistence should not have cache fields
    /// </summary>
    [Fact]
    public void Configuration_Persistence_Should_Not_Have_Cache_Fields()
    {
        var solutionRoot = GetSolutionRoot();
        var persistenceProjectPath = Path.Combine(solutionRoot, "src", "Infrastructure", "ZakYip.WheelDiverterSorter.Configuration.Persistence");

        if (!Directory.Exists(persistenceProjectPath))
        {
            Assert.True(true, "Configuration.Persistence project not found, skipping test");
            return;
        }

        var violations = new List<(string FileName, string FieldName, string FieldType)>();

        var sourceFiles = Directory.GetFiles(persistenceProjectPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配缓存相关字段声明
        var cacheFieldPattern = new Regex(
            @"private\s+(?:readonly\s+)?(?<fieldType>(?:IMemoryCache|MemoryCache|ConcurrentDictionary<[^>]+>|Dictionary<[^>]+>))\s+(?<fieldName>\w+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = cacheFieldPattern.Matches(content);
            var fileName = Path.GetFileName(file);

            foreach (Match match in matches)
            {
                var fieldType = match.Groups["fieldType"].Value;
                var fieldName = match.Groups["fieldName"].Value;
                
                // 排除明显不是缓存的字段（例如 _collection）
                if (!fieldName.Contains("collection", StringComparison.OrdinalIgnoreCase) &&
                    !fieldName.Contains("database", StringComparison.OrdinalIgnoreCase) &&
                    !fieldName.Contains("db", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add((fileName, fieldName, fieldType));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-CONFIG-HOTRELOAD01 违规: Configuration.Persistence 层发现 {violations.Count} 个缓存字段:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (fileName, fieldName, fieldType) in violations)
            {
                report.AppendLine($"\n❌ {fileName}");
                report.AppendLine($"   字段: {fieldName}");
                report.AppendLine($"   类型: {fieldType}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n规范要求:");
            report.AppendLine("  - Configuration.Persistence 层只负责持久化存取");
            report.AppendLine("  - 不应包含任何缓存逻辑（IMemoryCache, ConcurrentDictionary 等）");
            report.AppendLine("  - 缓存由 Application 层的 ISlidingConfigCache 统一管理");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除 Persistence 层的缓存字段");
            report.AppendLine("  2. 在 Application/Services/Config/ 层使用 ISlidingConfigCache");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成配置缓存类型分布报告
    /// </summary>
    [Fact]
    public void GenerateConfigCacheTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-CONFIG-HOTRELOAD01: 配置缓存类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var cachePattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Cache|Provider|Repository)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = cachePattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match =>
                    {
                        var typeName = match.Groups["typeName"].Value;
                        var isInAuthority = relativePath.Contains("Application/ZakYip.WheelDiverterSorter.Application/Services/Caching/");
                        var isAllowed = AllowedConfigCacheTypes.Contains(typeName);
                        return (TypeName: typeName, FilePath: relativePath, IsInAuthority: isInAuthority, IsAllowed: isAllowed);
                    });
            })
            .Where(t => t.TypeName.Contains("Config", StringComparison.OrdinalIgnoreCase) ||
                       t.TypeName.Contains("Cache", StringComparison.OrdinalIgnoreCase))
            .ToList();

        report.AppendLine("## 发现的配置缓存相关类型\n");
        report.AppendLine("| 类型名称 | 位置 | 状态 |");
        report.AppendLine("|----------|------|------|");

        foreach (var (typeName, filePath, isInAuthority, isAllowed) in foundTypes.OrderBy(t => t.FilePath))
        {
            var status = isAllowed ? "✅ 允许" : (isInAuthority ? "⚠️ 需确认" : "❌ 潜在违规");
            report.AppendLine($"| {typeName} | {filePath} | {status} |");
        }

        report.AppendLine("\n## 规范说明\n");
        report.AppendLine("根据 PR-CONFIG-HOTRELOAD01 规范，配置缓存只允许：");
        report.AppendLine("- **权威位置**: Application/Services/Caching/");
        report.AppendLine("  - ISlidingConfigCache (接口)");
        report.AppendLine("  - SlidingConfigCache (实现)");
        report.AppendLine("- **禁止位置**: Configuration.Persistence, Host/Controllers, Core, Execution, Drivers, Ingress");

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
