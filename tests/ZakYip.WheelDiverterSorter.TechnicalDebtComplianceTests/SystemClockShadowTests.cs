using System.Reflection;
using ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-TEST1: SystemClock 影分身检测测试
/// Tests to detect SystemClock shadow types and enforce unique implementation
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范：
/// 1. ISystemClock 接口必须唯一存在于 Core/Utilities/
/// 2. LocalSystemClock 实现类必须唯一存在于 Core/Utilities/
/// 3. 其他项目中不能定义任何以 *SystemClock 结尾的类型
/// </remarks>
public class SystemClockShadowTests
{
    /// <summary>
    /// 权威位置：ISystemClock 接口
    /// Authoritative location: ISystemClock interface
    /// </summary>
    private const string AuthoritativeISystemClockNamespace = "ZakYip.WheelDiverterSorter.Core.Utilities";
    private const string AuthoritativeISystemClockTypeName = "ISystemClock";

    /// <summary>
    /// 权威位置：LocalSystemClock 实现
    /// Authoritative location: LocalSystemClock implementation
    /// </summary>
    private const string AuthoritativeLocalSystemClockNamespace = "ZakYip.WheelDiverterSorter.Core.Utilities";
    private const string AuthoritativeLocalSystemClockTypeName = "LocalSystemClock";

    /// <summary>
    /// 获取所有非测试项目的程序集
    /// Get all non-test project assemblies
    /// </summary>
    private static IEnumerable<Assembly> GetNonTestAssemblies()
    {
        var assemblies = new[]
        {
            "ZakYip.WheelDiverterSorter.Core",
            "ZakYip.WheelDiverterSorter.Execution",
            "ZakYip.WheelDiverterSorter.Drivers",
            "ZakYip.WheelDiverterSorter.Ingress",
            "ZakYip.WheelDiverterSorter.Observability",
            "ZakYip.WheelDiverterSorter.Communication",
            "ZakYip.WheelDiverterSorter.Simulation",
            "ZakYip.WheelDiverterSorter.Application"
        };

        foreach (var assemblyName in assemblies)
        {
            Assembly? assembly = null;
            try
            {
                assembly = Assembly.Load(assemblyName);
            }
            catch
            {
                // Assembly not found, skip
            }

            if (assembly != null)
            {
                yield return assembly;
            }
        }
    }

    [Fact]
    public void ISystemClock_ShouldOnlyExistInCoreUtilities()
    {
        // Arrange
        var violations = new List<string>();
        
        // Act: 扫描所有程序集查找 ISystemClock 接口定义
        foreach (var assembly in GetNonTestAssemblies())
        {
            var systemClockInterfaces = assembly.GetTypes()
                .Where(t => t.IsInterface && t.Name == "ISystemClock")
                .ToList();

            foreach (var type in systemClockInterfaces)
            {
                var ns = type.Namespace ?? "global";
                if (ns != AuthoritativeISystemClockNamespace || type.Name != AuthoritativeISystemClockTypeName)
                {
                    violations.Add($"[{assembly.GetName().Name}] {ns}.{type.Name}");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 ISystemClock 接口定义：\n{report}\n\n" +
                $"⚠️ ISystemClock 接口只能定义在 {AuthoritativeISystemClockNamespace}.{AuthoritativeISystemClockTypeName}\n" +
                $"请删除其他位置的 ISystemClock 定义。");
        }
    }

    [Fact]
    public void LocalSystemClock_ShouldOnlyExistInCoreUtilities()
    {
        // Arrange
        var violations = new List<string>();
        
        // Act: 扫描所有程序集查找 LocalSystemClock 类定义
        foreach (var assembly in GetNonTestAssemblies())
        {
            var systemClockClasses = assembly.GetTypes()
                .Where(t => t.IsClass && t.Name == "LocalSystemClock")
                .ToList();

            foreach (var type in systemClockClasses)
            {
                var ns = type.Namespace ?? "global";
                if (ns != AuthoritativeLocalSystemClockNamespace || type.Name != AuthoritativeLocalSystemClockTypeName)
                {
                    violations.Add($"[{assembly.GetName().Name}] {ns}.{type.Name}");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 LocalSystemClock 类定义：\n{report}\n\n" +
                $"⚠️ LocalSystemClock 实现类只能定义在 {AuthoritativeLocalSystemClockNamespace}.{AuthoritativeLocalSystemClockTypeName}\n" +
                $"请删除其他位置的 LocalSystemClock 定义。");
        }
    }

    [Fact]
    public void ShouldNotDefineAnySystemClockTypesOutsideCoreUtilities()
    {
        // Arrange
        var violations = new List<string>();
        var allowedTypes = new HashSet<string>
        {
            $"{AuthoritativeISystemClockNamespace}.{AuthoritativeISystemClockTypeName}",
            $"{AuthoritativeLocalSystemClockNamespace}.{AuthoritativeLocalSystemClockTypeName}"
        };

        // Act: 扫描所有程序集查找任何以 *SystemClock 结尾的类型
        foreach (var assembly in GetNonTestAssemblies())
        {
            var systemClockTypes = assembly.GetTypes()
                .Where(t => t.Name.EndsWith("SystemClock") && !t.IsNested)
                .ToList();

            foreach (var type in systemClockTypes)
            {
                var fullName = $"{type.Namespace ?? "global"}.{type.Name}";
                if (!allowedTypes.Contains(fullName))
                {
                    violations.Add($"[{assembly.GetName().Name}] {fullName}");
                }
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 *SystemClock 类型定义：\n{report}\n\n" +
                $"⚠️ 系统时钟类型只允许存在于 Core/Utilities/ 目录中：\n" +
                $"   - {AuthoritativeISystemClockNamespace}.ISystemClock (接口)\n" +
                $"   - {AuthoritativeLocalSystemClockNamespace}.LocalSystemClock (唯一实现)\n" +
                $"请删除其他位置的 *SystemClock 类型定义。");
        }
    }

    [Fact]
    public void CoreUtilities_ShouldContainISystemClock()
    {
        // Arrange & Act: 验证权威位置存在 ISystemClock 接口
        var coreAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Core");
        var iSystemClock = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.IsInterface && 
                                 t.Name == AuthoritativeISystemClockTypeName && 
                                 t.Namespace == AuthoritativeISystemClockNamespace);

        // Assert
        Assert.NotNull(iSystemClock);
        Assert.True(iSystemClock.GetProperty("LocalNow") != null, 
            "ISystemClock 必须包含 LocalNow 属性");
        Assert.True(iSystemClock.GetProperty("LocalNowOffset") != null, 
            "ISystemClock 必须包含 LocalNowOffset 属性");
    }

    [Fact]
    public void CoreUtilities_ShouldContainLocalSystemClock()
    {
        // Arrange & Act: 验证权威位置存在 LocalSystemClock 实现
        var coreAssembly = Assembly.Load("ZakYip.WheelDiverterSorter.Core");
        var localSystemClock = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.IsClass && 
                                 t.Name == AuthoritativeLocalSystemClockTypeName && 
                                 t.Namespace == AuthoritativeLocalSystemClockNamespace);

        // Assert
        Assert.NotNull(localSystemClock);
        
        // 验证实现了 ISystemClock 接口
        var iSystemClock = coreAssembly.GetTypes()
            .FirstOrDefault(t => t.IsInterface && 
                                 t.Name == AuthoritativeISystemClockTypeName && 
                                 t.Namespace == AuthoritativeISystemClockNamespace);
        
        Assert.True(iSystemClock != null && localSystemClock.GetInterfaces().Contains(iSystemClock),
            "LocalSystemClock 必须实现 ISystemClock 接口");
    }

