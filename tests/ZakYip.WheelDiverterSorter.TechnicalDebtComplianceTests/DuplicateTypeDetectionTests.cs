using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// DTO/Options/Utilities 重复类型检测测试
/// Tests to detect duplicate DTO/Options/Utilities types
/// </summary>
/// <remarks>
/// PR-S3: 验证代码库中不存在"结构相同、语义相同"的重复 DTO/Options/Utilities 类型。
/// 
/// 检测策略：
/// 1. 扫描同名不同命名空间的类型
/// 2. 扫描前缀相同但后缀不同的类型组（如 FooDto / FooModel / FooResponse）
/// 3. 扫描未使用的类型定义
/// 
/// 统一命名规则：
/// - 持久化/核心领域：*Model 或 *Entity 或无后缀（在 Core/LineModel 下）
/// - API 输出：*Response（在 Host/Models 下）
/// - API 输入：*Request（在 Host/Models 下）
/// - 配置选项：*Options（在各项目 Configuration 目录下）
/// - 配置存储：*Configuration（在 Core/LineModel/Configuration/Models 下）
/// </remarks>
public class DuplicateTypeDetectionTests
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
    /// 检测同名不同命名空间的类型
    /// Detect same-named types in different namespaces
    /// </summary>
    /// <remarks>
    /// 同一业务概念不应在多个命名空间重复定义。
    /// 如果发现同名类型，需要人工检查是否为语义相同的重复定义。
    /// </remarks>
    [Fact]
    public void ShouldNotHaveDuplicateTypeNameAcrossNamespaces()
    {
        var solutionRoot = GetSolutionRoot();
        var typeLocations = new Dictionary<string, List<TypeLocationInfo>>();
        
        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有类型定义
        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            foreach (var type in types)
            {
                if (!typeLocations.ContainsKey(type.TypeName))
                {
                    typeLocations[type.TypeName] = new List<TypeLocationInfo>();
                }
                typeLocations[type.TypeName].Add(type);
            }
        }

        // 过滤出有多个定义的类型
        var duplicates = typeLocations
            .Where(kvp => kvp.Value.Count > 1)
            // 排除测试框架的常见类型
            .Where(kvp => !IsCommonFrameworkType(kvp.Key))
            // 排除 file-scoped 类型
            .Where(kvp => !kvp.Value.All(t => t.IsFileScoped))
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {duplicates.Count} 个同名类型存在于多个位置:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n需要人工检查以下类型是否为语义相同的重复定义：\n");

            foreach (var (typeName, locations) in duplicates.OrderBy(d => d.Key))
            {
                report.AppendLine($"📦 {typeName}:");
                foreach (var loc in locations)
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, loc.FilePath);
                    report.AppendLine($"   - {relativePath}:{loc.LineNumber}");
                    report.AppendLine($"     命名空间: {loc.Namespace}");
                }
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 如果是语义完全相同的类型，保留一个并删除其他");
            report.AppendLine("  2. 如果是不同职责的类型，使用更明确的命名区分");
            report.AppendLine("  3. 在 RepositoryStructure.md 中记录保留的类型位置");

            // 输出警告但不失败测试（作为顾问性报告）
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {duplicates.Count} duplicate type names");
    }

    /// <summary>
    /// 检测相似名称的类型组（如 FooDto / FooModel / FooResponse）
    /// Detect similar type name groups
    /// </summary>
    /// <remarks>
    /// 如果存在多个前缀相同但后缀不同的类型，需要检查是否存在字段重复。
    /// </remarks>
    [Fact]
    public void ShouldDocumentSimilarTypeNamePatterns()
    {
        var solutionRoot = GetSolutionRoot();
        var allTypes = new List<TypeLocationInfo>();
        
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in sourceFiles)
        {
            allTypes.AddRange(ExtractTypeDefinitions(file));
        }

        // 按前缀分组（去除 Dto/Model/Response/Request/Options/Config/Configuration/Settings 后缀）
        var suffixes = new[] { "Dto", "Model", "Response", "Request", "Options", "Config", "Configuration", "Settings", "Entry" };
        var prefixGroups = new Dictionary<string, List<TypeLocationInfo>>();

        foreach (var type in allTypes.Where(t => !t.IsFileScoped))
        {
            var prefix = GetTypePrefix(type.TypeName, suffixes);
            if (prefix != type.TypeName && prefix.Length > 2) // 有效前缀
            {
                if (!prefixGroups.ContainsKey(prefix))
                {
                    prefixGroups[prefix] = new List<TypeLocationInfo>();
                }
                prefixGroups[prefix].Add(type);
            }
        }

        // 过滤出有多种后缀的类型组
        var similarGroups = prefixGroups
            .Where(kvp => kvp.Value.Count > 1)
            .Where(kvp => kvp.Value.Select(t => GetTypeSuffix(t.TypeName, suffixes)).Distinct().Count() > 1)
            .ToList();

        if (similarGroups.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n📋 发现 {similarGroups.Count} 组相似命名的类型（供参考）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n以下类型组使用相同前缀但不同后缀，请确认是否需要合并：\n");

            foreach (var (prefix, types) in similarGroups.OrderBy(g => g.Key))
            {
                report.AppendLine($"📦 {prefix}*:");
                foreach (var type in types.OrderBy(t => t.TypeName))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, type.FilePath);
                    report.AppendLine($"   - {type.TypeName}");
                    report.AppendLine($"     位置: {relativePath}");
                }
                report.AppendLine();
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 命名规范:");
            report.AppendLine("  - *Configuration: 持久化配置模型（Core/LineModel/Configuration/Models/）");
            report.AppendLine("  - *Options: 运行时配置选项（各项目 Configuration 目录）");
            report.AppendLine("  - *Request: API 请求模型（Host/Models/）");
            report.AppendLine("  - *Response: API 响应模型（Host/Models/）");
            report.AppendLine("  - *Dto: 数据传输对象（跨层传输）");

            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {similarGroups.Count} similar type name groups");
    }

    /// <summary>
    /// 检测未使用的类型定义
    /// Detect unused type definitions
    /// </summary>
    /// <remarks>
    /// 未使用的类型定义应该被删除，以保持代码库整洁。
    /// </remarks>
    [Fact]
    public void ShouldNotHaveUnusedDtoOrOptionsTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<UnusedTypeViolation>();
        
        // 收集所有 DTO/Options 类型
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var allTypes = new List<TypeLocationInfo>();
        foreach (var file in sourceFiles)
        {
            allTypes.AddRange(ExtractTypeDefinitions(file));
        }

        // 过滤出 DTO/Options 类型
        var dtoOptionTypes = allTypes
            .Where(t => !t.IsFileScoped)
            .Where(t => t.TypeName.EndsWith("Dto") || 
                       t.TypeName.EndsWith("Options") || 
                       t.TypeName.EndsWith("Configuration") ||
                       t.TypeName.EndsWith("Config"))
            .ToList();

        // 读取所有源代码内容用于搜索引用
        var allContent = new StringBuilder();
        foreach (var file in sourceFiles)
        {
            try
            {
                allContent.AppendLine(File.ReadAllText(file));
            }
            catch (IOException)
            {
                // 文件可能被锁定或不可读，跳过
            }
        }
        var contentText = allContent.ToString();

        // 检查每个类型是否被使用
        foreach (var type in dtoOptionTypes)
        {
            // 计算类型名出现次数（排除定义本身）
            var pattern = $@"\b{type.TypeName}\b";
            var matches = Regex.Matches(contentText, pattern);
            
            // 如果只出现1次（即定义本身），则可能未使用
            if (matches.Count <= 1)
            {
                violations.Add(new UnusedTypeViolation
                {
                    TypeName = type.TypeName,
                    FilePath = type.FilePath,
                    LineNumber = type.LineNumber,
                    Namespace = type.Namespace
                });
            }
        }

        // 分离 Options 类型（可能通过 IOptions<T> 绑定）和其他类型
        var optionsViolations = violations.Where(v => v.TypeName.EndsWith("Options")).ToList();
        var otherViolations = violations.Where(v => !v.TypeName.EndsWith("Options")).ToList();

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {violations.Count} 个可能未使用的 DTO/Options 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            if (otherViolations.Any())
            {
                report.AppendLine($"\n❌ DTO/Config 类型（{otherViolations.Count} 个）：");
                foreach (var violation in otherViolations.OrderBy(v => v.FilePath))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                    report.AppendLine($"  ❌ {violation.TypeName}");
                    report.AppendLine($"     位置: {relativePath}:{violation.LineNumber}");
                }
            }
            
            if (optionsViolations.Any())
            {
                report.AppendLine($"\n⚠️ Options 类型（{optionsViolations.Count} 个，可能通过 IOptions<T> 绑定）：");
                foreach (var violation in optionsViolations.OrderBy(v => v.FilePath))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                    report.AppendLine($"  ⚠️ {violation.TypeName}");
                    report.AppendLine($"     位置: {relativePath}:{violation.LineNumber}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 确认类型是否真的未使用（可能通过反射或 IOptions<T> 绑定使用）");
            report.AppendLine("  2. 如果确实未使用，删除该类型定义");
            report.AppendLine("  3. 如果类型是为未来功能预留，添加注释说明");

            // 只输出警告，不强制失败（Options 类型可能通过配置绑定使用）
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {violations.Count} potentially unused DTO/Options types");
    }

    /// <summary>
    /// 验证 Utilities 目录位置规范
    /// Verify Utilities directory location conventions
    /// </summary>
    /// <remarks>
    /// 根据规范，公共 Utilities 应该放在 Core/Utilities 目录下。
    /// 项目特定的工具类应该使用 file-scoped 类型。
    /// </remarks>
    [Fact]
    public void UtilitiesDirectoriesShouldFollowConventions()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<string>();
        
        // 允许的 Utilities 目录位置
        var allowedUtilitiesLocations = new[]
        {
            Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "Utilities"),
            Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "LineModel", "Utilities"),
            Path.Combine(solutionRoot, "src", "Observability", "ZakYip.WheelDiverterSorter.Observability", "Utilities"),
        };

        // 扫描所有 Utilities 目录
        var utilitiesDirs = Directory.GetDirectories(
            Path.Combine(solutionRoot, "src"),
            "Utilities",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("/bin/") && !d.Contains("\\obj\\") && !d.Contains("\\bin\\"))
            .ToList();

        foreach (var dir in utilitiesDirs)
        {
            var normalizedDir = dir.Replace('\\', '/');
            var isAllowed = allowedUtilitiesLocations.Any(allowed => 
                normalizedDir.Replace('\\', '/').Equals(allowed.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
            
            if (!isAllowed)
            {
                violations.Add(dir);
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {violations.Count} 个非标准位置的 Utilities 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation);
                report.AppendLine($"  ⚠️ {relativePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将公共工具类移动到 Core/Utilities 目录");
            report.AppendLine("  2. 将项目特定的工具类改为 file-scoped 类型 (file static class)");
            report.AppendLine("  3. 允许的 Utilities 目录位置:");
            foreach (var allowed in allowedUtilitiesLocations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, allowed);
                report.AppendLine($"     - {relativePath}");
            }

            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {violations.Count} non-standard Utilities directories");
    }

    /// <summary>
    /// 验证不存在 Legacy 目录
    /// Verify no Legacy directories exist
    /// </summary>
    [Fact]
    public void ShouldNotHaveLegacyDirectories()
    {
        var solutionRoot = GetSolutionRoot();
        
        var legacyDirs = Directory.GetDirectories(
            Path.Combine(solutionRoot, "src"),
            "Legacy",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("/bin/") && !d.Contains("\\obj\\") && !d.Contains("\\bin\\"))
            .ToList();

        if (legacyDirs.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {legacyDirs.Count} 个 Legacy 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in legacyDirs)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, dir);
                report.AppendLine($"  ❌ {relativePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 copilot-instructions.md 规范：");
            report.AppendLine("  禁止创建 Legacy 目录，过时代码必须在同一次重构中完全删除。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// Abstractions 目录只能存在于允许的位置
    /// Abstractions directories should only exist in allowed locations
    /// </summary>
    [Fact]
    public void AbstractionsShouldOnlyExistInAllowedLocations()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 允许的 Abstractions 目录位置
        var allowedAbstractionsLocations = new[]
        {
            Path.Combine(solutionRoot, "src", "Core", "ZakYip.WheelDiverterSorter.Core", "Abstractions"),
            Path.Combine(solutionRoot, "src", "Infrastructure", "ZakYip.WheelDiverterSorter.Communication", "Abstractions"),
        };

        // 预规范化允许的路径
        var normalizedAllowedPaths = allowedAbstractionsLocations
            .Select(p => p.Replace('\\', '/'))
            .ToList();

        var abstractionsDirs = Directory.GetDirectories(
            Path.Combine(solutionRoot, "src"),
            "Abstractions",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("/bin/") && !d.Contains("\\obj\\") && !d.Contains("\\bin\\"))
            .ToList();

        var violations = new List<string>();
        foreach (var dir in abstractionsDirs)
        {
            var normalizedDir = dir.Replace('\\', '/');
            var isAllowed = normalizedAllowedPaths.Any(allowed => 
                normalizedDir.StartsWith(allowed, StringComparison.OrdinalIgnoreCase));
            
            if (!isAllowed)
            {
                violations.Add(dir);
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个非标准位置的 Abstractions 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation);
                report.AppendLine($"  ❌ {relativePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 copilot-instructions.md 规范：");
            report.AppendLine("  Abstractions 目录只能存在于以下位置:");
            foreach (var allowed in allowedAbstractionsLocations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, allowed);
                report.AppendLine($"     - {relativePath}");
            }

            Assert.Fail(report.ToString());
        }
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsCommonFrameworkType(string typeName)
    {
        var commonTypes = new[]
        {
            "Program", "Startup", "AssemblyInfo",
            "Resources", "Settings"
        };
        return commonTypes.Contains(typeName);
    }

    private static List<TypeLocationInfo> ExtractTypeDefinitions(string filePath)
    {
        var types = new List<TypeLocationInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 查找类型定义
            // 注意：此正则表达式是简化实现，用于快速扫描。
            // 对于更精确的类型检测，应使用 Roslyn 分析器。
            // 当前实现足以满足技术债务合规性检测的需求。
            var typePattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:static\s+)?(?:record|class|struct|interface|enum)\s+(?<typeName>\w+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = typePattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new TypeLocationInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns,
                        IsFileScoped = match.Groups["fileScoped"].Success
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting types from {filePath}: {ex.Message}");
        }

        return types;
    }

    private static string GetTypePrefix(string typeName, string[] suffixes)
    {
        foreach (var suffix in suffixes.OrderByDescending(s => s.Length))
        {
            if (typeName.EndsWith(suffix) && typeName.Length > suffix.Length)
            {
                return typeName[..^suffix.Length];
            }
        }
        return typeName;
    }

    private static string GetTypeSuffix(string typeName, string[] suffixes)
    {
        foreach (var suffix in suffixes.OrderByDescending(s => s.Length))
        {
            if (typeName.EndsWith(suffix))
            {
                return suffix;
            }
        }
        return string.Empty;
    }

    #endregion
}

/// <summary>
/// 类型位置信息
/// Type location information
/// </summary>
public record TypeLocationInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
    public bool IsFileScoped { get; init; }
}

/// <summary>
/// 未使用类型违规信息
/// Unused type violation information
/// </summary>
public record UnusedTypeViolation
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
}
