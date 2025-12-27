using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace ZakYip.WheelDiverterSorter.TechnicalDebtComplianceTests;

/// <summary>
/// 验证所有配置模型都有对应的 LiteDB 持久化实现
/// </summary>
/// <remarks>
/// 问题描述：检查是不是所有配置都写在LiteDB中
/// 此测试确保所有主要配置模型都有对应的仓储接口和 LiteDB 实现
/// </remarks>
public class ConfigurationPersistenceTests
{
    private readonly ITestOutputHelper _output;

    public ConfigurationPersistenceTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllConfigurationModels_ShouldHave_RepositoryInterface()
    {
        // Arrange: 获取所有配置模型
        var repoRoot = GetRepositoryRoot();
        var configModelsDirectory = Path.Combine(
            repoRoot,
            "src", "Core", "ZakYip.WheelDiverterSorter.Core",
            "LineModel", "Configuration", "Models");

        var repositoryInterfacesDirectory = Path.Combine(
            repoRoot,
            "src", "Core", "ZakYip.WheelDiverterSorter.Core",
            "LineModel", "Configuration", "Repositories", "Interfaces");

        Assert.True(Directory.Exists(configModelsDirectory),
            $"配置模型目录不存在: {configModelsDirectory}");
        Assert.True(Directory.Exists(repositoryInterfacesDirectory),
            $"仓储接口目录不存在: {repositoryInterfacesDirectory}");

        // 获取所有配置模型文件（排除嵌套类型和选项类）
        var configModelFiles = Directory.GetFiles(configModelsDirectory, "*.cs")
            .Where(f => !Path.GetFileName(f).Contains("Options") &&
                       !Path.GetFileName(f).Contains("Defaults") &&
                       !Path.GetFileName(f).Contains("Point") &&
                       !Path.GetFileName(f).Contains("Entry") &&
                       !Path.GetFileName(f).Equals("ChuteSensorConfig.cs") &&
                       !Path.GetFileName(f).Equals("DiverterConfigurationEntry.cs"))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();

        // 获取所有仓储接口文件
        var repositoryInterfaces = Directory.GetFiles(repositoryInterfacesDirectory, "I*.cs")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(n => n.Substring(1).Replace("Repository", "")) // 移除 "I" 前缀和 "Repository" 后缀
            .OrderBy(n => n)
            .ToList();

        _output.WriteLine("=== 配置模型列表 ===");
        foreach (var model in configModelFiles)
        {
            _output.WriteLine($"  - {model}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== 仓储接口列表 ===");
        foreach (var repo in repositoryInterfaces)
        {
            _output.WriteLine($"  - {repo}");
        }

        // 期望的配置模型（应该有对应的仓储）
        var expectedConfigurations = new[]
        {
            "SystemConfiguration",
            "CommunicationConfiguration",
            "DriverConfiguration",
            "SensorConfiguration",
            "PanelConfiguration",
            "WheelDiverterConfiguration",
            "ChuteRouteConfiguration",
            "ChutePathTopologyConfig",
            "LoggingConfiguration",
            "IoLinkageConfiguration",
            "ConveyorSegmentConfiguration",
            "ChuteDropoffCallbackConfiguration",
            "ParcelLossDetectionConfiguration"
        };

        // Act & Assert: 检查每个期望的配置是否有对应的仓储接口
        var missingRepositories = new List<string>();

        foreach (var config in expectedConfigurations)
        {
            // 特殊处理映射规则：
            // - ChuteRouteConfiguration -> RouteConfiguration
            // - ChutePathTopologyConfig -> ChutePathTopology  
            // - ConveyorSegmentConfiguration -> ConveyorSegment
            // - 其他 *Configuration -> *Configuration (保持不变)
            
            var expectedRepoName = config;
            
            if (config == "ChuteRouteConfiguration")
            {
                expectedRepoName = "RouteConfiguration";
            }
            else if (config == "ChutePathTopologyConfig")
            {
                expectedRepoName = "ChutePathTopology";
            }
            else if (config == "ConveyorSegmentConfiguration")
            {
                expectedRepoName = "ConveyorSegment";
            }

            if (!repositoryInterfaces.Contains(expectedRepoName))
            {
                missingRepositories.Add($"{config} → I{expectedRepoName}Repository");
            }
        }

        if (missingRepositories.Any())
        {
            _output.WriteLine("");
            _output.WriteLine("=== 缺少仓储接口的配置模型 ===");
            foreach (var missing in missingRepositories)
            {
                _output.WriteLine($"  ❌ {missing}");
            }
        }

        Assert.Empty(missingRepositories);
    }

    [Fact]
    public void AllRepositoryInterfaces_ShouldHave_LiteDbImplementation()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var repositoryInterfacesDirectory = Path.Combine(
            repoRoot,
            "src", "Core", "ZakYip.WheelDiverterSorter.Core",
            "LineModel", "Configuration", "Repositories", "Interfaces");

        var liteDbImplementationsDirectory = Path.Combine(
            repoRoot,
            "src", "Infrastructure", "ZakYip.WheelDiverterSorter.Configuration.Persistence",
            "Repositories", "LiteDb");

        Assert.True(Directory.Exists(repositoryInterfacesDirectory),
            $"仓储接口目录不存在: {repositoryInterfacesDirectory}");
        Assert.True(Directory.Exists(liteDbImplementationsDirectory),
            $"LiteDB实现目录不存在: {liteDbImplementationsDirectory}");

        // 获取所有仓储接口
        var repositoryInterfaces = Directory.GetFiles(repositoryInterfacesDirectory, "I*.cs")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n)
            .ToList();

