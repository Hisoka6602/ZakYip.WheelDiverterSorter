using System.Reflection;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// 分拣逻辑层数据库访问约束架构测试
/// Architecture tests for sorting logic layer database access constraints
/// </summary>
/// <remarks>
/// 根据 CORE_ROUTING_LOGIC.md 和 copilot-instructions.md 规范，这些测试确保：
/// 1. 分拣逻辑层（Execution、Core/Sorting、Application）不能直接引用 LiteDB
/// 2. 分拣逻辑层不能直接引用 Configuration.Persistence 项目
/// 3. 分拣逻辑层只能通过仓储接口（如 ISystemConfigurationRepository）访问数据
/// 4. 保持分层架构清晰，遵循依赖倒置原则（DIP）
/// 
/// These tests enforce:
/// 1. Sorting logic layers (Execution, Core/Sorting, Application) must not directly reference LiteDB
/// 2. Sorting logic layers must not directly reference Configuration.Persistence project
/// 3. Sorting logic layers can only access data through repository interfaces
/// 4. Maintain clear layered architecture and follow Dependency Inversion Principle (DIP)
/// </remarks>
public class SortingLayerDatabaseAccessTests
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
    /// 获取项目的依赖项目列表（通过解析 csproj 文件）
    /// </summary>
    private List<string> GetProjectReferences(string projectPath)
    {
        var references = new List<string>();
        
        if (!File.Exists(projectPath))
            return references;

        var content = File.ReadAllText(projectPath);
        
        // 查找 <ProjectReference Include="..." /> 模式
        var matches = Regex.Matches(
            content, 
            @"<ProjectReference\s+Include\s*=\s*""([^""]+)""");
        
        foreach (Match match in matches)
        {
            var refPath = match.Groups[1].Value;
            var projectName = Path.GetFileNameWithoutExtension(refPath);
            references.Add(projectName);
        }

        return references;
    }

    /// <summary>
    /// 获取项目中所有 C# 源文件的内容
    /// </summary>
    private IEnumerable<(string FilePath, string Content)> GetSourceFiles(string projectDir)
    {
        if (!Directory.Exists(projectDir))
            yield break;

        foreach (var file in Directory.EnumerateFiles(projectDir, "*.cs", SearchOption.AllDirectories))
        {
            // 跳过 obj 和 bin 目录
            if (file.Contains("/obj/") || file.Contains("\\obj\\") || 
                file.Contains("/bin/") || file.Contains("\\bin\\"))
                continue;

            yield return (file, File.ReadAllText(file));
        }
    }

    /// <summary>
    /// 测试：Execution 层禁止直接引用 LiteDB
    /// </summary>
    /// <remarks>
    /// Execution 层负责分拣编排和路径执行，是核心业务逻辑层。
    /// 必须通过仓储接口访问配置数据，不能直接使用 LiteDB。
    /// </remarks>
    [Fact]
    public void Execution_ShouldNotDirectlyReferenceLiteDB()
    {
        var executionDir = Path.Combine(SolutionRoot, "src/Execution/ZakYip.WheelDiverterSorter.Execution");
        var violations = new List<string>();

        foreach (var (filePath, content) in GetSourceFiles(executionDir))
        {
            // 检测 using LiteDB;
            if (Regex.IsMatch(content, @"^\s*using\s+LiteDB\s*;", RegexOptions.Multiline))
            {
                violations.Add($"{Path.GetFileName(filePath)}: 包含 'using LiteDB;'");
            }

            // 检测直接使用 LiteDB 类型
            var liteDbPatterns = new[]
            {
                @"\bILiteDatabase\b",
                @"\bLiteDatabase\b",
                @"\bILiteCollection\b",
                @"\bLiteCollection\b"
            };

            foreach (var pattern in liteDbPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    violations.Add($"{Path.GetFileName(filePath)}: 使用了 LiteDB 类型 '{pattern}'");
                }
            }
        }

        if (violations.Any())
        {
            var message = "Execution 层禁止直接引用 LiteDB。\n" +
                         "必须通过仓储接口（如 ISystemConfigurationRepository）访问数据。\n\n" +
                         "违规文件:\n" + string.Join("\n", violations);
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// 测试：Core/Sorting 层禁止直接引用 LiteDB
    /// </summary>
    /// <remarks>
    /// Core/Sorting 层包含分拣策略、编排接口和业务模型，是核心领域层。
    /// 必须保持与持久化技术无关，不能直接使用 LiteDB。
    /// </remarks>
    [Fact]
    public void CoreSorting_ShouldNotDirectlyReferenceLiteDB()
    {
        var coreSortingDir = Path.Combine(SolutionRoot, "src/Core/ZakYip.WheelDiverterSorter.Core/Sorting");
        var violations = new List<string>();

        foreach (var (filePath, content) in GetSourceFiles(coreSortingDir))
        {
            // 检测 using LiteDB;
            if (Regex.IsMatch(content, @"^\s*using\s+LiteDB\s*;", RegexOptions.Multiline))
            {
                violations.Add($"{Path.GetFileName(filePath)}: 包含 'using LiteDB;'");
            }

            // 检测直接使用 LiteDB 类型
            var liteDbPatterns = new[]
            {
                @"\bILiteDatabase\b",
                @"\bLiteDatabase\b",
                @"\bILiteCollection\b",
                @"\bLiteCollection\b"
            };

            foreach (var pattern in liteDbPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    violations.Add($"{Path.GetFileName(filePath)}: 使用了 LiteDB 类型 '{pattern}'");
                }
            }
        }

        if (violations.Any())
        {
            var message = "Core/Sorting 层禁止直接引用 LiteDB。\n" +
                         "核心领域层必须保持与持久化技术无关。\n\n" +
                         "违规文件:\n" + string.Join("\n", violations);
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// 测试：Application 层禁止直接引用 LiteDB
    /// </summary>
    /// <remarks>
    /// Application 层提供应用服务和用例编排。
    /// 必须通过仓储接口访问数据，不能直接使用 LiteDB。
    /// </remarks>
    [Fact]
    public void Application_ShouldNotDirectlyReferenceLiteDB()
    {
        var applicationDir = Path.Combine(SolutionRoot, "src/Application/ZakYip.WheelDiverterSorter.Application");
        var violations = new List<string>();

        foreach (var (filePath, content) in GetSourceFiles(applicationDir))
        {
            // 检测 using LiteDB;
            if (Regex.IsMatch(content, @"^\s*using\s+LiteDB\s*;", RegexOptions.Multiline))
            {
                violations.Add($"{Path.GetFileName(filePath)}: 包含 'using LiteDB;'");
            }

            // 检测直接使用 LiteDB 类型
            var liteDbPatterns = new[]
            {
                @"\bILiteDatabase\b",
                @"\bLiteDatabase\b",
                @"\bILiteCollection\b",
                @"\bLiteCollection\b"
            };

            foreach (var pattern in liteDbPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    violations.Add($"{Path.GetFileName(filePath)}: 使用了 LiteDB 类型 '{pattern}'");
                }
            }
        }

        if (violations.Any())
        {
            var message = "Application 层禁止直接引用 LiteDB。\n" +
                         "必须通过仓储接口访问数据。\n\n" +
                         "违规文件:\n" + string.Join("\n", violations);
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// 测试：Ingress 层禁止直接引用 LiteDB
    /// </summary>
    /// <remarks>
    /// Ingress 层负责包裹检测和传感器管理。
    /// 必须通过仓储接口访问配置，不能直接使用 LiteDB。
    /// </remarks>
    [Fact]
    public void Ingress_ShouldNotDirectlyReferenceLiteDB()
    {
        var ingressDir = Path.Combine(SolutionRoot, "src/Ingress/ZakYip.WheelDiverterSorter.Ingress");
        var violations = new List<string>();

        foreach (var (filePath, content) in GetSourceFiles(ingressDir))
        {
            // 检测 using LiteDB;
            if (Regex.IsMatch(content, @"^\s*using\s+LiteDB\s*;", RegexOptions.Multiline))
            {
                violations.Add($"{Path.GetFileName(filePath)}: 包含 'using LiteDB;'");
            }

            // 检测直接使用 LiteDB 类型
            var liteDbPatterns = new[]
            {
                @"\bILiteDatabase\b",
                @"\bLiteDatabase\b",
                @"\bILiteCollection\b",
                @"\bLiteCollection\b"
            };

            foreach (var pattern in liteDbPatterns)
            {
                if (Regex.IsMatch(content, pattern))
                {
                    violations.Add($"{Path.GetFileName(filePath)}: 使用了 LiteDB 类型 '{pattern}'");
                }
            }
        }

        if (violations.Any())
        {
            var message = "Ingress 层禁止直接引用 LiteDB。\n" +
                         "必须通过仓储接口访问配置。\n\n" +
                         "违规文件:\n" + string.Join("\n", violations);
            Assert.Fail(message);
        }
    }

    /// <summary>
    /// 测试：Execution 项目禁止引用 Configuration.Persistence 项目
    /// </summary>
    /// <remarks>
    /// Execution 层不应直接依赖 Configuration.Persistence 项目。
    /// 应该依赖 Core 中定义的仓储接口，通过 DI 注入具体实现。
    /// </remarks>
    [Fact]
    public void Execution_ShouldNotReferenceConfigurationPersistence()
    {
        var executionCsproj = Path.Combine(
            SolutionRoot,
            "src/Execution/ZakYip.WheelDiverterSorter.Execution/ZakYip.WheelDiverterSorter.Execution.csproj");

        var references = GetProjectReferences(executionCsproj);
        var forbiddenDep = references.FirstOrDefault(r => r.Contains("Configuration.Persistence"));

        if (forbiddenDep != null)
        {
            Assert.Fail($"Execution 项目不应直接引用 Configuration.Persistence 项目。\n" +
                       "应该依赖 Core 中的仓储接口（如 ISystemConfigurationRepository），\n" +
                       "由 Application 层通过 DI 注入具体实现。\n\n" +
                       $"发现的引用: {forbiddenDep}");
        }
    }

    /// <summary>
    /// 测试：Core 项目禁止引用 Configuration.Persistence 项目
    /// </summary>
    /// <remarks>
    /// Core 层只定义仓储接口，不应依赖具体的持久化实现。
    /// 遵循依赖倒置原则（DIP）。
    /// </remarks>
    [Fact]
    public void Core_ShouldNotReferenceConfigurationPersistence()
    {
        var coreCsproj = Path.Combine(
            SolutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core/ZakYip.WheelDiverterSorter.Core.csproj");

        var references = GetProjectReferences(coreCsproj);
        var forbiddenDep = references.FirstOrDefault(r => r.Contains("Configuration.Persistence"));

        if (forbiddenDep != null)
        {
            Assert.Fail($"Core 项目不应直接引用 Configuration.Persistence 项目。\n" +
                       "Core 层只定义仓储接口，不应依赖具体实现。\n" +
                       "遵循依赖倒置原则（DIP）。\n\n" +
                       $"发现的引用: {forbiddenDep}");
        }
    }

    /// <summary>
    /// 测试：Ingress 项目禁止引用 Configuration.Persistence 项目
    /// </summary>
    /// <remarks>
    /// Ingress 层不应直接依赖 Configuration.Persistence 项目。
    /// 应该依赖 Core 中定义的仓储接口。
    /// </remarks>
    [Fact]
    public void Ingress_ShouldNotReferenceConfigurationPersistence()
    {
        var ingressCsproj = Path.Combine(
            SolutionRoot,
            "src/Ingress/ZakYip.WheelDiverterSorter.Ingress/ZakYip.WheelDiverterSorter.Ingress.csproj");

        var references = GetProjectReferences(ingressCsproj);
        var forbiddenDep = references.FirstOrDefault(r => r.Contains("Configuration.Persistence"));

        if (forbiddenDep != null)
        {
            Assert.Fail($"Ingress 项目不应直接引用 Configuration.Persistence 项目。\n" +
                       "应该依赖 Core 中的仓储接口。\n\n" +
                       $"发现的引用: {forbiddenDep}");
        }
    }

    /// <summary>
    /// 测试：验证仓储接口只在 Core 中定义
    /// </summary>
    /// <remarks>
    /// 确保所有仓储接口都定义在 Core/LineModel/Configuration/Repositories/Interfaces 目录。
    /// 这是单一真实来源（Single Source of Truth）原则。
    /// </remarks>
    [Fact]
    public void RepositoryInterfaces_ShouldOnlyBeDefinedInCore()
    {
        var repositoryInterfaceDir = Path.Combine(
            SolutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Repositories/Interfaces");

        Assert.True(Directory.Exists(repositoryInterfaceDir), 
            "仓储接口目录应该存在: Core/LineModel/Configuration/Repositories/Interfaces");

        // 检查其他项目是否定义了仓储接口
        var violations = new List<string>();
        var projectsToCheck = new[]
        {
            "src/Execution",
            "src/Application",
            "src/Ingress",
            "src/Drivers",
            "src/Communication",
            "src/Observability"
        };

        foreach (var projectPath in projectsToCheck)
        {
            var fullPath = Path.Combine(SolutionRoot, projectPath);
            if (!Directory.Exists(fullPath))
                continue;

            foreach (var (filePath, content) in GetSourceFiles(fullPath))
            {
                // 检测定义仓储接口的模式
                if (Regex.IsMatch(content, @"public\s+interface\s+I\w*Repository\b"))
                {
                    var relativePath = filePath.Replace(SolutionRoot, "").TrimStart('/', '\\');
                    violations.Add($"{relativePath}: 定义了仓储接口");
                }
            }
        }

        if (violations.Any())
        {
            var message = "仓储接口应该只在 Core/LineModel/Configuration/Repositories/Interfaces 中定义。\n" +
                         "这是单一真实来源（Single Source of Truth）原则。\n\n" +
                         "违规文件:\n" + string.Join("\n", violations);
            Assert.Fail(message);
        }
    }
}
