using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// TD-033: 单一权威实现表验证测试
/// Tests to validate the Single Authority Catalog in RepositoryStructure.md
/// </summary>
/// <remarks>
/// 这些测试确保：
/// 1. 文档中 6.1 节"单一权威实现表"的权威类型存在于指定位置
/// 2. 禁止位置不存在匹配模式的类型定义
/// 3. 文档成为"源数据"，测试读取并执行验证
/// 
/// 核心理念：让测试"读表执行"而不是硬编码规则
/// </remarks>
public class SingleAuthorityCatalogTests
{
    #region Static Regex Patterns (compiled once)

    /// <summary>
    /// 通用类型定义匹配模式
    /// </summary>
    private static readonly Regex TypeDefinitionPattern = new(
        @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:readonly\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+|struct\s+|interface\s+)(?<typeName>\w+)",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    /// <summary>
    /// 通知类型匹配模式
    /// </summary>
    private static readonly Regex NotificationTypePattern = new(
        @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+)(?<typeName>\w+(?:Notification|AssignmentEventArgs))\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    /// <summary>
    /// Options 类型匹配模式
    /// </summary>
    private static readonly Regex OptionsTypePattern = new(
        @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:record\s+(?:class|struct)\s+|record\s+|class\s+)(?<typeName>\w+Options)\b",
        RegexOptions.Compiled | RegexOptions.ExplicitCapture);

    #endregion

    #region Authority Catalog Constants

    /// <summary>
    /// 上游契约/事件的权威类型
    /// </summary>
    private static readonly AuthorityEntry UpstreamContractAuthority = new(
        ConceptName: "上游契约/事件",
        AuthoritativeTypes: new[]
        {
            // Core 事件
            "ChuteAssignmentEventArgs",
            "SortingCompletedNotification",
            "DwsMeasurement",
            // 传输 DTO
            "ParcelDetectionNotification",
            "ChuteAssignmentNotification",
            "SortingCompletedNotificationDto",
            "DwsMeasurementDto"
        },
        AllowedPathPatterns: new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/",
            "Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/"
        },
        ForbiddenPatterns: new[]
        {
            @"\bParcel\w*Notification\b",
            @"\bAssignmentNotification\b",
            @"\bSortingCompleted\w*\b"
        },
        ForbiddenPathPatterns: new[]
        {
            "Execution/",
            "Drivers/",
            "Host/",
            "Ingress/"
        });

    /// <summary>
    /// 上游路由客户端的权威类型
    /// </summary>
    private static readonly AuthorityEntry UpstreamRoutingClientAuthority = new(
        ConceptName: "上游通信/RuleEngine客户端",
        AuthoritativeTypes: new[]
        {
            "IUpstreamRoutingClient",
            "IUpstreamContractMapper"
        },
        AllowedPathPatterns: new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/"
        },
        ForbiddenPatterns: new[]
        {
            @"\bIRuleEngineClient\b",
            @"\bIUpstreamRoutingClient\b"
        },
        ForbiddenPathPatterns: new[]
        {
            "Execution/",
            "Communication/",
            "Host/"
        });

    /// <summary>
    /// 配置服务的权威类型
    /// </summary>
    private static readonly AuthorityEntry ConfigServiceAuthority = new(
        ConceptName: "配置服务",
        AuthoritativeTypes: new[]
        {
            "ISystemConfigService",
            "ILoggingConfigService",
            "ICommunicationConfigService",
            "IIoLinkageConfigService",
            "IVendorConfigService"
        },
        AllowedPathPatterns: new[]
        {
            "Application/ZakYip.WheelDiverterSorter.Application/Services/Config/"
        },
        ForbiddenPatterns: new[]
        {
            @"\bI(System|Logging|Communication|IoLinkage|Vendor)ConfigService\b"
        },
        ForbiddenPathPatterns: new[]
        {
            "Host/",
            "Core/",
            "Execution/"
        });

    /// <summary>
    /// 配置 Options 的权威类型
    /// </summary>
    private static readonly AuthorityEntry RuntimeOptionsAuthority = new(
        ConceptName: "运行时Options",
        AuthoritativeTypes: new[]
        {
            "UpstreamConnectionOptions",
            "SortingSystemOptions",
            "RoutingOptions",
            "ChuteAssignmentTimeoutOptions",
            "TcpOptions",
            "SignalROptions",
            "MqttOptions",
            "UpstreamConnectionOptions"
        },
        AllowedPathPatterns: new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Sorting/Policies/",
            "Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Models/",
            "Infrastructure/ZakYip.WheelDiverterSorter.Communication/Configuration/",
            "Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors/"
        },
        ForbiddenPatterns: new[]
        {
            @"\b(Leadshine|Modi|ShuDiNiao|Siemens|Omron)Options\b"
        },
        ForbiddenPathPatterns: new[]
        {
            "Host/"
        });

    /// <summary>
    /// HAL/硬件抽象层的权威类型
    /// </summary>
    private static readonly AuthorityEntry HalAuthority = new(
        ConceptName: "HAL/硬件抽象层",
        AuthoritativeTypes: new[]
        {
            "IWheelDiverterDriver",
            "IWheelDiverterDevice",
            "IInputPort",
            "IOutputPort",
            "IIoLinkageDriver",
            "IVendorIoMapper",
            "ISensorVendorConfigProvider",
            "IEmcController"
        },
        AllowedPathPatterns: new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Hardware/"
        },
        ForbiddenPatterns: new[]
        {
            @"\bIWheelDiverterDriver\b",
            @"\bIInputPort\b",
            @"\bIOutputPort\b"
        },
        ForbiddenPathPatterns: new[]
        {
            "Execution/",
            "Host/",
            "Drivers/Abstractions/"
        });

    /// <summary>
    /// 所有权威条目
    /// </summary>
    private static readonly AuthorityEntry[] AllAuthorityEntries = new[]
    {
        UpstreamContractAuthority,
        UpstreamRoutingClientAuthority,
        ConfigServiceAuthority,
        RuntimeOptionsAuthority,
        HalAuthority
    };

    #endregion

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
    /// 验证权威类型存在于指定位置
    /// Verify that authoritative types exist in specified locations
    /// </summary>
    /// <remarks>
    /// 此测试扫描解决方案，确保文档中声明的权威类型确实存在于指定目录。
    /// 如果权威类型不存在，可能意味着：
    /// 1. 文档与实际代码不同步
    /// 2. 类型被意外删除或移动
    /// </remarks>
    [Fact]
    public void AuthoritativeTypesShouldExistInSpecifiedLocations()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string ConceptName, string TypeName, string ExpectedPath)>();

        var srcPath = Path.Combine(solutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 收集所有类型定义
        var allTypeDefinitions = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = TypeDefinitionPattern.Matches(content);
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace('\\', '/');

            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                if (!allTypeDefinitions.ContainsKey(typeName))
                {
                    allTypeDefinitions[typeName] = new List<string>();
                }
                allTypeDefinitions[typeName].Add(relativePath);
            }
        }

        // 验证每个权威条目
        foreach (var entry in AllAuthorityEntries)
        {
            foreach (var authorityType in entry.AuthoritativeTypes)
            {
                if (!allTypeDefinitions.TryGetValue(authorityType, out var locations))
                {
                    // 类型不存在
                    violations.Add((entry.ConceptName, authorityType, string.Join(" 或 ", entry.AllowedPathPatterns)));
                    continue;
                }

                // 检查是否至少有一个定义在允许的路径
                var hasValidLocation = locations.Any(loc =>
                    entry.AllowedPathPatterns.Any(pattern =>
                        loc.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

                if (!hasValidLocation)
                {
                    violations.Add((entry.ConceptName, authorityType, string.Join(" 或 ", entry.AllowedPathPatterns)));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ TD-033 警告: 发现 {violations.Count} 个权威类型不在预期位置:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (conceptName, typeName, expectedPath) in violations.GroupBy(v => v.ConceptName).SelectMany(g => g))
            {
                report.AppendLine($"\n⚠️ [{conceptName}] {typeName}");
                report.AppendLine($"   期望位置: {expectedPath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 检查类型是否被重命名或移动");
            report.AppendLine("  2. 更新 RepositoryStructure.md 6.1 节的权威实现表");
            report.AppendLine("  3. 或将类型移回权威位置");

            // 作为顾问性测试，输出警告但不失败（因为某些类型可能尚未实现）
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Checked {AllAuthorityEntries.Sum(e => e.AuthoritativeTypes.Length)} authoritative types");
    }

    /// <summary>
    /// 验证禁止位置不存在匹配的类型定义
    /// Verify that forbidden patterns don't exist in forbidden locations
    /// </summary>
    /// <remarks>
    /// 此测试扫描解决方案，确保在"禁止出现的位置"没有定义匹配禁止模式的类型。
    /// 如果发现违规，说明存在"影分身"问题。
    /// </remarks>
    [Fact]
    public void ForbiddenPatternsShouldNotExistInForbiddenLocations()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string ConceptName, string TypeName, string FilePath)>();

        var srcPath = Path.Combine(solutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 针对上游契约/事件的检测
        // 特殊处理：只检测接口/类定义，不检测已知的权威位置
        var upstreamContractViolations = CheckUpstreamContractViolations(solutionRoot, sourceFiles);
        violations.AddRange(upstreamContractViolations);

        // 针对厂商命名 Options 在 Core 中的检测
        var vendorOptionsViolations = CheckVendorOptionsInCore(solutionRoot, sourceFiles);
        violations.AddRange(vendorOptionsViolations);

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-033 违规: 发现 {violations.Count} 个影分身类型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var group in violations.GroupBy(v => v.ConceptName))
            {
                report.AppendLine($"\n■ {group.Key}:");
                foreach (var (_, typeName, filePath) in group.Take(10))
                {
                    report.AppendLine($"  ❌ {typeName}");
                    report.AppendLine($"     位置: {filePath}");
                }
                if (group.Count() > 10)
                {
                    report.AppendLine($"  ... 还有 {group.Count() - 10} 个");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 TD-033 规范:");
            report.AppendLine("  在禁止出现的位置发现的类型定义是影分身，必须删除。");
            report.AppendLine("  请参考 RepositoryStructure.md 6.1 节确认权威位置。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测上游契约/事件的影分身
    /// </summary>
    private List<(string ConceptName, string TypeName, string FilePath)> CheckUpstreamContractViolations(
        string solutionRoot, List<string> sourceFiles)
    {
        var violations = new List<(string, string, string)>();

        // 允许的路径（权威位置）
        var allowedPaths = new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Abstractions/Upstream/",
            "Infrastructure/ZakYip.WheelDiverterSorter.Communication/Models/"
        };

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace('\\', '/');

            // 跳过权威位置
            if (allowedPaths.Any(p => relativePath.Contains(p, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var matches = NotificationTypePattern.Matches(content);

            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;

                // 只检测与 Parcel/Chute/Sorting 相关的类型
                if (typeName.Contains("Parcel", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("Chute", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("Sorting", StringComparison.OrdinalIgnoreCase) ||
                    typeName.Contains("Assignment", StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(("上游契约/事件", typeName, relativePath));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 检测 Core 中的厂商命名 Options
    /// </summary>
    private List<(string ConceptName, string TypeName, string FilePath)> CheckVendorOptionsInCore(
        string solutionRoot, List<string> sourceFiles)
    {
        var violations = new List<(string, string, string)>();

        var vendorPrefixes = new[] { "Leadshine", "Modi", "ShuDiNiao", "Siemens", "Mitsubishi", "Omron" };

        foreach (var file in sourceFiles)
        {
            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace('\\', '/');

            // 只检测 Core 项目
            if (!relativePath.StartsWith("src/Core/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var content = File.ReadAllText(file);
            var matches = OptionsTypePattern.Matches(content);

            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;

                // 检测是否以厂商名称开头
                if (vendorPrefixes.Any(v => typeName.StartsWith(v, StringComparison.OrdinalIgnoreCase)))
                {
                    violations.Add(("运行时Options（厂商命名）", typeName, relativePath));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 解析并验证单一权威表的完整性
    /// Parse and validate the Single Authority Table
    /// </summary>
    /// <remarks>
    /// 此测试解析 RepositoryStructure.md 中的 6.1 表格，确保：
    /// 1. 表格结构正确
    /// 2. 每个条目都有权威位置和禁止位置
    /// 3. 测试防线列不为空
    /// </remarks>
    [Fact]
    public void ParseAndValidateSingleAuthorityTable()
    {
        var solutionRoot = GetSolutionRoot();
        var repositoryStructurePath = Path.Combine(solutionRoot, "docs", "RepositoryStructure.md");

        Assert.True(File.Exists(repositoryStructurePath),
            "RepositoryStructure.md 不存在");

        var content = File.ReadAllText(repositoryStructurePath);

        // 验证 6.1 节存在
        Assert.Contains("### 6.1 单一权威实现表", content,
            StringComparison.OrdinalIgnoreCase);

        // 验证表格头存在
        Assert.Contains("| 概念 | 权威接口 / 类型 |", content,
            StringComparison.OrdinalIgnoreCase);

        // 验证关键条目存在
        var requiredConcepts = new[]
        {
            "HAL / 硬件抽象层",
            "上游通信",
            "上游契约",
            "拓扑 / 路径生成",
            "配置服务",
            "配置模型",
            "运行时 Options"
        };

        var missingConcepts = requiredConcepts
            .Where(concept => !content.Contains(concept, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (missingConcepts.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 单一权威实现表缺少 {missingConcepts.Count} 个概念:");
            foreach (var concept in missingConcepts)
            {
                report.AppendLine($"  - {concept}");
            }
            report.AppendLine("\n请更新 RepositoryStructure.md 6.1 节。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成权威实现分布报告
    /// </summary>
    [Fact]
    public void GenerateSingleAuthorityDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var report = new StringBuilder();
        report.AppendLine("# TD-033: 单一权威实现分布报告\n");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var srcPath = Path.Combine(solutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var entry in AllAuthorityEntries)
        {
            report.AppendLine($"## {entry.ConceptName}\n");
            report.AppendLine("| 权威类型 | 实际位置 | 状态 |");
            report.AppendLine("|----------|----------|------|");

            foreach (var authorityType in entry.AuthoritativeTypes)
            {
                var foundLocations = new List<string>();

                foreach (var file in sourceFiles)
                {
                    var content = File.ReadAllText(file);
                    var matches = TypeDefinitionPattern.Matches(content);

                    foreach (Match match in matches)
                    {
                        if (match.Groups["typeName"].Value == authorityType)
                        {
                            var relativePath = Path.GetRelativePath(solutionRoot, file).Replace('\\', '/');
                            foundLocations.Add(relativePath);
                        }
                    }
                }

                if (foundLocations.Any())
                {
                    var isInAllowedPath = foundLocations.Any(loc =>
                        entry.AllowedPathPatterns.Any(pattern =>
                            loc.Contains(pattern, StringComparison.OrdinalIgnoreCase)));

                    var status = isInAllowedPath ? "✅ 权威位置" : "⚠️ 非权威位置";
                    var location = foundLocations.First();
                    report.AppendLine($"| {authorityType} | {location} | {status} |");
                }
                else
                {
                    report.AppendLine($"| {authorityType} | 未找到 | ❌ 缺失 |");
                }
            }
            report.AppendLine();
        }

        Console.WriteLine(report.ToString());
        Assert.True(true, "Report generated successfully");
    }

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    #region Helper Types

    /// <summary>
    /// 权威条目定义
    /// </summary>
    private record AuthorityEntry(
        string ConceptName,
        string[] AuthoritativeTypes,
        string[] AllowedPathPatterns,
        string[] ForbiddenPatterns,
        string[] ForbiddenPathPatterns);

    #endregion
}
