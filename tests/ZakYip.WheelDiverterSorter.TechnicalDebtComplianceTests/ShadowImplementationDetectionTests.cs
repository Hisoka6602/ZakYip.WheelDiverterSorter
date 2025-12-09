using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 影子实现检测测试 - 检测新旧两套等价实现并存的情况
/// Shadow Implementation Detection Tests - Detect coexisting old and new equivalent implementations
/// </summary>
/// <remarks>
/// TD-049: 防止出现新旧两套实现共存的影子代码
/// 
/// 检测策略：
/// 1. 检测是否存在 Legacy* / Old* / Deprecated* 命名的类型仍在代码中
/// 2. 检测是否存在功能相同但名称略有不同的重复实现
/// 3. 检测是否存在被标记但未删除的过时代码
/// </remarks>
public class ShadowImplementationDetectionTests
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
    public void ShouldNotHaveLegacyPrefixedTypes()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var legacyTypes = new List<string>();
        
        // 扫描所有 C# 文件查找 Legacy/Old/Deprecated 前缀的类型定义
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            // 检测类/接口/结构体/枚举定义
            var typePattern = @"(class|interface|struct|enum|record)\s+(Legacy|Old|Deprecated)\w+";
            var matches = Regex.Matches(content, typePattern);
            
            foreach (Match match in matches)
            {
                legacyTypes.Add($"{relativePath}: {match.Value}");
            }
        }
        
        Assert.Empty(legacyTypes);
    }

    [Fact]
    public void ShouldNotHaveObsoleteAttributeInSourceCode()
    {
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var obsoleteUsages = new List<string>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            var lines = File.ReadAllLines(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            for (int i = 0; i < lines.Length; i++)
            {
                if (lines[i].Contains("[Obsolete") || lines[i].Contains("[System.Obsolete"))
                {
                    obsoleteUsages.Add($"{relativePath}:{i+1}: {lines[i].Trim()}");
                }
            }
        }
        
        Assert.Empty(obsoleteUsages);
    }

    [Fact]
    public void ShouldNotHaveShadowServiceImplementations()
    {
        // 检测是否存在功能相同但名称略有不同的服务实现
        // 例如：FooService 和 FooServiceImpl 同时存在
        var solutionRoot = GetSolutionRoot();
        var srcDirectory = Path.Combine(solutionRoot, "src");
        
        var serviceTypes = new Dictionary<string, List<string>>();
        
        var csFiles = Directory.GetFiles(srcDirectory, "*.cs", SearchOption.AllDirectories);
        
        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            var relativePath = Path.GetRelativePath(srcDirectory, file);
            
            // 匹配服务类定义：class XXXService / class XXXServiceImpl / class XXXServiceV2
            var servicePattern = @"class\s+(\w+Service(?:Impl|V\d+|Old|New|Legacy)?)\s*[:\{]";
            var matches = Regex.Matches(content, servicePattern);
            
            foreach (Match match in matches)
            {
                var serviceName = match.Groups[1].Value;
                var baseName = Regex.Replace(serviceName, @"(Impl|V\d+|Old|New|Legacy)$", "");
                
                if (!serviceTypes.ContainsKey(baseName))
                {
                    serviceTypes[baseName] = new List<string>();
                }
                serviceTypes[baseName].Add($"{relativePath}: {serviceName}");
            }
        }
        
        // 查找有多个变体的服务
        var duplicateServices = serviceTypes
            .Where(kvp => kvp.Value.Count > 1)
            .Select(kvp => $"{kvp.Key} has {kvp.Value.Count} variants:\n  {string.Join("\n  ", kvp.Value)}")
            .ToList();
        
        Assert.Empty(duplicateServices);
    }
}
