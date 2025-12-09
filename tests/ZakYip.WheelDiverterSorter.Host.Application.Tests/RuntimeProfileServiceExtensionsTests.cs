using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Application.Extensions;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 运行时配置文件服务扩展测试
/// Tests for RuntimeProfileServiceExtensions (now in Application layer)
/// </summary>
/// <remarks>
/// PR-H1: 测试已更新以使用 Application 层的 AddWheelDiverterSorter 方法，
/// 该方法内部使用 file-scoped private Runtime Profile 类型。
/// 测试现在通过接口验证行为，而不是验证具体类型。
/// </remarks>
public class RuntimeProfileServiceExtensionsTests
{
    [Fact]
    public void AddWheelDiverterSorter_WithProductionMode_RegistersProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Production" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.Production, profile.Mode);
        // Production mode: NOT in simulation mode (uses real hardware by default)
        Assert.False(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddWheelDiverterSorter_WithSimulationMode_RegistersSimulationProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Simulation" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.Simulation, profile.Mode);
        // Simulation mode: IS in simulation mode (uses mock hardware)
        Assert.True(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddWheelDiverterSorter_WithPerformanceTestMode_RegistersPerformanceTestProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "PerformanceTest" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.PerformanceTest, profile.Mode);
        // PerformanceTest mode: NOT in simulation mode (different from Simulation mode)
        Assert.False(profile.IsSimulationMode);
        Assert.True(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddWheelDiverterSorter_WithNoConfiguration_DefaultsToProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.Production, profile.Mode);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    [InlineData("Production")]
    public void AddWheelDiverterSorter_WithCaseInsensitiveProductionMode_RegistersProductionProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.Production, profile.Mode);
    }

    [Theory]
    [InlineData("performance_test")]
    [InlineData("performance-test")]
    [InlineData("PerformanceTest")]
    [InlineData("performancetest")]
    public void AddWheelDiverterSorter_WithVariousPerformanceTestFormats_RegistersPerformanceTestProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.PerformanceTest, profile.Mode);
    }

    [Fact]
    public void AddWheelDiverterSorter_WithUnknownMode_DefaultsToProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "UnknownMode" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - verify behavior through interface, not concrete type
        Assert.Equal(RuntimeMode.Production, profile.Mode);
    }

    [Fact]
    public void ProductionProfile_HasExpectedSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Production" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.Equal(RuntimeMode.Production, profile.Mode);
        // Production mode: NOT in simulation mode (uses real hardware by default)
        Assert.False(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
        Assert.True(profile.EnableIoOperations);
        Assert.True(profile.EnableUpstreamCommunication);
        Assert.True(profile.EnableHealthCheckTasks);
        Assert.True(profile.EnablePerformanceMonitoring);
        Assert.Equal("生产模式 - 使用真实硬件驱动，连接实际上游系统", profile.GetModeDescription());
    }

    [Fact]
    public void SimulationProfile_HasExpectedSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Simulation" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.Equal(RuntimeMode.Simulation, profile.Mode);
        // Simulation mode: IS in simulation mode (uses mock hardware)
        Assert.True(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
        Assert.True(profile.EnableIoOperations);
        Assert.True(profile.EnableUpstreamCommunication);
        Assert.True(profile.EnableHealthCheckTasks);
        Assert.True(profile.EnablePerformanceMonitoring);
        Assert.Equal("仿真模式 - 使用模拟驱动器，虚拟传感器和条码源", profile.GetModeDescription());
    }

    [Fact]
    public void PerformanceTestProfile_HasExpectedSettings()
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "PerformanceTest" }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.Equal(RuntimeMode.PerformanceTest, profile.Mode);
        // PerformanceTest mode: NOT in simulation mode (different from Simulation mode)
        Assert.False(profile.IsSimulationMode);
        Assert.True(profile.IsPerformanceTestMode);
        Assert.False(profile.EnableIoOperations);
        Assert.False(profile.EnableUpstreamCommunication);
        Assert.False(profile.EnableHealthCheckTasks);
        Assert.True(profile.EnablePerformanceMonitoring);
        Assert.Equal("性能测试模式 - 跳过实际 IO，专注于路径/算法性能测试", profile.GetModeDescription());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("99")]
    public void AddWheelDiverterSorter_WithNumericString_DefaultsToProductionProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddWheelDiverterSorter(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - numeric strings should not be accepted, defaults to Production
        Assert.Equal(RuntimeMode.Production, profile.Mode);
    }
}
