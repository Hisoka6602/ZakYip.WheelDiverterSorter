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
    /// 验证 Application 项目不依赖 Host / Analyzers
    /// Application should not depend on Host / Analyzers
    /// </summary>
    /// <remarks>
    /// PR-H1: Application 层现在是 DI 聚合层，可以依赖 Simulation 项目以统一注册所有服务。
    /// Application is now the DI aggregation layer and can depend on Simulation to register all services.
    /// Host 层通过 Application 层间接访问 Simulation，不再直接依赖 Simulation。
    /// </remarks>
    [Fact]
    public void Application_ShouldNotDependOn_HostOrAnalyzers()
    {
        var applicationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Application/ZakYip.WheelDiverterSorter.Application/ZakYip.WheelDiverterSorter.Application.csproj");
        
        var references = GetProjectReferences(applicationCsproj);
        
        // PR-H1: Simulation 现在是允许的依赖，因为 Application 层负责统一 DI 注册
        var forbiddenDependencies = new[] { "Host", "Analyzers" };
        var violations = references
            .Where(r => forbiddenDependencies.Any(fd => r.Contains(fd, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Application 项目不应依赖以下项目: {string.Join(", ", violations)}\n" +
                       "根据架构规范，Application 层不能依赖 Host / Analyzers");
        }
    }

    /// <summary>
    /// 验证 Application 项目只允许依赖指定的项目
    /// Application should only depend on allowed projects
    /// </summary>
    /// <remarks>
    /// PR-H1: Application 层现在是 DI 聚合层，允许依赖 Simulation 项目以统一注册所有服务。
    /// Application is now the DI aggregation layer and can depend on Simulation to register all services.
    /// </remarks>
    [Fact]
    public void Application_ShouldOnlyDependOn_AllowedProjects()
    {
        var applicationCsproj = Path.Combine(
            SolutionRoot, 
            "src/Application/ZakYip.WheelDiverterSorter.Application/ZakYip.WheelDiverterSorter.Application.csproj");
        
        var references = GetProjectReferences(applicationCsproj);
        
        // PR-H1: Application 允许依赖的项目（新增 Simulation，因为 Application 是 DI 聚合层）
        var allowedProjects = new[]
        {
            "ZakYip.WheelDiverterSorter.Core",
            "ZakYip.WheelDiverterSorter.Execution",
            "ZakYip.WheelDiverterSorter.Drivers",
            "ZakYip.WheelDiverterSorter.Ingress",
            "ZakYip.WheelDiverterSorter.Communication",
            "ZakYip.WheelDiverterSorter.Observability",
            "ZakYip.WheelDiverterSorter.Simulation"  // PR-H1: 允许 Application 依赖 Simulation
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
    /// 验证 Host 项目只允许依赖 Application/Core/Observability
    /// Host should only depend on Application/Core/Observability
    /// </summary>
    /// <remarks>
    /// PR-H1: Host 层依赖收缩 - Host 只能依赖 Application/Core/Observability。
    /// Host 不能直接依赖 Execution/Drivers/Ingress/Communication/Simulation。
    /// 这些依赖现在通过 Application 层传递。
    /// </remarks>
    [Fact]
    public void Host_ShouldOnlyDependOn_ApplicationCoreObservability()
    {
        var hostCsproj = Path.Combine(
            SolutionRoot, 
            "src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj");
        
        var references = GetProjectReferences(hostCsproj);
        
        // PR-H1: Host 只允许依赖的项目
        var allowedProjects = new[]
        {
            "ZakYip.WheelDiverterSorter.Application",
            "ZakYip.WheelDiverterSorter.Core",
            "ZakYip.WheelDiverterSorter.Observability"
        };

        var violations = references
            .Where(r => !allowedProjects.Any(ap => r.Equals(ap, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Host 项目包含未允许的直接依赖: {string.Join(", ", violations)}\n" +
                       $"允许的依赖: {string.Join(", ", allowedProjects)}\n" +
                       "PR-H1: Host 层应只依赖 Application/Core/Observability，其他依赖应通过 Application 层传递");
        }
    }

    /// <summary>
    /// 验证 Host 项目不直接依赖 Execution/Drivers/Ingress/Communication/Simulation
    /// Host should not directly depend on Execution/Drivers/Ingress/Communication/Simulation
    /// </summary>
    /// <remarks>
    /// PR-H1: Host 层依赖收缩 - 这些项目的依赖现在通过 Application 层传递。
    /// </remarks>
    [Fact]
    public void Host_ShouldNotDirectlyDependOn_ExecutionDriversIngressCommunicationSimulation()
    {
        var hostCsproj = Path.Combine(
            SolutionRoot, 
            "src/Host/ZakYip.WheelDiverterSorter.Host/ZakYip.WheelDiverterSorter.Host.csproj");
        
        var references = GetProjectReferences(hostCsproj);
        
        // PR-H1: Host 禁止直接依赖的项目
        var forbiddenDependencies = new[]
        {
            "Execution",
            "Drivers",
            "Ingress",
            "Communication",
            "Simulation"
        };

        var violations = references
            .Where(r => forbiddenDependencies.Any(fd => r.Contains(fd, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Host 项目不应直接依赖以下项目: {string.Join(", ", violations)}\n" +
                       "PR-H1: Host 层应通过 Application 层间接访问这些项目的服务");
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
    /// 验证 Drivers 项目不依赖 Execution（PR-TD4）
    /// Drivers should not depend on Execution
    /// </summary>
    /// <remarks>
    /// PR-TD4: Drivers 层是底层硬件驱动实现，不应依赖执行层。
    /// 如果 Drivers 需要使用 Execution 层的抽象，应将该抽象上移到 Core 层。
    /// </remarks>
    [Fact]
    public void Drivers_ShouldNotDependOn_Execution()
    {
        var driversCsproj = Path.Combine(
            SolutionRoot, 
            "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/ZakYip.WheelDiverterSorter.Drivers.csproj");
        
        var references = GetProjectReferences(driversCsproj);
        
        var violations = references
            .Where(r => r.Contains("Execution", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Drivers 项目不应依赖 Execution: {string.Join(", ", violations)}\n" +
                       "PR-TD4: Drivers 层是底层硬件驱动实现，不应依赖执行层。\n" +
                       "如果需要共享抽象，应将接口上移到 Core/Abstractions/ 层。");
        }
    }

    /// <summary>
    /// 验证 Drivers 项目不依赖 Execution 或 Communication（PR-RS11）
    /// Drivers should not depend on Execution or Communication
    /// </summary>
    /// <remarks>
    /// PR-RS11: Drivers 层只应依赖 Core（可选 Observability），完全不依赖 Execution / Communication。
    /// HAL 接口定义在 Core/Hardware/**，Drivers 实现这些接口。
    /// </remarks>
    [Fact]
    public void Drivers_ShouldNotDependOn_Execution_Or_Communication()
    {
        var driversCsproj = Path.Combine(
            SolutionRoot, 
            "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/ZakYip.WheelDiverterSorter.Drivers.csproj");
        
        var references = GetProjectReferences(driversCsproj);
        
        // PR-RS11: Drivers 禁止依赖的项目
        var forbiddenDependencies = new[]
        {
            "Execution",
            "Communication"
        };

        var violations = references
            .Where(r => forbiddenDependencies.Any(fd => r.Contains(fd, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Drivers 项目不应依赖以下项目: {string.Join(", ", violations)}\n" +
                       "PR-RS11: Drivers 层只应依赖 Core（可选 Observability），不应依赖 Execution / Communication。\n" +
                       "如果需要共享抽象，应将接口上移到 Core/Hardware/** 层。");
        }
    }

    /// <summary>
    /// 验证 Drivers 只允许依赖 Core 和 Observability（PR-RS11）
    /// Drivers should only depend on Core and optionally Observability
    /// </summary>
    /// <remarks>
    /// PR-RS11: Drivers 层的目标状态是只依赖 Core（必要时可依赖 Observability）。
    /// 这确保了 Drivers 层是纯粹的硬件驱动实现层。
    /// </remarks>
    [Fact]
    public void Drivers_ShouldOnlyDependOn_CoreOrObservability()
    {
        var driversCsproj = Path.Combine(
            SolutionRoot, 
            "src/Drivers/ZakYip.WheelDiverterSorter.Drivers/ZakYip.WheelDiverterSorter.Drivers.csproj");
        
        var references = GetProjectReferences(driversCsproj);
        
        // PR-RS11: Drivers 只允许依赖的项目
        var allowedProjects = new[]
        {
            "ZakYip.WheelDiverterSorter.Core",
            "ZakYip.WheelDiverterSorter.Observability"
        };

        var violations = references
            .Where(r => !allowedProjects.Any(ap => r.Equals(ap, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (violations.Any())
        {
            Assert.Fail($"Drivers 项目包含未允许的依赖: {string.Join(", ", violations)}\n" +
                       $"允许的依赖: {string.Join(", ", allowedProjects)}\n" +
                       "PR-RS11: Drivers 层只应依赖 Core（可选 Observability），不应依赖其他项目。");
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

        report.AppendLine("## Expected Layer Rules (PR-H1)\n");
        report.AppendLine("```");
        report.AppendLine("Host → Application → Core/Execution/Drivers/Ingress/Communication/Observability/Simulation");
        report.AppendLine("```\n");
        report.AppendLine("- Host 只能依赖 Application/Core/Observability（PR-H1: 依赖收缩）");
        report.AppendLine("- Application 可以依赖 Core/Execution/Drivers/Ingress/Communication/Observability/Simulation（DI 聚合层）");
        report.AppendLine("- 其他项目不得依赖 Application（避免反向依赖）");

        Console.WriteLine(report.ToString());
        
        // 这个测试总是通过，只用于生成报告
        Assert.True(true);
    }
}
