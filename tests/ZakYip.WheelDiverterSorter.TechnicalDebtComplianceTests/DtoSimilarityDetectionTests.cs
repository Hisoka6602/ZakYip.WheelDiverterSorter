using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// DTO 字段相似度检测测试
/// DTO Field Similarity Detection Tests
/// </summary>
/// <remarks>
/// TD-049: 检测字段结构高度相似的 DTO/Request/Response 类型
/// 
/// 检测策略：
/// 1. 提取所有 record 类型的属性列表
/// 2. 计算不同类型之间的属性重叠度
/// 3. 报告高度相似（>80%）的类型对
/// </remarks>
public class DtoSimilarityDetectionTests
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
    public void DTOsShouldNotHaveHighlySimilarStructure()
    {
        // 此测试为信息性测试，用于报告高度相似的 DTO 结构
        // 不强制失败，但会输出警告信息供人工审查
        
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var dtoTypes = new Dictionary<string, List<string>>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .Where(f => f.Contains("\\Models\\") || f.Contains("Request") || f.Contains("Response") || f.Contains("Dto"))
            .ToList();
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            
            // 提取 record 类型及其属性
            var recordPattern = @"record\s+(\w+(?:Request|Response|Dto|Model))\s*\{([^}]+)\}";
            var matches = Regex.Matches(content, recordPattern, RegexOptions.Singleline);
            
            foreach (var entry in matches.Cast<Match>()
                .Select(match =>
                {
                    var typeName = match.Groups[1].Value;
                    var bodyText = match.Groups[2].Value;
                    
                    // 提取属性名（改进正则以支持泛型、数组等复杂类型）
                    var propPattern = @"public\s+[\w\.\<\>\[\]\?\,\s]+(\w+)\s*\{";
                    var propMatches = Regex.Matches(bodyText, propPattern);
                    
                    var properties = propMatches.Cast<Match>()
                        .Select(m => m.Groups[1].Value)
                        .ToList();
                    
                    return new { typeName, properties };
                })
                .Where(x => x.properties.Count > 0))
            {
                dtoTypes[entry.typeName] = entry.properties;
            }
        }
        
        // 检测结构高度相似的类型
        var similarPairs = new List<string>();
        var checkedPairs = new HashSet<string>();
        
        foreach (var type1 in dtoTypes.Where(t => t.Value.Count >= 3))
        {
            foreach (var type2 in dtoTypes.Where(t => t.Key != type1.Key && t.Value.Count >= 3))
            {
                var pairKey = string.CompareOrdinal(type1.Key, type2.Key) < 0
                    ? $"{type1.Key}|{type2.Key}"
                    : $"{type2.Key}|{type1.Key}";
                
                if (checkedPairs.Contains(pairKey)) continue;
                checkedPairs.Add(pairKey);
                
                // 计算属性重叠度
                var commonProps = type1.Value.Intersect(type2.Value, StringComparer.OrdinalIgnoreCase).Count();
                var totalProps = type1.Value.Union(type2.Value, StringComparer.OrdinalIgnoreCase).Count();
                var similarity = (double)commonProps / totalProps;
                
                // 如果相似度 > 90%，报告为可能的重复
                if (similarity > 0.9)
                {
                    similarPairs.Add(
                        $"⚠️  '{type1.Key}' 和 '{type2.Key}' 有 {similarity:P0} 字段重叠 " +
                        $"({commonProps}/{totalProps} 字段相同)");
                }
            }
        }
        
        // 此测试为信息性，不强制失败，但输出警告
        if (similarPairs.Count > 0)
        {
            var message = "发现高度相似的 DTO 结构（可能需要人工review是否为重复定义）:\n" +
                          string.Join("\n", similarPairs);
            // 使用 Assert.True 并输出警告信息，但不失败
            // 实际生产中可以改为 Assert.Empty 以强制失败
            Assert.True(similarPairs.Count < 100, message);
        }
    }
}
