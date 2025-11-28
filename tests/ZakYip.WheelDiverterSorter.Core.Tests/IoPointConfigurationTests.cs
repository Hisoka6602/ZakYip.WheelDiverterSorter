using System;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// IoPointConfiguration 单元测试
/// </summary>
public class IoPointConfigurationTests
{
    [Fact]
    public void Create_ShouldCreateValidConfiguration()
    {
        // Act
        var config = IoPointConfiguration.Create(
            "StartButton",
            0,
            IoType.Input,
            TriggerLevel.ActiveHigh);

        // Assert
        Assert.NotNull(config);
        Assert.Equal("StartButton", config.Name);
        Assert.Equal(0, config.ChannelNumber);
        Assert.Equal(IoType.Input, config.Type);
        Assert.Equal(TriggerLevel.ActiveHigh, config.TriggerLevel);
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void Validate_ShouldPass_ForValidConfiguration()
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            BoardId = "Card0",
            ChannelNumber = 100,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh,
            Description = "Test IO point",
            IsEnabled = true
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenNameIsEmpty()
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "",
            ChannelNumber = 0,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("名称", errorMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenChannelNumberIsNegative()
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = -1,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("0-1023", errorMessage);
    }

    [Fact]
    public void Validate_ShouldFail_WhenChannelNumberExceedsMaximum()
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = 1024,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("0-1023", errorMessage);
    }

    [Fact]
    public void Validate_ShouldPass_ForBoundaryChannelNumbers()
    {
        // Arrange
        var config0 = new IoPointConfiguration
        {
            Name = "IO0",
            ChannelNumber = 0,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        var config1023 = new IoPointConfiguration
        {
            Name = "IO1023",
            ChannelNumber = 1023,
            Type = IoType.Output,
            TriggerLevel = TriggerLevel.ActiveLow
        };

        // Act
        var (isValid0, _) = config0.Validate();
        var (isValid1023, _) = config1023.Validate();

        // Assert
        Assert.True(isValid0);
        Assert.True(isValid1023);
    }

    [Theory]
    [InlineData(IoType.Input)]
    [InlineData(IoType.Output)]
    public void Validate_ShouldPass_ForValidIoTypes(IoType ioType)
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = 50,
            Type = ioType,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var (isValid, _) = config.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Theory]
    [InlineData(TriggerLevel.ActiveHigh)]
    [InlineData(TriggerLevel.ActiveLow)]
    public void Validate_ShouldPass_ForValidTriggerLevels(TriggerLevel triggerLevel)
    {
        // Arrange
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = 50,
            Type = IoType.Input,
            TriggerLevel = triggerLevel
        };

        // Act
        var (isValid, _) = config.Validate();

        // Assert
        Assert.True(isValid);
    }

    [Fact]
    public void Configuration_ShouldSupportOptionalBoardId()
    {
        // Arrange
        var configWithBoard = new IoPointConfiguration
        {
            Name = "IO1",
            BoardId = "Card0",
            ChannelNumber = 1,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        var configWithoutBoard = new IoPointConfiguration
        {
            Name = "IO2",
            BoardId = null,
            ChannelNumber = 2,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var (isValid1, _) = configWithBoard.Validate();
        var (isValid2, _) = configWithoutBoard.Validate();

        // Assert
        Assert.True(isValid1);
        Assert.True(isValid2);
        Assert.Equal("Card0", configWithBoard.BoardId);
        Assert.Null(configWithoutBoard.BoardId);
    }

    [Fact]
    public void Configuration_ShouldSupportOptionalDescription()
    {
        // Arrange
        var configWithDesc = new IoPointConfiguration
        {
            Name = "StartButton",
            ChannelNumber = 0,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh,
            Description = "系统启动按钮"
        };

        var configWithoutDesc = new IoPointConfiguration
        {
            Name = "LED1",
            ChannelNumber = 10,
            Type = IoType.Output,
            TriggerLevel = TriggerLevel.ActiveHigh,
            Description = null
        };

        // Act
        var (isValid1, _) = configWithDesc.Validate();
        var (isValid2, _) = configWithoutDesc.Validate();

        // Assert
        Assert.True(isValid1);
        Assert.True(isValid2);
        Assert.Equal("系统启动按钮", configWithDesc.Description);
        Assert.Null(configWithoutDesc.Description);
    }

    [Fact]
    public void Configuration_ShouldDefaultToEnabled()
    {
        // Arrange & Act
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = 0,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Assert
        Assert.True(config.IsEnabled);
    }

    [Fact]
    public void Configuration_ShouldSupportDisabling()
    {
        // Arrange & Act
        var config = new IoPointConfiguration
        {
            Name = "TestIO",
            ChannelNumber = 0,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh,
            IsEnabled = false
        };

        // Assert
        Assert.False(config.IsEnabled);
    }

    [Fact]
    public void Record_ShouldSupportWithExpression()
    {
        // Arrange
        var original = new IoPointConfiguration
        {
            Name = "Original",
            ChannelNumber = 5,
            Type = IoType.Input,
            TriggerLevel = TriggerLevel.ActiveHigh
        };

        // Act
        var modified = original with { Name = "Modified", ChannelNumber = 10 };

        // Assert
        Assert.Equal("Modified", modified.Name);
        Assert.Equal(10, modified.ChannelNumber);
        Assert.Equal(IoType.Input, modified.Type); // Unchanged
        Assert.Equal(TriggerLevel.ActiveHigh, modified.TriggerLevel); // Unchanged
    }
}
