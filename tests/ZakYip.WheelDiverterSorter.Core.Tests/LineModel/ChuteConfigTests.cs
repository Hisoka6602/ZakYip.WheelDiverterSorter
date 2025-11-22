using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 测试 ChuteConfig 格口配置功能
/// </summary>
public class ChuteConfigTests
{
    [Fact]
    public void ChuteConfig_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Main Chute 1",
            IsExceptionChute = false,
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            DropOffsetMm = 150.0,
            IsEnabled = true,
            Remarks = "Primary sorting chute"
        };

        // Assert
        Assert.NotNull(chute);
        Assert.Equal("CHUTE-001", chute.ChuteId);
        Assert.Equal("Main Chute 1", chute.ChuteName);
        Assert.False(chute.IsExceptionChute);
        Assert.Equal("WHEEL-1", chute.BoundNodeId);
        Assert.Equal("Left", chute.BoundDirection);
        Assert.Equal(150.0, chute.DropOffsetMm);
        Assert.True(chute.IsEnabled);
        Assert.Equal("Primary sorting chute", chute.Remarks);
    }

    [Fact]
    public void ChuteConfig_ExceptionChute_ConfiguresCorrectly()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-EXCEPTION",
            ChuteName = "Exception Chute",
            IsExceptionChute = true,
            BoundNodeId = "WHEEL-LAST",
            BoundDirection = "Straight",
            IsEnabled = true
        };

        // Assert
        Assert.True(chute.IsExceptionChute);
        Assert.Equal("CHUTE-EXCEPTION", chute.ChuteId);
        Assert.Equal("Exception Chute", chute.ChuteName);
    }

    [Fact]
    public void ChuteConfig_LeftDirection_ConfiguresCorrectly()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-L1",
            ChuteName = "Left Chute 1",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            IsExceptionChute = false
        };

        // Assert
        Assert.Equal("Left", chute.BoundDirection);
    }

    [Fact]
    public void ChuteConfig_RightDirection_ConfiguresCorrectly()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-R1",
            ChuteName = "Right Chute 1",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Right",
            IsExceptionChute = false
        };

        // Assert
        Assert.Equal("Right", chute.BoundDirection);
    }

    [Fact]
    public void ChuteConfig_StraightDirection_ConfiguresCorrectly()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-S1",
            ChuteName = "Straight Chute 1",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Straight",
            IsExceptionChute = false
        };

        // Assert
        Assert.Equal("Straight", chute.BoundDirection);
    }

    [Fact]
    public void ChuteConfig_WithDropOffset_StoresCorrectValue()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Chute with Offset",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            DropOffsetMm = 250.5
        };

        // Assert
        Assert.Equal(250.5, chute.DropOffsetMm);
    }

    [Fact]
    public void ChuteConfig_WithZeroDropOffset_AllowsZero()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "No Offset Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            DropOffsetMm = 0.0
        };

        // Assert
        Assert.Equal(0.0, chute.DropOffsetMm);
    }

    [Fact]
    public void ChuteConfig_DefaultDropOffset_IsZero()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Default Offset Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left"
            // Not specifying DropOffsetMm
        };

        // Assert
        Assert.Equal(0.0, chute.DropOffsetMm);
    }

    [Fact]
    public void ChuteConfig_DefaultIsEnabled_IsTrue()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Default Enabled Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left"
            // Not specifying IsEnabled
        };

        // Assert
        Assert.True(chute.IsEnabled);
    }

    [Fact]
    public void ChuteConfig_IsEnabled_CanBeSetToFalse()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-DISABLED",
            ChuteName = "Disabled Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            IsEnabled = false
        };

        // Assert
        Assert.False(chute.IsEnabled);
    }

    [Fact]
    public void ChuteConfig_NullRemarks_IsAllowed()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "No Remarks Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            Remarks = null
        };

        // Assert
        Assert.Null(chute.Remarks);
    }

    [Fact]
    public void ChuteConfig_EmptyRemarks_IsAllowed()
    {
        // Arrange & Act
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Empty Remarks Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            Remarks = ""
        };

        // Assert
        Assert.Equal("", chute.Remarks);
    }

    [Fact]
    public void ChuteConfig_RecordEquality_SameValues()
    {
        // Arrange
        var chute1 = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Test Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            IsExceptionChute = false
        };

        var chute2 = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Test Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            IsExceptionChute = false
        };

        // Act & Assert
        Assert.Equal(chute1, chute2);
    }

    [Fact]
    public void ChuteConfig_RecordInequality_DifferentIds()
    {
        // Arrange
        var chute1 = new ChuteConfig
        {
            ChuteId = "CHUTE-001",
            ChuteName = "Chute 1",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left"
        };

        var chute2 = new ChuteConfig
        {
            ChuteId = "CHUTE-002",
            ChuteName = "Chute 2",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Right"
        };

        // Act & Assert
        Assert.NotEqual(chute1, chute2);
    }

    [Fact]
    public void ChuteConfig_WithLargeDropOffset_StoresCorrectly()
    {
        // Arrange & Act - 1 meter drop offset
        var chute = new ChuteConfig
        {
            ChuteId = "CHUTE-LONG",
            ChuteName = "Long Drop Chute",
            BoundNodeId = "WHEEL-1",
            BoundDirection = "Left",
            DropOffsetMm = 1000.0
        };

        // Assert
        Assert.Equal(1000.0, chute.DropOffsetMm);
    }
}
