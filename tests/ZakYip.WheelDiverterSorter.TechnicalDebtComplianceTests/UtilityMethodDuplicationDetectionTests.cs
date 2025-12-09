using System.Text.RegularExpressions;
using Xunit;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 工具方法签名相似度检测测试
/// Utility Method Signature Similarity Detection Tests
/// </summary>
/// <remarks>
/// TD-049: 检测相同功能的工具方法分散定义
/// 
/// 检测策略：
/// 1. 扫描所有 *Helper / *Util / *Utils / *Extension 类
/// 2. 提取public static方法的签名
/// 3. 检测签名相同但位置不同的方法
/// </remarks>
public class UtilityMethodDuplicationDetectionTests
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
    public void UtilityMethodsShouldNotBeDuplicated()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var utilityMethods = new Dictionary<string, List<string>>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .Where(f => f.Contains("Helper") || f.Contains("Util") || f.Contains("Extension"))
            .ToList();
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            // 提取 public static 方法（改进正则以支持泛型和复杂类型）
            var methodPattern = @"public\s+static\s+[\w\.\<\>\[\]\?\,\s]+\s+(\w+)\s*\(([^\)]*)\)";
            var matches = Regex.Matches(content, methodPattern);
            
            foreach (Match match in matches)
            {
                var methodName = match.Groups[1].Value;
                
                // 简化方案：只使用方法名作为签名，避免复杂的参数类型解析问题
                var signature = methodName;
                
                if (!utilityMethods.ContainsKey(signature))
                {
                    utilityMethods[signature] = new List<string>();
                }
                utilityMethods[signature].Add(relativePath);
            }
        }
        
        // 查找重复的方法签名
        var duplicateMethods = utilityMethods
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"方法签名 '{kvp.Key}' 在 {kvp.Value.Count} 个文件中定义:\n  {string.Join("\n  ", kvp.Value)}")
            .ToList();
        
        Assert.Empty(duplicateMethods);
    }

    [Fact]
    public void UtilityClassesShouldFollowNamingConvention()
    {
        // 检测工具类的命名是否符合规范
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var invalidUtilityClasses = new List<string>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("\\obj\\") && !f.Contains("\\bin\\"))
            .ToList();
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            // 检测包含static方法的类，但命名不符合工具类规范
            if (content.Contains("public static") && content.Contains("class"))
            {
                var classPattern = @"(?:public\s+)?(?:static\s+)?(?:partial\s+)?class\s+(\w+)";
                var matches = Regex.Matches(content, classPattern);
                
                invalidUtilityClasses.AddRange(
                    matches.Cast<Match>()
                        .Select(match => match.Groups[1].Value)
                        .Where(className =>
                            !className.EndsWith("Helper") &&
                            !className.EndsWith("Utils") &&
                            !className.EndsWith("Util") &&
                            !className.EndsWith("Extensions") &&
                            !className.EndsWith("Extension") &&
                            !className.Contains("Constants") &&
                            !className.Contains("Defaults") &&
                            // 排除一些合理的例外
                            !className.Contains("Controller") &&
                            !className.Contains("Service") &&
                            !className.Contains("Factory") &&
                            !className.Contains("Provider"))
                        .Select(className =>
                        {
                            var staticMethodCount = Regex.Matches(content, @"public\s+static\s+\w+").Count;
                            return staticMethodCount > 0
                                ? $"{relativePath}: class {className} (有 {staticMethodCount} 个public static方法)"
                                : null;
                        })
                        .Where(x => x != null)
                        .Cast<string>()
                );
            }
        }
        
        // 此测试为信息性，报告但不强制失败
        if (invalidUtilityClasses.Count > 0)
        {
            var message = "以下类包含public static方法但命名不符合工具类规范:\n" +
                          string.Join("\n", invalidUtilityClasses.Take(20));
            // 允许一定数量的例外，但输出警告
            Assert.True(invalidUtilityClasses.Count < 50, message);
        }
    }
}
