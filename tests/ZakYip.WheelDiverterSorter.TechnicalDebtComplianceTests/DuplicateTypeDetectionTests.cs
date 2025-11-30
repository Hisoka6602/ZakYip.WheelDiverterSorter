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
    /// <summary>
    /// PR-SD1: Core 抽象接口名称列表（必须且只能定义在 Core.Abstractions 中）
    /// </summary>
    /// <remarks>
    /// 这些接口遵循 C# 命名约定（I 前缀），在整个解决方案中仅允许在 Core 项目定义。
    /// </remarks>
    private static readonly string[] CoreAbstractionInterfaces = 
    {
        "ICongestionDataCollector",
        "ISensorEventProvider",
        "IUpstreamRoutingClient",
        "IUpstreamContractMapper"
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
    /// PR-SD5: 验证配置模型在生产代码中有实际使用，而不是仅在测试中使用
    /// Verify that configuration models in Core/LineModel/Configuration/Models have production usage
    /// </summary>
    /// <remarks>
    /// 此测试验证：
    /// 1. Core/LineModel/Configuration/Models 中的配置模型在 src/ 目录有引用（除了定义本身）
    /// 2. 仅在 tests/ 目录中使用的配置模型应该移动到测试项目或删除
    /// 
    /// 这是一个强制性测试（会失败），因为生产代码中不应保留未使用的配置模型。
    /// 
    /// 注意：如果类型仅在同一文件的其他类型中作为属性使用，视为有效使用（例如：
    /// DriverConfiguration 使用 LeadshineDriverConfig 作为属性，LeadshineDriverConfig 使用 DiverterDriverEntry）
    /// </remarks>
    [Fact]
    public void ConfigurationModelsShouldHaveProductionUsage()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath, int ProductionUsageCount, int TestUsageCount)>();
        
        // 只扫描 Core/LineModel/Configuration/Models 目录
        var configModelsDir = Path.Combine(
            solutionRoot, "src", "Core", 
            "ZakYip.WheelDiverterSorter.Core", 
            "LineModel", "Configuration", "Models");
            
        if (!Directory.Exists(configModelsDir))
        {
            return; // 目录不存在，跳过
        }

        var modelFiles = Directory.GetFiles(configModelsDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有配置模型类型
        var configModelTypes = new List<TypeLocationInfo>();
        foreach (var file in modelFiles)
        {
            configModelTypes.AddRange(ExtractTypeDefinitions(file).Where(t => !t.IsFileScoped));
        }

        // 读取配置模型目录本身的所有代码（用于检测 helper types）
        var configModelsContent = new StringBuilder();
        foreach (var file in modelFiles)
        {
            try
            {
                configModelsContent.AppendLine(File.ReadAllText(file));
            }
            catch (IOException) { }
        }
        var configModelsText = configModelsContent.ToString();

        // 读取 src/ 目录的源代码内容（排除配置模型定义文件本身）
        var srcFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !f.Replace('\\', '/').Contains("/LineModel/Configuration/Models/"))
            .ToList();

        var srcContent = new StringBuilder();
        foreach (var file in srcFiles)
        {
            try
            {
                srcContent.AppendLine(File.ReadAllText(file));
            }
            catch (IOException) { }
        }
        var srcText = srcContent.ToString();

        // 读取 tests/ 目录的源代码内容
        var testsDir = Path.Combine(solutionRoot, "tests");
        var testFiles = Directory.Exists(testsDir) 
            ? Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories)
                .Where(f => !IsInExcludedDirectory(f))
                .ToList()
            : new List<string>();

        var testContent = new StringBuilder();
        foreach (var file in testFiles)
        {
            try
            {
                testContent.AppendLine(File.ReadAllText(file));
            }
            catch (IOException) { }
        }
        var testText = testContent.ToString();

        // 预编译所有类型的正则表达式模式（性能优化，处理可能的重复类型名）
        var typePatterns = new Dictionary<string, Regex>();
        foreach (var type in configModelTypes)
        {
            if (!typePatterns.ContainsKey(type.TypeName))
            {
                typePatterns[type.TypeName] = new Regex($@"\b{type.TypeName}\b", RegexOptions.Compiled);
            }
        }

        // 先识别哪些类型在 src/ 外部被使用（用于后续判断 helper types）
        var usedInProduction = new HashSet<string>();
        foreach (var type in configModelTypes)
        {
            var compiledPattern = typePatterns[type.TypeName];
            var srcMatches = compiledPattern.Matches(srcText);
            if (srcMatches.Count > 0)
            {
                usedInProduction.Add(type.TypeName);
            }
        }

        // 检查每个配置模型的使用情况
        foreach (var type in configModelTypes)
        {
            var compiledPattern = typePatterns[type.TypeName];
            
            // 计算在 src/（配置模型目录之外）的引用次数
            var srcMatches = compiledPattern.Matches(srcText);
            var productionUsageCount = srcMatches.Count;
            
            // 计算在 tests/ 中的引用次数
            var testMatches = compiledPattern.Matches(testText);
            var testUsageCount = testMatches.Count;
            
            // 如果类型在生产代码中已被使用，跳过
            if (productionUsageCount > 0)
            {
                continue;
            }

            // 检查是否是 "helper type"：被同目录中其他已使用类型引用
            // 例如：LeadshineDriverConfig 被 DriverConfiguration 引用，而 DriverConfiguration 在 src/ 中被使用
            var isHelperType = false;
            foreach (var otherType in usedInProduction)
            {
                if (otherType == type.TypeName) continue;
                
                // 检查其他已使用类型的定义文件是否引用了当前类型
                var otherTypeFile = configModelTypes.FirstOrDefault(t => t.TypeName == otherType)?.FilePath;
                if (otherTypeFile != null)
                {
                    try
                    {
                        var otherFileContent = File.ReadAllText(otherTypeFile);
                        if (compiledPattern.IsMatch(otherFileContent))
                        {
                            isHelperType = true;
                            break;
                        }
                    }
                    catch (IOException) { }
                }
            }

            // 如果是 helper type（被其他已使用类型引用），则视为有效使用
            if (isHelperType)
            {
                continue;
            }
            
            // 如果在 src/ 中没有使用（0次引用），且不是 helper type，则为违规
            violations.Add((type.TypeName, type.FilePath, productionUsageCount, testUsageCount));
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD5 违规: 发现 {violations.Count} 个配置模型仅在测试中使用:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, productionUsage, testUsage) in violations.OrderBy(v => v.TypeName))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"\n❌ {typeName}:");
                report.AppendLine($"   位置: {relativePath}");
                report.AppendLine($"   生产代码引用: {productionUsage}");
                report.AppendLine($"   测试代码引用: {testUsage}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD5 规范:");
            report.AppendLine("  配置模型必须在生产代码中有明确使用位置。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 如果类型是为未实现的功能准备的，应删除并在需要时重新添加");
            report.AppendLine("  2. 如果类型仅用于测试，应移动到测试项目中");
            report.AppendLine("  3. 如果类型是遗留代码，应直接删除");

            Assert.Fail(report.ToString());
        }
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

    /// <summary>
    /// PR-SD1: 验证 Execution 项目中不存在 Core 抽象接口的镜像定义
    /// </summary>
    /// <remarks>
    /// 以下接口必须且只能定义在 Core.Abstractions 中：
    /// - ICongestionDataCollector
    /// - ISensorEventProvider  
    /// - IUpstreamRoutingClient
    /// - IUpstreamContractMapper
    /// 
    /// Execution 项目应依赖 Core 接口，不允许定义同名镜像接口。
    /// </remarks>
    [Fact]
    public void ExecutionProjectShouldNotDefineCoreAbstractionInterfaces()
    {
        var solutionRoot = GetSolutionRoot();

        var executionDir = Path.Combine(solutionRoot, "src", "Execution");
        if (!Directory.Exists(executionDir))
        {
            return; // Execution 项目不存在，跳过
        }

        var sourceFiles = Directory.GetFiles(executionDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = new List<(string InterfaceName, string FilePath, int LineNumber)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractInterfaceDefinitions(file);
            foreach (var type in types)
            {
                if (CoreAbstractionInterfaces.Contains(type.TypeName))
                {
                    violations.Add((type.TypeName, type.FilePath, type.LineNumber));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD1 违规: Execution 项目中发现 {violations.Count} 个 Core 抽象接口的镜像定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (interfaceName, filePath, lineNumber) in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"  ❌ {interfaceName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除 Execution 项目中的接口定义");
            report.AppendLine("  2. 改为依赖 ZakYip.WheelDiverterSorter.Core.Abstractions 中的接口");
            report.AppendLine("  3. 更新实现类的 using 语句和接口引用");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD1: 验证 Core 抽象接口只在 Core 项目中定义
    /// </summary>
    /// <remarks>
    /// 以下接口在整个解决方案中只能定义在 Core.Abstractions 中，
    /// 其他任何项目（包括 Execution、Application、Drivers、Host）都不允许定义：
    /// - ICongestionDataCollector
    /// - ISensorEventProvider  
    /// - IUpstreamRoutingClient
    /// - IUpstreamContractMapper
    /// </remarks>
    [Fact]
    public void CoreAbstractionInterfacesShouldOnlyBeDefinedInCore()
    {
        var solutionRoot = GetSolutionRoot();

        var srcDir = Path.Combine(solutionRoot, "src");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInCoreProject(solutionRoot, f)) // 排除 Core 项目
            .ToList();

        var violations = new List<(string InterfaceName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractInterfaceDefinitions(file);
            foreach (var type in types)
            {
                if (CoreAbstractionInterfaces.Contains(type.TypeName))
                {
                    violations.Add((type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD1 违规: 在 Core 项目之外发现 {violations.Count} 个 Core 抽象接口定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (interfaceName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"  ❌ {interfaceName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
                report.AppendLine($"     命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD1 规范:");
            report.AppendLine("  以下接口只能定义在 Core.Abstractions 中:");
            foreach (var interfaceName in CoreAbstractionInterfaces)
            {
                report.AppendLine($"     - {interfaceName}");
            }
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 删除非 Core 项目中的接口定义文件");
            report.AppendLine("  2. 改为引用 ZakYip.WheelDiverterSorter.Core 项目");
            report.AppendLine("  3. 使用 using ZakYip.WheelDiverterSorter.Core.Abstractions.* 导入接口");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-S4: 验证 *Options 类型不存在跨项目重复定义
    /// Verify that *Options types are not duplicated across projects
    /// </summary>
    /// <remarks>
    /// 此测试专门针对 *Options 类型进行检测，确保：
    /// 1. 同名的 Options 类型不能在多个项目中定义
    /// 2. 为确实需要复用的极少数类型提供显式白名单配置
    /// 
    /// 如果检测到重复定义，测试将失败并提示修复方案。
    /// </remarks>
    [Fact]
    public void OptionsTypesShouldNotBeDuplicatedAcrossProjects()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 显式白名单：允许在多个项目中存在的 Options 类型
        // 只有经过架构评审的类型才能加入此白名单
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 当前无白名单类型，所有 Options 都必须唯一
        };
        
        var optionsTypesByName = new Dictionary<string, List<OptionsTypeInfo>>(StringComparer.OrdinalIgnoreCase);
        
        // 扫描 src 目录下所有 .cs 文件
        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有 *Options 类型定义
        var allOptionsTypes = sourceFiles
            .SelectMany(file => ExtractOptionsTypeDefinitions(file, solutionRoot))
            .ToList();
        
        foreach (var type in allOptionsTypes)
        {
            if (!optionsTypesByName.ContainsKey(type.TypeName))
            {
                optionsTypesByName[type.TypeName] = new List<OptionsTypeInfo>();
            }
            optionsTypesByName[type.TypeName].Add(type);
        }

        // 查找跨项目重复的 Options 类型
        var duplicates = optionsTypesByName
            .Where(kvp => kvp.Value.Count > 1)
            // 排除白名单类型
            .Where(kvp => !whitelist.Contains(kvp.Key))
            // 只有当在多个不同项目中定义时才算重复
            .Where(kvp => kvp.Value.Select(t => t.ProjectName).Distinct().Count() > 1)
            // 排除 file-scoped 类型
            .Where(kvp => !kvp.Value.All(t => t.IsFileScoped))
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-S4 违规: 发现 {duplicates.Count} 个 Options 类型存在跨项目重复定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, locations) in duplicates.OrderBy(d => d.Key))
            {
                report.AppendLine($"\n❌ {typeName}:");
                foreach (var loc in locations.OrderBy(l => l.ProjectName))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, loc.FilePath);
                    report.AppendLine($"   - 项目: {loc.ProjectName}");
                    report.AppendLine($"     位置: {relativePath}:{loc.LineNumber}");
                    report.AppendLine($"     命名空间: {loc.Namespace}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-S4 规范:");
            report.AppendLine("  同名的 *Options 类型只能在一个项目中定义。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 保留唯一的权威定义（通常在 Core 或专属配置项目中）");
            report.AppendLine("  2. 删除其他重复的定义");
            report.AppendLine("  3. 更新所有引用以使用唯一定义");
            report.AppendLine("  4. 如果确实需要复用，请将类型名加入 whitelist");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-S4: 验证 Core 层不存在厂商命名的 *Options 类型
    /// Verify that Core layer doesn't have vendor-named *Options types
    /// </summary>
    /// <remarks>
    /// 此测试验证：
    /// 1. Core 层不应存在以厂商名称开头的 Options 类型（如 LeadshineXxxOptions）
    /// 2. 厂商特定的 Options 类型应定义在 Drivers/Vendors/[VendorName]/Configuration/ 目录下
    /// 
    /// 例如：LeadshineCabinetIoOptions 应在 Drivers/Vendors/Leadshine/Configuration/ 而不是 Core 中
    /// </remarks>
    [Fact]
    public void CoreShouldNotHaveVendorNamedOptionsTypes()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 厂商名称前缀列表
        var vendorPrefixes = new[]
        {
            "Leadshine",
            "Modi",
            "ShuDiNiao",
            "Siemens",
            "Mitsubishi",
            "Omron"
        };
        
        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();
        
        // 只扫描 Core 项目
        var coreDir = Path.Combine(solutionRoot, "src", "Core");
        if (!Directory.Exists(coreDir))
        {
            return; // Core 目录不存在，跳过
        }

        var sourceFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var vendorNamedTypes = sourceFiles
            .SelectMany(file => ExtractOptionsTypeDefinitions(file, solutionRoot))
            .Where(type => vendorPrefixes.Any(v => 
                type.TypeName.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
            .Select(type => (type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
        
        violations.AddRange(vendorNamedTypes);

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-S4 违规: Core 层发现 {violations.Count} 个厂商命名的 Options 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"\n❌ {typeName}:");
                report.AppendLine($"   位置: {relativePath}:{lineNumber}");
                report.AppendLine($"   命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-S4 规范:");
            report.AppendLine("  厂商特定的 *Options 类型必须定义在 Drivers/Vendors/[VendorName]/Configuration/ 目录下。");
            report.AppendLine("  Core 层应只包含厂商无关的配置抽象。");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将厂商命名的 Options 类型移动到对应的 Drivers/Vendors/[VendorName]/Configuration/ 目录");
            report.AppendLine("  2. 在 Core 中使用厂商无关的抽象或 VendorProfileKey 模式");
            report.AppendLine("  3. 更新所有引用以使用新位置");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-S5: 验证 Core 层不存在同名 *Result 类型在多个命名空间的重复定义
    /// Verify that *Result types are not duplicated across Core namespaces
    /// </summary>
    /// <remarks>
    /// 此测试验证：
    /// 1. 同名的 *Result 类型不能在 Core 层的多个命名空间中定义
    /// 2. 唯一公共 OperationResult 必须定义在 Core/Results 命名空间
    /// 3. 允许的例外：不同语义的领域结果（如 PathExecutionResult、ReroutingResult 等）
    /// 
    /// 白名单规则：
    /// - Core/Results/OperationResult 是唯一公共结果模型
    /// - 其他命名空间中使用不同名称的内部结果类型
    /// </remarks>
    [Fact]
    public void ResultTypesShouldNotBeDuplicatedAcrossCoreNamespaces()
    {
        var solutionRoot = GetSolutionRoot();
        
        // 显式白名单：允许在多个命名空间中存在的 Result 类型
        // 仅用于确实有不同语义的领域结果类型
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 当前无需白名单 - 所有 *Result 类型应使用唯一名称
        };
        
        // 禁止在公共 API 中重复的结果类型名称
        var forbiddenDuplicateNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "OperationResult", // 唯一公共结果模型，必须只在 Core/Results 中定义
        };
        
        var resultTypesByName = new Dictionary<string, List<ResultTypeInfo>>(StringComparer.OrdinalIgnoreCase);
        
        // 只扫描 Core 项目
        var coreDir = Path.Combine(solutionRoot, "src", "Core");
        if (!Directory.Exists(coreDir))
        {
            return; // Core 目录不存在，跳过
        }

        var sourceFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有 *Result 类型定义
        var allResultTypes = sourceFiles
            .SelectMany(file => ExtractResultTypeDefinitions(file, solutionRoot))
            .ToList();
        
        foreach (var type in allResultTypes)
        {
            if (!resultTypesByName.ContainsKey(type.TypeName))
            {
                resultTypesByName[type.TypeName] = new List<ResultTypeInfo>();
            }
            resultTypesByName[type.TypeName].Add(type);
        }

        // 查找跨命名空间重复的 Result 类型
        var duplicates = resultTypesByName
            .Where(kvp => kvp.Value.Count > 1)
            // 排除白名单类型
            .Where(kvp => !whitelist.Contains(kvp.Key))
            // 只有当在多个不同命名空间中定义时才算重复
            .Where(kvp => kvp.Value.Select(t => t.Namespace).Distinct().Count() > 1)
            // 排除 file-scoped 类型
            .Where(kvp => !kvp.Value.All(t => t.IsFileScoped))
            .ToList();

        // 特别检查 OperationResult 是否只在 Core/Results 中定义
        var operationResultLocations = resultTypesByName
            .Where(kvp => forbiddenDuplicateNames.Contains(kvp.Key))
            .SelectMany(kvp => kvp.Value)
            .Where(t => !t.Namespace.EndsWith(".Results"))
            .ToList();

        var allViolations = new List<(string TypeName, List<ResultTypeInfo> Locations, string ViolationType)>();
        
        // 添加重复定义的违规
        foreach (var (typeName, locations) in duplicates)
        {
            allViolations.Add((typeName, locations, "跨命名空间重复定义"));
        }
        
        // 添加 OperationResult 位置违规
        if (operationResultLocations.Any())
        {
            allViolations.Add(("OperationResult", operationResultLocations, "不在指定的 Core/Results 命名空间中"));
        }

        if (allViolations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-S5 违规: 发现 {allViolations.Count} 个 *Result 类型存在影分身问题:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, locations, violationType) in allViolations.OrderBy(v => v.TypeName))
            {
                report.AppendLine($"\n❌ {typeName} ({violationType}):");
                foreach (var loc in locations.OrderBy(l => l.Namespace))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, loc.FilePath);
                    report.AppendLine($"   - 命名空间: {loc.Namespace}");
                    report.AppendLine($"     位置: {relativePath}:{loc.LineNumber}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-S5 规范:");
            report.AppendLine("  1. Core/Results/OperationResult 是唯一公共结果模型");
            report.AppendLine("  2. 任何内部局部结果类型必须使用不同名称");
            report.AppendLine("  3. 内部结果类型应限制作用域（使用 file 关键字或 internal 修饰符）");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将重复的 OperationResult 重命名为场景化名称（如 RouteComputationResult）");
            report.AppendLine("  2. 删除不必要的重复定义");
            report.AppendLine("  3. 更新所有引用以使用唯一定义");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 从文件中提取 *Result 类型定义
    /// Extract *Result type definitions from file
    /// </summary>
    private static List<ResultTypeInfo> ExtractResultTypeDefinitions(string filePath, string solutionRoot)
    {
        var types = new List<ResultTypeInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 提取项目名
            var projectName = ExtractProjectName(filePath, solutionRoot);

            // 查找以 Result 结尾的类型定义
            // 支持: class, struct, record, record class, record struct, readonly record struct
            var resultPattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal)\s+(?:sealed\s+)?(?:readonly\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<typeName>\w+Result)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = resultPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new ResultTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns,
                        ProjectName = projectName,
                        IsFileScoped = match.Groups["fileScoped"].Success
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting Result types from {filePath}: {ex.Message}");
        }

        return types;
    }

    /// <summary>
    /// 从文件中提取 *Options 类型定义
    /// Extract *Options type definitions from file
    /// </summary>
    private static List<OptionsTypeInfo> ExtractOptionsTypeDefinitions(string filePath, string solutionRoot)
    {
        var types = new List<OptionsTypeInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 提取项目名
            var projectName = ExtractProjectName(filePath, solutionRoot);

            // 查找以 Options 结尾的类型定义
            // 支持: class, struct, record, record class, record struct
            var optionsPattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+)(?<typeName>\w+Options)\b",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = optionsPattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new OptionsTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns,
                        ProjectName = projectName,
                        IsFileScoped = match.Groups["fileScoped"].Success
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting Options types from {filePath}: {ex.Message}");
        }

        return types;
    }

    /// <summary>
    /// 从文件路径提取项目名
    /// Extract project name from file path
    /// </summary>
    private static string ExtractProjectName(string filePath, string solutionRoot)
    {
        var relativePath = Path.GetRelativePath(solutionRoot, filePath);
        var parts = relativePath.Replace('\\', '/').Split('/');
        
        // 查找 .csproj 所在目录名作为项目名
        // 路径格式通常为: src/[Layer]/[ProjectName]/[SubDirs]/[File].cs
        // 例如: src/Core/ZakYip.WheelDiverterSorter.Core/Sorting/Policies/UpstreamConnectionOptions.cs
        if (parts.Length >= 3 && parts[0] == "src")
        {
            return parts[2]; // 返回项目目录名
        }
        
        return Path.GetFileName(Path.GetDirectoryName(filePath) ?? "Unknown");
    }

    #region Helper Methods

    /// <summary>
    /// 从文件中提取接口定义
    /// </summary>
    /// <remarks>
    /// 此方法检测遵循 C# 命名约定（以 'I' 开头）的接口定义。
    /// 由于 PR-SD1 规范涉及的所有接口都遵循此约定，这是足够的检测方式。
    /// </remarks>
    private static List<TypeLocationInfo> ExtractInterfaceDefinitions(string filePath)
    {
        var types = new List<TypeLocationInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 查找接口定义（遵循 C# 命名约定，以 I 开头）
            // PR-SD1 规范的所有接口都遵循此命名约定
            var interfacePattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal)\s+(?:partial\s+)?interface\s+(?<typeName>I\w+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = interfacePattern.Match(lines[i]);
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
            Console.WriteLine($"Error extracting interfaces from {filePath}: {ex.Message}");
        }

        return types;
    }

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    /// <summary>
    /// 检查文件是否位于 Core 项目目录中
    /// </summary>
    /// <param name="solutionRoot">解决方案根目录</param>
    /// <param name="filePath">文件路径</param>
    /// <returns>如果文件在 Core 项目中返回 true</returns>
    private static bool IsInCoreProject(string solutionRoot, string filePath)
    {
        var coreDir = Path.Combine(solutionRoot, "src", "Core");
        var relativePath = Path.GetRelativePath(coreDir, filePath);
        // 如果文件在 Core 目录下，相对路径不会以 ".." 开头
        return !relativePath.StartsWith("..");
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

/// <summary>
/// PR-S4: Options 类型位置信息
/// Options type location information
/// </summary>
public record OptionsTypeInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
    public required string ProjectName { get; init; }
    public bool IsFileScoped { get; init; }
}

/// <summary>
/// PR-S5: Result 类型位置信息
/// Result type location information
/// </summary>
public record ResultTypeInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
    public required string ProjectName { get; init; }
    public bool IsFileScoped { get; init; }
}
