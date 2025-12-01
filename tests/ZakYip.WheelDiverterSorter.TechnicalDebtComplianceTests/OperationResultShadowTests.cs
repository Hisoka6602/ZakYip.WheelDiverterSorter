using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests.Utilities;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-RS12: OperationResult 和 ErrorCodes 影分身检测测试
/// Tests to detect OperationResult and ErrorCodes shadow types and enforce unique implementation
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 和 RepositoryStructure.md 规范：
/// 1. OperationResult 类型必须唯一存在于 Core/Results/
/// 2. ErrorCodes 类型必须唯一存在于 Core/Results/
/// 3. 其他项目中不能定义任何与 OperationResult 完全同名的类型
/// 4. VendorCapabilities 类型必须唯一存在于 Core/Hardware/
/// 
/// 注意：领域特定的 *OperationResult 类型（如 ConveyorOperationResult, PanelOperationResult）
/// 是允许的，因为它们具有不同的语义和字段。此测试仅检测真正的"影分身"。
/// </remarks>
public class OperationResultShadowTests
{
    /// <summary>
    /// 权威位置：OperationResult 类型
    /// Authoritative location: OperationResult type
    /// </summary>
    private const string AuthoritativeOperationResultNamespace = "ZakYip.WheelDiverterSorter.Core.Results";
    private const string AuthoritativeOperationResultTypeName = "OperationResult";

    /// <summary>
    /// 权威位置：ErrorCodes 类型
    /// Authoritative location: ErrorCodes type
    /// </summary>
    private const string AuthoritativeErrorCodesNamespace = "ZakYip.WheelDiverterSorter.Core.Results";
    private const string AuthoritativeErrorCodesTypeName = "ErrorCodes";

    /// <summary>
    /// 权威位置：VendorCapabilities 类型
    /// Authoritative location: VendorCapabilities type
    /// </summary>
    private const string AuthoritativeVendorCapabilitiesNamespace = "ZakYip.WheelDiverterSorter.Core.Hardware";
    private const string AuthoritativeVendorCapabilitiesTypeName = "VendorCapabilities";

