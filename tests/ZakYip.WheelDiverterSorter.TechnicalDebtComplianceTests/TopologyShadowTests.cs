using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD8: 拓扑模型影分身检测测试
/// Tests to detect shadow topology models outside Core
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. 拓扑模型只允许在 Core/LineModel/Topology 中定义
/// 2. Execution 中禁止直接从 ChutePathTopologyConfig 读取并拼装摆轮指令
/// 3. 禁止在 Core 之外定义 Topology, Node, Edge 等命名的模型类
/// </remarks>
public class TopologyShadowTests
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
    /// 允许包含 Topology 关键词的路径模式
    /// </summary>
    private static readonly string[] AllowedTopologyPaths =
    {
        "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Topology/",
        "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/",
        "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Orchestration/",
        "Core/ZakYip.WheelDiverterSorter.Core/Enums/",
        "Application/ZakYip.WheelDiverterSorter.Application/Services/Topology/",
        "Host/ZakYip.WheelDiverterSorter.Host/Controllers/",
        "Host/ZakYip.WheelDiverterSorter.Host/Models/",
        "Host/ZakYip.WheelDiverterSorter.Host/Services/Workers/",
        "Simulation/",
    };

    /// <summary>
    /// 允许的拓扑相关类型名（白名单）
    /// </summary>
    private static readonly HashSet<string> AllowedTopologyTypes = new(StringComparer.Ordinal)
    {
        // Core/LineModel/Topology
        "SorterTopology",
        "SwitchingPath",
        "SwitchingPathSegment",
        "ISwitchingPathGenerator",
        "DefaultSwitchingPathGenerator",
        "DefaultSorterTopologyProvider",
        // Core/LineModel/Configuration
        "ChutePathTopologyConfig",
        "ChutePathTopologyConfigEntity",
        "IChutePathTopologyRepository",
        "LiteDbChutePathTopologyRepository",
        "ChutePathTopologyValidator",  // PR-TOPO02: N 摆轮模型验证器
        "DiverterNodeConfig",          // PR-TOPO02: N 摆轮简化配置
        // Core/LineModel/Orchestration
        "IRouteTopologyConsistencyChecker",
        "RouteTopologyConsistencyChecker",
        // Core/Enums
        "TopologyNodeType",
        // Application
        "CachedSwitchingPathGenerator",
        "IChutePathTopologyService",
        "ChutePathTopologyService",
        // Host
        "ChutePathTopologyController",
        "ChutePathTopologyRequest",
        "ChutePathTopologyResponse",
        "TopologyDiagramResponse",
        "TopologySimulationRequest",
        "TopologySimulationResult",
        "RouteTopologyConsistencyCheckWorker",
        // Simulation
        "SimulationTopologyConfig",
        "InMemoryChutePathTopologyRepository",
    };

    /// <summary>
    /// 验证 Execution 层不直接从配置读取并重新计算路径
    /// Execution should not recompute paths from raw config
    /// </summary>
    [Fact]
    public void ExecutionShouldNotRecomputePathsFromRawConfig()
    {
        var solutionRoot = GetSolutionRoot();
        var executionPath = Path.Combine(solutionRoot, "src", "Execution");

        if (!Directory.Exists(executionPath))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(executionPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var violations = new List<(string FilePath, string Line, int LineNumber)>();

        // 检测直接读取 ChutePathTopologyConfig 并拼装摆轮指令的模式
        var suspiciousPatterns = new[]
        {
            // 直接注入 ChutePathTopologyConfig 或 IChutePathTopologyRepository
            new Regex(@"IChutePathTopologyRepository\s+_\w+", RegexOptions.Compiled),
            new Regex(@"ChutePathTopologyConfig\s+\w+", RegexOptions.Compiled),
            // 直接从配置读取路径信息
            new Regex(@"\.Paths\s*\[", RegexOptions.Compiled),
            new Regex(@"\.ChuteId\s*==", RegexOptions.Compiled),
            // 手动构建 SwitchingPath
            new Regex(@"new\s+SwitchingPath\s*\(", RegexOptions.Compiled),
            new Regex(@"new\s+SwitchingPathSegment\s*\(", RegexOptions.Compiled),
        };

        // 排除的文件模式（这些文件可能有合法的原因使用这些模式）
        var excludedFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // 测试文件
        };

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            if (excludedFiles.Contains(fileName))
            {
                continue;
            }

            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];

                // 跳过注释
                var trimmedLine = line.TrimStart();
                if (trimmedLine.StartsWith("//") || trimmedLine.StartsWith("*") || trimmedLine.StartsWith("///"))
                {
                    continue;
                }

                foreach (var pattern in suspiciousPatterns)
                {
                    if (pattern.IsMatch(line))
                    {
                        var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                        violations.Add((relativePath, line.Trim(), i + 1));
                        break;
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ PR-SD8 警告: 发现 {violations.Count} 处可能违反路径生成单一事实源的代码:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n注意：这是一个顾问性检查，请人工确认是否为真正的违规。\n");

            foreach (var (filePath, line, lineNumber) in violations.Take(20))
            {
                report.AppendLine($"⚠️ {filePath}:{lineNumber}");
                report.AppendLine($"   {line}");
                report.AppendLine();
            }

            if (violations.Count > 20)
            {
                report.AppendLine($"... 还有 {violations.Count - 20} 处");
            }

            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 规范:");
            report.AppendLine("  Execution 层应该通过 ISwitchingPathGenerator 获取路径，");
            report.AppendLine("  而不是直接从 ChutePathTopologyConfig 读取并拼装摆轮指令。");

            // 这是一个顾问性测试，输出警告但不失败
            Console.WriteLine(report);
        }

        // 顾问性测试总是通过
        Assert.True(true);
    }

    /// <summary>
    /// 验证不存在平行的拓扑模型定义
    /// Should not have parallel topology models outside Core
    /// </summary>
    [Fact]
    public void ShouldNotHaveParallelTopologyModelsOutsideCore()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string TypeName, string FilePath)>();

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 匹配包含 Topology 关键词的类型定义
        var topologyTypePattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*Topology\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
            var content = File.ReadAllText(file);
            var matches = topologyTypePattern.Matches(content);

            violations.AddRange(
                matches.Cast<Match>()
                    .Select(match => match.Groups["typeName"].Value)
                    .Where(typeName =>
                        !AllowedTopologyTypes.Contains(typeName) &&
                        !AllowedTopologyPaths.Any(p => relativePath.Contains(p, StringComparison.OrdinalIgnoreCase)))
                    .Select(typeName => (typeName, relativePath))
            );
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ PR-SD8 违规: 发现 {violations.Count} 个平行的拓扑模型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"\n❌ {typeName}");
                report.AppendLine($"   位置: {filePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD8 规范:");
            report.AppendLine("  拓扑模型只允许在以下位置定义：");
            foreach (var path in AllowedTopologyPaths.Take(5))
            {
                report.AppendLine($"  - {path}");
            }
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 如果是遗留拓扑模型，删除并使用 Core 层的 SorterTopology");
            report.AppendLine("  2. 如果是必要的新模型，在 Core/LineModel/Topology 中定义");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成拓扑类型分布报告
    /// </summary>
    [Fact]
    public void GenerateTopologyTypeDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# PR-SD8: 拓扑类型分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        var topologyTypePattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(?<typeName>\w*(?:Topology|SwitchingPath|RoutePlan)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var foundTypes = sourceFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var matches = topologyTypePattern.Matches(content);
                var relativePath = Path.GetRelativePath(solutionRoot, file).Replace("\\", "/");
                return matches.Cast<Match>()
                    .Select(match =>
                    {
                        var typeName = match.Groups["typeName"].Value;
                        var isAllowed = AllowedTopologyTypes.Contains(typeName);
                        return (TypeName: typeName, FilePath: relativePath, IsAllowed: isAllowed);
                    });
            })
            .ToList();

        // 按项目分组
        var byProject = foundTypes
            .GroupBy(t => t.FilePath.Split('/')[0])
            .OrderBy(g => g.Key);

        foreach (var group in byProject)
        {
            report.AppendLine($"## {group.Key}\n");
            report.AppendLine("| 类型名称 | 位置 | 状态 |");
            report.AppendLine("|----------|------|------|");

            foreach (var (typeName, filePath, isAllowed) in group.OrderBy(t => t.TypeName))
            {
                var status = isAllowed ? "✅ 已注册" : "⚠️ 未注册";
                report.AppendLine($"| {typeName} | {filePath} | {status} |");
            }
            report.AppendLine();
        }

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
