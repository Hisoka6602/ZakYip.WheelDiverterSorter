using ZakYip.WheelDiverterSorter.Core.Enums.System;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime;
using ZakYip.WheelDiverterSorter.Host.Services.Application;
using ZakYip.WheelDiverterSorter.Host.Services.RuntimeProfiles;
using Moq;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 仿真模式提供者测试
/// Tests for SimulationModeProvider
/// </summary>
public class SimulationModeProviderTests
{
    [Fact]
    public void IsSimulationMode_WithProductionProfile_ReturnsFalse()
    {
        // Arrange
        var profile = new ProductionRuntimeProfile();
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
        var profile = new SimulationRuntimeProfile();
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
        var profile = new PerformanceTestRuntimeProfile();
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
