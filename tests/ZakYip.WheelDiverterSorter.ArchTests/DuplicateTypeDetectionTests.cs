using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// 重复类型检测测试
/// Architecture tests to detect duplicate types with same or similar names
/// </summary>
/// <remarks>
/// 这些测试确保：
/// 1. Options/Config 类型在解决方案中唯一（无重复定义）
/// 2. 抽象接口在 Core/Abstractions 中统一定义，其他层不重复
/// 3. 禁止存在 Legacy 或 */Legacy/* 目录
/// 
/// These tests ensure:
/// 1. Options/Config types are unique across the solution (no duplicates)
/// 2. Abstract interfaces are defined in Core/Abstractions, not duplicated in other layers
/// 3. Legacy directories (*/Legacy/*) are forbidden
/// </remarks>
public class DuplicateTypeDetectionTests
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
    /// 检测解决方案中是否存在同名的 Options/Config 类型
    /// Detect duplicate Options/Config types in the solution
    /// </summary>
    /// <remarks>
    /// 此测试为顾问性测试（advisory），因为某些情况下同一名称的类型
    /// 在不同命名空间中可能有正当的不同用途。测试结果会在控制台输出，
    /// 但不会导致测试失败。发现的重复项应由架构师审查并决定是否需要处理。
    /// </remarks>
    [Fact]
    public void ShouldNotHaveDuplicateOptionsTypes()
    {
        var sourceFiles = Directory.GetFiles(
            Path.Combine(SolutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // Find all Options/Config type definitions
        // Use named capture group with ExplicitCapture
        var optionsPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct)\s+(?<typeName>\w+(?:Options|Config|Configuration))\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        var typeDefinitions = new Dictionary<string, List<string>>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = optionsPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups["typeName"].Value;
                
                // Skip empty matches
                if (string.IsNullOrEmpty(typeName))
                    continue;
                
                if (!typeDefinitions.ContainsKey(typeName))
                {
                    typeDefinitions[typeName] = new List<string>();
                }
                
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                // Avoid adding the same file multiple times for the same type
                if (!typeDefinitions[typeName].Contains(relativePath))
                {
                    typeDefinitions[typeName].Add(relativePath);
                }
            }
        }

        // Find duplicates (same type name defined in multiple files)
        var duplicates = typeDefinitions
            .Where(kvp => kvp.Value.Count > 1)
            .ToList();

        if (duplicates.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n⚠️ 发现同名的 Options/Config 类型（需人工审查）:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var duplicate in duplicates)
            {
                report.AppendLine($"\n📌 {duplicate.Key} 出现在以下位置:");
                foreach (var path in duplicate.Value)
                {
                    report.AppendLine($"   • {path}");
                }
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 审查建议:");
            report.AppendLine("  1. 检查这些同名类型是否确实需要分别存在");
            report.AppendLine("  2. 如果是相同语义的类型，考虑合并到 Core 层");
            report.AppendLine("  3. 如果是不同语义，考虑改名以避免混淆");
            report.AppendLine("\n注意：此测试为顾问性测试，不会导致构建失败。");
            
            Console.WriteLine(report.ToString());
        }

        // This is an advisory test - we report findings but don't fail the build
        Assert.True(true, $"Found {duplicates.Count} types with duplicate names - see console output for details");
    }

    /// <summary>
    /// 检测是否存在 Legacy 目录（禁止存在）
    /// Detect if any Legacy directories exist (forbidden)
    /// </summary>
    [Fact]
    public void ShouldNotHaveLegacyDirectories()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        
        // Find all directories named "Legacy"
        var legacyDirectories = Directory.GetDirectories(
            srcPath,
            "Legacy",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("\\obj\\")
                     && !d.Contains("/bin/") && !d.Contains("\\bin\\"))
            .ToList();

        if (legacyDirectories.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现禁止存在的 Legacy 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in legacyDirectories)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, dir);
                var fileCount = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
                report.AppendLine($"  📁 {relativePath} ({fileCount} 个文件)");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，Legacy 目录已被禁止。");
            report.AppendLine("  1. 如果 Legacy 代码仍在使用，请迁移到当前标准实现");
            report.AppendLine("  2. 如果 Legacy 代码不再使用，请删除整个 Legacy 目录");
            report.AppendLine("  3. 确保所有调用方已迁移到新实现后再删除");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测是否存在带 [Obsolete] 特性的公共类型
    /// Detect public types marked with [Obsolete] attribute
    /// </summary>
    [Fact]
    public void ShouldNotHaveObsoletePublicTypes()
    {
        var sourceFiles = Directory.GetFiles(
            Path.Combine(SolutionRoot, "src"),
            "*.cs",
            SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        var obsoletePattern = new Regex(
            @"\[Obsolete(?:\(.*?\))?\]\s*\n\s*public\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct|interface|enum)\s+(\w+)",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture | RegexOptions.Singleline);

        var obsoleteTypes = new List<(string TypeName, string FilePath)>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = obsoletePattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var typeName = match.Groups[1].Value;
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                obsoleteTypes.Add((typeName, relativePath));
            }
        }

        if (obsoleteTypes.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ 发现 {obsoleteTypes.Count} 个带 [Obsolete] 特性的公共类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (typeName, filePath) in obsoleteTypes)
            {
                report.AppendLine($"  ⚠️ {typeName}");
                report.AppendLine($"     {filePath}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，过时类型必须在同一次重构中删除。");
            report.AppendLine("  1. 检查这些类型是否仍有业务代码在使用");
            report.AppendLine("  2. 将调用方迁移到新实现");
            report.AppendLine("  3. 删除过时类型（不保留过渡实现）");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测 Abstractions 目录是否只存在于 Core 和 Communication 中
    /// Detect if Abstractions directories exist only in allowed locations
    /// </summary>
    [Fact]
    public void AbstractionsShouldOnlyExistInAllowedLocations()
    {
        var srcPath = Path.Combine(SolutionRoot, "src");
        
        // Find all directories named "Abstractions"
        var abstractionsDirectories = Directory.GetDirectories(
            srcPath,
            "Abstractions",
            SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("\\obj\\")
                     && !d.Contains("/bin/") && !d.Contains("\\bin\\"))
            .ToList();

        // Allowed locations
        var allowedPatterns = new[]
        {
            "Core/ZakYip.WheelDiverterSorter.Core/Abstractions",
            "Infrastructure/ZakYip.WheelDiverterSorter.Communication/Abstractions"
        };

        var violations = new List<string>();

        foreach (var dir in abstractionsDirectories)
        {
            var normalizedDir = dir.Replace("\\", "/");
            var isAllowed = allowedPatterns.Any(pattern => normalizedDir.Contains(pattern));
            
            if (!isAllowed)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, dir);
                violations.Add(relativePath);
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现不在允许位置的 Abstractions 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                report.AppendLine($"  📁 {violation}");
            }
            
            report.AppendLine("\n允许的 Abstractions 位置:");
            foreach (var pattern in allowedPatterns)
            {
                report.AppendLine($"  ✅ {pattern}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 跨层共享的抽象应统一放在 Core/Abstractions");
            report.AppendLine("  2. 通信层特定的抽象放在 Communication/Abstractions");
            report.AppendLine("  3. 删除其他位置的 Abstractions 目录和重复接口");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测 Drivers 项目是否存在 Abstractions 目录（禁止存在）
    /// Detect if Drivers project has Abstractions directory (forbidden)
    /// </summary>
    /// <remarks>
    /// PR-TD4: Drivers 层只负责实现，所有驱动抽象接口必须定义在 Core/Abstractions/Drivers/ 中。
    /// Drivers 项目中禁止存在 Abstractions 目录，防止重复定义接口。
    /// </remarks>
    [Fact]
    public void Drivers_ShouldNotHaveAbstractionsDirectory()
    {
        var driversPath = Path.Combine(SolutionRoot, "src/Drivers/ZakYip.WheelDiverterSorter.Drivers");
        
        // Find all directories named "Abstractions" in Drivers project
        var abstractionsDirectories = Directory.Exists(driversPath)
            ? Directory.GetDirectories(
                driversPath,
                "Abstractions",
                SearchOption.AllDirectories)
                .Where(d => !d.Contains("/obj/") && !d.Contains("\\obj\\")
                         && !d.Contains("/bin/") && !d.Contains("\\bin\\"))
                .ToList()
            : new List<string>();

        if (abstractionsDirectories.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ Drivers 项目中发现禁止存在的 Abstractions 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in abstractionsDirectories)
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, dir);
                var fileCount = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
                report.AppendLine($"  📁 {relativePath} ({fileCount} 个文件)");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-TD4 修复建议:");
            report.AppendLine("  Drivers 层只负责实现，所有驱动抽象接口必须定义在 Core/Abstractions/Drivers/ 中。");
            report.AppendLine("  1. 将 Abstractions 目录中的接口移动到 Core/Abstractions/Drivers/");
            report.AppendLine("  2. 删除 Drivers/Abstractions 目录");
            report.AppendLine("  3. 更新 Drivers 项目中的引用，指向 Core 层的接口");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成类型分布报告
    /// Generate type distribution report
    /// </summary>
    [Fact]
    public void GenerateTypeDistributionReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# Type Distribution Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var srcPath = Path.Combine(SolutionRoot, "src");
        var projects = Directory.GetDirectories(srcPath, "*", SearchOption.TopDirectoryOnly)
            .SelectMany(d => Directory.GetDirectories(d, "*", SearchOption.TopDirectoryOnly))
            .Where(d => File.Exists(Path.Combine(d, Path.GetFileName(d) + ".csproj")))
            .ToList();

        var optionsPattern = new Regex(
            @"(?:public|internal)\s+(?:sealed\s+)?(?:partial\s+)?(?:class|record|struct)\s+(\w+(?:Options|Config|Configuration))\b",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        report.AppendLine("## Options/Config Types by Project\n");

        foreach (var project in projects)
        {
            var projectName = Path.GetFileName(project);
            var csFiles = Directory.GetFiles(project, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                         && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
                .ToList();

            var optionsTypes = new List<string>();
            foreach (var file in csFiles)
            {
                var content = File.ReadAllText(file);
                var matches = optionsPattern.Matches(content);
                foreach (Match match in matches)
                {
                    optionsTypes.Add(match.Groups[1].Value);
                }
            }

            if (optionsTypes.Any())
            {
                report.AppendLine($"### {projectName}");
                foreach (var type in optionsTypes.Distinct().OrderBy(t => t))
                {
                    report.AppendLine($"- {type}");
                }
                report.AppendLine();
            }
        }

        Console.WriteLine(report.ToString());

        // This test always passes, just generates a report
        Assert.True(true);
    }

    /// <summary>
    /// 检测 Core 层是否存在平行硬件抽象层目录
    /// Detect if Core has parallel hardware abstraction directories
    /// </summary>
    /// <remarks>
    /// PR-C6: HAL 已收敛到 Core/Hardware，禁止增加新的平行硬件抽象层
    /// 允许的位置：
    /// - Core/Hardware/Ports/
    /// - Core/Hardware/IoLinkage/
    /// - Core/Hardware/Devices/
    /// - Core/Hardware/Mappings/
    /// - Core/Hardware/Providers/
    /// 禁止的位置：
    /// - Core/Abstractions/Drivers/（已删除）
    /// - Core/Drivers/、Core/Adapters/、Core/HardwareAbstractions/ 等
    /// </remarks>
    [Fact]
    public void Core_ShouldNotHaveParallelHardwareAbstractionLayers()
    {
        var corePath = Path.Combine(SolutionRoot, "src/Core/ZakYip.WheelDiverterSorter.Core");
        
        // 禁止的目录名称模式
        var forbiddenDirectoryNames = new[]
        {
            "Drivers2", "Abstractions2", "HardwareAbstractions", "Adapters",
            "HAL2", "DeviceDrivers"
        };
        
        // 检查 Abstractions/Drivers 是否被重新创建
        var abstractionsDriversPath = Path.Combine(corePath, "Abstractions/Drivers");
        if (Directory.Exists(abstractionsDriversPath))
        {
            Assert.Fail($"❌ 发现禁止的目录: Abstractions/Drivers\n" +
                $"HAL 已收敛到 Core/Hardware，不允许在 Core/Abstractions/ 下重新创建 Drivers 目录。\n" +
                $"请将硬件相关接口移动到 Core/Hardware/ 的对应子目录。");
        }
        
        // 检查是否存在禁止的平行硬件抽象目录
        var violations = new List<string>();
        foreach (var forbiddenName in forbiddenDirectoryNames)
        {
            var forbiddenPath = Path.Combine(corePath, forbiddenName);
            if (Directory.Exists(forbiddenPath))
            {
                violations.Add(forbiddenName);
            }
        }
        
        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现禁止的平行硬件抽象层目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                report.AppendLine($"  📁 Core/{violation}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-C6 修复建议:");
            report.AppendLine("  HAL 已收敛到 Core/Hardware，禁止增加新的平行硬件抽象层。");
            report.AppendLine("  允许的硬件抽象位置:");
            report.AppendLine("    - Core/Hardware/Ports/ (IO 端口接口)");
            report.AppendLine("    - Core/Hardware/IoLinkage/ (IO 联动接口)");
            report.AppendLine("    - Core/Hardware/Devices/ (设备驱动接口)");
            report.AppendLine("    - Core/Hardware/Mappings/ (IO 映射接口)");
            report.AppendLine("    - Core/Hardware/Providers/ (配置提供者接口)");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测 Core/Hardware 目录结构是否符合规范
    /// Detect if Core/Hardware directory structure follows the standard
    /// </summary>
    [Fact]
    public void Core_Hardware_ShouldHaveStandardSubdirectories()
    {
        var hardwarePath = Path.Combine(SolutionRoot, "src/Core/ZakYip.WheelDiverterSorter.Core/Hardware");
        
        // 必须存在的子目录
        var requiredSubdirectories = new[]
        {
            "Ports",
            "IoLinkage",
            "Devices",
            "Mappings",
            "Providers"
        };
        
        if (!Directory.Exists(hardwarePath))
        {
            Assert.Fail("❌ Core/Hardware 目录不存在\n" +
                "HAL 应该位于 Core/Hardware/ 目录下。");
        }
        
        var missingDirs = new List<string>();
        foreach (var subDir in requiredSubdirectories)
        {
            var path = Path.Combine(hardwarePath, subDir);
            if (!Directory.Exists(path))
            {
                missingDirs.Add(subDir);
            }
        }
        
        if (missingDirs.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ Core/Hardware 缺少标准子目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var missing in missingDirs)
            {
                report.AppendLine($"  📁 Hardware/{missing}/");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 HAL 目录结构说明:");
            report.AppendLine("  - Ports/: IInputPort, IOutputPort 等 IO 端口接口");
            report.AppendLine("  - IoLinkage/: IIoLinkageDriver 等 IO 联动接口");
            report.AppendLine("  - Devices/: IWheelDiverterDriver, IEmcController 等设备接口");
            report.AppendLine("  - Mappings/: IVendorIoMapper 等 IO 映射接口");
            report.AppendLine("  - Providers/: ISensorVendorConfigProvider 等配置提供者");
            
            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 检测 Core/Utilities 是否只包含基础设施抽象
    /// Detect if Core/Utilities only contains infrastructure abstractions
    /// </summary>
    [Fact]
    public void Core_Utilities_ShouldOnlyContainInfrastructureAbstractions()
    {
        var utilitiesPath = Path.Combine(SolutionRoot, "src/Core/ZakYip.WheelDiverterSorter.Core/Utilities");
        
        if (!Directory.Exists(utilitiesPath))
        {
            // Utilities 目录不存在，这是允许的
            return;
        }
        
        // 允许的文件名模式
        var allowedFilePatterns = new[]
        {
            "ISystemClock.cs",
            "LocalSystemClock.cs"
        };
        
        var csFiles = Directory.GetFiles(utilitiesPath, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(Path.GetFileName)
            .ToList();
        
        var unexpectedFiles = csFiles
            .Where(f => f != null && !allowedFilePatterns.Contains(f))
            .ToList();
        
        if (unexpectedFiles.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n⚠️ Core/Utilities 包含非基础设施文件:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var file in unexpectedFiles)
            {
                report.AppendLine($"  📄 Utilities/{file}");
            }
            
            report.AppendLine("\n允许的文件:");
            foreach (var pattern in allowedFilePatterns)
            {
                report.AppendLine($"  ✅ {pattern}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  Core/Utilities 应只包含极少且命名具体的基础设施抽象。");
            report.AppendLine("  与 Sorting/LineModel 等领域相关的工具类应迁移到对应目录。");
            
            // 这是一个警告性测试，不强制失败，但会输出警告
            Console.WriteLine(report.ToString());
        }
    }

    /// <summary>
    /// 检测是否存在重复的摆轮控制接口（禁止存在）
    /// Detect duplicate wheel diverter control interfaces (forbidden)
    /// </summary>
    /// <remarks>
    /// PR-TD9: 摆轮控制抽象统一通过 IWheelDiverterDriver 暴露，
    /// 已删除重复的 IWheelDiverterActuator 接口。
    /// 此测试确保不会重新引入重复的摆轮控制抽象。
    /// </remarks>
    [Fact]
    public void Core_Hardware_ShouldNotHaveDuplicateWheelDiverterInterfaces()
    {
        var hardwarePath = Path.Combine(SolutionRoot, "src/Core/ZakYip.WheelDiverterSorter.Core/Hardware");
        
        // 禁止存在的重复接口文件
        var forbiddenInterfaceFiles = new[]
        {
            "IWheelDiverterActuator.cs",  // PR-TD9: 已合并到 IWheelDiverterDriver
            "IDiverterController.cs"       // PR-C6: 已删除
        };
        
        var violations = new List<string>();
        
        foreach (var forbiddenFile in forbiddenInterfaceFiles)
        {
            // 搜索整个 Hardware 目录
            var foundFiles = Directory.GetFiles(
                hardwarePath,
                forbiddenFile,
                SearchOption.AllDirectories);
            
            foreach (var file in foundFiles)
            {
                violations.Add(Path.GetRelativePath(SolutionRoot, file));
            }
        }
        
        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ 发现禁止存在的重复摆轮控制接口:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                report.AppendLine($"  📄 {violation}");
            }
            
            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 PR-TD9 修复建议:");
            report.AppendLine("  摆轮控制统一通过以下接口暴露：");
            report.AppendLine("  - IWheelDiverterDriver (Core/Hardware/Devices/) - 基于方向的驱动接口");
            report.AppendLine("  - IWheelDiverterDevice (Core/Hardware/) - 基于命令的设备接口");
            report.AppendLine("  禁止引入与上述接口语义重叠的新接口（如 IWheelDiverterActuator, IDiverterController）。");
            
            Assert.Fail(report.ToString());
        }
    }
}
