using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology.Legacy;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

#pragma warning disable CS0618 // 遗留拓扑类型正在逐步迁移中
namespace ZakYip.WheelDiverterSorter.Core.Tests.Topology;

public class LineTopologyTests
{
    [Fact]
    public void LineTopology_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var topology = new LineTopology
        {
            TopologyId = "TOPO_001",
            TopologyName = "Test Line",
            Description = "Test topology for unit testing",
            DiverterNodes = new List<DiverterNodeConfig>
            {
                new()
                {
                    NodeId = "D1",
                    NodeName = "Diverter 1",
                    PositionIndex = 0,
                    SupportedDirections = new[] { DiverterSide.Straight, DiverterSide.Left, DiverterSide.Right }
                }
            },
            Chutes = new List<ChuteConfig>
            {
                new()
                {
                    ChuteId = "CHUTE_A",
                    ChuteName = "Chute A",
                    BoundNodeId = "D1",
                    BoundDirection = "Left"
                }
            },
            EntrySensorLogicalName = "EntrySensor",
            DefaultLineSpeedMmps = 500m
        };

        // Assert
        Assert.NotNull(topology);
        Assert.Equal("TOPO_001", topology.TopologyId);
        Assert.Equal("Test Line", topology.TopologyName);
        Assert.Single(topology.DiverterNodes);
        Assert.Single(topology.Chutes);
        Assert.Equal("EntrySensor", topology.EntrySensorLogicalName);
        Assert.Equal(500m, topology.DefaultLineSpeedMmps);
    }

    [Fact]
    public void GetExceptionChute_ShouldReturnExceptionChute()
    {
        // Arrange
        var topology = new LineTopology
        {
            TopologyId = "TOPO_001",
            TopologyName = "Test Line",
            DiverterNodes = Array.Empty<DiverterNodeConfig>(),
            Chutes = new List<ChuteConfig>
            {
                new()
                {
                    ChuteId = "CHUTE_A",
                    ChuteName = "Chute A",
                    BoundNodeId = "D1",
                    BoundDirection = "Left",
                    IsExceptionChute = false
                },
                new()
                {
                    ChuteId = "CHUTE_EXCEPTION",
                    ChuteName = "Exception Chute",
                    BoundNodeId = "D2",
                    BoundDirection = "Right",
                    IsExceptionChute = true
                }
            }
        };

        // Act
        var exceptionChute = topology.GetExceptionChute();

        // Assert
        Assert.NotNull(exceptionChute);
        Assert.Equal("CHUTE_EXCEPTION", exceptionChute.ChuteId);
        Assert.True(exceptionChute.IsExceptionChute);
    }

    [Fact]
    public void FindNodeById_ShouldReturnCorrectNode()
    {
        // Arrange
        var topology = new LineTopology
        {
            TopologyId = "TOPO_001",
            TopologyName = "Test Line",
            DiverterNodes = new List<DiverterNodeConfig>
            {
                new()
                {
                    NodeId = "D1",
                    NodeName = "Diverter 1",
                    PositionIndex = 0
                },
                new()
                {
                    NodeId = "D2",
                    NodeName = "Diverter 2",
                    PositionIndex = 1
                }
            },
            Chutes = Array.Empty<ChuteConfig>()
        };

        // Act
        var node = topology.FindNodeById("D2");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("D2", node.NodeId);
        Assert.Equal("Diverter 2", node.NodeName);
        Assert.Equal(1, node.PositionIndex);
    }

    [Fact]
    public void FindChuteById_ShouldReturnCorrectChute()
    {
        // Arrange
        var topology = new LineTopology
        {
            TopologyId = "TOPO_001",
            TopologyName = "Test Line",
            DiverterNodes = Array.Empty<DiverterNodeConfig>(),
            Chutes = new List<ChuteConfig>
            {
                new()
                {
                    ChuteId = "CHUTE_A",
                    ChuteName = "Chute A",
                    BoundNodeId = "D1",
                    BoundDirection = "Left"
                },
                new()
                {
                    ChuteId = "CHUTE_B",
                    ChuteName = "Chute B",
                    BoundNodeId = "D2",
                    BoundDirection = "Right"
                }
            }
        };

        // Act
        var chute = topology.FindChuteById("CHUTE_B");

        // Assert
        Assert.NotNull(chute);
        Assert.Equal("CHUTE_B", chute.ChuteId);
        Assert.Equal("Chute B", chute.ChuteName);
    }

    [Fact]
    public void FindNodeById_WithNonExistentId_ShouldReturnNull()
    {
        // Arrange
        var topology = new LineTopology
        {
            TopologyId = "TOPO_001",
            TopologyName = "Test Line",
            DiverterNodes = new List<DiverterNodeConfig>
            {
                new()
                {
                    NodeId = "D1",
                    NodeName = "Diverter 1",
                    PositionIndex = 0
                }
            },
            Chutes = Array.Empty<ChuteConfig>()
        };

        // Act
        var node = topology.FindNodeById("D99");

        // Assert
        Assert.Null(node);
    }
}
