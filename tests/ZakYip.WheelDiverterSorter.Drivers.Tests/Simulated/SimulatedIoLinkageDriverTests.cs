using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.Simulated;

public class SimulatedIoLinkageDriverTests
{
    private readonly SimulatedIoLinkageDriver _driver;

    public SimulatedIoLinkageDriverTests()
    {
        _driver = new SimulatedIoLinkageDriver(NullLogger<SimulatedIoLinkageDriver>.Instance);
    }

    [Fact]
    public async Task SetIoPointAsync_ShouldSetIoState()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 3,
            Level = TriggerLevel.ActiveHigh
        };

        // Act
        await _driver.SetIoPointAsync(ioPoint);
        var state = await _driver.ReadIoPointAsync(3);

        // Assert
        Assert.True(state);
    }

    [Fact]
    public async Task SetIoPointAsync_WithActiveLow_ShouldSetToFalse()
    {
        // Arrange
        var ioPoint = new IoLinkagePoint
        {
            BitNumber = 5,
            Level = TriggerLevel.ActiveLow
        };

        // Act
        await _driver.SetIoPointAsync(ioPoint);
        var state = await _driver.ReadIoPointAsync(5);

        // Assert
        Assert.False(state);
    }

    [Fact]
    public async Task SetIoPointsAsync_ShouldSetMultipleIoStates()
    {
        // Arrange
        var ioPoints = new List<IoLinkagePoint>
        {
            new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
            new() { BitNumber = 5, Level = TriggerLevel.ActiveLow },
            new() { BitNumber = 7, Level = TriggerLevel.ActiveHigh }
        };

        // Act
        await _driver.SetIoPointsAsync(ioPoints);

        // Assert
        Assert.True(await _driver.ReadIoPointAsync(3));
        Assert.False(await _driver.ReadIoPointAsync(5));
        Assert.True(await _driver.ReadIoPointAsync(7));
    }

    [Fact]
    public async Task ReadIoPointAsync_WhenNotSet_ShouldReturnFalse()
    {
        // Act
        var state = await _driver.ReadIoPointAsync(100);

        // Assert
        Assert.False(state);
    }

    [Fact]
    public async Task ResetAllIoPointsAsync_ShouldClearAllStates()
    {
        // Arrange
        var ioPoints = new List<IoLinkagePoint>
        {
            new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
            new() { BitNumber = 5, Level = TriggerLevel.ActiveHigh }
        };
        await _driver.SetIoPointsAsync(ioPoints);

        // Act
        await _driver.ResetAllIoPointsAsync();

        // Assert
        Assert.False(await _driver.ReadIoPointAsync(3));
        Assert.False(await _driver.ReadIoPointAsync(5));
    }

    [Fact]
    public async Task GetAllIoStates_ShouldReturnAllSetStates()
    {
        // Arrange
        var ioPoints = new List<IoLinkagePoint>
        {
            new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
            new() { BitNumber = 5, Level = TriggerLevel.ActiveLow },
            new() { BitNumber = 7, Level = TriggerLevel.ActiveHigh }
        };
        await _driver.SetIoPointsAsync(ioPoints);

        // Act
        var allStates = _driver.GetAllIoStates();

        // Assert
        Assert.Equal(3, allStates.Count);
        Assert.True(allStates[3]);
        Assert.False(allStates[5]);
        Assert.True(allStates[7]);
    }

    [Fact]
    public async Task SetIoPointAsync_ShouldOverwritePreviousState()
    {
        // Arrange
        var ioPoint1 = new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh };
        var ioPoint2 = new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveLow };

        // Act
        await _driver.SetIoPointAsync(ioPoint1);
        Assert.True(await _driver.ReadIoPointAsync(3));

        await _driver.SetIoPointAsync(ioPoint2);
        Assert.False(await _driver.ReadIoPointAsync(3));
    }

    [Fact]
    public async Task SetIoPointsAsync_WithEmptyList_ShouldNotThrow()
    {
        // Arrange
        var ioPoints = new List<IoLinkagePoint>();

        // Act & Assert
        await _driver.SetIoPointsAsync(ioPoints); // Should not throw
    }
}
