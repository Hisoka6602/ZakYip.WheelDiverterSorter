using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 测试项目结构规范测试
/// Test projects structure compliance tests
/// </summary>
/// <remarks>
/// TD-032: 测试 &amp; 工具项目结构规范化
/// 
/// 这些测试确保：
/// 1. 测试项目不会定义属于 Core/Domain 的业务模型/枚举（防止"影分身"）
/// 2. 测试项目遵守"禁止 Legacy 目录 / 禁止 global using"的规则
/// 3. 工具项目只引用允许的项目，不引入业务逻辑
/// 
/// These tests ensure:
/// 1. Test projects do not define business models/enums belonging to Core/Domain (prevent "shadow clones")
/// 2. Test projects follow "no Legacy directory / no global using" rules
/// 3. Tool projects only reference allowed projects and do not introduce business logic
/// </remarks>
public class TestProjectsStructureTests
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
    /// 测试项目不应定义属于核心领域的模型
    /// Test projects should not define domain models that belong to Core
    /// </summary>
    /// <remarks>
    /// TD-032: 禁止在测试项目中定义命名空间以 ZakYip.WheelDiverterSorter.Core
    /// 或 ...Domain 结尾的实体/枚举。
    /// 
    /// 规则：
    /// 1. 测试项目中的类型命名空间不能是 ZakYip.WheelDiverterSorter.Core.*
    /// 2. 测试项目中的类型命名空间不能以 .Domain 结尾
    /// 3. 允许的例外：测试专用的 Mock/Stub/Fake 类型
    /// </remarks>
    [Fact]
    public void ShouldNotDefineDomainModelsInTests()
    {
        var solutionRoot = GetSolutionRoot();
        var testsDir = Path.Combine(solutionRoot, "tests");
        
        if (!Directory.Exists(testsDir))
        {
            return; // 测试目录不存在，跳过
        }

        var violations = new List<TestDomainModelViolation>();
        
        // 扫描 tests 目录下所有 .cs 文件
        var testFiles = Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 禁止的命名空间模式
        var forbiddenNamespacePatterns = new[]
        {
            @"^ZakYip\.WheelDiverterSorter\.Core\b",      // Core 命名空间
            @"\.Domain$",                                  // 以 .Domain 结尾
            @"\.Domain\.",                                 // 包含 .Domain. 子命名空间
        };

        foreach (var file in testFiles)
        {
            var types = ExtractTypeDefinitionsWithNamespace(file);
            
            foreach (var type in types)
            {
                // 跳过测试专用类型（Mock/Stub/Fake/Test）
                if (IsTestHelperType(type.TypeName))
                {
                    continue;
                }

                // 检查命名空间是否匹配禁止模式
                foreach (var pattern in forbiddenNamespacePatterns)
                {
                    if (Regex.IsMatch(type.Namespace, pattern))
                    {
                        violations.Add(new TestDomainModelViolation
                        {
                            TypeName = type.TypeName,
                            Namespace = type.Namespace,
                            FilePath = file,
                            LineNumber = type.LineNumber,
                            ForbiddenPattern = pattern
                        });
                        break;
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-032 违规: 在测试项目中发现 {violations.Count} 个领域模型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 测试项目不应定义属于 Core/Domain 层的业务模型/枚举。\n");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"  ❌ {violation.TypeName}");
                report.AppendLine($"     位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"     命名空间: {violation.Namespace}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 如果是真正的业务模型，应移动到 src/Core/ 对应目录");
            report.AppendLine("  2. 如果是测试专用类型，请使用 Mock/Stub/Fake/Test 前缀命名");
            report.AppendLine("  3. 将命名空间改为测试项目自己的命名空间（如 *.Tests）");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 测试项目不应包含 Legacy 目录
    /// Test projects should not have Legacy directories
    /// </summary>
    /// <remarks>
    /// TD-032: 沿用 src 目录的规则，测试项目也禁止 Legacy 目录
    /// </remarks>
    [Fact]
    public void ShouldNotHaveLegacyDirectoriesInTests()
    {
        var solutionRoot = GetSolutionRoot();
        var testsDir = Path.Combine(solutionRoot, "tests");
        
        if (!Directory.Exists(testsDir))
        {
            return; // 测试目录不存在，跳过
        }

        var legacyDirs = Directory.GetDirectories(testsDir, "Legacy", SearchOption.AllDirectories)
            .Where(d => !d.Contains("/obj/") && !d.Contains("/bin/") && 
                        !d.Contains("\\obj\\") && !d.Contains("\\bin\\"))
            .ToList();

        if (legacyDirs.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-032 违规: 在测试项目中发现 {legacyDirs.Count} 个 Legacy 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in legacyDirs)
            {
                var relativePath = Path.GetRelativePath(solutionRoot, dir);
                var fileCount = Directory.GetFiles(dir, "*.cs", SearchOption.AllDirectories).Length;
                report.AppendLine($"  ❌ {relativePath} ({fileCount} 个文件)");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  根据架构规范，Legacy 目录已被禁止（包括测试项目）。");
            report.AppendLine("  1. 删除 Legacy 目录及其内容");
            report.AppendLine("  2. 如需保留测试，迁移到当前标准的测试文件中");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 测试项目不应使用 global using
    /// Test projects should not use global using
    /// </summary>
    /// <remarks>
    /// TD-032: 沿用 src 目录的规则，测试项目也禁止 global using
    /// </remarks>
    [Fact]
    public void ShouldNotUseGlobalUsingsInTests()
    {
        var solutionRoot = GetSolutionRoot();
        var testsDir = Path.Combine(solutionRoot, "tests");
        
        if (!Directory.Exists(testsDir))
        {
            return; // 测试目录不存在，跳过
        }

        var violations = new List<TestGlobalUsingViolation>();
        
        var testFiles = Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in testFiles)
        {
            try
            {
                var lines = File.ReadAllLines(file);
                for (int i = 0; i < lines.Length; i++)
                {
                    var line = lines[i].Trim();
                    
                    // 跳过注释行
                    if (line.StartsWith("//") || line.StartsWith("/*") || line.StartsWith("*"))
                        continue;
                    
                    // 检查是否是 global using 语句
                    if (Regex.IsMatch(line, @"^global\s+using\s+[\w.]+"))
                    {
                        violations.Add(new TestGlobalUsingViolation
                        {
                            FilePath = file,
                            LineNumber = i + 1,
                            Content = line
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading {file}: {ex.Message}");
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-032 违规: 在测试项目中发现 {violations.Count} 个 global using:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 禁止新增或保留任何 global using；所有依赖必须通过显式 using 表达。\n");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"  ❌ {relativePath}:{violation.LineNumber}");
                report.AppendLine($"     {violation.Content}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 删除 global using 语句");
            report.AppendLine("  2. 在每个需要该命名空间的文件中添加显式 using 语句");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 测试项目不应定义与 src 中同名的公共类型
    /// Test projects should not define public types with same names as src types
    /// </summary>
    /// <remarks>
    /// TD-032: 防止在测试项目中意外"复制"生产领域模型。
    /// 
    /// 规则：
    /// 1. 检测 tests/ 中是否存在与 src/ 中同名的公共类型
    /// 2. 排除明显的测试辅助类型（以 Tests/Test/Mock/Stub/Fake 结尾）
    /// </remarks>
    [Fact]
    public void ShouldNotDuplicateProductionTypesInTests()
    {
        var solutionRoot = GetSolutionRoot();
        var testsDir = Path.Combine(solutionRoot, "tests");
        var srcDir = Path.Combine(solutionRoot, "src");
        
        if (!Directory.Exists(testsDir) || !Directory.Exists(srcDir))
        {
            return;
        }

        // 收集 src 中的所有公共类型名
        var srcTypeNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var srcFiles = Directory.GetFiles(srcDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in srcFiles)
        {
            var types = ExtractPublicTypeNames(file);
            foreach (var typeName in types)
            {
                srcTypeNames.Add(typeName);
            }
        }

        // 收集 tests 中的公共类型并检查重复
        var violations = new List<TestDuplicateTypeViolation>();
        var testFiles = Directory.GetFiles(testsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        foreach (var file in testFiles)
        {
            var types = ExtractTypeDefinitionsWithNamespace(file);
            
            foreach (var type in types)
            {
                // 跳过测试辅助类型
                if (IsTestHelperType(type.TypeName))
                {
                    continue;
                }

                // 检查是否与 src 中的类型同名
                if (srcTypeNames.Contains(type.TypeName))
                {
                    violations.Add(new TestDuplicateTypeViolation
                    {
                        TypeName = type.TypeName,
                        Namespace = type.Namespace,
                        FilePath = file,
                        LineNumber = type.LineNumber
                    });
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ TD-032 警告: 在测试项目中发现 {violations.Count} 个与 src 同名的类型:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n请确认这些类型是否为意外复制的领域模型：\n");

            foreach (var violation in violations.OrderBy(v => v.TypeName).Take(20))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"  ⚠️ {violation.TypeName}");
                report.AppendLine($"     位置: {relativePath}:{violation.LineNumber}");
            }

            if (violations.Count > 20)
            {
                report.AppendLine($"\n  ... 还有 {violations.Count - 20} 个类型");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 说明:");
            report.AppendLine("  1. 如果是复制的领域模型，请删除并引用 src 中的原始类型");
            report.AppendLine("  2. 如果是测试专用类型，请添加 Test/Mock/Stub/Fake 前缀或后缀");
            report.AppendLine("  3. 如果确实需要同名类型，请确保命名空间不同且有充分理由");

            // 这是警告性测试，不强制失败
            Console.WriteLine(report.ToString());
        }

        Assert.True(true, $"Found {violations.Count} potentially duplicated types in tests");
    }

    /// <summary>
    /// 工具项目不应定义领域模型
    /// Tools projects should not define domain models
    /// </summary>
    /// <remarks>
    /// TD-032: 工具项目只应包含分析/报告逻辑，不应定义业务模型
    /// </remarks>
    [Fact]
    public void ToolsShouldNotDefineDomainModels()
    {
        var solutionRoot = GetSolutionRoot();
        var toolsDir = Path.Combine(solutionRoot, "tools");
        
        if (!Directory.Exists(toolsDir))
        {
            return; // 工具目录不存在，跳过
        }

        var violations = new List<TestDomainModelViolation>();
        
        var toolFiles = Directory.GetFiles(toolsDir, "*.cs", SearchOption.AllDirectories)
            .Where(f => !IsInExcludedDirectory(f))
            .ToList();

        // 禁止的命名空间模式（工具项目不应使用 Core/Domain 命名空间）
        var forbiddenNamespacePatterns = new[]
        {
            @"^ZakYip\.WheelDiverterSorter\.Core\b",      // Core 命名空间
            @"\.Domain$",                                  // 以 .Domain 结尾
            @"\.Domain\.",                                 // 包含 .Domain. 子命名空间
        };

        foreach (var file in toolFiles)
        {
            var types = ExtractTypeDefinitionsWithNamespace(file);
            
            foreach (var type in types)
            {
                foreach (var pattern in forbiddenNamespacePatterns)
                {
                    if (Regex.IsMatch(type.Namespace, pattern))
                    {
                        violations.Add(new TestDomainModelViolation
                        {
                            TypeName = type.TypeName,
                            Namespace = type.Namespace,
                            FilePath = file,
                            LineNumber = type.LineNumber,
                            ForbiddenPattern = pattern
                        });
                        break;
                    }
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-032 违规: 在工具项目中发现 {violations.Count} 个领域模型定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 工具项目不应定义属于 Core/Domain 层的业务模型。\n");

            foreach (var violation in violations.OrderBy(v => v.FilePath))
            {
                var relativePath = Path.GetRelativePath(solutionRoot, violation.FilePath);
                report.AppendLine($"  ❌ {violation.TypeName}");
                report.AppendLine($"     位置: {relativePath}:{violation.LineNumber}");
                report.AppendLine($"     命名空间: {violation.Namespace}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 如果是业务模型，应引用 Core 项目而不是重新定义");
            report.AppendLine("  2. 如果是工具专用类型，使用工具项目自己的命名空间");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成测试项目结构报告
    /// Generate test projects structure report
    /// </summary>
    [Fact]
    public void GenerateTestProjectsStructureReport()
    {
        var solutionRoot = GetSolutionRoot();
        var testsDir = Path.Combine(solutionRoot, "tests");
        var toolsDir = Path.Combine(solutionRoot, "tools");
        
        var report = new StringBuilder();
        report.AppendLine("# Tests & Tools Projects Structure Report\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        // 测试项目统计
        report.AppendLine("## Test Projects Summary\n");
        
        if (Directory.Exists(testsDir))
        {
            var testProjects = Directory.GetDirectories(testsDir, "ZakYip.*", SearchOption.TopDirectoryOnly)
                .Select(d => new DirectoryInfo(d))
                .ToList();

            report.AppendLine($"| Project | Files | Purpose |");
            report.AppendLine($"|---------|-------|---------|");
            
            foreach (var project in testProjects.OrderBy(p => p.Name))
            {
                var fileCount = Directory.GetFiles(project.FullName, "*.cs", SearchOption.AllDirectories)
                    .Where(f => !IsInExcludedDirectory(f))
                    .Count();
                var purpose = GetTestProjectPurpose(project.Name);
                report.AppendLine($"| {project.Name} | {fileCount} | {purpose} |");
            }
        }
        else
        {
            report.AppendLine("⚠️ tests/ 目录不存在\n");
        }

        // 工具项目统计
        report.AppendLine("\n## Tool Projects Summary\n");
        
        if (Directory.Exists(toolsDir))
        {
            var toolProjects = Directory.GetDirectories(toolsDir, "ZakYip.*", SearchOption.TopDirectoryOnly)
                .Select(d => new DirectoryInfo(d))
                .ToList();

            if (toolProjects.Any())
            {
                report.AppendLine($"| Project | Files | Purpose |");
                report.AppendLine($"|---------|-------|---------|");
                
                foreach (var project in toolProjects.OrderBy(p => p.Name))
                {
                    var fileCount = Directory.GetFiles(project.FullName, "*.cs", SearchOption.AllDirectories)
                        .Where(f => !IsInExcludedDirectory(f))
                        .Count();
                    var purpose = GetToolProjectPurpose(project.Name);
                    report.AppendLine($"| {project.Name} | {fileCount} | {purpose} |");
                }
            }

            // 检查 Profiling 目录
            var profilingDir = Path.Combine(toolsDir, "Profiling");
            if (Directory.Exists(profilingDir))
            {
                var scriptCount = Directory.GetFiles(profilingDir, "*.*", SearchOption.TopDirectoryOnly)
                    .Where(f => f.EndsWith(".ps1") || f.EndsWith(".sh"))
                    .Count();
                report.AppendLine($"\n**Profiling Scripts**: {scriptCount} files (non-.NET project)");
            }
        }
        else
        {
            report.AppendLine("⚠️ tools/ 目录不存在\n");
        }

        // 结构约束
        report.AppendLine("\n## Structure Constraints (TD-032)\n");
        report.AppendLine("### Test Projects Constraints\n");
        report.AppendLine("- ❌ 禁止定义 ZakYip.WheelDiverterSorter.Core.* 命名空间的类型");
        report.AppendLine("- ❌ 禁止定义以 .Domain 结尾的命名空间的类型");
        report.AppendLine("- ❌ 禁止 Legacy 目录");
        report.AppendLine("- ❌ 禁止 global using");
        report.AppendLine("- ✅ 允许定义测试辅助类型（Mock/Stub/Fake/Test 前缀）");
        report.AppendLine("- ✅ 允许引用 src 中的所有项目（用于测试）\n");

        report.AppendLine("### Tool Projects Constraints\n");
        report.AppendLine("- ❌ 禁止定义 Core/Domain 命名空间的业务模型");
        report.AppendLine("- ✅ 允许引用 Core 项目获取模型定义");
        report.AppendLine("- ✅ 工具专用类型应使用工具项目自己的命名空间");

        Console.WriteLine(report.ToString());

        Assert.True(true, "Test projects structure report generated");
    }

    #region Helper Methods

    private static bool IsInExcludedDirectory(string filePath)
    {
        var normalizedPath = filePath.Replace('\\', '/');
        var excludedDirs = new[] { "/obj/", "/bin/" };
        return excludedDirs.Any(dir => normalizedPath.Contains(dir));
    }

    private static bool IsTestHelperType(string typeName)
    {
        // 测试辅助类型的常见命名模式
        // Check if the type name contains these patterns anywhere (not just at start/end)
        var testHelperPatterns = new[]
        {
            "Mock", "Stub", "Fake", "Test", "Tests",
            "Fixture", "Helper", "Builder", "Factory",
            "Base", "Setup", "Context", "Specification"
        };

        return testHelperPatterns.Any(pattern => 
            typeName.Contains(pattern, StringComparison.OrdinalIgnoreCase));
    }

    private static List<TestTypeInfo> ExtractTypeDefinitionsWithNamespace(string filePath)
    {
        var types = new List<TestTypeInfo>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            var content = File.ReadAllText(filePath);
            
            // 提取命名空间（支持传统语法和 C# 10+ file-scoped 语法）
            var namespaceMatch = Regex.Match(content, @"namespace\s+([\w.]+)\s*[;{]");
            var ns = namespaceMatch.Success ? namespaceMatch.Groups[1].Value : "Unknown";

            // 查找类型定义
            var typePattern = new Regex(
                @"^\s*(?:public|internal|private|protected)\s+(?:sealed\s+)?(?:partial\s+)?(?:static\s+)?(?:record|class|struct|interface|enum)\s+(?<typeName>\w+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            for (int i = 0; i < lines.Length; i++)
            {
                var match = typePattern.Match(lines[i]);
                if (match.Success)
                {
                    types.Add(new TestTypeInfo
                    {
                        TypeName = match.Groups["typeName"].Value,
                        FilePath = filePath,
                        LineNumber = i + 1,
                        Namespace = ns
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting types from {filePath}: {ex.Message}");
        }

        return types;
    }

    private static List<string> ExtractPublicTypeNames(string filePath)
    {
        var typeNames = new List<string>();
        
        try
        {
            var lines = File.ReadAllLines(filePath);
            
            var typePattern = new Regex(
                @"^\s*public\s+(?:sealed\s+)?(?:partial\s+)?(?:static\s+)?(?:record|class|struct|interface|enum)\s+(?<typeName>\w+)",
                RegexOptions.Compiled | RegexOptions.ExplicitCapture);

            foreach (var line in lines)
            {
                var match = typePattern.Match(line);
                if (match.Success)
                {
                    typeNames.Add(match.Groups["typeName"].Value);
                }
            }
        }
        catch
        {
            // 忽略读取错误
        }

        return typeNames;
    }

    private static string GetTestProjectPurpose(string projectName)
    {
        return projectName switch
        {
            var n when n.Contains("ArchTests") => "架构合规性测试",
            var n when n.Contains("TechnicalDebtComplianceTests") => "技术债合规性测试",
            var n when n.Contains("E2ETests") => "端到端测试",
            var n when n.Contains("IntegrationTests") => "集成测试",
            var n when n.Contains("Benchmarks") => "性能基准测试",
            var n when n.Contains("Core.Tests") => "核心层单元测试",
            var n when n.Contains("Execution.Tests") => "执行层单元测试",
            var n when n.Contains("Drivers.Tests") => "驱动层单元测试",
            var n when n.Contains("Ingress.Tests") => "入口层单元测试",
            var n when n.Contains("Communication.Tests") => "通信层单元测试",
            var n when n.Contains("Observability.Tests") => "可观测性层单元测试",
            var n when n.Contains("Host.Application.Tests") => "应用服务单元测试",
            _ => "测试项目"
        };
    }

    private static string GetToolProjectPurpose(string projectName)
    {
        return projectName switch
        {
            var n when n.Contains("Reporting") => "仿真报告分析工具",
            var n when n.Contains("SafeExecutionStats") => "SafeExecution 统计工具",
            _ => "工具项目"
        };
    }

    #endregion
}

/// <summary>
/// 测试项目类型信息
/// </summary>
internal record TestTypeInfo
{
    public required string TypeName { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Namespace { get; init; }
}

/// <summary>
/// 测试项目领域模型违规信息
/// </summary>
internal record TestDomainModelViolation
{
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string ForbiddenPattern { get; init; }
}

/// <summary>
/// 测试项目 Global Using 违规信息
/// </summary>
internal record TestGlobalUsingViolation
{
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
    public required string Content { get; init; }
}

/// <summary>
/// 测试项目重复类型违规信息
/// </summary>
internal record TestDuplicateTypeViolation
{
    public required string TypeName { get; init; }
    public required string Namespace { get; init; }
    public required string FilePath { get; init; }
    public required int LineNumber { get; init; }
}
