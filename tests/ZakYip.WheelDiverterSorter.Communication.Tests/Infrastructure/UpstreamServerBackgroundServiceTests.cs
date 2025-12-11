using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Infrastructure;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.Infrastructure;

/// <summary>
/// Tests for UpstreamServerBackgroundService and service registration
/// </summary>
public class UpstreamServerBackgroundServiceTests
{
    [Fact]
    public void AddUpstreamConnectionManagement_WithClientMode_RegistersClientBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000",
                ["RuleEngineConnection:EnableInfiniteRetry"] = "true",
                ["RuleEngineConnection:MaxBackoffMs"] = "2000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_WithServerMode_RegistersServerBackgroundService()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersBothBackgroundServices()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Both services are registered
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_RegistersBothServices_RegardlessOfMode()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - Both services are registered (they check mode at runtime)
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersConnectionManager()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var connectionManager = serviceProvider.GetService<Abstractions.IUpstreamConnectionManager>();
        Assert.NotNull(connectionManager);
    }

    [Fact]
    public void AddUpstreamConnectionManagement_AlwaysRegistersServerFactory()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert
        var serverFactory = serviceProvider.GetService<RuleEngineServerFactory>();
        Assert.NotNull(serverFactory);
    }

    /// <summary>
    /// PR-HOTRELOAD: 验证 UpstreamConnectionBackgroundService 可以作为 Singleton 直接注入
    /// 保持一致性 - Client 模式的后台服务也使用 Singleton + HostedService 模式
    /// </summary>
    [Fact]
    public void AddUpstreamConnectionManagement_RegistersConnectionBackgroundServiceAsSingleton_ForConsistency()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Client",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Client,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - 验证可以直接获取 UpstreamConnectionBackgroundService 实例
        // Verify that UpstreamConnectionBackgroundService can be directly resolved as singleton
        var connectionService = serviceProvider.GetService<UpstreamConnectionBackgroundService>();
        Assert.NotNull(connectionService);

        // 验证它也被注册为 IHostedService
        // Verify it's also registered as IHostedService
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamConnectionBackgroundService));

        // 验证两个实例是同一个（Singleton 保证）
        // Verify both are the same instance (Singleton guarantee)
        var hostedInstance = hostedServices.FirstOrDefault(s => s.GetType() == typeof(UpstreamConnectionBackgroundService));
        Assert.Same(connectionService, hostedInstance);
    }

    /// <summary>
    /// PR-HOTRELOAD: 验证 UpstreamServerBackgroundService 可以作为 Singleton 直接注入
    /// 这是修复 TCP Server 模式热更新的关键 - CommunicationConfigService 需要注入此服务
    /// </summary>
    [Fact]
    public void AddUpstreamConnectionManagement_RegistersServerBackgroundServiceAsSingleton_ForHotReload()
    {
        // Arrange
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RuleEngineConnection:Mode"] = "Tcp",
                ["RuleEngineConnection:ConnectionMode"] = "Server",
                ["RuleEngineConnection:TcpServer"] = "localhost:9000"
            })
            .Build();

        // Mock the repository to simulate production environment
        var mockRepository = new Mock<ICommunicationConfigurationRepository>();
        mockRepository.Setup(r => r.Get()).Returns(new CommunicationConfiguration
        {
            Mode = CommunicationMode.Tcp,
            ConnectionMode = ConnectionMode.Server,
            TcpServer = "localhost:9000"
        });
        services.AddSingleton(mockRepository.Object);

        // Add required dependencies
        services.AddLogging();
        services.AddInfrastructureServices();
        services.AddRuleEngineCommunication(configuration);

        // Act
        services.AddUpstreamConnectionManagement(configuration);
        var serviceProvider = services.BuildServiceProvider();

        // Assert - 验证可以直接获取 UpstreamServerBackgroundService 实例
        // Verify that UpstreamServerBackgroundService can be directly resolved as singleton
        var serverService = serviceProvider.GetService<UpstreamServerBackgroundService>();
        Assert.NotNull(serverService);

        // 验证它也被注册为 IHostedService
        // Verify it's also registered as IHostedService
        var hostedServices = serviceProvider.GetServices<IHostedService>().ToList();
        Assert.Contains(hostedServices, s => s.GetType() == typeof(UpstreamServerBackgroundService));

        // 验证两个实例是同一个（Singleton 保证）
        // Verify both are the same instance (Singleton guarantee)
        var hostedInstance = hostedServices.FirstOrDefault(s => s.GetType() == typeof(UpstreamServerBackgroundService));
        Assert.Same(serverService, hostedInstance);
    }
}
