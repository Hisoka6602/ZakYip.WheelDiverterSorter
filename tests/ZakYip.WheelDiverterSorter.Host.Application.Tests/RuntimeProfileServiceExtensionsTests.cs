using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 运行时配置文件服务扩展测试
/// Tests for RuntimeProfileServiceExtensions
/// </summary>
public class RuntimeProfileServiceExtensionsTests
{
    [Fact]
    public void AddRuntimeProfile_WithProductionMode_RegistersProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Production" }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<ProductionRuntimeProfile>(profile);
        Assert.Equal(RuntimeMode.Production, profile.Mode);
        Assert.True(profile.UseHardwareDriver);
        Assert.False(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddRuntimeProfile_WithSimulationMode_RegistersSimulationProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Simulation" }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<SimulationRuntimeProfile>(profile);
        Assert.Equal(RuntimeMode.Simulation, profile.Mode);
        Assert.False(profile.UseHardwareDriver);
        Assert.True(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddRuntimeProfile_WithPerformanceTestMode_RegistersPerformanceTestProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "PerformanceTest" }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<PerformanceTestRuntimeProfile>(profile);
        Assert.Equal(RuntimeMode.PerformanceTest, profile.Mode);
        Assert.False(profile.UseHardwareDriver);
        Assert.False(profile.IsSimulationMode);
        Assert.True(profile.IsPerformanceTestMode);
    }

    [Fact]
    public void AddRuntimeProfile_WithNoConfiguration_DefaultsToProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<ProductionRuntimeProfile>(profile);
        Assert.Equal(RuntimeMode.Production, profile.Mode);
    }

    [Theory]
    [InlineData("production")]
    [InlineData("PRODUCTION")]
    [InlineData("Production")]
    public void AddRuntimeProfile_WithCaseInsensitiveProductionMode_RegistersProductionProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<ProductionRuntimeProfile>(profile);
    }

    [Theory]
    [InlineData("performance_test")]
    [InlineData("performance-test")]
    [InlineData("PerformanceTest")]
    [InlineData("performancetest")]
    public void AddRuntimeProfile_WithVariousPerformanceTestFormats_RegistersPerformanceTestProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<PerformanceTestRuntimeProfile>(profile);
    }

    [Fact]
    public void AddRuntimeProfile_WithUnknownMode_DefaultsToProductionProfile()
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "UnknownMode" }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert
        Assert.IsType<ProductionRuntimeProfile>(profile);
    }

    [Fact]
    public void ProductionRuntimeProfile_HasExpectedSettings()
    {
        // Arrange
        var profile = new ProductionRuntimeProfile();

        // Assert
        Assert.Equal(RuntimeMode.Production, profile.Mode);
        Assert.True(profile.UseHardwareDriver);
        Assert.False(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
        Assert.True(profile.EnableIoOperations);
        Assert.True(profile.EnableUpstreamCommunication);
        Assert.True(profile.EnableHealthCheckTasks);
        Assert.True(profile.EnablePerformanceMonitoring);
        Assert.Equal("生产模式 - 使用真实硬件驱动，连接实际上游系统", profile.GetModeDescription());
    }

    [Fact]
    public void SimulationRuntimeProfile_HasExpectedSettings()
    {
        // Arrange
        var profile = new SimulationRuntimeProfile();

        // Assert
        Assert.Equal(RuntimeMode.Simulation, profile.Mode);
        Assert.False(profile.UseHardwareDriver);
        Assert.True(profile.IsSimulationMode);
        Assert.False(profile.IsPerformanceTestMode);
        Assert.True(profile.EnableIoOperations);
        Assert.True(profile.EnableUpstreamCommunication);
        Assert.True(profile.EnableHealthCheckTasks);
        Assert.True(profile.EnablePerformanceMonitoring);
        Assert.Equal("仿真模式 - 使用模拟驱动器，虚拟传感器和条码源", profile.GetModeDescription());
    }

    [Fact]
    public void PerformanceTestRuntimeProfile_HasExpectedSettings()
    {
        // Arrange
        var profile = new PerformanceTestRuntimeProfile();

        // Assert
        Assert.Equal(RuntimeMode.PerformanceTest, profile.Mode);
        Assert.False(profile.UseHardwareDriver);
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
    public void AddRuntimeProfile_WithNumericString_DefaultsToProductionProfile(string modeValue)
    {
        // Arrange
        var services = new ServiceCollection();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", modeValue }
            })
            .Build();

        // Act
        services.AddRuntimeProfile(config);
        var provider = services.BuildServiceProvider();
        var profile = provider.GetRequiredService<IRuntimeProfile>();

        // Assert - numeric strings should not be accepted, defaults to Production
        Assert.IsType<ProductionRuntimeProfile>(profile);
    }
}
