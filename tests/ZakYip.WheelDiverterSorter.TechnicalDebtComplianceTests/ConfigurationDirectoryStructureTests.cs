using System.Text;
using System.Text.RegularExpressions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// TD-004: LineModel/Configuration 目录结构合规测试
/// Configuration directory structure compliance tests
/// </summary>
/// <remarks>
/// 此测试类验证 Core/LineModel/Configuration/ 目录结构符合以下规范：
/// 1. 直接子目录必须限制在 { "Models", "Repositories", "Validation" }
/// 2. 配置目录根下禁止直接存在 .cs 文件（或只允许白名单集合）
/// 3. 各子目录职责单一：
///    - Models/：纯配置模型和相关的枚举/值对象
///    - Repositories/Interfaces/：仓储接口
///    - Repositories/LiteDb/：LiteDB 实现（已迁移到 Configuration.Persistence 项目）
///    - Validation/：配置验证相关的类型
/// 
/// PR-TD-ZERO01: 新增此测试类作为 TD-004 的结构防线
/// </remarks>
public partial class ConfigurationDirectoryStructureTests
{
    /// <summary>
    /// 允许在 Configuration 目录根下存在的直接子目录
    /// </summary>
    private static readonly HashSet<string> AllowedSubdirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Models",
        "Repositories",
        "Validation"
    };

    /// <summary>
    /// 允许在 Configuration 目录根下存在的文件（白名单）
    /// </summary>
    /// <remarks>
    /// 当前不允许任何文件直接存在于配置目录根下。
    /// 如果将来需要添加如 ConfigurationModule.cs 之类的入口文件，可以在此白名单中添加。
    /// </remarks>
    private static readonly HashSet<string> AllowedRootFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        // 当前不允许任何文件
        // 如果需要添加入口文件，可以在此处添加，例如：
        // "ConfigurationModule.cs",
    };

    // 编译的正则表达式用于文件内容检查
    [GeneratedRegex(@"\binterface\s+I\w+Repository\b", RegexOptions.Compiled)]
    private static partial Regex RepositoryInterfacePattern();

    [GeneratedRegex(@"\b(?:interface|class)\s+\w+(?:Service|Validator|Handler)\b", RegexOptions.Compiled)]
    private static partial Regex ServiceValidatorHandlerPattern();

    [GeneratedRegex(@"\bclass\s+\w+Configuration\b", RegexOptions.Compiled)]
    private static partial Regex ClassConfigurationPattern();

    [GeneratedRegex(@"\brecord\s+\w+Configuration\b", RegexOptions.Compiled)]
    private static partial Regex RecordConfigurationPattern();

    private static string GetSolutionRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "ZakYip.WheelDiverterSorter.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? Directory.GetCurrentDirectory();
    }

    private static string GetConfigurationDirectory()
    {
        var solutionRoot = GetSolutionRoot();
        return Path.Combine(
            solutionRoot, "src", "Core",
            "ZakYip.WheelDiverterSorter.Core",
            "LineModel", "Configuration");
    }

    /// <summary>
    /// 验证目录存在，如果不存在则使测试失败
    /// </summary>
    private static void AssertDirectoryExists(string path, string description)
    {
        if (!Directory.Exists(path))
        {
            Assert.Fail($"{description} not found: {path}");
        }
    }

    /// <summary>
    /// TD-004: 验证 Configuration 目录的直接子目录只允许指定的目录
    /// Verify that Configuration directory only has allowed subdirectories
    /// </summary>
    /// <remarks>
    /// 目录即语义边界：
    /// - Models/: 存放纯配置模型类
    /// - Repositories/: 存放仓储接口（Interfaces/）和实现
    /// - Validation/: 存放配置验证器
    /// 
    /// 禁止创建其他子目录（如 Services/、Adapters/、Utilities/ 等）
    /// </remarks>
    [Fact]
    public void ConfigurationDirectoryShouldOnlyHaveAllowedSubdirectories()
    {
        var configDir = GetConfigurationDirectory();
        AssertDirectoryExists(configDir, "Configuration directory");

        var actualSubdirectories = Directory.GetDirectories(configDir)
            .Select(d => Path.GetFileName(d))
            .Where(name => !name.StartsWith('.')) // 排除隐藏目录
            .ToList();

        var disallowedDirectories = actualSubdirectories
            .Where(d => !AllowedSubdirectories.Contains(d))
            .ToList();

        if (disallowedDirectories.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-004 违规: Configuration 目录存在未允许的子目录:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var dir in disallowedDirectories)
            {
                report.AppendLine($"  ❌ {dir}/");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 TD-004 规范:");
            report.AppendLine("  Configuration 目录只允许以下直接子目录:");
            foreach (var allowed in AllowedSubdirectories.OrderBy(d => d))
            {
                report.AppendLine($"     - {allowed}/");
            }
            report.AppendLine("\n  修复建议:");
            report.AppendLine("  1. 将配置模型移动到 Models/ 目录");
            report.AppendLine("  2. 将仓储接口移动到 Repositories/Interfaces/ 目录");
            report.AppendLine("  3. 将验证逻辑移动到 Validation/ 目录");
            report.AppendLine("  4. 删除不符合规范的目录");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// TD-004: 验证 Configuration 目录根下禁止直接存在 .cs 文件
    /// Verify that no .cs files exist directly in Configuration directory root
    /// </summary>
    /// <remarks>
    /// 配置目录根下不应有平铺的 .cs 文件，所有类型都应归类到对应子目录。
    /// 如需添加入口文件（如 ConfigurationModule.cs），应先添加到白名单。
    /// </remarks>
    [Fact]
    public void ConfigurationDirectoryShouldNotHaveFlatCsFiles()
    {
        var configDir = GetConfigurationDirectory();
        AssertDirectoryExists(configDir, "Configuration directory");

        var flatCsFiles = Directory.GetFiles(configDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f))
            .Where(f => !AllowedRootFiles.Contains(f))
            .ToList();

        if (flatCsFiles.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-004 违规: Configuration 目录根下存在未归类的 .cs 文件:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var file in flatCsFiles)
            {
                report.AppendLine($"  ❌ {file}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 根据 TD-004 规范:");
            report.AppendLine("  Configuration 目录根下不应有平铺的 .cs 文件。");
            report.AppendLine("  所有类型都应归类到对应子目录：");
            report.AppendLine("     - 配置模型 → Models/");
            report.AppendLine("     - 仓储接口 → Repositories/Interfaces/");
            report.AppendLine("     - 验证器   → Validation/");
            report.AppendLine("\n  如果确需在根目录添加入口文件（如 ConfigurationModule.cs），");
            report.AppendLine("  请先将其添加到测试的 AllowedRootFiles 白名单中。");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// TD-004: 验证 Models 目录只包含配置模型类型
    /// Verify that Models directory only contains configuration model types
    /// </summary>
    /// <remarks>
    /// Models/ 目录应只包含：
    /// - 配置模型类（*Configuration, *Options, *Config 等）
    /// - 相关的枚举和值对象
    /// 
    /// 不应包含：仓储接口、服务、验证器
    /// </remarks>
    [Fact]
    public void ModelsShouldOnlyContainConfigurationModels()
    {
        var configDir = GetConfigurationDirectory();
        var modelsDir = Path.Combine(configDir, "Models");
        AssertDirectoryExists(modelsDir, "Models directory");

        var violations = new List<(string FileName, string ViolationType)>();
        var csFiles = Directory.GetFiles(modelsDir, "*.cs", SearchOption.TopDirectoryOnly);

        foreach (var file in csFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);

            // 检查是否包含接口定义（仓储接口不应在 Models 目录）
            if (RepositoryInterfacePattern().IsMatch(content))
            {
                violations.Add((fileName, "包含仓储接口定义，应移至 Repositories/Interfaces/"));
            }

            // 检查是否包含服务接口或实现
            if (ServiceValidatorHandlerPattern().IsMatch(content) &&
                !fileName.Contains("Configuration", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add((fileName, "包含服务/验证器定义，应移至 Validation/ 或其他适当目录"));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ TD-004 警告: Models 目录存在可能不符合规范的文件:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (fileName, violationType) in violations)
            {
                report.AppendLine($"  ⚠️ {fileName}");
                report.AppendLine($"     问题: {violationType}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 Models 目录应只包含:");
            report.AppendLine("  - 配置模型类（*Configuration, *Options, *Config 等）");
            report.AppendLine("  - 相关的枚举和值对象");

            // 这是一个警告性测试，不会导致失败
            // 如果需要严格检查，可以将 Console.WriteLine 改为 Assert.Fail
            Console.WriteLine(report);
        }
    }

    /// <summary>
    /// TD-004: 验证 Repositories 目录结构正确
    /// Verify that Repositories directory has correct structure
    /// </summary>
    /// <remarks>
    /// Repositories/ 目录应包含：
    /// - Interfaces/ 子目录：仓储接口定义
    /// - 可选的实现子目录（如 LiteDb/，但根据 TD-030 已迁移到 Configuration.Persistence 项目）
    /// 
    /// Repositories 目录根下不应有平铺的 .cs 文件
    /// </remarks>
    [Fact]
    public void RepositoriesShouldHaveCorrectStructure()
    {
        var configDir = GetConfigurationDirectory();
        var repositoriesDir = Path.Combine(configDir, "Repositories");
        AssertDirectoryExists(repositoriesDir, "Repositories directory");

        var violations = new List<string>();

        // 1. 检查 Interfaces 子目录是否存在
        var interfacesDir = Path.Combine(repositoriesDir, "Interfaces");
        if (!Directory.Exists(interfacesDir))
        {
            violations.Add("缺少 Interfaces/ 子目录（仓储接口应定义在此目录）");
        }

        // 2. 检查 Repositories 目录根下是否有平铺的 .cs 文件
        var flatCsFiles = Directory.GetFiles(repositoriesDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f))
            .ToList();

        if (flatCsFiles.Any())
        {
            violations.Add($"Repositories 目录根下存在平铺的 .cs 文件: {string.Join(", ", flatCsFiles)}");
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n❌ TD-004 违规: Repositories 目录结构不正确:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var violation in violations)
            {
                report.AppendLine($"  ❌ {violation}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 Repositories 目录应有以下结构:");
            report.AppendLine("  Repositories/");
            report.AppendLine("  └── Interfaces/     # 仓储接口定义");
            report.AppendLine("\n  注意: LiteDB 实现已迁移到 Configuration.Persistence 项目 (TD-030)");

            Assert.Fail(report.ToString());
        }
    }

    /// <summary>
    /// TD-004: 验证 Validation 目录只包含验证相关类型
    /// Verify that Validation directory only contains validation related types
    /// </summary>
    [Fact]
    public void ValidationShouldOnlyContainValidators()
    {
        var configDir = GetConfigurationDirectory();
        var validationDir = Path.Combine(configDir, "Validation");
        
        if (!Directory.Exists(validationDir))
        {
            // Validation 目录可能为空或不存在，这是允许的
            return;
        }

        var csFiles = Directory.GetFiles(validationDir, "*.cs", SearchOption.AllDirectories);
        var violations = new List<(string FileName, string ViolationType)>();

        foreach (var file in csFiles)
        {
            var fileName = Path.GetFileName(file);
            var content = File.ReadAllText(file);

            // 检查是否包含配置模型定义（应在 Models 目录）
            if (ClassConfigurationPattern().IsMatch(content) ||
                RecordConfigurationPattern().IsMatch(content))
            {
                violations.Add((fileName, "包含配置模型定义，应移至 Models/"));
            }

            // 检查是否包含仓储接口（应在 Repositories/Interfaces 目录）
            if (RepositoryInterfacePattern().IsMatch(content))
            {
                violations.Add((fileName, "包含仓储接口定义，应移至 Repositories/Interfaces/"));
            }
        }

        if (violations.Any())
        {
            var report = new StringBuilder();
            report.AppendLine($"\n⚠️ TD-004 警告: Validation 目录存在可能不符合规范的文件:");
            report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            
            foreach (var (fileName, violationType) in violations)
            {
                report.AppendLine($"  ⚠️ {fileName}");
                report.AppendLine($"     问题: {violationType}");
            }

            report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
            report.AppendLine("\n💡 Validation 目录应只包含:");
            report.AppendLine("  - 配置验证器（*Validator）");
            report.AppendLine("  - 验证规则定义");

            Console.WriteLine(report);
        }
    }

    /// <summary>
    /// TD-004: 生成 Configuration 目录结构报告
    /// Generate Configuration directory structure report
    /// </summary>
    /// <remarks>
    /// 此测试生成目录结构的可视化报告，便于审查当前状态。
    /// 测试始终通过，仅用于输出报告。
    /// </remarks>
    [Fact]
    public void GenerateConfigurationDirectoryStructureReport()
    {
        var configDir = GetConfigurationDirectory();
        
        if (!Directory.Exists(configDir))
        {
            Console.WriteLine($"Configuration directory not found: {configDir}");
            return;
        }

        var report = new StringBuilder();
        report.AppendLine("\n📁 LineModel/Configuration 目录结构报告");
        report.AppendLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        
        // 列出目录根的内容
        report.AppendLine("\nConfiguration/");
        
        // 列出直接子目录
        var subdirectories = Directory.GetDirectories(configDir)
            .Select(d => Path.GetFileName(d))
            .Where(name => !name.StartsWith('.'))
            .OrderBy(d => d)
            .ToList();

        foreach (var dir in subdirectories)
        {
            var isAllowed = AllowedSubdirectories.Contains(dir);
            var icon = isAllowed ? "✅" : "❌";
            report.AppendLine($"├── {icon} {dir}/");
            
            // 列出子目录内容
            var subDirPath = Path.Combine(configDir, dir);
            var subFiles = Directory.GetFiles(subDirPath, "*.cs", SearchOption.TopDirectoryOnly)
                .Select(f => Path.GetFileName(f))
                .OrderBy(f => f)
                .ToList();
            
            var subDirs = Directory.GetDirectories(subDirPath)
                .Select(d => Path.GetFileName(d))
                .Where(name => !name.StartsWith('.'))
                .OrderBy(d => d)
                .ToList();

            foreach (var subDir in subDirs)
            {
                report.AppendLine($"│   ├── 📁 {subDir}/");
                var subSubFiles = Directory.GetFiles(Path.Combine(subDirPath, subDir), "*.cs", SearchOption.TopDirectoryOnly)
                    .Select(f => Path.GetFileName(f))
                    .OrderBy(f => f)
                    .ToList();
                foreach (var file in subSubFiles)
                {
                    report.AppendLine($"│   │   └── 📄 {file}");
                }
            }

            foreach (var file in subFiles)
            {
                report.AppendLine($"│   └── 📄 {file}");
            }
        }

        // 列出直接在根目录的文件
        var rootFiles = Directory.GetFiles(configDir, "*.cs", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileName(f))
            .OrderBy(f => f)
            .ToList();

        if (rootFiles.Any())
        {
            foreach (var file in rootFiles)
            {
                var isAllowed = AllowedRootFiles.Contains(file);
                var icon = isAllowed ? "✅" : "❌";
                report.AppendLine($"└── {icon} {file} (平铺文件)");
            }
        }

        report.AppendLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        report.AppendLine($"\n统计:");
        report.AppendLine($"  - 子目录数: {subdirectories.Count}");
        report.AppendLine($"  - 允许的子目录: {subdirectories.Count(d => AllowedSubdirectories.Contains(d))}");
        report.AppendLine($"  - 平铺文件数: {rootFiles.Count}");

        Console.WriteLine(report);
        
        Assert.True(true, "Report generated successfully");
    }
}
