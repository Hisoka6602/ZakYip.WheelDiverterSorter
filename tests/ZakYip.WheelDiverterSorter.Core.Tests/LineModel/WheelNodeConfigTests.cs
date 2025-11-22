using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 测试 WheelNodeConfig 摆轮节点配置功能
/// </summary>
public class WheelNodeConfigTests
{
    [Fact]
    public void WheelNodeConfig_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First Wheel",
            PositionIndex = 0,
            HasLeftChute = true,
            HasRightChute = false,
            LeftChuteIds = new[] { "CHUTE-L1", "CHUTE-L2" },
            RightChuteIds = Array.Empty<string>(),
            SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left },
            Remarks = "First sorting wheel"
        };

        // Assert
        Assert.NotNull(node);
        Assert.Equal("WHEEL-1", node.NodeId);
        Assert.Equal("First Wheel", node.NodeName);
        Assert.Equal(0, node.PositionIndex);
        Assert.True(node.HasLeftChute);
        Assert.False(node.HasRightChute);
        Assert.Equal(2, node.LeftChuteIds.Count);
        Assert.Contains("CHUTE-L1", node.LeftChuteIds);
        Assert.Contains("CHUTE-L2", node.LeftChuteIds);
        Assert.Empty(node.RightChuteIds);
        Assert.Equal(2, node.SupportedSides.Count);
        Assert.Contains(DiverterSide.Straight, node.SupportedSides);
        Assert.Contains(DiverterSide.Left, node.SupportedSides);
        Assert.Equal("First sorting wheel", node.Remarks);
    }

    [Fact]
    public void WheelNodeConfig_WithDefaultSupportedSides_HasAllDirections()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First Wheel",
            PositionIndex = 0
        };

        // Assert
        Assert.Equal(3, node.SupportedSides.Count);
        Assert.Contains(DiverterSide.Straight, node.SupportedSides);
        Assert.Contains(DiverterSide.Left, node.SupportedSides);
        Assert.Contains(DiverterSide.Right, node.SupportedSides);
    }

    [Fact]
    public void WheelNodeConfig_WithLeftChuteOnly_ConfiguresCorrectly()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-2",
            NodeName = "Second Wheel",
            PositionIndex = 1,
            HasLeftChute = true,
            HasRightChute = false,
            LeftChuteIds = new[] { "CHUTE-5" },
            SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left }
        };

        // Assert
        Assert.True(node.HasLeftChute);
        Assert.False(node.HasRightChute);
        Assert.Single(node.LeftChuteIds);
        Assert.Equal("CHUTE-5", node.LeftChuteIds[0]);
        Assert.Empty(node.RightChuteIds);
    }

    [Fact]
    public void WheelNodeConfig_WithRightChuteOnly_ConfiguresCorrectly()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-3",
            NodeName = "Third Wheel",
            PositionIndex = 2,
            HasLeftChute = false,
            HasRightChute = true,
            RightChuteIds = new[] { "CHUTE-10", "CHUTE-11" },
            SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Right }
        };

        // Assert
        Assert.False(node.HasLeftChute);
        Assert.True(node.HasRightChute);
        Assert.Empty(node.LeftChuteIds);
        Assert.Equal(2, node.RightChuteIds.Count);
        Assert.Contains("CHUTE-10", node.RightChuteIds);
        Assert.Contains("CHUTE-11", node.RightChuteIds);
    }

    [Fact]
    public void WheelNodeConfig_WithBothChutes_ConfiguresCorrectly()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-4",
            NodeName = "Fourth Wheel",
            PositionIndex = 3,
            HasLeftChute = true,
            HasRightChute = true,
            LeftChuteIds = new[] { "CHUTE-L3" },
            RightChuteIds = new[] { "CHUTE-R3" },
            SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left, DiverterSide.Right }
        };

        // Assert
        Assert.True(node.HasLeftChute);
        Assert.True(node.HasRightChute);
        Assert.Single(node.LeftChuteIds);
        Assert.Single(node.RightChuteIds);
        Assert.Equal(3, node.SupportedSides.Count);
    }

    [Fact]
    public void WheelNodeConfig_WithNoChutes_PassThroughOnly()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-PASS",
            NodeName = "Pass Through Wheel",
            PositionIndex = 0,
            HasLeftChute = false,
            HasRightChute = false,
            SupportedSides = new[] { DiverterSide.Straight }
        };

        // Assert
        Assert.False(node.HasLeftChute);
        Assert.False(node.HasRightChute);
        Assert.Empty(node.LeftChuteIds);
        Assert.Empty(node.RightChuteIds);
        Assert.Single(node.SupportedSides);
        Assert.Contains(DiverterSide.Straight, node.SupportedSides);
    }

    [Fact]
    public void WheelNodeConfig_PositionIndex_OrdersNodesCorrectly()
    {
        // Arrange
        var node1 = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First",
            PositionIndex = 0
        };

        var node2 = new WheelNodeConfig
        {
            NodeId = "WHEEL-2",
            NodeName = "Second",
            PositionIndex = 1
        };

        var node3 = new WheelNodeConfig
        {
            NodeId = "WHEEL-3",
            NodeName = "Third",
            PositionIndex = 2
        };

        var nodes = new List<WheelNodeConfig> { node3, node1, node2 };

        // Act
        var sortedNodes = nodes.OrderBy(n => n.PositionIndex).ToList();

        // Assert
        Assert.Equal("WHEEL-1", sortedNodes[0].NodeId);
        Assert.Equal("WHEEL-2", sortedNodes[1].NodeId);
        Assert.Equal("WHEEL-3", sortedNodes[2].NodeId);
    }

    [Fact]
    public void WheelNodeConfig_RecordInequality_DifferentNodeIds()
    {
        // Arrange
        var node1 = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First Wheel",
            PositionIndex = 0
        };

        var node2 = new WheelNodeConfig
        {
            NodeId = "WHEEL-2",
            NodeName = "Second Wheel",
            PositionIndex = 1
        };

        // Act & Assert
        Assert.NotEqual(node1, node2);
    }

    [Fact]
    public void WheelNodeConfig_WithMultipleLeftChutes_StoresAllCorrectly()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-MULTI",
            NodeName = "Multi Chute Wheel",
            PositionIndex = 0,
            HasLeftChute = true,
            LeftChuteIds = new[] { "CHUTE-1", "CHUTE-2", "CHUTE-3", "CHUTE-4", "CHUTE-5" }
        };

        // Assert
        Assert.Equal(5, node.LeftChuteIds.Count);
        for (int i = 1; i <= 5; i++)
        {
            Assert.Contains($"CHUTE-{i}", node.LeftChuteIds);
        }
    }

    [Fact]
    public void WheelNodeConfig_NullRemarks_IsAllowed()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First Wheel",
            PositionIndex = 0,
            Remarks = null
        };

        // Assert
        Assert.Null(node.Remarks);
    }

    [Fact]
    public void WheelNodeConfig_EmptyChuteIds_DefaultsToEmpty()
    {
        // Arrange & Act
        var node = new WheelNodeConfig
        {
            NodeId = "WHEEL-1",
            NodeName = "First Wheel",
            PositionIndex = 0
            // Not specifying LeftChuteIds or RightChuteIds
        };

        // Assert
        Assert.NotNull(node.LeftChuteIds);
        Assert.NotNull(node.RightChuteIds);
        Assert.Empty(node.LeftChuteIds);
        Assert.Empty(node.RightChuteIds);
    }
}