    /// <summary>
    /// 允许的领域特定 *OperationResult 类型白名单
    /// 这些类型具有不同的语义和字段，不是真正的"影分身"
    /// </summary>
    private static readonly HashSet<string> AllowedDomainSpecificResultTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        // 输送线段操作结果 - 包含 SegmentId, Timestamp 等领域特定字段
        "ZakYip.WheelDiverterSorter.Core.LineModel.Segments.ConveyorOperationResult",
        // 面板操作结果 - 包含 CurrentState, PreviousState 等领域特定字段
        "ZakYip.WheelDiverterSorter.Application.Services.Simulation.PanelOperationResult"
    };

    /// <summary>
    /// 预编译的正则表达式用于检测 OperationResult 类定义（完全匹配）
    /// Precompiled regex for detecting OperationResult class definitions (exact match)
    /// </summary>
    private static readonly Regex OperationResultExactClassPattern = new(
        @"\b(?:class|struct|record)\s+OperationResult\b", 
        RegexOptions.Compiled);

    /// <summary>
    /// 预编译的正则表达式用于检测 ErrorCodes 类定义
    /// Precompiled regex for detecting ErrorCodes class definitions
    /// </summary>
    private static readonly Regex ErrorCodesClassPattern = new(
        @"\b(?:class|static\s+class)\s+\w*ErrorCodes\w*\b", 
        RegexOptions.Compiled);

    /// <summary>
    /// 预编译的正则表达式用于检测 VendorCapabilities 类定义
    /// Precompiled regex for detecting VendorCapabilities class definitions
    /// </summary>
    private static readonly Regex VendorCapabilitiesClassPattern = new(
        @"\b(?:class|struct|record)\s+\w*VendorCapabilities\w*\b", 
        RegexOptions.Compiled);

    /// <summary>
    /// 获取 Core 程序集（缓存加载）
    /// </summary>
    private static readonly Lazy<Assembly> CoreAssemblyLazy = new(
        () => Assembly.Load("ZakYip.WheelDiverterSorter.Core"));
    
    private static Assembly CoreAssembly => CoreAssemblyLazy.Value;

    /// <summary>
    /// 判断是否应该跳过某一行（注释或 using 语句）
    /// </summary>
    private static bool ShouldSkipLine(string line)
    {
        var trimmedLine = line.Trim();
        return trimmedLine.StartsWith("//") || 
               trimmedLine.StartsWith("*") || 
               trimmedLine.StartsWith("///") ||
               trimmedLine.StartsWith("using");
    }

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
    public void OperationResult_ShouldOnlyExistInCoreResults()
    {
        // Arrange
        var violations = new List<string>();
        
        // Act: 扫描所有程序集查找 OperationResult 类型定义（完全匹配）
        foreach (var assembly in GetNonTestAssemblies())
        {
            var operationResultTypes = assembly.GetTypes()
                .Where(t => t.Name == "OperationResult" || t.Name.StartsWith("OperationResult`"))
                .Where(t => !t.IsNested)
                .ToList();

            foreach (var type in operationResultTypes)
            {
                var ns = type.Namespace ?? "global";
                if (ns != AuthoritativeOperationResultNamespace)
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
                $"发现 {violations.Count} 个非法的 OperationResult 类型定义：\n{report}\n\n" +
                $"⚠️ OperationResult 类型只能定义在 {AuthoritativeOperationResultNamespace}\n" +
                $"请删除其他位置的 OperationResult 定义，使用 Core.Results.OperationResult。");
        }
    }

    [Fact]
    public void ErrorCodes_ShouldOnlyExistInCoreResults()
    {
        // Arrange
        var violations = new List<string>();
        
        // Act: 扫描所有程序集查找 ErrorCodes 类型定义
        foreach (var assembly in GetNonTestAssemblies())
        {
            var errorCodesTypes = assembly.GetTypes()
                .Where(t => t.Name == "ErrorCodes")
                .Where(t => !t.IsNested)
                .ToList();

            foreach (var type in errorCodesTypes)
            {
                var ns = type.Namespace ?? "global";
                if (ns != AuthoritativeErrorCodesNamespace)
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
                $"发现 {violations.Count} 个非法的 ErrorCodes 类型定义：\n{report}\n\n" +
                $"⚠️ ErrorCodes 类型只能定义在 {AuthoritativeErrorCodesNamespace}\n" +
                $"请删除其他位置的 ErrorCodes 定义，使用 Core.Results.ErrorCodes。");
        }
    }

    [Fact]
    public void VendorCapabilities_ShouldOnlyExistInCoreHardware()
    {
        // Arrange
        var violations = new List<string>();
        
        // Act: 扫描所有程序集查找 VendorCapabilities 类型定义
        foreach (var assembly in GetNonTestAssemblies())
        {
            var vendorCapabilitiesTypes = assembly.GetTypes()
                .Where(t => t.Name == "VendorCapabilities")
                .Where(t => !t.IsNested)
                .ToList();

            foreach (var type in vendorCapabilitiesTypes)
            {
                var ns = type.Namespace ?? "global";
                if (ns != AuthoritativeVendorCapabilitiesNamespace)
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
                $"发现 {violations.Count} 个非法的 VendorCapabilities 类型定义：\n{report}\n\n" +
                $"⚠️ VendorCapabilities 类型只能定义在 {AuthoritativeVendorCapabilitiesNamespace}\n" +
                $"请删除其他位置的 VendorCapabilities 定义，使用 Core.Hardware.VendorCapabilities。");
        }
    }

    [Fact]
    public void ShouldNotDefineAnyOperationResultTypesOutsideCoreResults()
    {
        // Arrange
        var violations = new List<string>();

        // Act: 扫描所有程序集查找任何名称完全是 OperationResult 的类型（排除领域特定类型）
        foreach (var assembly in GetNonTestAssemblies())
        {
            // 匹配 OperationResult 和 OperationResult`N (任意泛型参数数量)
            var operationResultTypes = assembly.GetTypes()
                .Where(t => t.Name == "OperationResult" || t.Name.StartsWith("OperationResult`"))
                .Where(t => !t.IsNested)
                .ToList();

            violations.AddRange(
                operationResultTypes
                    .Where(type => (type.Namespace ?? "global") != AuthoritativeOperationResultNamespace)
                    .Select(type => $"[{assembly.GetName().Name}] {type.Namespace ?? "global"}.{type.Name}")
            );
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 OperationResult 类型定义：\n{report}\n\n" +
                $"⚠️ OperationResult 类型只允许存在于 Core/Results/ 目录中：\n" +
                $"   - {AuthoritativeOperationResultNamespace}.OperationResult (不携带数据)\n" +
                $"   - {AuthoritativeOperationResultNamespace}.OperationResult<T> (携带数据)\n" +
                $"请删除其他位置的 OperationResult 类型定义。\n\n" +
                $"注意：领域特定的 *OperationResult 类型（如 ConveyorOperationResult）是允许的。");
        }
    }

    [Fact]
    public void ShouldNotDefineAnyErrorCodesTypesOutsideCoreResults()
    {
        // Arrange
        var violations = new List<string>();

        // Act: 扫描所有程序集查找任何以 *ErrorCodes* 命名的类型
        foreach (var assembly in GetNonTestAssemblies())
        {
            var errorCodesTypes = assembly.GetTypes()
                .Where(t => t.Name.Contains("ErrorCodes") && !t.IsNested)
                .ToList();

            violations.AddRange(
                errorCodesTypes
                    .Where(type => (type.Namespace ?? "global") != AuthoritativeErrorCodesNamespace)
                    .Select(type => $"[{assembly.GetName().Name}] {type.Namespace ?? "global"}.{type.Name}")
            );
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 *ErrorCodes* 类型定义：\n{report}\n\n" +
                $"⚠️ ErrorCodes 类型只允许存在于 Core/Results/ 目录中：\n" +
                $"   - {AuthoritativeErrorCodesNamespace}.ErrorCodes\n" +
                $"请删除其他位置的 *ErrorCodes* 类型定义。");
        }
    }

    [Fact]
    public void CoreResults_ShouldContainOperationResult()
    {
        // Arrange & Act: 验证权威位置存在 OperationResult 类型
        var operationResult = CoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == AuthoritativeOperationResultTypeName && 
                                 t.Namespace == AuthoritativeOperationResultNamespace);

        // Assert
        Assert.NotNull(operationResult);
        Assert.NotNull(operationResult.GetProperty("IsSuccess"));
        Assert.NotNull(operationResult.GetProperty("ErrorCode"));
        Assert.NotNull(operationResult.GetProperty("ErrorMessage"));
    }

    [Fact]
    public void CoreResults_ShouldContainOperationResultGeneric()
    {
        // Arrange & Act: 验证权威位置存在 OperationResult<T> 类型
        var operationResultGeneric = CoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == "OperationResult`1" && 
                                 t.Namespace == AuthoritativeOperationResultNamespace);

        // Assert
        Assert.NotNull(operationResultGeneric);
        Assert.NotNull(operationResultGeneric.GetProperty("IsSuccess"));
        Assert.NotNull(operationResultGeneric.GetProperty("Data"));
        Assert.NotNull(operationResultGeneric.GetProperty("ErrorCode"));
        Assert.NotNull(operationResultGeneric.GetProperty("ErrorMessage"));
    }

    [Fact]
    public void CoreResults_ShouldContainErrorCodes()
    {
        // Arrange & Act: 验证权威位置存在 ErrorCodes 类型
        var errorCodes = CoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == AuthoritativeErrorCodesTypeName && 
                                 t.Namespace == AuthoritativeErrorCodesNamespace);

        // Assert
        Assert.NotNull(errorCodes);
        Assert.True(errorCodes.IsAbstract && errorCodes.IsSealed, "ErrorCodes 必须是 static class");
        
        // 验证关键的错误码常量
        Assert.NotNull(errorCodes.GetField("Unknown"));
        Assert.NotNull(errorCodes.GetField("Timeout"));
        Assert.NotNull(errorCodes.GetField("NotFound"));
    }

    [Fact]
    public void CoreHardware_ShouldContainVendorCapabilities()
    {
        // Arrange & Act: 验证权威位置存在 VendorCapabilities 类型
        var vendorCapabilities = CoreAssembly.GetTypes()
            .FirstOrDefault(t => t.Name == AuthoritativeVendorCapabilitiesTypeName && 
                                 t.Namespace == AuthoritativeVendorCapabilitiesNamespace);

        // Assert
        Assert.NotNull(vendorCapabilities);
    }

    [Fact]
    public void ShouldNotDefineExactOperationResultInOtherProjects()
    {
        // Arrange
        var violations = new List<string>();
        var files = CodeScanner.GetAllSourceFiles("src")
            .Where(f => !f.Contains("/Core/ZakYip.WheelDiverterSorter.Core/Results/"))
            .Where(f => !f.Contains("\\Core\\ZakYip.WheelDiverterSorter.Core\\Results\\"))
            .Where(f => !f.Contains("/Analyzers/") && !f.Contains("\\Analyzers\\"))
            .ToList();

        // Act: 扫描源代码文件查找 OperationResult 类定义（完全匹配，不是领域特定类型）
        foreach (var file in files)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // 排除注释和 using 语句
                    if (ShouldSkipLine(line))
                    {
                        continue;
                    }
                    
                    // 使用预编译的正则表达式查找类定义（完全匹配 OperationResult）
                    if (OperationResultExactClassPattern.IsMatch(line))
                    {
                        var relativePath = CodeScanner.GetRelativePath(file);
                        violations.Add($"{relativePath}:{i + 1} - {line}");
                    }
                }
            }
            catch (IOException)
            {
                // 文件读取错误，跳过继续处理其他文件
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 OperationResult 类型定义：\n{report}\n\n" +
                $"⚠️ OperationResult 类型只能定义在 Core/Results/ 目录中。\n" +
                $"请删除这些重复的类型定义，改为使用 Core.Results.OperationResult。\n\n" +
                $"注意：领域特定的 *OperationResult 类型（如 ConveyorOperationResult）是允许的。");
        }
    }

    [Fact]
    public void ShouldNotDefineCustomErrorCodesInOtherProjects()
    {
        // Arrange
        var violations = new List<string>();
        var files = CodeScanner.GetAllSourceFiles("src")
            .Where(f => !f.Contains("/Core/") && !f.Contains("\\Core\\"))
            .Where(f => !f.Contains("/Analyzers/") && !f.Contains("\\Analyzers\\"))
            .ToList();

        // Act: 扫描源代码文件查找 ErrorCodes 类定义
        foreach (var file in files)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // 排除注释和 using 语句
                    if (ShouldSkipLine(line))
                    {
                        continue;
                    }
                    
                    // 使用预编译的正则表达式查找类定义
                    if (ErrorCodesClassPattern.IsMatch(line))
                    {
                        var relativePath = CodeScanner.GetRelativePath(file);
                        violations.Add($"{relativePath}:{i + 1} - {line}");
                    }
                }
            }
            catch (IOException)
            {
                // 文件读取错误，跳过继续处理其他文件
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 ErrorCodes 类型定义：\n{report}\n\n" +
                $"⚠️ ErrorCodes 类型只能定义在 Core/Results/ 目录中。\n" +
                $"请删除这些重复的类型定义，改为使用 Core.Results.ErrorCodes。");
        }
    }

    [Fact]
    public void ShouldNotDefineCustomVendorCapabilitiesInOtherProjects()
    {
        // Arrange
        var violations = new List<string>();
        var files = CodeScanner.GetAllSourceFiles("src")
            .Where(f => !f.Contains("/Core/") && !f.Contains("\\Core\\"))
            .Where(f => !f.Contains("/Analyzers/") && !f.Contains("\\Analyzers\\"))
            .ToList();

        // Act: 扫描源代码文件查找 VendorCapabilities 类定义
        foreach (var file in files)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // 排除注释和 using 语句
                    if (ShouldSkipLine(line))
                    {
                        continue;
                    }
                    
                    // 使用预编译的正则表达式查找类定义
                    if (VendorCapabilitiesClassPattern.IsMatch(line))
                    {
                        var relativePath = CodeScanner.GetRelativePath(file);
                        violations.Add($"{relativePath}:{i + 1} - {line}");
                    }
                }
            }
            catch (IOException)
            {
                // 文件读取错误，跳过继续处理其他文件
            }
        }

        // Assert
        if (violations.Any())
        {
            var report = string.Join("\n", violations.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {violations.Count} 个非法的 VendorCapabilities 类型定义：\n{report}\n\n" +
                $"⚠️ VendorCapabilities 类型只能定义在 Core/Hardware/ 目录中。\n" +
                $"请删除这些重复的类型定义，改为使用 Core.Hardware.VendorCapabilities。");
        }
    }

    [Fact]
    public void DomainSpecificResultTypes_ShouldBeDocumented()
    {
        // 这个测试用于记录和验证所有已知的领域特定 *OperationResult 类型
        // 如果发现新的领域特定结果类型，应该将其添加到白名单并更新此测试

        // Arrange
        var foundDomainSpecificTypes = new List<string>();

        // Act: 扫描所有程序集查找任何以 *OperationResult 命名的类型（排除基础 OperationResult）
        foreach (var assembly in GetNonTestAssemblies())
        {
            var resultTypes = assembly.GetTypes()
                .Where(t => t.Name.EndsWith("OperationResult") && 
                           t.Name != "OperationResult" &&
                           !t.IsNested)
                .ToList();

            foundDomainSpecificTypes.AddRange(
                resultTypes.Select(type => $"{type.Namespace ?? "global"}.{type.Name}")
            );
        }

        // Assert: 验证所有发现的领域特定类型都在白名单中
        var undocumentedTypes = foundDomainSpecificTypes
            .Where(t => !AllowedDomainSpecificResultTypes.Contains(t))
            .ToList();

        if (undocumentedTypes.Any())
        {
            var report = string.Join("\n", undocumentedTypes.Select(v => $"  - {v}"));
            Assert.Fail(
                $"发现 {undocumentedTypes.Count} 个未记录的领域特定 *OperationResult 类型：\n{report}\n\n" +
                $"如果这些是有效的领域特定类型，请将它们添加到 AllowedDomainSpecificResultTypes 白名单中。\n" +
                $"如果这些是影分身，请删除它们并使用 Core.Results.OperationResult。");
        }
    }
}
