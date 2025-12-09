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
        
        var enumLocations = new Dictionary<string, List<string>>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            // 匹配枚举定义
            var enumPattern = @"enum\s+(\w+)\s*[:\{]";
            var matches = Regex.Matches(content, enumPattern);
            
            foreach (Match match in matches)
            {
                var enumName = match.Groups[1].Value;
                
                if (!enumLocations.ContainsKey(enumName))
                {
                    enumLocations[enumName] = new List<string>();
                }
                enumLocations[enumName].Add(relativePath);
            }
        }
        
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
            
            foreach (Match match in matches)
            {
                var enumName = match.Groups[1].Value;
                var membersText = match.Groups[2].Value;
                
                // 提取枚举成员名称
                var memberPattern = @"(\w+)\s*(?:=|,)";
                var memberMatches = Regex.Matches(membersText, memberPattern);
                
                var members = new HashSet<string>(
                    memberMatches.Cast<Match>()
                        .Select(m => m.Groups[1].Value)
                        .Where(m => !string.IsNullOrWhiteSpace(m))
                );
                
                if (members.Count > 0)
                {
                    enums[enumName] = members;
                }
            }
        }
        
        // 检测成员集合完全相同的不同枚举
        var similarEnums = new List<string>();
        var checkedPairs = new HashSet<string>();
        
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
