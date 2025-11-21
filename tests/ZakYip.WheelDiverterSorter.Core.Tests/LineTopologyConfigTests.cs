using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class LineTopologyConfigTests
{
    [Fact]
    public void GetExceptionChute_WithExceptionChute_ReturnsCorrectChute()
    {
        // Arrange
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_A",
                NodeName = "Node A",
                PositionIndex = 0,
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Normal Chute",
                BoundNodeId = "NODE_A",
                BoundDirection = "Left",
                IsExceptionChute = false
            },
            new ChuteConfig
            {
                ChuteId = "CHUTE_EX",
                ChuteName = "Exception Chute",
                BoundNodeId = "NODE_A",
                BoundDirection = "Straight",
                IsExceptionChute = true
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = chutes
        };

        // Act
        var exceptionChute = topology.GetExceptionChute();

        // Assert
        Assert.NotNull(exceptionChute);
        Assert.Equal("CHUTE_EX", exceptionChute.ChuteId);
        Assert.True(exceptionChute.IsExceptionChute);
    }

    [Fact]
    public void GetExceptionChute_WithoutExceptionChute_ReturnsNull()
    {
        // Arrange
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_A",
                NodeName = "Node A",
                PositionIndex = 0,
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Normal Chute",
                BoundNodeId = "NODE_A",
                BoundDirection = "Left",
                IsExceptionChute = false
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = chutes
        };

        // Act
        var exceptionChute = topology.GetExceptionChute();

        // Assert
        Assert.Null(exceptionChute);
    }

    [Fact]
    public void FindNodeById_WithExistingNode_ReturnsNode()
    {
        // Arrange
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_A",
                NodeName = "Node A",
                PositionIndex = 0,
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left }
            },
            new WheelNodeConfig
            {
                NodeId = "NODE_B",
                NodeName = "Node B",
                PositionIndex = 1,
                HasRightChute = true,
                RightChuteIds = new[] { "CHUTE_2" },
                SupportedSides = new[] { DiverterSide.Right }
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = new List<ChuteConfig>()
        };

        // Act
        var node = topology.FindNodeById("NODE_B");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("NODE_B", node.NodeId);
        Assert.Equal(1, node.PositionIndex);
    }

    [Fact]
    public void FindNodeById_WithNonExistentNode_ReturnsNull()
    {
        // Arrange
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_A",
                NodeName = "Node A",
                PositionIndex = 0,
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left }
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = new List<ChuteConfig>()
        };

        // Act
        var node = topology.FindNodeById("NON_EXISTENT");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void FindChuteById_WithExistingChute_ReturnsChute()
    {
        // Arrange
        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Chute 1",
                BoundNodeId = "NODE_A",
                BoundDirection = "Left",
                IsExceptionChute = false
            },
            new ChuteConfig
            {
                ChuteId = "CHUTE_2",
                ChuteName = "Chute 2",
                BoundNodeId = "NODE_B",
                BoundDirection = "Right",
                IsExceptionChute = false
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = new List<WheelNodeConfig>(),
            Chutes = chutes
        };

        // Act
        var chute = topology.FindChuteById("CHUTE_2");

        // Assert
        Assert.NotNull(chute);
        Assert.Equal("CHUTE_2", chute.ChuteId);
        Assert.Equal("NODE_B", chute.BoundNodeId);
    }

    [Fact]
    public void FindChuteById_WithNonExistentChute_ReturnsNull()
    {
        // Arrange
        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Chute 1",
                BoundNodeId = "NODE_A",
                BoundDirection = "Left",
                IsExceptionChute = false
            }
        };

        var topology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = new List<WheelNodeConfig>(),
            Chutes = chutes
        };

        // Act
        var chute = topology.FindChuteById("NON_EXISTENT");

        // Assert
        Assert.Null(chute);
    }
}
