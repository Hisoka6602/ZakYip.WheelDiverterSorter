using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// PR-SD7: 拓扑 &amp; 路径生成"影分身"防线测试
/// Architecture tests for Topology and Path execution single mainline
/// </summary>
/// <remarks>
/// 这些测试确保：
/// 1. Core/LineModel 负责拓扑模型与路径计划
/// 2. Execution 负责执行
/// 3. Simulation 只是调用同一条链路做仿真
/// 4. 禁止在非 Core 项目中出现拓扑/路径核心模型类型的定义
/// 
/// These tests ensure:
/// 1. Core/LineModel is responsible for topology models and path planning
/// 2. Execution is responsible for execution
/// 3. Simulation only calls the same chain for simulation
/// 4. Forbid topology/path core model type definitions in non-Core projects
/// </remarks>
public class TopologyPathExecutionDefenseTests
{
    private static readonly string SolutionRoot = GetSolutionRoot();

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
    /// PR-SD7: 核心拓扑模型类型只能在 Core 项目中定义
    /// Core topology model types should only be defined in Core project
    /// </summary>
    /// <remarks>
    /// 以下核心拓扑/路径模型类型只能在 Core/LineModel 定义：
    /// - SorterTopology
    /// - SwitchingPath
    /// - SwitchingPathSegment
    /// - RoutePlan
    /// - DiverterNode
    /// 
    /// 不适用于以下情况：
    /// - Host/Models 下的 API DTO（如 ChutePathTopologyRequest/Response）
    /// - Application 层的服务接口（如 IChutePathTopologyService）
    /// - 仓储实现（如 InMemoryRoutePlanRepository）
    /// - 执行器实现（如 ConcurrentSwitchingPathExecutor）
    /// </remarks>
    [Fact]
    public void CoreTopologyModelsShouldOnlyBeDefinedInCore()
    {
        // 核心拓扑/路径模型类型名称（精确匹配）
        var coreTopologyModelNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SorterTopology",
            "SwitchingPath",
            "SwitchingPathSegment",
            "RoutePlan",
            "DiverterNode"
        };

