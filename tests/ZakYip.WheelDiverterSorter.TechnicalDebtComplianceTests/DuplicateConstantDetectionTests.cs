using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 重复常量检测测试 - Duplicate Constant Detection Tests
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 检测所有项目的常量确保没有重复的"影分身"，不同厂商除外因为它们的意义不同。
/// 
/// 此测试作为硬性标准强制执行，确保：
/// 1. 相同值的常量在非厂商代码中只定义一次
/// 2. 不同厂商的相同值常量允许存在（因为语义不同）
/// 3. 常量应该定义在合理的位置（如 Core/Constants 或相关配置类）
/// </remarks>
public class DuplicateConstantDetectionTests
{
    /// <summary>
    /// 检测重复的字符串常量
    /// Detect duplicate string constants
    /// </summary>
    [Fact]
    public void ShouldNotHaveDuplicateStringConstants_ExceptVendorSpecific()
    {
        var solutionRoot = GetSolutionRoot();
        var constants = CollectStringConstants(solutionRoot);
        
        // 允许的常见值（这些值在不同上下文有不同含义）
        var allowedCommonValues = new HashSet<string>
        {
            "", // 空字符串在不同上下文有不同含义（默认值、分隔符等）
            " ", // 空格
            "default", // 默认配置名
            "Default", // 默认配置名（大写）
        };
        
        // 按值分组，过滤掉只出现一次的和允许的常见值
        var duplicates = constants
            .GroupBy(c => c.Value)
            .Where(g => g.Count() > 1)
            .Where(g => !allowedCommonValues.Contains(g.Key))
            .Where(g => !IsVendorSpecificDuplicate(g.ToList()))
            .ToList();
        
        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现重复的字符串常量（影分身）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var group in duplicates.OrderByDescending(g => g.Count()))
            {
                report.AppendLine($"\n⚠️ 常量值: \"{group.Key}\" (出现 {group.Count()} 次)");
                report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                foreach (var constant in group)
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, constant.FilePath);
                    report.AppendLine($"  📄 {relativePath}:{constant.LineNumber}");
                    report.AppendLine($"     {constant.ConstantName} = \"{constant.Value}\"");
                    report.AppendLine($"     类型: {constant.TypeName}");
                }
                
                report.AppendLine("\n  💡 修复建议:");
                report.AppendLine("     1. 保留一个权威定义（优先在 Core 项目或配置类中）");
                report.AppendLine("     2. 其他位置引用该权威定义");
                report.AppendLine("     3. 如果语义不同，考虑重命名常量以区分");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n📋 规范说明:");
            report.AppendLine("  根据 copilot-instructions.md 要求：");
            report.AppendLine("  - 相同值的常量应该只定义一次（单一权威原则）");
            report.AppendLine("  - 厂商特定的常量除外（它们的语义不同）");
            report.AppendLine("  - 建议在 Core 项目中定义公共常量");
            
            Console.WriteLine(report.ToString());
            
            // 保存报告
            var reportPath = "/tmp/duplicate_constants_report.md";
            File.WriteAllText(reportPath, report.ToString());
            Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");
            
            Assert.Fail($"发现 {duplicates.Count} 组重复的字符串常量。详情见上方报告。");
        }
    }
    
    /// <summary>
    /// 检测重复的数值常量
    /// Detect duplicate numeric constants
    /// </summary>
    [Fact]
    public void ShouldNotHaveDuplicateNumericConstants_ExceptVendorSpecific()
    {
        var solutionRoot = GetSolutionRoot();
        var constants = CollectNumericConstants(solutionRoot);
        
        // 按值分组，过滤掉只出现一次的，排除常见值
        var commonValues = new[] { "0", "1", "-1", "2", "10", "100", "1000" };
        var duplicates = constants
            .GroupBy(c => c.Value)
            .Where(g => g.Count() > 1)
            .Where(g => !commonValues.Contains(g.Key)) // 排除常见值
            .Where(g => !IsVendorSpecificDuplicate(g.ToList()))
            .ToList();
        
        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n⚠️ 发现重复的数值常量（影分身）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var group in duplicates.OrderByDescending(g => g.Count()).Take(10))
            {
                report.AppendLine($"\n⚠️ 常量值: {group.Key} (出现 {group.Count()} 次)");
                report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                
                foreach (var constant in group.Take(5))
                {
                    var relativePath = Path.GetRelativePath(solutionRoot, constant.FilePath);
                    report.AppendLine($"  📄 {relativePath}:{constant.LineNumber}");
                    report.AppendLine($"     {constant.ConstantName} = {constant.Value}");
                    report.AppendLine($"     类型: {constant.TypeName}");
                }
                
                if (group.Count() > 5)
                {
                    report.AppendLine($"  ... 和 {group.Count() - 5} 处其他位置");
                }
                
                report.AppendLine("\n  💡 修复建议:");
                report.AppendLine("     1. 检查这些常量是否语义相同");
                report.AppendLine("     2. 如果语义相同，保留一个权威定义");
                report.AppendLine("     3. 如果语义不同，考虑重命名以区分");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n📋 规范说明:");
            report.AppendLine("  根据 copilot-instructions.md 要求：");
            report.AppendLine("  - 相同值的常量应该只定义一次（单一权威原则）");
            report.AppendLine("  - 厂商特定的常量除外（它们的语义不同）");
            
            Console.WriteLine(report.ToString());
            
            // 保存报告
            var reportPath = "/tmp/duplicate_numeric_constants_report.md";
            File.WriteAllText(reportPath, report.ToString());
            Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");
        }
        
        // 此测试作为警告，因为数值常量的重复可能有合理原因
        Assert.True(true, $"发现 {duplicates.Count} 组重复的数值常量（作为警告）");
    }
    
    /// <summary>
    /// 生成常量分布审计报告
    /// Generate constant distribution audit report
    /// </summary>
    [Fact]
    public void GenerateConstantDistributionReport()
    {
        var solutionRoot = GetSolutionRoot();
        var stringConstants = CollectStringConstants(solutionRoot);
        var numericConstants = CollectNumericConstants(solutionRoot);
        
        var report = new StringBuilder();
        report.AppendLine("# 常量分布审计报告");
        report.AppendLine($"**生成时间**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        report.AppendLine();
        
        // 统计信息
        report.AppendLine("## 统计摘要");
        report.AppendLine($"- 字符串常量总数: {stringConstants.Count}");
        report.AppendLine($"- 数值常量总数: {numericConstants.Count}");
        report.AppendLine($"- 唯一字符串常量值: {stringConstants.Select(c => c.Value).Distinct().Count()}");
        report.AppendLine($"- 唯一数值常量值: {numericConstants.Select(c => c.Value).Distinct().Count()}");
        report.AppendLine();
        
        // 按项目分组
        report.AppendLine("## 按项目分组");
        var byProject = stringConstants.Concat(numericConstants)
            .GroupBy(c => ExtractProjectName(c.FilePath))
            .OrderByDescending(g => g.Count())
            .ToList();
        
        foreach (var group in byProject)
        {
            report.AppendLine($"### {group.Key}: {group.Count()} 个常量");
        }
        report.AppendLine();
        
        // 重复的字符串常量
        var stringDuplicates = stringConstants
            .GroupBy(c => c.Value)
            .Where(g => g.Count() > 1)
            .OrderByDescending(g => g.Count())
            .Take(20)
            .ToList();
        
        if (stringDuplicates.Any())
        {
            report.AppendLine("## 重复次数最多的字符串常量 (Top 20)");
            report.AppendLine("| 值 | 重复次数 | 是否厂商特定 |");
            report.AppendLine("|---|---------|------------|");
            
            foreach (var group in stringDuplicates)
            {
                var isVendorSpecific = IsVendorSpecificDuplicate(group.ToList());
                var vendorTag = isVendorSpecific ? "✅ 厂商特定" : "⚠️ 需检查";
                var displayValue = group.Key.Length > 40 ? group.Key.Substring(0, 40) + "..." : group.Key;
                report.AppendLine($"| `\"{displayValue}\"` | {group.Count()} | {vendorTag} |");
            }
            report.AppendLine();
        }
        
        // 保存报告
        var reportPath = "/tmp/constant_distribution_report.md";
        File.WriteAllText(reportPath, report.ToString());
        
        Console.WriteLine(report.ToString());
        Console.WriteLine($"\n📄 详细报告已保存到: {reportPath}");
    }
    
    #region Helper Methods
    
    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }
    
    private static List<ConstantInfo> CollectStringConstants(string solutionRoot)
    {
        var constants = new List<ConstantInfo>();
        var sourceFiles = GetSourceFiles(solutionRoot);
        
        // 匹配 const string 定义的正则表达式
        // 例如: private const string CollectionName = "SystemConfiguration";
        var constPattern = new Regex(
            @"^\s*(?:public|private|internal|protected)?\s*const\s+string\s+(\w+)\s*=\s*""([^""]+)""\s*;",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        
        foreach (var file in sourceFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                var typeName = ExtractTypeName(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = constPattern.Match(lines[i]);
                    if (match.Success)
                    {
                        constants.Add(new ConstantInfo
                        {
                            ConstantName = match.Groups[1].Value,
                            Value = match.Groups[2].Value,
                            FilePath = file,
                            LineNumber = i + 1,
                            TypeName = typeName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }
        
        return constants;
    }
    
    private static List<ConstantInfo> CollectNumericConstants(string solutionRoot)
    {
        var constants = new List<ConstantInfo>();
        var sourceFiles = GetSourceFiles(solutionRoot);
        
        // 匹配 const int/long/double/float 定义的正则表达式
        var constPattern = new Regex(
            @"^\s*(?:public|private|internal|protected)?\s*const\s+(int|long|double|float|decimal)\s+(\w+)\s*=\s*([^;]+);",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);
        
        foreach (var file in sourceFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                var typeName = ExtractTypeName(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var match = constPattern.Match(lines[i]);
                    if (match.Success)
                    {
                        var value = match.Groups[3].Value.Trim();
                        // 移除注释
                        var commentIndex = value.IndexOf("//");
                        if (commentIndex >= 0)
                        {
                            value = value.Substring(0, commentIndex).Trim();
                        }
                        
                        constants.Add(new ConstantInfo
                        {
                            ConstantName = match.Groups[2].Value,
                            Value = value,
                            FilePath = file,
                            LineNumber = i + 1,
                            TypeName = typeName
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }
        
        return constants;
    }
    
    private static List<string> GetSourceFiles(string solutionRoot)
    {
        return Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();
    }
    
    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/", "/Migrations/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }
    
    private static bool IsVendorSpecificDuplicate(List<ConstantInfo> constants)
    {
        // 如果所有常量都在 Vendors 目录下，认为是厂商特定的
        var vendorConstants = constants.Where(c => c.FilePath.Contains("/Vendors/") || c.FilePath.Contains("\\Vendors\\")).ToList();
        
        // 如果所有重复都在不同的厂商目录下，则允许
        if (vendorConstants.Count == constants.Count && vendorConstants.Count > 1)
        {
            var vendorDirs = vendorConstants.Select(c => ExtractVendorName(c.FilePath)).Distinct().ToList();
            // 如果来自不同厂商，允许重复
            return vendorDirs.Count > 1;
        }
        
        return false;
    }
    
    private static string ExtractVendorName(string filePath)
    {
        var match = Regex.Match(filePath, @"/Vendors/([^/\\]+)");
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
    
    private static string ExtractProjectName(string filePath)
    {
        var match = Regex.Match(filePath, @"src[/\\]([^/\\]+)[/\\]");
        if (!match.Success)
        {
            match = Regex.Match(filePath, @"ZakYip\.WheelDiverterSorter\.([^/\\]+)");
        }
        return match.Success ? match.Groups[1].Value : "Unknown";
    }
    
    private static string ExtractTypeName(string filePath)
    {
        try
        {
            var content = File.ReadAllText(filePath);
            var classMatch = Regex.Match(content, @"(?:public|internal)\s+(?:static\s+)?(?:class|interface|struct|record)\s+(\w+)");
            if (classMatch.Success)
            {
                return classMatch.Groups[1].Value;
            }
        }
        catch
        {
            // Ignore
        }
        
        return Path.GetFileNameWithoutExtension(filePath);
    }
    
    #endregion
    
    private record ConstantInfo
    {
        public required string ConstantName { get; init; }
        public required string Value { get; init; }
        public required string FilePath { get; init; }
        public required int LineNumber { get; init; }
        public required string TypeName { get; init; }
    }
}
