using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
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

    /// <summary>
    /// 合法的重复方法白名单
    /// 说明：扩展方法在不同上下文中重复定义是合理的
    /// </summary>
    private static readonly HashSet<string> AllowedDuplicateMethods = new()
    {
        // 扩展方法：在不同层可能有相同的扩展方法名
        "ToParcelDescriptor",  // 包裹描述符转换，在多个层都有合理需求
        "AddRange",            // 集合操作扩展
        "Configure",           // 配置扩展
        "GetOrAdd",            // 字典操作扩展
        "SafeInvoke",          // 事件安全调用扩展
        "TryGetValue",         // 安全获取值扩展
    };

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
        
        // 查找重复的方法签名，排除白名单中的方法
        var duplicateMethods = utilityMethods
            .Where(kvp => kvp.Value.Count > 1 && !AllowedDuplicateMethods.Contains(kvp.Key))
            .Select(kvp => $"方法签名 '{kvp.Key}' 在 {kvp.Value.Count} 个文件中定义:\n  {string.Join("\n  ", kvp.Value)}")
            .ToList();
        
        Assert.Empty(duplicateMethods);
    }

    /// <summary>
    /// 合法的工具类命名白名单
    /// 说明：这些类型虽然包含static方法，但有合理的业务原因不遵循*Helper/*Utils命名规范
    /// </summary>
    private static readonly HashSet<string> AllowedNonUtilityClassNames = new()
    {
        // 领域特定的工厂/建造者类
        "ErrorCodes",                       // 错误码定义
        "DefaultConfiguration",             // 默认配置
        "WheelDriverException",             // 异常类型（包含工厂方法）
        
        // 运行时配置档案
        "ProductionRuntimeProfile",         // 生产环境配置档案
        "SimulationRuntimeProfile",         // 仿真环境配置档案
        "PerformanceTestRuntimeProfile",    // 性能测试配置档案
        "S7DefaultConfiguration",           // S7默认配置
        
        // Chaos工程相关
        "ChaosInjectionOptions",            // Chaos注入选项
        "ChaosLayerOptions",                // Chaos层选项
        "ChaosProfiles",                    // Chaos档案
        "ChaosScenarioDefinitions",         // Chaos场景定义
        
        // 场景和脚本定义
        "ScenarioDefinitions",              // 场景定义
        "SimulationScenarioSerializer",     // 场景序列化器
        "SimulationScenarioRunner",         // 场景运行器
        
        // 适配器和健康检查
        "SystemStateManagerAdapter",        // 系统状态管理器适配器
        "PathHealthChecker",                // 路径健康检查器
        "PathHealthResult",                 // 路径健康结果
        "SimpleOptionsMonitor",             // 简单选项监视器
    };

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
                            !className.Contains("Provider") &&
                            // 排除白名单中的类
                            !AllowedNonUtilityClassNames.Contains(className))
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
