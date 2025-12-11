using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// PR-RS13: 持久化层合规性测试
/// 验证 Core 项目不引用 LiteDB，持久化实现已正确分离到 Configuration.Persistence 项目
/// </summary>
public class PersistenceLayerComplianceTests
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
    /// 验证 Core 项目不引用 LiteDB NuGet 包
    /// PR-RS13: Core 应该只定义仓储接口，LiteDB 实现已移至 Configuration.Persistence
    /// </summary>
    [Fact]
    public void Core_ShouldNotReferenceLiteDB()
    {
        var coreCsproj = Path.Combine(
            SolutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core/ZakYip.WheelDiverterSorter.Core.csproj");

        var content = File.ReadAllText(coreCsproj);

        // 检查是否引用 LiteDB 包
        var hasLiteDbReference = content.Contains("LiteDB", StringComparison.OrdinalIgnoreCase)
            && content.Contains("PackageReference", StringComparison.OrdinalIgnoreCase);

        Assert.False(hasLiteDbReference,
            "PR-RS13 违规: Core 项目不应引用 LiteDB 包。\n" +
            "LiteDB 仓储实现已移至 Configuration.Persistence 项目。\n" +
            "Core 只应定义仓储接口（ISystemConfigurationRepository 等）。");
    }

    /// <summary>
    /// 验证 Core 项目中没有 LiteDb 目录
    /// PR-RS13: LiteDb 实现已移至 Configuration.Persistence
    /// </summary>
    [Fact]
    public void Core_ShouldNotHaveLiteDbDirectory()
    {
        var liteDbDir = Path.Combine(
            SolutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core/LineModel/Configuration/Repositories/LiteDb");

        Assert.False(Directory.Exists(liteDbDir),
            "PR-RS13 违规: Core 项目中不应存在 LiteDb 目录。\n" +
            $"发现目录: {liteDbDir}\n" +
            "LiteDB 仓储实现已移至 Configuration.Persistence 项目。");
    }

    /// <summary>
    /// 验证 Core 项目中的源文件不包含 using LiteDB
    /// PR-RS13: Core 不应依赖 LiteDB 命名空间
    /// </summary>
    [Fact]
    public void Core_ShouldNotHaveLiteDBUsings()
    {
        var coreDir = Path.Combine(
            SolutionRoot,
            "src/Core/ZakYip.WheelDiverterSorter.Core");

        var csFiles = Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories);

        var violations = new List<string>();

        foreach (var file in csFiles)
        {
            var content = File.ReadAllText(file);
            if (content.Contains("using LiteDB"))
            {
                var relativePath = Path.GetRelativePath(SolutionRoot, file);
                violations.Add(relativePath);
            }
        }

        if (violations.Any())
        {
            Assert.Fail(
                $"PR-RS13 违规: Core 项目中发现 {violations.Count} 个文件包含 'using LiteDB':\n" +
                string.Join("\n", violations.Select(v => $"  - {v}")) + "\n\n" +
                "Core 不应依赖 LiteDB 命名空间。LiteDB 相关代码应移至 Configuration.Persistence 项目。");
        }
    }

    /// <summary>
    /// 验证 Configuration.Persistence 项目存在并包含 LiteDB 仓储实现
    /// PR-RS13: LiteDB 仓储实现应在 Configuration.Persistence 项目中
    /// </summary>
    [Fact]
    public void ConfigurationPersistence_ShouldContainLiteDbRepositories()
    {
        var liteDbDir = Path.Combine(
            SolutionRoot,
            "src/Infrastructure/ZakYip.WheelDiverterSorter.Configuration.Persistence/Repositories/LiteDb");

        Assert.True(Directory.Exists(liteDbDir),
            "PR-RS13 违规: Configuration.Persistence 项目中应存在 Repositories/LiteDb 目录。\n" +
            $"期望目录: {liteDbDir}");

        var csFiles = Directory.GetFiles(liteDbDir, "*.cs", SearchOption.TopDirectoryOnly);

        Assert.True(csFiles.Length > 0,
            "PR-RS13 违规: Configuration.Persistence/Repositories/LiteDb 目录中应包含 LiteDB 仓储实现文件。");
    }

    /// <summary>
    /// 验证 Configuration.Persistence 项目引用 LiteDB NuGet 包
    /// PR-RS13: LiteDB 包引用应在 Configuration.Persistence 项目中
    /// </summary>
    [Fact]
    public void ConfigurationPersistence_ShouldReferenceLiteDB()
    {
        var csproj = Path.Combine(
            SolutionRoot,
            "src/Infrastructure/ZakYip.WheelDiverterSorter.Configuration.Persistence/ZakYip.WheelDiverterSorter.Configuration.Persistence.csproj");

        Assert.True(File.Exists(csproj),
            "PR-RS13 违规: Configuration.Persistence 项目文件不存在。\n" +
            $"期望路径: {csproj}");

        var content = File.ReadAllText(csproj);

        var hasLiteDbReference = content.Contains("LiteDB", StringComparison.OrdinalIgnoreCase)
            && content.Contains("PackageReference", StringComparison.OrdinalIgnoreCase);

        Assert.True(hasLiteDbReference,
            "PR-RS13 违规: Configuration.Persistence 项目应引用 LiteDB 包。\n" +
            "请确保在 csproj 中添加: <PackageReference Include=\"LiteDB\" ... />");
    }
}
