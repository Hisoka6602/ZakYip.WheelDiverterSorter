using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Bindings;namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class DefaultIoLinkageCoordinatorTests
{
    private readonly DefaultIoLinkageCoordinator _coordinator = new();

    [Fact]
    public void DetermineIoLinkagePoints_WhenDisabled_ShouldReturnEmpty()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = false,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        var result = _coordinator.DetermineIoLinkagePoints(SystemOperatingState.Running, options);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void DetermineIoLinkagePoints_WhenRunning_ShouldReturnRunningStateIos()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow },
                new() { BitNumber = 5, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        var result = _coordinator.DetermineIoLinkagePoints(SystemOperatingState.Running, options);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.BitNumber == 3 && p.Level == TriggerLevel.ActiveLow);
        Assert.Contains(result, p => p.BitNumber == 5 && p.Level == TriggerLevel.ActiveLow);
    }

    [Fact]
    public void DetermineIoLinkagePoints_WhenStopped_ShouldReturnStoppedStateIos()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
                new() { BitNumber = 5, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        var result = _coordinator.DetermineIoLinkagePoints(SystemOperatingState.Stopped, options);

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, p => p.BitNumber == 3 && p.Level == TriggerLevel.ActiveHigh);
        Assert.Contains(result, p => p.BitNumber == 5 && p.Level == TriggerLevel.ActiveHigh);
    }

    [Fact]
    public void DetermineIoLinkagePoints_WhenStandby_ShouldReturnStoppedStateIos()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        var result = _coordinator.DetermineIoLinkagePoints(SystemOperatingState.Standby, options);

        // Assert
        Assert.Single(result);
        Assert.Contains(result, p => p.BitNumber == 3 && p.Level == TriggerLevel.ActiveHigh);
    }

    [Fact]
    public void DetermineIoLinkagePoints_WhenStopping_ShouldReturnStoppedStateIos()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        var result = _coordinator.DetermineIoLinkagePoints(SystemOperatingState.Stopping, options);

        // Assert
        Assert.Single(result);
        Assert.Contains(result, p => p.BitNumber == 3 && p.Level == TriggerLevel.ActiveHigh);
    }

    [Fact]
    public void DetermineIoLinkagePoints_WhenOtherStates_ShouldReturnEmpty()
    {
        // Arrange
        var options = new IoLinkageOptions
        {
            Enabled = true,
            RunningStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new List<IoLinkagePoint>
            {
                new() { BitNumber = 3, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act & Assert - Test various states that should not trigger IO linkage
        Assert.Empty(_coordinator.DetermineIoLinkagePoints(SystemOperatingState.Initializing, options));
        Assert.Empty(_coordinator.DetermineIoLinkagePoints(SystemOperatingState.Paused, options));
        Assert.Empty(_coordinator.DetermineIoLinkagePoints(SystemOperatingState.Faulted, options));
        Assert.Empty(_coordinator.DetermineIoLinkagePoints(SystemOperatingState.EmergencyStopped, options));
        Assert.Empty(_coordinator.DetermineIoLinkagePoints(SystemOperatingState.WaitingUpstream, options));
    }

    [Fact]
    public void ShouldActivateIoLinkage_WhenRunning_ShouldReturnTrue()
    {
        // Act
        var result = _coordinator.ShouldActivateIoLinkage(SystemOperatingState.Running);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldActivateIoLinkage_WhenStopped_ShouldReturnTrue()
    {
        // Act
        var result = _coordinator.ShouldActivateIoLinkage(SystemOperatingState.Stopped);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldActivateIoLinkage_WhenStandby_ShouldReturnTrue()
    {
        // Act
        var result = _coordinator.ShouldActivateIoLinkage(SystemOperatingState.Standby);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldActivateIoLinkage_WhenStopping_ShouldReturnTrue()
    {
        // Act
        var result = _coordinator.ShouldActivateIoLinkage(SystemOperatingState.Stopping);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void ShouldActivateIoLinkage_WhenOtherStates_ShouldReturnFalse()
    {
        // Act & Assert
        Assert.False(_coordinator.ShouldActivateIoLinkage(SystemOperatingState.Initializing));
        Assert.False(_coordinator.ShouldActivateIoLinkage(SystemOperatingState.Paused));
        Assert.False(_coordinator.ShouldActivateIoLinkage(SystemOperatingState.Faulted));
        Assert.False(_coordinator.ShouldActivateIoLinkage(SystemOperatingState.EmergencyStopped));
        Assert.False(_coordinator.ShouldActivateIoLinkage(SystemOperatingState.WaitingUpstream));
    }
}
