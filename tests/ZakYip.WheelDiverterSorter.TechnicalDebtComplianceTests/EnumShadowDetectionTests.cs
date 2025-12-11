using System.Text.RegularExpressions;
using Xunit;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 枚举影分身检测测试 - 检测相同语义的枚举重复定义
/// Enum Shadow Detection Tests - Detect duplicate enum definitions with same semantics
/// </summary>
/// <remarks>
/// TD-049: 增强枚举影分身检测，防止相同语义的枚举在多处定义
/// 
/// 检测策略：
/// 1. 检测同名枚举在不同命名空间的定义
/// 2. 检测枚举值相同但枚举名不同的情况
/// 3. 检测枚举成员名称高度相似的不同枚举
/// </remarks>
public class EnumShadowDetectionTests
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

    [Fact]
    public void ShouldNotHaveDuplicateEnumNames()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();
        
        // 使用 LINQ 直接构建 enumLocations 字典
        var enumPattern = @"enum\s+(\w+)\s*[:\{]";
        var enumLocations = csFiles
            .SelectMany(file =>
            {
                var content = File.ReadAllText(file);
                var relativePath = Path.GetRelativePath(srcDirectory, file);
                var matches = Regex.Matches(content, enumPattern);
                return matches.Cast<Match>()
                    .Select(match => new { EnumName = match.Groups[1].Value, RelativePath = relativePath });
            })
            .GroupBy(x => x.EnumName)
            .ToDictionary(
                g => g.Key,
                g => g.Select(x => x.RelativePath).ToList()
            );
        
        // 查找重复的枚举名
        var duplicateEnums = enumLocations
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"枚举 '{kvp.Key}' 在 {kvp.Value.Count} 个文件中定义:\n  {string.Join("\n  ", kvp.Value)}")
            .ToList();
        
        Assert.Empty(duplicateEnums);
    }

    [Fact]
    public void ShouldNotHaveSimilarEnumMemberSets()
    {
        // 检测枚举成员高度相似的不同枚举（可能是重复定义）
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var enums = new Dictionary<string, HashSet<string>>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            
            // 提取枚举定义及其成员
            var enumPattern = @"enum\s+(\w+)\s*\{([^}]+)\}";
            var matches = Regex.Matches(content, enumPattern, RegexOptions.Singleline);
            
            foreach (var entry in matches.Cast<Match>()
                .Select(match =>
                {
                    var enumName = match.Groups[1].Value;
                    var membersText = match.Groups[2].Value;
                    
                    // 提取枚举成员名称（改进以正确匹配最后一个成员）
                    var memberPattern = @"(\w+)\s*(?:=\s*\d+\s*)?(?:,|(?=\}))";
                    var memberMatches = Regex.Matches(membersText, memberPattern);
                    
                    var members = new HashSet<string>(
                        memberMatches.Cast<Match>()
                            .Select(m => m.Groups[1].Value)
                            .Where(m => !string.IsNullOrWhiteSpace(m))
                    );
                    
                    return new { enumName, members };
                })
                .Where(x => x.members.Count > 0))
            {
                enums[entry.enumName] = entry.members;
            }
        }
        
        // 检测成员集合完全相同的不同枚举
        var similarEnums = new List<string>();
        var checkedPairs = new HashSet<string>();
        
        // 白名单：已确认为合理的相似枚举对（有不同的语义和用途）
        var whitelist = new HashSet<string>
        {
            "ConnectionMode|ShuDiNiaoMode",  // 通用通信模式 vs 数递鸟厂商专用模式
            "DriverVendorType|SensorVendorType",  // 驱动器厂商 vs 传感器厂商
            "DiverterDirection|DiverterSide"  // 硬件驱动层方向 vs 拓扑模型层方向
        };
        
        foreach (var enum1 in enums)
        {
            foreach (var enum2 in enums)
            {
                if (enum1.Key == enum2.Key) continue;
                
                var pairKey = string.CompareOrdinal(enum1.Key, enum2.Key) < 0
                    ? $"{enum1.Key}|{enum2.Key}"
                    : $"{enum2.Key}|{enum1.Key}";
                
                if (checkedPairs.Contains(pairKey)) continue;
                checkedPairs.Add(pairKey);
                
                // 跳过白名单中的枚举对
                if (whitelist.Contains(pairKey)) continue;
                
                // 计算相似度：相同成员数 / 总成员数
                var commonMembers = enum1.Value.Intersect(enum2.Value).Count();
                var totalMembers = enum1.Value.Union(enum2.Value).Count();
                var similarity = (double)commonMembers / totalMembers;
                
                // 如果相似度 > 80%，认为可能是重复定义
                if (similarity > 0.8)
                {
                    similarEnums.Add(
                        $"枚举 '{enum1.Key}' 和 '{enum2.Key}' 有 {similarity:P0} 相似度 " +
                        $"(共同成员: {commonMembers}/{totalMembers})");
                }
            }
        }
        
        Assert.Empty(similarEnums);
    }
}
