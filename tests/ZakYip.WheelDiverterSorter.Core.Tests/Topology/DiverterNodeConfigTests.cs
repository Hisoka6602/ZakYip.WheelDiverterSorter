using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;

#pragma warning disable CS0618 // 遗留拓扑类型正在逐步迁移中
namespace ZakYip.WheelDiverterSorter.Core.Tests.Topology;

public class DiverterNodeConfigTests
{
    [Fact]
    public void DiverterNodeConfig_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var nodeConfig = new DiverterNodeConfig
        {
            NodeId = "D1",
            NodeName = "Diverter 1",
            PositionIndex = 0,
            SupportedDirections = new[] { DiverterSide.Straight, DiverterSide.Left },
            LeftChuteIds = new[] { "CHUTE_A", "CHUTE_B" },
            RightChuteIds = Array.Empty<string>(),
            SensorLogicalName = "D1_Sensor",
            LeftActuatorLogicalName = "D1_Left",
            RightActuatorLogicalName = "D1_Right"
        };

        // Assert
        Assert.NotNull(nodeConfig);
        Assert.Equal("D1", nodeConfig.NodeId);
        Assert.Equal("Diverter 1", nodeConfig.NodeName);
        Assert.Equal(0, nodeConfig.PositionIndex);
        Assert.Equal(2, nodeConfig.SupportedDirections.Count);
        Assert.Equal(2, nodeConfig.LeftChuteIds.Count);
        Assert.Empty(nodeConfig.RightChuteIds);
    }

    [Fact]
    public void SupportsDirection_WithSupportedDirection_ShouldReturnTrue()
    {
        // Arrange
        var nodeConfig = new DiverterNodeConfig
        {
            NodeId = "D1",
            NodeName = "Diverter 1",
            PositionIndex = 0,
            SupportedDirections = new[] { DiverterSide.Straight, DiverterSide.Left }
        };

        // Act & Assert
        Assert.True(nodeConfig.SupportsDirection(DiverterSide.Straight));
        Assert.True(nodeConfig.SupportsDirection(DiverterSide.Left));
        Assert.False(nodeConfig.SupportsDirection(DiverterSide.Right));
    }

    [Fact]
    public void HasLeftChute_WithLeftChutes_ShouldReturnTrue()
    {
        // Arrange
        var nodeConfig = new DiverterNodeConfig
        {
            NodeId = "D1",
            NodeName = "Diverter 1",
            PositionIndex = 0,
            LeftChuteIds = new[] { "CHUTE_A" }
        };

        // Act & Assert
        Assert.True(nodeConfig.HasLeftChute);
    }

    [Fact]
    public void HasRightChute_WithoutRightChutes_ShouldReturnFalse()
    {
        // Arrange
        var nodeConfig = new DiverterNodeConfig
        {
            NodeId = "D1",
            NodeName = "Diverter 1",
            PositionIndex = 0,
            RightChuteIds = Array.Empty<string>()
        };

        // Act & Assert
        Assert.False(nodeConfig.HasRightChute);
    }

    [Fact]
    public void DiverterNodeConfig_WithDefaultSupportedDirections_ShouldSupportAllDirections()
    {
        // Arrange & Act
        var nodeConfig = new DiverterNodeConfig
        {
            NodeId = "D1",
            NodeName = "Diverter 1",
            PositionIndex = 0
            // Using default SupportedDirections
        };

        // Assert
        Assert.True(nodeConfig.SupportsDirection(DiverterSide.Straight));
        Assert.True(nodeConfig.SupportsDirection(DiverterSide.Left));
        Assert.True(nodeConfig.SupportsDirection(DiverterSide.Right));
    }
}