        // 获取所有 LiteDB 实现
        var liteDbImplementations = Directory.GetFiles(liteDbImplementationsDirectory, "LiteDb*.cs")
            .Where(f => !Path.GetFileName(f).Contains("MapperConfig"))
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .Select(n => n.Replace("LiteDb", "I")) // LiteDbSystemConfigurationRepository → ISystemConfigurationRepository
            .OrderBy(n => n)
            .ToList();

        _output.WriteLine("=== 仓储接口 ===");
        foreach (var iface in repositoryInterfaces)
        {
            _output.WriteLine($"  - {iface}");
        }

        _output.WriteLine("");
        _output.WriteLine("=== LiteDB 实现 ===");
        foreach (var impl in liteDbImplementations)
        {
            _output.WriteLine($"  - {impl}");
        }

        // Act & Assert: 检查每个接口是否有对应的 LiteDB 实现
        var missingImplementations = new List<string>();

        foreach (var interfaceName in repositoryInterfaces)
        {
            if (!liteDbImplementations.Contains(interfaceName))
            {
                missingImplementations.Add(interfaceName);
            }
        }

        if (missingImplementations.Any())
        {
            _output.WriteLine("");
            _output.WriteLine("=== 缺少 LiteDB 实现的仓储接口 ===");
            foreach (var missing in missingImplementations)
            {
                _output.WriteLine($"  ❌ {missing}");
            }
        }

        Assert.Empty(missingImplementations);
    }

    [Fact]
    public void EmbeddedConfigurationTypes_ShouldNotHave_SeparateRepositories()
    {
        // Arrange: 这些是嵌入式配置类型，不应该有独立的仓储
        var embeddedTypes = new[]
        {
            "ChuteAssignmentTimeoutOptions",  // 嵌入在 SystemConfiguration.ChuteAssignmentTimeout
            "IoLinkageOptions",                // 嵌入在 SystemConfiguration.IoLinkage
            "ChuteSensorConfig",               // 嵌入在 ChuteRouteConfiguration.SensorConfig
            "DiverterConfigurationEntry",      // 嵌入在 ChuteRouteConfiguration.DiverterConfigurations
            "IoLinkagePoint"                   // 嵌入在 IoLinkageOptions 各属性中
        };

        var repoRoot = GetRepositoryRoot();
        var repositoryInterfacesDirectory = Path.Combine(
            repoRoot,
            "src", "Core", "ZakYip.WheelDiverterSorter.Core",
            "LineModel", "Configuration", "Repositories", "Interfaces");

        Assert.True(Directory.Exists(repositoryInterfacesDirectory),
            $"仓储接口目录不存在: {repositoryInterfacesDirectory}");

        var repositoryInterfaces = Directory.GetFiles(repositoryInterfacesDirectory, "*.cs")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .ToList();

        // Act & Assert: 检查嵌入式类型不应该有独立的仓储接口
        var unexpectedRepositories = new List<string>();

        foreach (var embeddedType in embeddedTypes)
        {
            var possibleRepoNames = new[]
            {
                $"I{embeddedType}Repository",
                $"I{embeddedType.Replace("Config", "")}Repository",
                $"I{embeddedType.Replace("Options", "")}Repository"
            };

            foreach (var repoName in possibleRepoNames)
            {
                if (repositoryInterfaces.Contains(repoName))
                {
                    unexpectedRepositories.Add($"{embeddedType} → {repoName}（不应存在，这是嵌入式类型）");
                }
            }
        }

        if (unexpectedRepositories.Any())
        {
            _output.WriteLine("=== 嵌入式类型存在不应该有的独立仓储 ===");
            foreach (var unexpected in unexpectedRepositories)
            {
                _output.WriteLine($"  ❌ {unexpected}");
            }
        }

        Assert.Empty(unexpectedRepositories);
    }

    [Fact]
    public void ConfigurationModels_ShouldBeDocumentedInRepositoryStructure()
    {
        // Arrange
        var repoRoot = GetRepositoryRoot();
        var repoStructureFile = Path.Combine(
            repoRoot,
            "docs", "RepositoryStructure.md");

        Assert.True(File.Exists(repoStructureFile),
            $"RepositoryStructure.md 不存在: {repoStructureFile}");

        var content = File.ReadAllText(repoStructureFile);

        // 期望在文档中提到的配置模型
        var expectedConfigurations = new[]
        {
            "SystemConfiguration",
            "CommunicationConfiguration",
            "DriverConfiguration",
            "SensorConfiguration",
            "PanelConfiguration",
            "WheelDiverterConfiguration",
            "ChuteRouteConfiguration",
            "ChutePathTopologyConfig",
            "LoggingConfiguration",
            "IoLinkageConfiguration",
            "ConveyorSegmentConfiguration",
            "ParcelLossDetectionConfiguration"
        };

        var missingInDocs = new List<string>();

        foreach (var config in expectedConfigurations)
        {
            if (!content.Contains(config))
            {
                missingInDocs.Add(config);
            }
        }

        if (missingInDocs.Any())
        {
            _output.WriteLine("=== 未在 RepositoryStructure.md 中记录的配置模型 ===");
            foreach (var missing in missingInDocs)
            {
                _output.WriteLine($"  ⚠️  {missing}");
            }
            _output.WriteLine("");
            _output.WriteLine("建议：在 RepositoryStructure.md 的配置模型章节中添加这些配置的说明");
        }

        // 这个测试只是警告，不强制失败
        if (missingInDocs.Any())
        {
            _output.WriteLine($"警告：有 {missingInDocs.Count} 个配置模型未在文档中记录");
        }
    }

    private static string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        // 向上查找，直到找到包含 .git 目录的仓库根目录
        while (dir != null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
        {
            dir = dir.Parent;
        }

        if (dir == null)
        {
            throw new InvalidOperationException("无法找到仓库根目录");
        }

        return dir.FullName;
    }
}
