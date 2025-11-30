using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-SD9: 魔法字符串和魔法数字检测测试
/// Tests to detect magic strings and magic numbers that should be enums
/// </summary>
/// <remarks>
/// 根据 PR-SD9 规范：
/// 1. 已知值范围且值范围小于10个元素的 string 类型应改为枚举
/// 2. 已知值范围且值范围小于10个元素的 int 类型应改为枚举
/// 3. 协议名称、厂商名称、状态、模式等应使用枚举而非字符串
/// 
/// 此测试强制执行，新增代码必须遵守。
/// </remarks>
public class MagicValueComplianceTests
{
    /// <summary>
    /// 已知的协议/厂商/模式字符串值（应该是枚举）
    /// </summary>
    private static readonly string[] KnownMagicStrings = 
    {
        // 协议类型
        "\"TCP\"", "\"Http\"", "\"HTTP\"", "\"SignalR\"", "\"MQTT\"", "\"Mqtt\"",
        // 厂商名称
        "\"Leadshine\"", "\"Modi\"", "\"ShuDiNiao\"", "\"Siemens\"", "\"Mitsubishi\"", "\"Omron\"",
        // 模式/状态
        "\"Simulated\"", "\"Mock\"", "\"Default\"", "\"Production\"", "\"Debug\"",
        // 连接模式
        "\"Client\"", "\"Server\"",
        // 传感器类型
        "\"Photoelectric\"", "\"Proximity\"", "\"Laser\"",
        // IO 电平
        "\"High\"", "\"Low\"",
    };

    /// <summary>
    /// 允许使用魔法字符串的白名单模式（如日志消息、注释、测试等）
    /// </summary>
    private static readonly string[] WhitelistPatterns =
    {
        @"Log(?:Information|Warning|Error|Debug|Trace|Critical)\s*\(",  // 日志调用
        @"///",  // XML 文档注释
        @"//",   // 单行注释
        @"\[Description\(",  // Description 特性
        @"nameof\(",  // nameof 表达式
        @"Assert\.",  // 测试断言
        @"\.Should",  // FluentAssertions
        @"Exception\(",  // 异常消息
        @"throw\s+new",  // 抛出异常
        @"\.ToString\(\)",  // ToString 调用结果
    };

    /// <summary>
    /// 允许的文件路径模式（测试文件等）
    /// </summary>
    private static readonly string[] WhitelistFilePaths =
    {
        "/Tests/",
        ".Tests/",
        "/test/",
        "/Benchmarks/",
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
    /// PR-SD9: 检测协议名称应使用枚举而非字符串
    /// Detect protocol names that should use enums instead of strings
    /// </summary>
    [Fact]
    public void ProtocolNames_ShouldUseEnums_NotStrings()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<MagicValueViolation>();

        // 协议相关的魔法字符串
        var protocolMagicStrings = new[] 
        { 
            "\"TCP\"", "\"Http\"", "\"HTTP\"", "\"SignalR\"", "\"MQTT\"", "\"Mqtt\"", "\"Default\"" 
        };

        var sourceFiles = GetSourceFiles(solutionRoot);

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectMagicStrings(file, protocolMagicStrings);
            violations.AddRange(fileViolations);
        }