    [Fact]
    public void ShouldNotDefineCustomISystemClockInOtherProjects()
    {
        // Arrange
        var violations = new List<string>();
        var files = CodeScanner.GetAllSourceFiles("src")
            .Where(f => !f.Contains("/Core/"))  // 排除 Core 项目
            .Where(f => !f.Contains("\\Core\\"))
            .Where(f => !f.Contains("/Analyzers/"))  // 排除 Analyzers 项目
            .Where(f => !f.Contains("\\Analyzers\\"))
            .ToList();

        // Act: 扫描源代码文件查找 ISystemClock 接口定义
        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var lines = File.ReadAllLines(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    // 查找接口定义
                    if (line.Contains("interface ISystemClock") || 
                        line.Contains("interface I") && line.Contains("SystemClock"))
                    {
                        // 排除 using 语句和注释
                        if (!line.StartsWith("//") && 
                            !line.StartsWith("*") && 
                            !line.StartsWith("///") &&
                            !line.StartsWith("using"))
                        {
                            var relativePath = GetRelativePath(file);
                            violations.Add($"{relativePath}:{i + 1} - {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 ISystemClock 接口定义：\n{report}\n\n" +
                $"⚠️ ISystemClock 接口只能定义在 Core/Utilities/ 目录中。\n" +
                $"请删除这些重复的接口定义，改为使用 Core.Utilities.ISystemClock。");
        }
    }

    [Fact]
    public void ShouldNotDefineCustomSystemClockClassInOtherProjects()
    {
        // Arrange
        var violations = new List<string>();
        var files = CodeScanner.GetAllSourceFiles("src")
            .Where(f => !f.Contains("/Core/"))  // 排除 Core 项目
            .Where(f => !f.Contains("\\Core\\"))
            .Where(f => !f.Contains("/Analyzers/"))  // 排除 Analyzers 项目
            .Where(f => !f.Contains("\\Analyzers\\"))
            .ToList();

        // Act: 扫描源代码文件查找 SystemClock 类定义
        foreach (var file in files)
        {
            try
            {
                var content = File.ReadAllText(file);
                var lines = File.ReadAllLines(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    // 查找类定义 (class XxxSystemClock)
                    if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\bclass\s+\w*SystemClock\b"))
                    {
                        // 排除注释
                        if (!line.StartsWith("//") && 
                            !line.StartsWith("*") && 
                            !line.StartsWith("///"))
                        {
                            var relativePath = GetRelativePath(file);
                            violations.Add($"{relativePath}:{i + 1} - {line}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scanning {file}: {ex.Message}");
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 *SystemClock 类定义：\n{report}\n\n" +
                $"⚠️ SystemClock 实现类只能定义在 Core/Utilities/ 目录中。\n" +
                $"请删除这些重复的类定义，改为使用 Core.Utilities.LocalSystemClock。");
        }
    }

    private static string GetRelativePath(string fullPath)
    {
        var parts = fullPath.Split(new[] { "/src/", "\\src\\" }, StringSplitOptions.None);
        return parts.Length > 1 ? "src/" + parts[1] : fullPath;
    }
}
