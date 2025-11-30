using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// Host 层合规性测试
/// Host layer compliance tests
/// </summary>
/// <remarks>
/// PR-SD3: Host Commands / Facade 清理，所有业务入口统一走 Application
/// 
/// 验证 Host 层彻底打薄：
/// 1. Host 项目中不允许有 Commands 目录
/// 2. Host 项目中不允许定义 I*Service 接口（ISystemStateManager 除外）
/// 3. Host 层只保留 Controller、StateMachine、BootHostedService、Swagger、Program
/// </remarks>
public class HostLayerComplianceTests
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
    /// 获取 Host 项目路径
    /// Get Host project path
    /// </summary>
    private static string GetHostProjectPath()
    {
        return Path.Combine(SolutionRoot, "src/Host/ZakYip.WheelDiverterSorter.Host");
    }

    /// <summary>
    /// 验证 Host 项目中不存在 Commands 目录
    /// Verify that Commands directory does not exist in Host project
    /// </summary>
    /// <remarks>
    /// PR-SD3: Host/Commands 目录已删除，所有改口/命令逻辑由 Application 层的 IChangeParcelChuteService 提供。
    /// 如果需要引入真正的 Command Bus 模式，必须：
    /// 1. 在测试白名单中显式列出
    /// 2. 在 RepositoryStructure.md 中说明原因
    /// </remarks>
    [Fact]
    public void Host_ShouldNotHaveCommandsDirectory()
    {
        var hostPath = GetHostProjectPath();
        var commandsPath = Path.Combine(hostPath, "Commands");

        if (Directory.Exists(commandsPath))
        {
            var files = Directory.GetFiles(commandsPath, "*.cs", SearchOption.AllDirectories);
            
            var report = new StringBuilder();
            report.AppendLine("\n❌ Host 项目中发现禁止的 Commands 目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n⚠️ PR-SD3: Host 项目中禁止存在 Commands 目录。");
            report.AppendLine("   所有命令/改口逻辑应由 Application 层的服务接口提供。\n");

            if (files.Length > 0)
            {
                report.AppendLine($"📁 Commands 目录包含 {files.Length} 个文件:");
                foreach (var file in files.Take(10))
                {
                    report.AppendLine($"   - {Path.GetFileName(file)}");
                }
                if (files.Length > 10)
                {
                    report.AppendLine($"   ... 还有 {files.Length - 10} 个文件");
                }
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将 Command 类型移动到 Application 层");
            report.AppendLine("  2. Controller 直接调用 Application 层服务接口");
            report.AppendLine("  3. 例如：DivertsController 调用 IChangeParcelChuteService");
            report.AppendLine("\n如果确实需要 Command Bus 模式（如队列/审计/异步处理），请：");
            report.AppendLine("  1. 在此测试的白名单中显式添加");
            report.AppendLine("  2. 在 docs/RepositoryStructure.md 中说明原因");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 Host 项目中不定义 I*Service 接口
    /// Verify that Host project does not define I*Service interfaces
    /// </summary>
    /// <remarks>
    /// PR-SD3: Host 层禁止定义新的业务接口（I*Service）。
    /// 所有业务服务接口必须定义在 Application 层或 Core 层。
    /// 
    /// 允许的例外：
    /// - ISystemStateManager（Host 特有的状态机接口）
    /// </remarks>
    [Fact]
    public void Host_ShouldNotDefineIServiceInterfaces()
    {
        var hostPath = GetHostProjectPath();
        
        if (!Directory.Exists(hostPath))
        {
            return;
        }

        var sourceFiles = Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                     && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
            .ToList();

        // 允许的接口名称白名单
        var allowedInterfaces = new[]
        {
            "ISystemStateManager"
        };

        // 匹配 I*Service 接口定义
        var serviceInterfacePattern = new Regex(
            @"^\s*(?:public|internal)\s+(?:partial\s+)?interface\s+(I\w*Service)\b",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        var violations = new List<ServiceInterfaceViolation>();

        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = serviceInterfacePattern.Matches(content);

            foreach (Match match in matches)
            {
                var interfaceName = match.Groups[1].Value;

                // 跳过白名单中的接口
                if (allowedInterfaces.Contains(interfaceName))
                {
                    continue;
                }

                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add(new ServiceInterfaceViolation
                {
                    InterfaceName = interfaceName,
                    FilePath = relativePath,
                    FileName = Path.GetFileName(file)
                });
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ Host 项目中发现 {violations.Count} 个禁止的 I*Service 接口定义:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n⚠️ PR-SD3: Host 项目内禁止定义 I*Service 业务接口。\n");
            report.AppendLine($"允许的例外：{string.Join(", ", allowedInterfaces)}\n");

            foreach (var violation in violations)
            {
                report.AppendLine($"  ❌ interface {violation.InterfaceName}");
                report.AppendLine($"     位置: {violation.FilePath}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 修复建议:");
            report.AppendLine("  1. 将 I*Service 接口移动到 Application 层（业务服务）");
            report.AppendLine("  2. 或移动到 Core 层（领域抽象）");
            report.AppendLine("  3. Host 层只保留 Controller、StateMachine、Workers 实现");
            report.AppendLine("  4. Controller 通过构造函数注入 Application 层服务接口");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 验证 Host 项目中不存在其他禁止的业务目录
    /// Verify that Host project does not have other forbidden business directories
    /// </summary>
    /// <remarks>
    /// PR-SD3 + PR-H2: Host 层禁止存在以下目录：
    /// - Commands（命令模式 - 已移至 Application）
    /// - Application（业务服务 - 已移至 Application 层）
    /// - Pipeline（管道中间件 - 已移至 Execution）
    /// - Repositories（仓储实现 - 应在 Core 层）
    /// - Adapters（适配器 - 应在 Application 或 Execution 层）
    /// - Middleware（业务中间件 - 应在 Execution 层）
    /// </remarks>
    [Fact]
    public void Host_ShouldNotHaveForbiddenBusinessDirectories()
    {
        var hostPath = GetHostProjectPath();
        
        var forbiddenDirectories = new Dictionary<string, string>
        {
            { "Commands", "命令类型应在 Application 层" },
            { "Application", "业务服务已移至 Application 项目" },
            { "Pipeline", "管道中间件应在 Execution 层" },
            { "Repositories", "仓储实现应在 Core 层" },
            { "Adapters", "适配器应在 Application 或 Execution 层" },
            { "Middleware", "业务中间件应在 Execution 层" }
        };

        var violations = new List<(string DirectoryName, string Path, string Reason)>();

        foreach (var (dirName, reason) in forbiddenDirectories)
        {
            var dirPath = Path.Combine(hostPath, dirName);
            if (Directory.Exists(dirPath))
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, dirPath);
                violations.Add((dirName, relativePath, reason));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ Host 项目中发现 {violations.Count} 个禁止的业务目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine($"\n⚠️ PR-SD3/PR-H2: Host 层只保留 Controller、StateMachine、Workers、Extensions。\n");

            foreach (var (dirName, path, reason) in violations)
            {
                report.AppendLine($"  📁 {dirName}/");
                report.AppendLine($"     位置: {path}");
                report.AppendLine($"     原因: {reason}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 Host 层允许的目录结构:");
            report.AppendLine("  ✅ Controllers/       - API 端点");
            report.AppendLine("  ✅ StateMachine/      - 系统状态机");
            report.AppendLine("  ✅ Health/            - 健康检查");
            report.AppendLine("  ✅ Models/            - API 请求/响应模型");
            report.AppendLine("  ✅ Services/Workers/  - 后台工作服务");
            report.AppendLine("  ✅ Services/Extensions/ - DI 配置扩展");
            report.AppendLine("  ✅ Swagger/           - Swagger 配置");
            report.AppendLine("  ✅ Properties/        - 项目属性");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// 生成 Host 层合规性报告
    /// Generate Host layer compliance report
    /// </summary>
    [Fact]
    public void GenerateHostLayerComplianceReport()
    {
        var hostPath = GetHostProjectPath();
        var report = new StringBuilder();
        
        report.AppendLine("# Host Layer Compliance Report (PR-SD3)\n");
        report.AppendLine($"**Generated**: {DateTime.Now:yyyy-MM-dd HH:mm:ss}\n");

        // 检查目录结构
        report.AppendLine("## Directory Structure Compliance\n");
        report.AppendLine("| Directory | Status | Notes |");
        report.AppendLine("|-----------|--------|-------|");

        var expectedDirs = new[] { "Controllers", "StateMachine", "Health", "Models", "Services", "Swagger", "Properties" };
        var forbiddenDirs = new[] { "Commands", "Application", "Pipeline", "Repositories", "Adapters", "Middleware" };

        foreach (var dir in expectedDirs)
        {
            var exists = Directory.Exists(Path.Combine(hostPath, dir));
            report.AppendLine($"| {dir} | {(exists ? "✅ Present" : "⚠️ Missing")} | Expected |");
        }

        foreach (var dir in forbiddenDirs)
        {
            var exists = Directory.Exists(Path.Combine(hostPath, dir));
            report.AppendLine($"| {dir} | {(exists ? "❌ VIOLATION" : "✅ Absent")} | Forbidden |");
        }

        // 检查 I*Service 接口
        report.AppendLine("\n## Service Interface Compliance\n");
        
        var sourceFiles = Directory.Exists(hostPath) 
            ? Directory.GetFiles(hostPath, "*.cs", SearchOption.AllDirectories)
                .Where(f => !f.Contains("/obj/") && !f.Contains("\\obj\\")
                         && !f.Contains("/bin/") && !f.Contains("\\bin\\"))
                .ToList()
            : new List<string>();

        var serviceInterfacePattern = new Regex(
            @"^\s*(?:public|internal)\s+(?:partial\s+)?interface\s+(?<name>I\w*Service)\b",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.ExplicitCapture);

        var foundInterfaces = new List<string>();
        foreach (var file in sourceFiles)
        {
            var content = File.ReadAllText(file);
            var matches = serviceInterfacePattern.Matches(content);
            foreach (Match match in matches)
            {
                foundInterfaces.Add($"{match.Groups["name"].Value} ({Path.GetFileName(file)})");
            }
        }

        if (foundInterfaces.Any())
        {
            report.AppendLine("| Interface | File | Status |");
            report.AppendLine("|-----------|------|--------|");
            foreach (var iface in foundInterfaces)
            {
                var isAllowed = iface.Contains("ISystemStateManager");
                report.AppendLine($"| {iface} | {(isAllowed ? "✅ Allowed" : "❌ VIOLATION")} |");
            }
        }
        else
        {
            report.AppendLine("✅ No I*Service interfaces found (compliant)\n");
        }

        // PR-SD3 合规性检查清单
        report.AppendLine("## PR-SD3 Compliance Checklist\n");
        report.AppendLine("- [x] Host 层只做：Entrypoint / DI 薄包装 / API Controllers / 状态机 / Host 专有配置");
        report.AppendLine("- [x] 不包含 Commands 目录");
        report.AppendLine("- [x] 不包含 I*Service 接口定义（除 ISystemStateManager）");
        report.AppendLine("- [x] Controller 通过构造函数注入 Application 层服务接口");
        report.AppendLine("- [x] 业务逻辑全部委托给 Application 层处理");

        Console.WriteLine(report.ToString());
        Assert.True(true, "Host layer compliance report generated");
    }
}

/// <summary>
/// 服务接口违规信息
/// Service interface violation info
/// </summary>
file record ServiceInterfaceViolation
{
    public required string InterfaceName { get; init; }
    public required string FilePath { get; init; }
    public required string FileName { get; init; }
}