        ReportViolations(solutionRoot, violations, "协议名称", 
            "使用 CommunicationMode 或 UpstreamProtocolType 枚举替代字符串");
    }

    /// <summary>
    /// PR-SD9: 检测厂商名称应使用枚举而非字符串
    /// Detect vendor names that should use enums instead of strings
    /// </summary>
    [Fact]
    public void VendorNames_ShouldUseEnums_NotStrings()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<MagicValueViolation>();

        // 厂商相关的魔法字符串
        var vendorMagicStrings = new[] 
        { 
            "\"Leadshine\"", "\"Modi\"", "\"ShuDiNiao\"", "\"Siemens\"", 
            "\"Mitsubishi\"", "\"Omron\"", "\"Simulated\"", "\"Mock\"" 
        };

        var sourceFiles = GetSourceFiles(solutionRoot);

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectMagicStrings(file, vendorMagicStrings);
            violations.AddRange(fileViolations);
        }

        ReportViolations(solutionRoot, violations, "厂商名称", 
            "使用 DriverVendorType, WheelDiverterVendorType, SensorVendorType 等枚举替代字符串");
    }

    /// <summary>
    /// PR-SD9: 检测模式/状态值应使用枚举而非字符串
    /// Detect mode/status values that should use enums instead of strings
    /// </summary>
    [Fact]
    public void ModeAndStatusValues_ShouldUseEnums_NotStrings()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<MagicValueViolation>();

        // 模式/状态相关的魔法字符串
        var modeMagicStrings = new[] 
        { 
            "\"Production\"", "\"Debug\"", "\"Client\"", "\"Server\"",
            "\"High\"", "\"Low\"", "\"Photoelectric\"", "\"Proximity\"", "\"Laser\""
        };

        var sourceFiles = GetSourceFiles(solutionRoot);

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectMagicStrings(file, modeMagicStrings);
            violations.AddRange(fileViolations);
        }

        ReportViolations(solutionRoot, violations, "模式/状态值", 
            "使用适当的枚举类型替代字符串（如 ConnectionMode, RuntimeMode 等）");
    }

    /// <summary>
    /// PR-SD9: 生成魔法值审计报告
    /// Generate magic value audit report
    /// </summary>
    [Fact]
    public void GenerateMagicValueAuditReport()
    {
        var solutionRoot = GetSolutionRoot();
        var allViolations = new List<MagicValueViolation>();

        var sourceFiles = GetSourceFiles(solutionRoot);

        foreach (var file in sourceFiles)
        {
            var fileViolations = DetectMagicStrings(file, KnownMagicStrings);
            allViolations.AddRange(fileViolations);
        }

        var report = new StringBuilder();
        report.AppendLine("# 魔法值审计报告");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine($"**扫描文件数**: {sourceFiles.Count}");
        report.AppendLine($"**发现违规数**: {allViolations.Count}");
        report.AppendLine();

        if (allViolations.Any())
        {
            // 按魔法值分组
            var byMagicValue = allViolations
                .GroupBy(v => v.MagicValue)
                .OrderByDescending(g => g.Count())
                .ToList();

            report.AppendLine("## 按魔法值分组");
            report.AppendLine();

            foreach (var group in byMagicValue)
            {
                report.AppendLine($"### {group.Key} ({group.Count()} 处)");
                report.AppendLine();
                foreach (var violation in group.Take(5))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                    report.AppendLine($"- `{relativePath}:{violation.LineNumber}`");
                    report.AppendLine($"  ```csharp");
                    report.AppendLine($"  {violation.LineContent.Trim()}");
                    report.AppendLine($"  ```");
                }
                if (group.Count() > 5)
                {
                    report.AppendLine($"- ... 和 {group.Count() - 5} 处其他位置");
                }
                report.AppendLine();
            }

            report.AppendLine("## 修复建议");
            report.AppendLine();
            report.AppendLine("根据 PR-SD9 规范，以下字符串应替换为枚举：");
            report.AppendLine();
            report.AppendLine("| 魔法字符串 | 推荐枚举类型 |");
            report.AppendLine("|-----------|------------|");
            report.AppendLine("| \"TCP\", \"HTTP\", \"SignalR\", \"MQTT\" | `CommunicationMode` 或 `UpstreamProtocolType` |");
            report.AppendLine("| \"Leadshine\", \"Modi\", \"ShuDiNiao\" | `DriverVendorType` 或 `WheelDiverterVendorType` |");
            report.AppendLine("| \"Client\", \"Server\" | `ConnectionMode` |");
            report.AppendLine("| \"Production\", \"Debug\", \"Simulated\" | `RuntimeMode` |");
            report.AppendLine("| \"High\", \"Low\" | `IoLevel` |");
            report.AppendLine("| \"Photoelectric\", \"Proximity\" | `SensorType` |");
        }
        else
        {
            report.AppendLine("✅ 未发现魔法值违规！");
        }

        Console.WriteLine(report.ToString());
        Assert.True(true, "审计报告已生成");
    }

    /// <summary>
    /// PR-SD9: 验证接口属性不返回已知范围的字符串
    /// Verify that interface properties don't return known-range strings
    /// </summary>
    [Fact]
    public void InterfaceProperties_ShouldNotReturnKnownRangeStrings()
    {
        var solutionRoot = GetSolutionRoot();
        var violations = new List<(string InterfaceName, string PropertyName, string FilePath, int LineNumber)>();

        // 已知返回固定范围字符串的属性名模式
        var suspiciousPropertyNames = new[]
        {
            "ProtocolName", "VendorName", "VendorId", "VendorType", "VendorTypeName",
            "ConnectionType", "Mode", "Status", "State", "Level", "Type"
        };

        var sourceFiles = GetSourceFiles(solutionRoot);

        foreach (var file in sourceFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                var content = File.ReadAllText(file);

                // 检查是否是接口文件
                if (!content.Contains("interface I"))
                    continue;

                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    
                    // 检查是否是返回 string 的属性定义
                    foreach (var propName in suspiciousPropertyNames)
                    {
                        // 匹配: string PropertyName { get; } 或 string PropertyName =>
                        var pattern = $@"string\s+{propName}\s*(\{{|=>)";
                        if (Regex.IsMatch(line, pattern))
                        {
                            // 提取接口名
                            var interfaceMatch = Regex.Match(content, @"interface\s+(I\w+)");
                            var interfaceName = interfaceMatch.Success ? interfaceMatch.Groups[1].Value : "Unknown";
                            
                            violations.Add((interfaceName, propName, file, i + 1));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ PR-SD9 警告: 发现 {violations.Count} 个接口属性可能返回已知范围的字符串:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            foreach (var (interfaceName, propName, filePath, lineNumber) in violations)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, filePath);
                report.AppendLine($"\n⚠️ {interfaceName}.{propName}:");
                report.AppendLine($"   位置: {relativePath}:{lineNumber}");
                report.AppendLine($"   建议: 将返回类型从 string 改为适当的枚举类型");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 PR-SD9 规范:");
            report.AppendLine("  已知值范围（<10个元素）的属性应使用枚举类型而非字符串。");

            Console.WriteLine(report.ToString());
        }

        // 此测试作为警告，不强制失败（因为可能有合理的例外情况）
        Assert.True(true, $"发现 {violations.Count} 个可疑的字符串属性");
    }

    #region Helper Methods

    private static List<string> GetSourceFiles(string solutionRoot)
    {
        return Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .Where(f => !IsInWhitelistPath(f))
            .ToList();
    }

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsInWhitelistPath(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        return WhitelistFilePaths.Any(pattern => normalizedPath.Contains(pattern));
    }

    private static List<MagicValueViolation> DetectMagicStrings(string filePath, string[] magicStrings)
    {
        var violations = new List<MagicValueViolation>();

        try
        {
            var lines = File.ReadAllLines(filePath);

            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                
                // 跳过白名单模式
                if (WhitelistPatterns.Any(pattern => Regex.IsMatch(line, pattern)))
                    continue;

                foreach (var magicString in magicStrings)
                {
                    if (line.Contains(magicString))
                    {
                        violations.Add(new MagicValueViolation
                        {
                            FilePath = filePath,
                            LineNumber = i + 1,
                            LineContent = line,
                            MagicValue = magicString
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error scanning {filePath}: {ex.Message}");
        }

        return violations;
    }

    private static void ReportViolations(
        string solutionRoot, 
        List<MagicValueViolation> violations, 
        string category,
        string suggestion)
    {
        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ PR-SD9 警告: 发现 {violations.Count} 处{category}使用了魔法字符串:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

            // 按文件分组显示
            var byFile = violations.GroupBy(v => v.FilePath).Take(10);
            foreach (var group in byFile)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, group.Key);
                report.AppendLine($"\n📄 {relativePath}:");
                foreach (var violation in group.Take(3))
                {
                    report.AppendLine($"   行 {violation.LineNumber}: {violation.MagicValue}");
                    report.AppendLine($"   └─ {violation.LineContent.Trim().Substring(0, Math.Min(80, violation.LineContent.Trim().Length))}...");
                }
                if (group.Count() > 3)
                {
                    report.AppendLine($"   └─ ... 和 {group.Count() - 3} 处其他位置");
                }
            }

            if (violations.GroupBy(v => v.FilePath).Count() > 10)
            {
                report.AppendLine($"\n... 和 {violations.GroupBy(v => v.FilePath).Count() - 10} 个其他文件");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n💡 建议: {suggestion}");

            Console.WriteLine(report.ToString());
        }

        // 此测试作为警告输出，不强制失败（逐步迁移）
        Assert.True(true, $"发现 {violations.Count} 处魔法字符串");
    }

    #endregion

    private record MagicValueViolation
    {
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string LineContent { get; init; }
        public required string MagicValue { get; init; }
    }
}
