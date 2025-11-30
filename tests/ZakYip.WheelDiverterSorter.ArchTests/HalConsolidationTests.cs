using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// HAL（硬件抽象层）收敛测试
/// Hardware Abstraction Layer Consolidation Tests
/// </summary>
/// <remarks>
/// PR-SD2: 确保 HAL 接口统一在 Core/Hardware 目录下，
/// 所有厂商摆轮实现统一命名为 WheelDiverterDriver 或 WheelDiverterDevice。
/// 禁止使用 *DiverterController 命名。
/// </remarks>
public class HalConsolidationTests
{
    private static readonly string SolutionRoot = GetSolutionRoot();

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
    /// 检测是否存在 *DiverterController 类型（禁止存在）
    /// Detect *DiverterController types (forbidden)
    /// </summary>
    /// <remarks>
    /// PR-SD2: 所有摆轮实现必须命名为 WheelDiverterDriver 或 WheelDiverterDevice，
    /// 禁止使用 *DiverterController 命名。
    /// 例外：Swagger 文档过滤器等非硬件相关的 Controller 允许存在。
    /// </remarks>
    [Fact]
    public void ShouldNotHaveDiverterControllerTypes()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // 匹配以 DiverterController 结尾的类型定义（包括接口）
        // 但排除 Swagger/文档相关的 Controller（如 WheelDiverterControllerDocumentFilter）
        var diverterControllerPattern = new Regex(
            @"(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface)\s+(\w*DiverterController)(?!\w)(?![A-Z])",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = diverterControllerPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value;
                
                // 排除 Swagger 文档过滤器等非硬件相关类型
                if (typeName.Contains("DocumentFilter") || 
                    typeName.Contains("Swagger") ||
                    typeName.Contains("Api"))
                {
                    continue;
                }
                
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add((typeName, relativePath));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个禁止的 *DiverterController 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD2 修复建议:");
            report.AppendLine("  所有摆轮实现必须统一命名为：");
            report.AppendLine("  - <VendorName>WheelDiverterDriver（实现 IWheelDiverterDriver）");
            report.AppendLine("  - <VendorName>WheelDiverterDevice（实现 IWheelDiverterDevice）");
            report.AppendLine("  禁止使用 *DiverterController 命名。");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测是否存在 IDiverterController 接口（禁止存在）
    /// Detect IDiverterController interface (forbidden)
    /// </summary>
    [Fact]
    public void ShouldNotHaveIDiverterControllerInterface()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        var interfacePattern = new Regex(
            @"(?:public|internal)\s+interface\s+IDiverterController\b",
            RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            if (interfacePattern.IsMatch(content))
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add(relativePath);
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现禁止的 IDiverterController 接口定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var filePath in violations)
            {
                report.AppendLine($"  📄 {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD2 修复建议:");
            report.AppendLine("  HAL 接口已统一到 Core/Hardware/：");
            report.AppendLine("  - IWheelDiverterDevice（Core/Hardware/）- 基于命令的设备接口");
            report.AppendLine("  - IWheelDiverterDriver（Core/Hardware/Devices/）- 基于方向的驱动接口");
            report.AppendLine("  请删除 IDiverterController 接口并使用上述接口。");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证所有公共 *WheelDiverter* 类型实现正确的接口
    /// Verify all public *WheelDiverter* types implement correct interfaces
    /// </summary>
    /// <remarks>
    /// PR-SD2: 所有 Vendors 目录下的公共 *WheelDiverter* 类型必须实现
    /// IWheelDiverterDevice 或 IWheelDiverterDriver 接口。
    /// </remarks>
    [Fact]
    public void WheelDiverterTypes_ShouldImplementHalInterface()
    {
        var vendorsPath = Path.Combine(SolutionRoot, "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors");
        
        if (!Directory.Exists(vendorsPath))
        {
            // Vendors 目录不存在，跳过测试
            return;
        }

        var sourceFiles = Directory.GetFiles(vendorsPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // 匹配包含 WheelDiverter 的公共类定义
        // 排除 Manager 类型（如 ShuDiNiaoWheelDiverterDriverManager），它们实现 IWheelDiverterDriverManager
        var wheelDiverterPattern = new Regex(
            @"public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<typeName>\w*WheelDiverter(?!DriverManager)\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = new List<(string TypeName, string FilePath, string Issue)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = wheelDiverterPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                
                // 跳过空匹配或 Manager 类型
                if (string.IsNullOrEmpty(typeName) || typeName.EndsWith("Manager"))
                {
                    continue;
                }
                
                var relativePath = Path.GetRelativePath(SolutionRoot, file);

                // 检查是否实现了 HAL 接口（使用正则表达式更精确匹配）
                // 匹配类定义中的接口实现部分，排除注释和字符串中的匹配
                var halImplementationPattern = new Regex(
                    @"class\s+\w+\s*:\s*[^{]*\b(?<interface>IWheelDiverterDevice|IWheelDiverterDriver)\b",
                    RegexOptions.Compiled | RegexOptions.ExplicitCapture);
                var implementsHal = halImplementationPattern.IsMatch(content);

                if (!implementsHal)
                {
                    violations.Add((typeName, relativePath, "未实现 IWheelDiverterDevice 或 IWheelDiverterDriver"));
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个未实现 HAL 接口的 WheelDiverter 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath, issue) in violations)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
                report.AppendLine($"     问题: {issue}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD2 修复建议:");
            report.AppendLine("  所有 Vendors 目录下的 WheelDiverter 类型必须实现：");
            report.AppendLine("  - IWheelDiverterDevice（基于命令的高层接口）");
            report.AppendLine("  - IWheelDiverterDriver（基于方向的低层接口）");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证厂商摆轮实现的命名规范
    /// Verify vendor wheel diverter naming conventions
    /// </summary>
    /// <remarks>
    /// PR-SD2: 厂商摆轮实现必须统一命名为：
    /// - <VendorName>WheelDiverterDriver
    /// - <VendorName>WheelDiverterDevice
    /// </remarks>
    [Fact]
    public void VendorWheelDiverterTypes_ShouldFollowNamingConvention()
    {
        var vendorsPath = Path.Combine(SolutionRoot, "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/Vendors");
        
        if (!Directory.Exists(vendorsPath))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(vendorsPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // 使用正向匹配：合法的摆轮类型命名模式
        var validWheelDiverterSuffixes = new[]
        {
            "WheelDiverterDriver",
            "WheelDiverterDevice", 
            "WheelDiverterDeviceAdapter",
            "WheelDiverterDriverManager"
        };
        
        // 允许的辅助类型后缀
        var allowedAuxiliarySuffixes = new[]
        {
            "Config", "Configuration", "Entry", "Protocol", "Options", "Mapping"
        };

        // 匹配任何包含 Diverter 的类定义
        var diverterPattern = new Regex(
            @"public\s+(?:sealed\s+)?(?:partial\s+)?class\s+(?<typeName>\w*Diverter\w*)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var warnings = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = diverterPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                
                // 检查是否符合有效的摆轮类型命名
                var isValidWheelDiverter = validWheelDiverterSuffixes.Any(suffix => 
                    typeName.EndsWith(suffix));
                
                // 检查是否是允许的辅助类型
                var isAuxiliaryType = allowedAuxiliarySuffixes.Any(suffix => 
                    typeName.Contains(suffix));
                
                // 排除仿真类型（使用宽松规则）
                var isSimulated = typeName.StartsWith("Simulated");
                
                if (!isValidWheelDiverter && !isAuxiliaryType && !isSimulated)
                {
                    var relativePath = Path.GetRelativePath(SolutionRoot, file);
                    warnings.Add((typeName, relativePath));
                }
            }
        }

        if (warnings.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ 发现 {warnings.Count} 个可能不符合命名规范的 Diverter 类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath) in warnings)
            {
                report.AppendLine($"  📄 {typeName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD2 命名规范:");
            report.AppendLine("  摆轮实现类型应命名为：");
            report.AppendLine("  - <VendorName>WheelDiverterDriver");
            report.AppendLine("  - <VendorName>WheelDiverterDevice");
            report.AppendLine("  - <VendorName>WheelDiverterDeviceAdapter（适配器）");
            
            // 这是一个警告性测试，不强制失败
            Console.WriteLine(report.ToString());
        }

        // 这个测试总是通过，只是输出警告
        Assert.True(true);
    }

    /// <summary>
    /// 验证 HAL 接口只存在于 Core/Hardware 目录
    /// Verify HAL interfaces only exist in Core/Hardware directory
    /// </summary>
    [Fact]
    public void HalInterfaces_ShouldOnlyExistInCoreHardware()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        var allowedPath = "Core/ZakYip.WheelDiverterSorter.Core/Hardware";
        
        var sourceFiles = Directory.GetFiles(srcPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // HAL 接口定义模式
        var halInterfacePattern = new Regex(
            @"public\s+interface\s+(?<interfaceName>IWheelDiverterDevice|IWheelDiverterDriver|IDiverterController|IWheelDiverterActuator)\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var violations = new List<(string InterfaceName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var normalizedPath = file.Replace("\\", "/");
            
            // 如果文件在允许的路径中，跳过
            if (normalizedPath.Contains(allowedPath))
            {
                continue;
            }
            
            var content = File.ReadAllText(file);
            var matches = halInterfacePattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var interfaceName = match.Groups["interfaceName"].Value;
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add((interfaceName, relativePath));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {violations.Count} 个在 Core/Hardware 目录外定义的 HAL 接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (interfaceName, filePath) in violations)
            {
                report.AppendLine($"  ⚠️ {interfaceName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-SD2 修复建议:");
            report.AppendLine("  HAL 接口必须统一定义在 Core/Hardware/ 目录下：");
            report.AppendLine("  - IWheelDiverterDevice（Core/Hardware/）");
            report.AppendLine("  - IWheelDiverterDriver（Core/Hardware/Devices/）");
            report.AppendLine("  请删除其他位置的 HAL 接口定义。");
            
            Assert.Fail(report.ToString());
        }
    }
}