        var srcDir = Path.Combine(SolutionRoot, "src");
        var coreDir = Path.Combine(srcDir, "Core");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInCoreDirectory(coreDir, f)) // 排除 Core 项目
            .ToList();

        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            foreach (var type in types)
            {
                if (coreTopologyModelNames.Contains(type.TypeName))
                {
                    violations.Add((type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD7 违规: 在 Core 项目之外发现 {violations.Count} 个核心拓扑/路径模型类型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, filePath);
                report.AppendLine($"  ❌ {typeName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
                report.AppendLine($"     命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD7 规范:");
            report.AppendLine("  以下核心拓扑/路径模型类型只能在 Core/LineModel 定义:");
            foreach (var name in coreTopologyModelNames)
            {
                report.AppendLine($"     - {name}");
            }
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 删除非 Core 项目中的重复类型定义");
            report.AppendLine("  2. 改为引用 Core 项目中的统一定义");
            report.AppendLine("  3. 如果是 API DTO，使用不同的命名（如 *Request/*Response）");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD7: 禁止在非 Core 项目中定义类型名包含 Topology 的核心模型
    /// Forbid defining types with 'Topology' in name outside Core (except allowed patterns)
    /// </summary>
    /// <remarks>
    /// 允许的例外：
    /// - Host/Models 下的 API DTO（如 *TopologyRequest, *TopologyResponse, *TopologyDto）
    /// - Application 层的服务接口/实现（如 *TopologyService）
    /// - Worker 类型（如 *TopologyCheckWorker）
    /// - 仓储接口/实现（如 *TopologyRepository）
    /// - Controller 类型（如 *TopologyController）
    /// </remarks>
    [Fact]
    public void NonCoreProjectsShouldNotDefineTopologyModelTypes()
    {
        var srcDir = Path.Combine(SolutionRoot, "src");
        var coreDir = Path.Combine(srcDir, "Core");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInCoreDirectory(coreDir, f)) // 排除 Core 项目
            .ToList();

        // 允许的后缀模式（服务/DTO/仓储/Worker/Controller）
        var allowedSuffixPatterns = new[]
        {
            "Request", "Response", "Result", "Dto", "Service", "Repository", "Worker", "Controller", "Config"
        };

        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            foreach (var type in types)
            {
                // 检查类型名是否包含 "Topology"
                if (type.TypeName.Contains("Topology", StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否是允许的模式（服务/DTO/仓储）
                    var isAllowed = allowedSuffixPatterns.Any(suffix =>
                        type.TypeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                    // 检查是否是接口（I开头）
                    var isInterface = type.TypeName.StartsWith("I") &&
                                      type.TypeName.Length > 1 &&
                                      char.IsUpper(type.TypeName[1]);

                    if (!isAllowed && !isInterface)
                    {
                        violations.Add((type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD7 违规: 在非 Core 项目中发现 {violations.Count} 个包含 'Topology' 的核心模型类型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, filePath);
                report.AppendLine($"  ❌ {typeName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
                report.AppendLine($"     命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD7 规范:");
            report.AppendLine("  拓扑核心模型只能在 Core/LineModel/Topology 定义。");
            report.AppendLine("\n  允许的例外（不受此规则限制）:");
            report.AppendLine("    - *TopologyService, *TopologyRepository, *TopologyController");
            report.AppendLine("    - *TopologyRequest, *TopologyResponse, *TopologyDto, *TopologyConfig");
            report.AppendLine("    - *TopologyWorker, I*Topology* (接口)");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将核心拓扑模型移动到 Core/LineModel/Topology");
            report.AppendLine("  2. 或者重命名为服务/DTO 后缀的类型");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD7: 禁止在非 Core 项目中定义类型名包含 SwitchingPath 的核心模型
    /// Forbid defining types with 'SwitchingPath' in name outside Core (except implementations)
    /// </summary>
    /// <remarks>
    /// 允许的例外：
    /// - 实现 ISwitchingPathGenerator 的类型（如 CachedSwitchingPathGenerator）
    /// - 实现 ISwitchingPathExecutor 的类型（如 ConcurrentSwitchingPathExecutor, MockSwitchingPathExecutor）
    /// </remarks>
    [Fact]
    public void NonCoreProjectsShouldNotDefineSwitchingPathModelTypes()
    {
        var srcDir = Path.Combine(SolutionRoot, "src");
        var coreDir = Path.Combine(srcDir, "Core");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInCoreDirectory(coreDir, f)) // 排除 Core 项目
            .ToList();

        // 允许的后缀模式（实现类）
        var allowedSuffixPatterns = new[]
        {
            "Generator", "Executor", "Service", "Middleware"
        };

        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            foreach (var type in types)
            {
                // 检查类型名是否包含 "SwitchingPath"
                if (type.TypeName.Contains("SwitchingPath", StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否是允许的模式（实现类）
                    var isAllowed = allowedSuffixPatterns.Any(suffix =>
                        type.TypeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                    // 检查是否是接口（I开头）
                    var isInterface = type.TypeName.StartsWith("I") &&
                                      type.TypeName.Length > 1 &&
                                      char.IsUpper(type.TypeName[1]);

                    if (!isAllowed && !isInterface)
                    {
                        violations.Add((type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD7 违规: 在非 Core 项目中发现 {violations.Count} 个包含 'SwitchingPath' 的核心模型类型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, filePath);
                report.AppendLine($"  ❌ {typeName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
                report.AppendLine($"     命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD7 规范:");
            report.AppendLine("  SwitchingPath 核心模型只能在 Core/LineModel/Topology 定义。");
            report.AppendLine("\n  允许的例外（不受此规则限制）:");
            report.AppendLine("    - *SwitchingPathGenerator (实现 ISwitchingPathGenerator)");
            report.AppendLine("    - *SwitchingPathExecutor (实现 ISwitchingPathExecutor)");
            report.AppendLine("    - *SwitchingPathService, *SwitchingPathMiddleware");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将核心 SwitchingPath 模型移动到 Core/LineModel/Topology");
            report.AppendLine("  2. 非 Core 项目只能实现接口，不能重新定义模型");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD7: 禁止在非 Core 项目中定义类型名包含 RoutePlan 的核心模型
    /// Forbid defining types with 'RoutePlan' in name outside Core (except implementations)
    /// </summary>
    /// <remarks>
    /// 允许的例外：
    /// - 实现 IRoutePlanRepository 的类型（如 InMemoryRoutePlanRepository）
    /// - Middleware 类型（如 RoutePlanningMiddleware）
    /// </remarks>
    [Fact]
    public void NonCoreProjectsShouldNotDefineRoutePlanModelTypes()
    {
        var srcDir = Path.Combine(SolutionRoot, "src");
        var coreDir = Path.Combine(srcDir, "Core");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInCoreDirectory(coreDir, f)) // 排除 Core 项目
            .ToList();

        // 允许的后缀模式（实现类）
        var allowedSuffixPatterns = new[]
        {
            "Repository", "Service", "Middleware", "Manager", "Handler"
        };

        var violations = new List<(string TypeName, string FilePath, int LineNumber, string Namespace)>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            foreach (var type in types)
            {
                // 检查类型名是否包含 "RoutePlan" (但不是 "RoutePlanning")
                if (type.TypeName.Contains("RoutePlan", StringComparison.OrdinalIgnoreCase) &&
                    !type.TypeName.Contains("RoutePlanning", StringComparison.OrdinalIgnoreCase))
                {
                    // 检查是否是允许的模式（实现类）
                    var isAllowed = allowedSuffixPatterns.Any(suffix =>
                        type.TypeName.EndsWith(suffix, StringComparison.OrdinalIgnoreCase));

                    // 检查是否是接口（I开头）
                    var isInterface = type.TypeName.StartsWith("I") &&
                                      type.TypeName.Length > 1 &&
                                      char.IsUpper(type.TypeName[1]);

                    if (!isAllowed && !isInterface)
                    {
                        violations.Add((type.TypeName, type.FilePath, type.LineNumber, type.Namespace));
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD7 违规: 在非 Core 项目中发现 {violations.Count} 个包含 'RoutePlan' 的核心模型类型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath, lineNumber, ns) in violations)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, filePath);
                report.AppendLine($"  ❌ {typeName}");
                report.AppendLine($"     位置: {relativePath}:{lineNumber}");
                report.AppendLine($"     命名空间: {ns}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD7 规范:");
            report.AppendLine("  RoutePlan 核心模型只能在 Core/LineModel/Routing 定义。");
            report.AppendLine("\n  允许的例外（不受此规则限制）:");
            report.AppendLine("    - *RoutePlanRepository (实现 IRoutePlanRepository)");
            report.AppendLine("    - *RoutePlanService, *RoutePlanManager, *RoutePlanHandler");
            report.AppendLine("    - RoutePlanningMiddleware");
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将核心 RoutePlan 模型移动到 Core/LineModel/Routing");
            report.AppendLine("  2. 非 Core 项目只能实现接口，不能重新定义模型");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD7: 验证 ISwitchingPathGenerator 只在 Core/LineModel/Topology 定义
    /// Verify ISwitchingPathGenerator is only defined in Core/LineModel/Topology
    /// </summary>
    [Fact]
    public void ISwitchingPathGenerator_ShouldOnlyBeDefinedInCoreTopology()
    {
        var srcDir = Path.Combine(SolutionRoot, "src");
        var coreTopologyDir = Path.Combine(srcDir, "Core", "ZakYip.WheelDiverterSorter.Core", "LineModel", "Topology");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var definitions = new List<(string FilePath, int LineNumber)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var lines = File.ReadAllLines(file);

            // 查找接口定义
            var pattern = new Regex(
                @"^\s*(?:public|internal)\s+interface\s+ISwitchingPathGenerator\b",
                RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    definitions.Add((file, i + 1));
                }
            }
        }

        // 应该只有一个定义
        Assert.Single(definitions);

        // 并且应该在 Core/LineModel/Topology 目录
        var (filePath, _) = definitions[0];
        var normalizedPath = filePath.Replace('\\', '/');
        Assert.Contains("/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Topology/", normalizedPath);
    }

    /// <summary>
    /// PR-SD7: 验证 ISwitchingPathExecutor 只在 Core/Abstractions/Execution 定义
    /// Verify ISwitchingPathExecutor is only defined in Core/Abstractions/Execution
    /// </summary>
    [Fact]
    public void ISwitchingPathExecutor_ShouldOnlyBeDefinedInCoreAbstractions()
    {
        var srcDir = Path.Combine(SolutionRoot, "src");

        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var definitions = new List<(string FilePath, int LineNumber)>();

        foreach (var file in sourceFiles)
        {
            var lines = File.ReadAllLines(file);

            // 查找接口定义
            var pattern = new Regex(
                @"^\s*(?:public|internal)\s+interface\s+ISwitchingPathExecutor\b",
                RegexOptions.Compiled);

            for (int i = 0; i < lines.Length; i++)
            {
                if (pattern.IsMatch(lines[i]))
                {
                    definitions.Add((file, i + 1));
                }
            }
        }

        // 应该只有一个定义
        Assert.Single(definitions);

        // 并且应该在 Core/Abstractions 目录
        var (filePath, _) = definitions[0];
        var normalizedPath = filePath.Replace('\\', '/');
        Assert.Contains("/Core/ZakYip.WheelDiverterSorter.Core/Abstractions/", normalizedPath);
    }

    /// <summary>
    /// PR-SD7: 生成拓扑/路径类型分布报告
    /// Generate topology/path type distribution report
    /// </summary>
    [Fact]
    public void GenerateTopologyPathTypeDistributionReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# PR-SD7: Topology/Path Type Distribution Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var srcDir = Path.Combine(SolutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var topologyTypes = new Dictionary<string, List<string>>();
        var switchingPathTypes = new Dictionary<string, List<string>>();
        var routePlanTypes = new Dictionary<string, List<string>>();
        var pathGeneratorTypes = new Dictionary<string, List<string>>();

        foreach (var file in sourceFiles)
        {
            var types = ExtractTypeDefinitions(file);
            var relativePath = Path.GetRelativePath(SolutionRoot, file);

            foreach (var type in types)
            {
                if (type.TypeName.Contains("Topology", StringComparison.OrdinalIgnoreCase))
                {
                    if (!topologyTypes.ContainsKey(type.TypeName))
                        topologyTypes[type.TypeName] = new List<string>();
                    topologyTypes[type.TypeName].Add(relativePath);
                }

                if (type.TypeName.Contains("SwitchingPath", StringComparison.OrdinalIgnoreCase))
                {
                    if (!switchingPathTypes.ContainsKey(type.TypeName))
                        switchingPathTypes[type.TypeName] = new List<string>();
                    switchingPathTypes[type.TypeName].Add(relativePath);
                }

                if (type.TypeName.Contains("RoutePlan", StringComparison.OrdinalIgnoreCase))
                {
                    if (!routePlanTypes.ContainsKey(type.TypeName))
                        routePlanTypes[type.TypeName] = new List<string>();
                    routePlanTypes[type.TypeName].Add(relativePath);
                }

                if (type.TypeName.Contains("PathGenerator", StringComparison.OrdinalIgnoreCase) ||
                    type.TypeName.Contains("RoutePlanner", StringComparison.OrdinalIgnoreCase))
                {
                    if (!pathGeneratorTypes.ContainsKey(type.TypeName))
                        pathGeneratorTypes[type.TypeName] = new List<string>();
                    pathGeneratorTypes[type.TypeName].Add(relativePath);
                }
            }
        }

        // Output reports
        report.AppendLine("## Types containing 'Topology'\n");
        OutputTypeReport(report, topologyTypes);

        report.AppendLine("\n## Types containing 'SwitchingPath'\n");
        OutputTypeReport(report, switchingPathTypes);

        report.AppendLine("\n## Types containing 'RoutePlan'\n");
        OutputTypeReport(report, routePlanTypes);

        report.AppendLine("\n## Types containing 'PathGenerator' or 'RoutePlanner'\n");
        OutputTypeReport(report, pathGeneratorTypes);

        Console.WriteLine(report.ToString());
        Assert.True(true);
    }

    private static void OutputTypeReport(StringBuilder report, Dictionary<string, List<string>> types)
    {
        if (!types.Any())
        {
            report.AppendLine("_None found_");
            return;
        }

        foreach (var (typeName, locations) in types.OrderBy(t => t.Key))
        {
            report.AppendLine($"### {typeName}");
            foreach (var location in locations)
            {
                report.AppendLine($"- {location}");
            }
            report.AppendLine();
        }
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsInCoreDirectory(string coreDir, string filePath)
    {
        var normalizedCoreDir = coreDir.Replace('\\', '/');
        var normalizedFilePath = filePath.Replace('\\', '/');
        return normalizedFilePath.StartsWith(normalizedCoreDir, StringComparison.OrdinalIgnoreCase);
    }

    private static List<TypeDefinition> ExtractTypeDefinitions(string filePath)
    {
        var types = new List<TypeDefinition>();

        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);

            // 提取命名空间（支持传统语法和 C# 10+ file-scoped 语法）
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 查找类型定义
            var typePattern = new Regex(
                @"^\s*(?<fileScoped>file\s+)?(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:static\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+|interface\s+|enum\s+)(?<typeName>\w+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = typePattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new TypeDefinition
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

    private record TypeDefinition
    {
        public required string TypeName { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string Namespace { get; init; }
        public bool IsFileScoped { get; init; }
    }

    #endregion
}
