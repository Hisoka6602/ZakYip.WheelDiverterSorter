using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Application.Services;
using ZakYip.WheelDiverterSorter.Application.Extensions;
using Moq;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 仿真模式提供者测试
/// Tests for SimulationModeProvider
/// </summary>
/// <remarks>
/// PR-H1: 测试已更新以使用接口驱动的方式创建 IRuntimeProfile，
/// 而不是直接实例化具体的 Runtime Profile 类型（现已移至 Application 层并为 private）。
/// </remarks>
public class SimulationModeProviderTests
{
    /// <summary>
    /// 创建测试用的 IRuntimeProfile
    /// </summary>
    private static IRuntimeProfile CreateProductionProfile()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Production" }
            })
            .Build();
        services.AddWheelDiverterSorter(config);
        return services.BuildServiceProvider().GetRequiredService<IRuntimeProfile>();
    }

    private static IRuntimeProfile CreateSimulationProfile()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "Simulation" }
            })
            .Build();
        services.AddWheelDiverterSorter(config);
        return services.BuildServiceProvider().GetRequiredService<IRuntimeProfile>();
    }

    private static IRuntimeProfile CreatePerformanceTestProfile()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "Runtime:Mode", "PerformanceTest" }
            })
            .Build();
        services.AddWheelDiverterSorter(config);
        return services.BuildServiceProvider().GetRequiredService<IRuntimeProfile>();
    }

    [Fact]
    public void IsSimulationMode_WithProductionProfile_ReturnsFalse()
    {
        // Arrange
        var profile = CreateProductionProfile();
        var provider = new SimulationModeProvider(profile);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSimulationMode_WithSimulationProfile_ReturnsTrue()
    {
        // Arrange
        var profile = CreateSimulationProfile();
        var provider = new SimulationModeProvider(profile);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSimulationMode_WithPerformanceTestProfile_ReturnsFalse()
    {
        // Arrange
        var profile = CreatePerformanceTestProfile();
        var provider = new SimulationModeProvider(profile);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSimulationMode_WithNullProfile_ReturnsFalse()
    {
        // Arrange
        var provider = new SimulationModeProvider(null);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsSimulationMode_WithMockedSimulationProfile_ReturnsTrue()
    {
        // Arrange
        var mockProfile = new Mock<IRuntimeProfile>();
        mockProfile.Setup(p => p.IsSimulationMode).Returns(true);
        var provider = new SimulationModeProvider(mockProfile.Object);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.True(result);
        mockProfile.VerifyGet(p => p.IsSimulationMode, Times.Once);
    }

    [Fact]
    public void IsSimulationMode_WithMockedProductionProfile_ReturnsFalse()
    {
        // Arrange
        var mockProfile = new Mock<IRuntimeProfile>();
        mockProfile.Setup(p => p.IsSimulationMode).Returns(false);
        var provider = new SimulationModeProvider(mockProfile.Object);

        // Act
        var result = provider.IsSimulationMode();

        // Assert
        Assert.False(result);
        mockProfile.VerifyGet(p => p.IsSimulationMode, Times.Once);
    }
}
