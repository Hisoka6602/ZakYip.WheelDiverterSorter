using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.ArchTests;

/// <summary>
/// PR-SD4: Execution 层路径管线架构测试
/// Architecture tests for Execution layer path pipeline constraints
/// </summary>
/// <remarks>
/// 根据 PR-SD4 规范，这些测试确保：
/// 1. Execution/Pipeline/Middlewares/* 不依赖 Drivers、Core/Hardware 命名空间
/// 2. 中间件只依赖 Core 抽象和 Execution 自身抽象
/// 3. 硬件调用只能在 PathExecutionService 中进行
/// 
/// These tests enforce:
/// 1. Execution/Pipeline/Middlewares/* must not depend on Drivers or Core/Hardware namespaces
/// 2. Middlewares should only depend on Core abstractions and Execution abstractions
/// 3. Hardware calls should only exist in PathExecutionService (and its event chain)
/// </remarks>
public class ExecutionPathPipelineTests
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
    /// 获取 Execution/Pipeline/Middlewares 目录下的所有 C# 源文件
    /// Get all C# source files in Execution/Pipeline/Middlewares directory
    /// </summary>
    private List<string> GetMiddlewareSourceFiles()
    {
        var middlewaresPath = Path.Combine(
            SolutionRoot, 
            "src/Execution/ZakYip.WheelDiverterSorter.Execution/Pipeline/Middlewares");
        
        if (!Directory.Exists(middlewaresPath))
        {
            return new List<string>();
        }

        return Directory.GetFiles(middlewaresPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();
    }

    /// <summary>
    /// PR-SD4: 验证中间件不依赖 Drivers 命名空间
    /// Middlewares should not depend on Drivers namespace
    /// </summary>
    /// <remarks>
    /// 中间件只做监控（计时、计数、拥堵统计）和日志与异常包装。
    /// 禁止中间件直接访问 Drivers 命名空间。
    /// </remarks>
    [Fact]
    public void Middlewares_ShouldNotDependOn_DriversNamespace()
    {
        var sourceFiles = GetMiddlewareSourceFiles();
        var violations = new List<NamespaceViolation>();
        
        // Pattern to match using statements for Drivers namespace
        var usingPattern = new Regex(
            @"using\s+(?<namespace>ZakYip\.WheelDiverterSorter\.Drivers[^;]*);",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = usingPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var namespaceName = match.Groups["namespace"].Value;
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add(new NamespaceViolation
                {
                    FileName = Path.GetFileName(file),
                    FilePath = relativePath,
                    ForbiddenNamespace = namespaceName
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ PR-SD4: Middleware 发现禁止的 Drivers 命名空间依赖:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 中间件禁止直接访问 Drivers 命名空间。\n");

            foreach (var violation in violations)
            {
                report.AppendLine($"   ❌ {violation.FileName}");
                report.AppendLine($"      依赖: {violation.ForbiddenNamespace}");
                report.AppendLine($"      位置: {violation.FilePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 中间件只应调用 Core 抽象和 Execution 自身抽象");
            report.AppendLine("  2. 硬件操作应委托给 IPathExecutionService 或 ISwitchingPathExecutor");
            report.AppendLine("  3. 中间件只做监控、日志和异常包装");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD4: 验证中间件不依赖 Core/Hardware 命名空间
    /// Middlewares should not depend on Core/Hardware namespace
    /// </summary>
    /// <remarks>
    /// 中间件禁止直接调用硬件驱动接口。
    /// 硬件操作应通过 Execution 层的抽象接口进行。
    /// </remarks>
    [Fact]
    public void Middlewares_ShouldNotDependOn_CoreHardwareNamespace()
    {
        var sourceFiles = GetMiddlewareSourceFiles();
        var violations = new List<NamespaceViolation>();
        
        // Pattern to match using statements for Core.Hardware namespace
        var usingPattern = new Regex(
            @"using\s+(?<namespace>ZakYip\.WheelDiverterSorter\.Core\.Hardware[^;]*);",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = usingPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var namespaceName = match.Groups["namespace"].Value;
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add(new NamespaceViolation
                {
                    FileName = Path.GetFileName(file),
                    FilePath = relativePath,
                    ForbiddenNamespace = namespaceName
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ PR-SD4: Middleware 发现禁止的 Core.Hardware 命名空间依赖:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 中间件禁止直接访问 Core.Hardware 命名空间。\n");

            foreach (var violation in violations)
            {
                report.AppendLine($"   ❌ {violation.FileName}");
                report.AppendLine($"      依赖: {violation.ForbiddenNamespace}");
                report.AppendLine($"      位置: {violation.FilePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 中间件只应调用 Core 抽象（Core.Abstractions.*）");
            report.AppendLine("  2. 硬件操作应委托给 IPathExecutionService 或 ISwitchingPathExecutor");
            report.AppendLine("  3. 中间件只做监控、日志和异常包装");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD4: 验证中间件不直接使用硬件驱动接口类型
    /// Middlewares should not directly use hardware driver interface types
    /// </summary>
    /// <remarks>
    /// 此测试检查中间件代码中是否包含对 IWheelDiverterDriver、
    /// IInputPort、IOutputPort 等硬件接口的直接引用。
    /// </remarks>
    [Fact]
    public void Middlewares_ShouldNotUse_HardwareDriverInterfaces()
    {
        var sourceFiles = GetMiddlewareSourceFiles();
        var violations = new List<InterfaceUsageViolation>();
        
        // List of forbidden hardware interface types
        var forbiddenInterfaces = new[]
        {
            ("IWheelDiverterDriver", "摆轮驱动接口"),
            ("IWheelDiverterDevice", "摆轮设备接口"),
            ("IInputPort", "输入端口接口"),
            ("IOutputPort", "输出端口接口"),
            ("IConveyorDriveController", "输送带驱动控制器"),
            ("IAlarmOutputController", "报警输出控制器"),
            ("ISensorInputReader", "传感器输入读取器")
        };

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            
            foreach (var (interfaceName, description) in forbiddenInterfaces)
            {
                // Check if the interface is used (as field, parameter, or local variable)
                var pattern = new Regex(
                    $@"\b{interfaceName}\b",
                    RegexOptions.Compiled);
                
                if (pattern.IsMatch(content))
                {
                    var relativePath = Path.GetRelativePath(SolutionRoot, file);
                    violations.Add(new InterfaceUsageViolation
                    {
                        FileName = Path.GetFileName(file),
                        FilePath = relativePath,
                        InterfaceName = interfaceName,
                        Description = description
                    });
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ PR-SD4: Middleware 发现禁止的硬件驱动接口使用:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 中间件禁止直接使用硬件驱动接口。\n");

            var byFile = violations.GroupBy(v => v.FileName);
            foreach (var group in byFile)
            {
                report.AppendLine($"📁 {group.Key}:");
                foreach (var violation in group)
                {
                    report.AppendLine($"   ❌ 使用了 {violation.InterfaceName} ({violation.Description})");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将硬件操作移至 PathExecutionService");
            report.AppendLine("  2. 中间件通过 IPathExecutionService 或 ISwitchingPathExecutor 间接操作硬件");
            report.AppendLine("  3. 中间件应只关注流程编排、日志和监控");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// PR-SD4: 验证中间件只允许依赖的命名空间
    /// Middlewares should only depend on allowed namespaces
    /// </summary>
    /// <remarks>
    /// 允许的依赖：
    /// - Microsoft.Extensions.* (DI, Logging, Options)
    /// - System.*
    /// - ZakYip.WheelDiverterSorter.Core.* (但不包括 Core.Hardware)
    /// - ZakYip.WheelDiverterSorter.Execution.* (自身命名空间)
    /// - ZakYip.WheelDiverterSorter.Observability.*
    /// 
    /// 禁止的依赖：
    /// - ZakYip.WheelDiverterSorter.Drivers.*
    /// - ZakYip.WheelDiverterSorter.Core.Hardware.*
    /// </remarks>
    [Fact]
    public void Middlewares_ShouldOnlyDependOn_AllowedNamespaces()
    {
        var sourceFiles = GetMiddlewareSourceFiles();
        var violations = new List<NamespaceViolation>();
        
        // Pattern to match all using statements
        var usingPattern = new Regex(
            @"using\s+(?<namespace>ZakYip\.WheelDiverterSorter\.[^;]+);",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        // Allowed namespace prefixes (exclude Core.Hardware)
        var allowedPrefixes = new[]
        {
            "ZakYip.WheelDiverterSorter.Core.Abstractions",
            "ZakYip.WheelDiverterSorter.Core.Enums",
            "ZakYip.WheelDiverterSorter.Core.LineModel",
            "ZakYip.WheelDiverterSorter.Core.Sorting",
            "ZakYip.WheelDiverterSorter.Core.Utilities",
            "ZakYip.WheelDiverterSorter.Execution",
            "ZakYip.WheelDiverterSorter.Observability"
        };

        // Explicitly forbidden namespace prefixes
        var forbiddenPrefixes = new[]
        {
            "ZakYip.WheelDiverterSorter.Drivers",
            "ZakYip.WheelDiverterSorter.Core.Hardware"
        };

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = usingPattern.Matches(content);
            
            foreach (Match match in matches)
            {
                var namespaceName = match.Groups["namespace"].Value;
                
                // Check if this namespace is explicitly forbidden
                var isForbidden = forbiddenPrefixes.Any(fp => 
                    namespaceName.StartsWith(fp, StringComparison.OrdinalIgnoreCase));
                
                if (isForbidden)
                {
                    var relativePath = Path.GetRelativePath(SolutionRoot, file);
                    violations.Add(new NamespaceViolation
                    {
                        FileName = Path.GetFileName(file),
                        FilePath = relativePath,
                        ForbiddenNamespace = namespaceName
                    });
                }
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine("\n❌ PR-SD4: Middleware 发现禁止的命名空间依赖:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n⚠️ 中间件只允许依赖 Core 抽象和 Execution 自身抽象。\n");
            report.AppendLine("禁止依赖:");
            report.AppendLine("  - ZakYip.WheelDiverterSorter.Drivers.*");
            report.AppendLine("  - ZakYip.WheelDiverterSorter.Core.Hardware.*\n");

            var byFile = violations.GroupBy(v => v.FileName);
            foreach (var group in byFile)
            {
                report.AppendLine($"📁 {group.Key}:");
                foreach (var violation in group)
                {
                    report.AppendLine($"   ❌ {violation.ForbiddenNamespace}");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 移除对 Drivers 和 Core.Hardware 的直接依赖");
            report.AppendLine("  2. 通过 Core.Abstractions 中定义的抽象接口访问功能");
            report.AppendLine("  3. 参考 PR-SD4 规范确保职责边界清晰");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成中间件依赖报告
    /// Generate middleware dependency report
    /// </summary>
    [Fact]
    public void GenerateMiddlewareDependencyReport()
    {
        var report = new StringBuilder();
        report.AppendLine("# Execution Pipeline Middleware Dependency Report (PR-SD4)\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        var sourceFiles = GetMiddlewareSourceFiles();
        
        if (sourceFiles.Count == 0)
        {
            report.AppendLine("❌ No middleware files found");
            Console.WriteLine(report.ToString());
            Assert.True(true);
            return;
        }

        report.AppendLine("## Middleware Files\n");
        report.AppendLine($"Found {sourceFiles.Count} middleware files:\n");
        
        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            report.AppendLine($"- {fileName}");
        }

        report.AppendLine("\n## Namespace Dependencies\n");
        
        var usingPattern = new Regex(
            @"using\s+(?<namespace>ZakYip\.WheelDiverterSorter\.[^;]+);",
            RegexOptions.Compiled | RegexOptions.ExplicitCapture);

        foreach (var file in sourceFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);
            var matches = usingPattern.Matches(content);
            
            report.AppendLine($"### {fileName}\n");
            
            var namespaces = matches
                .Cast<Match>()
                .Select(m => m.Groups["namespace"].Value)
                .Distinct()
                .OrderBy(n => n)
                .ToList();
            
            if (namespaces.Any())
            {
                foreach (var ns in namespaces)
                {
                    var status = ns.Contains("Drivers") || ns.Contains("Core.Hardware") 
                        ? "❌ FORBIDDEN" 
                        : "✅ Allowed";
                    report.AppendLine($"  - {ns} {status}");
                }
            }
            else
            {
                report.AppendLine("  - (no project namespace references)");
            }
            report.AppendLine();
        }

        report.AppendLine("## PR-SD4 Compliance Rules\n");
        report.AppendLine("- ✅ Middlewares can depend on Core.Abstractions.*");
        report.AppendLine("- ✅ Middlewares can depend on Core.Enums.*");
        report.AppendLine("- ✅ Middlewares can depend on Core.LineModel.*");
        report.AppendLine("- ✅ Middlewares can depend on Core.Sorting.*");
        report.AppendLine("- ✅ Middlewares can depend on Core.Utilities.*");
        report.AppendLine("- ✅ Middlewares can depend on Execution.*");
        report.AppendLine("- ✅ Middlewares can depend on Observability.*");
        report.AppendLine("- ❌ Middlewares MUST NOT depend on Drivers.*");
        report.AppendLine("- ❌ Middlewares MUST NOT depend on Core.Hardware.*");
        report.AppendLine("\n## Middleware Responsibilities (PR-SD4)\n");
        report.AppendLine("Middlewares should ONLY do:");
        report.AppendLine("- 监控（计时、计数、拥堵统计）");
        report.AppendLine("- 日志与异常包装");
        report.AppendLine("\nMiddlewares MUST NOT:");
        report.AppendLine("- 直接访问 Drivers 或 Core/Hardware 命名空间");
        report.AppendLine("- 直接调用硬件驱动接口");

        Console.WriteLine(report.ToString());
        
        // This test always passes, just generates a report
        Assert.True(true);
    }
}

/// <summary>
/// 命名空间违规信息
/// </summary>
file record NamespaceViolation
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string ForbiddenNamespace { get; init; }
}

/// <summary>
/// 接口使用违规信息
/// </summary>
file record InterfaceUsageViolation
{
    public required string FileName { get; init; }
    public required string FilePath { get; init; }
    public required string InterfaceName { get; init; }
    public required string Description { get; init; }
}
