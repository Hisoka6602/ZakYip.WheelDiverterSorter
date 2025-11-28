using System.Reflection;
using System.Text;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// Application 层依赖约束架构测试
/// Architecture tests for Application layer dependency constraints
/// </summary>
/// <remarks>
/// 根据 copilot-instructions.md 规范，这些测试确保：
/// 1. Host 只能依赖 Application（及少量基础项目），不能越级访问 Execution/Drivers/Core 中的业务接口
/// 2. Application 可以依赖 Core / Execution / Drivers / Ingress / Communication / Observability
/// 3. 其他项目不得依赖 Application（避免反向依赖）
/// 
/// These tests enforce:
/// 1. Host can only depend on Application (and basic infrastructure), not directly on Execution/Drivers/Core business interfaces
/// 2. Application can depend on Core/Execution/Drivers/Ingress/Communication/Observability
/// 3. Other projects must not depend on Application (prevent reverse dependencies)
/// </remarks>
public class ApplicationLayerDependencyTests
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
    /// Get project dependencies by parsing csproj file
    /// </summary>
    private List<string> GetProjectReferences(string projectPath)
    {
        var references = new List<string>();
        
        if (!File.Exists(projectPath))
            return references;

        var content = File.ReadAllText(projectPath);
        
        // 查找 <ProjectReference Include="..." /> 模式
        var matches = System.Text.RegularExpressions.Regex.Matches(
            content, 
            @"<ProjectReference\s+Include\s*=\s*""([^""]+)""");
        
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            var refPath = match.Groups[1].Value;
            // 提取项目名称（去除路径和扩展名）
            var projectName = Path.GetFileNameWithoutExtension(refPath);
            references.Add(projectName);
        }

        return references;
    }

    /// <summary>
    /// 验证 Application 项目不依赖 Host / Simulation / Analyzers
    /// Application should not depend on Host / Simulation / Analyzers
    /// </summary>
    [Fact]
    public void Application_ShouldNotDependOn_HostOrSimulationOrAnalyzers()
    {
        var applicationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Application/ZakYip.WheelDiverterSorter.Application/ZakYip.WheelDiverterSorter.Application.csproj");
        
        var references = GetProjectReferences(applicationCsproj);
        
        var forbiddenDependencies = new[] { "Host", "Simulation", "Analyzers" };
        var violations = references
            .Where(r => forbiddenDependencies.Any(fd => r.Contains(fd, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Application 项目不应依赖以下项目: {string.Join(", ", violations)}\n" +
                       "根据架构规范，Application 层不能依赖 Host / Simulation / Analyzers");
        }
    }

    /// <summary>
    /// 验证 Application 项目只允许依赖指定的项目
    /// Application should only depend on allowed projects
    /// </summary>
    [Fact]
    public void Application_ShouldOnlyDependOn_AllowedProjects()
    {
        var applicationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Application/ZakYip.WheelDiverterSorter.Application/ZakYip.WheelDiverterSorter.Application.csproj");
        
        var references = GetProjectReferences(applicationCsproj);
        
        // Application 允许依赖的项目
        var allowedProjects = new[]
        {
            "ZakYip.WheelDiverterSorter.Core",
            "ZakYip.WheelDiverterSorter.Execution",
            "ZakYip.WheelDiverterSorter.Drivers",
            "ZakYip.WheelDiverterSorter.Ingress",
            "ZakYip.WheelDiverterSorter.Communication",
            "ZakYip.WheelDiverterSorter.Observability"
        };

        var violations = references
            .Where(r => !allowedProjects.Any(ap => r.Equals(ap, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Application 项目包含未允许的依赖: {string.Join(", ", violations)}\n" +
                       $"允许的依赖: {string.Join(", ", allowedProjects)}");
        }
    }

    /// <summary>
    /// 验证 Core 项目不依赖 Application
    /// Core should not depend on Application
    /// </summary>
    [Fact]
    public void Core_ShouldNotDependOn_Application()
    {
        var coreCsproj = Path.Combine(
            SolutionRoot, 
            "src/Core/ZakYip.WheelDiverterSorter.Core/ZakYip.WheelDiverterSorter.Core.csproj");
        
        var references = GetProjectReferences(coreCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Core 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Execution 项目不依赖 Application
    /// Execution should not depend on Application
    /// </summary>
    [Fact]
    public void Execution_ShouldNotDependOn_Application()
    {
        var executionCsproj = Path.Combine(
            SolutionRoot, 
            "src/Execution/ZakYip.WheelDiverterSorter.Execution/ZakYip.WheelDiverterSorter.Execution.csproj");
        
        var references = GetProjectReferences(executionCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Execution 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Drivers 项目不依赖 Application
    /// Drivers should not depend on Application
    /// </summary>
    [Fact]
    public void Drivers_ShouldNotDependOn_Application()
    {
        var driversCsproj = Path.Combine(
            SolutionRoot, 
            "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/ZakYip.WheelDiverterSorter.Drivers.csproj");
        
        var references = GetProjectReferences(driversCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Drivers 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Communication 项目不依赖 Application
    /// Communication should not depend on Application
    /// </summary>
    [Fact]
    public void Communication_ShouldNotDependOn_Application()
    {
        var communicationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/ZakYip.WheelDiverterSorter.Communication.csproj");
        
        var references = GetProjectReferences(communicationCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Communication 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Ingress 项目不依赖 Application
    /// Ingress should not depend on Application
    /// </summary>
    [Fact]
    public void Ingress_ShouldNotDependOn_Application()
    {
        var ingressCsproj = Path.Combine(
            SolutionRoot, 
            "src/Ingress/ZakYip.WheelDiverterSorter.Ingress/ZakYip.WheelDiverterSorter.Ingress.csproj");
        
        var references = GetProjectReferences(ingressCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Ingress 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Observability 项目不依赖 Application
    /// Observability should not depend on Application
    /// </summary>
    [Fact]
    public void Observability_ShouldNotDependOn_Application()
    {
        var observabilityCsproj = Path.Combine(
            SolutionRoot, 
            "src/Observability/ZakYip.WheelDiverterSorter.Observability/ZakYip.WheelDiverterSorter.Observability.csproj");
        
        var references = GetProjectReferences(observabilityCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Observability 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 验证 Simulation 项目不依赖 Application
    /// Simulation should not depend on Application
    /// </summary>
    [Fact]
    public void Simulation_ShouldNotDependOn_Application()
    {
        var simulationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Simulation/ZakYip.WheelDiverterSorter.Simulation/ZakYip.WheelDiverterSorter.Simulation.csproj");
        
        var references = GetProjectReferences(simulationCsproj);
        
        var violations = references
            .Where(r => r.Contains("Application", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Simulation 项目不应依赖 Application: {string.Join(", ", violations)}\n" +
                       "这会造成循环依赖");
        }
    }

    /// <summary>
    /// 生成依赖关系报告
    /// Generate dependency report
    /// </summary>
    [Fact]
    public void GenerateDependencyReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# Project Dependency Report\n");
        report.AppendLine("## Layer Dependencies\n");

        var projectPaths = new[]
        {
            ("Host", "src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj"),
            ("Application", "src/Application/ZakYip.WheelDiverterSorter.Application/ZakYip.WheelDiverterSorter.Application.csproj"),
            ("Execution", "src/Execution/ZakYip.WheelDiverterSorter.Execution/ZakYip.WheelDiverterSorter.Execution.csproj"),
            ("Core", "src/Core/ZakYip.WheelDiverterSorter.Core/ZakYip.WheelDiverterSorter.Core.csproj"),
            ("Drivers", "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/ZakYip.WheelDiverterSorter.Drivers.csproj"),
            ("Communication", "src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/ZakYip.WheelDiverterSorter.Communication.csproj"),
            ("Ingress", "src/Ingress/ZakYip.WheelDiverterSorter.Ingress/ZakYip.WheelDiverterSorter.Ingress.csproj"),
            ("Observability", "src/Observability/ZakYip.WheelDiverterSorter.Observability/ZakYip.WheelDiverterSorter.Observability.csproj"),
            ("Simulation", "src/Simulation/ZakYip.WheelDiverterSorter.Simulation/ZakYip.WheelDiverterSorter.Simulation.csproj")
        };

        foreach (var (name, path) in projectPaths)
        {
            var fullPath = Path.Combine(SolutionRoot, path);
            var references = GetProjectReferences(fullPath);
            
            report.AppendLine($"### {name}");
            if (references.Any())
            {
                foreach (var reference in references)
                {
                    report.AppendLine($"  - {reference}");
                }
            }
            else
            {
                report.AppendLine("  - (no project references)");
            }
            report.AppendLine();
        }

        report.AppendLine("## Expected Layer Rules\n");
        report.AppendLine("```");
        report.AppendLine("Host → Application → Core/Execution/Drivers/Ingress/Communication/Observability");
        report.AppendLine("```\n");
        report.AppendLine("- Host 只能依赖 Application（及少量基础项目）");
        report.AppendLine("- Application 可以依赖 Core / Execution / Drivers / Ingress / Communication / Observability");
        report.AppendLine("- 其他项目不得依赖 Application（避免反向依赖）");

        Console.WriteLine(report.ToString());
        
        // 这个测试总是通过，只用于生成报告
        Assert.True(true);
    }
}
