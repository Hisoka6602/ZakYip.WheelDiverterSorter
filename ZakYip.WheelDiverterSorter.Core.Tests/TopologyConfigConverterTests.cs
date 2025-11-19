using Xunit;
using ZakYip.WheelDiverterSorter.Core;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class TopologyConfigConverterTests
{
    [Fact]
    public void ToSorterTopology_ConvertsLineTopologyConfigCorrectly()
    {
        // Arrange
        var lineTopology = new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            Description = "Test Description",
            WheelNodes = new List<WheelNodeConfig>
            {
                new WheelNodeConfig
                {
                    NodeId = "NODE_A",
                    NodeName = "Node A",
                    PositionIndex = 0,
                    HasLeftChute = true,
                    LeftChuteIds = new[] { "CHUTE_1", "CHUTE_2" },
                    SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left }
                },
                new WheelNodeConfig
                {
                    NodeId = "NODE_B",
                    NodeName = "Node B",
                    PositionIndex = 1,
                    HasRightChute = true,
                    RightChuteIds = new[] { "CHUTE_3" },
                    SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Right }
                }
            },
            Chutes = new List<ChuteConfig>()
        };

        // Act
        var sorterTopology = TopologyConfigConverter.ToSorterTopology(lineTopology);

        // Assert
        Assert.NotNull(sorterTopology);
        Assert.Equal("TEST_TOPOLOGY", sorterTopology.TopologyId);
        Assert.Equal("Test Description", sorterTopology.Description);
        Assert.Equal(2, sorterTopology.Nodes.Count);
        
        var nodeA = sorterTopology.Nodes[0];
        Assert.Equal("NODE_A", nodeA.NodeId);
        Assert.Equal("Node A", nodeA.NodeName);
        Assert.Contains(DiverterSide.Straight, nodeA.SupportedActions);
        Assert.Contains(DiverterSide.Left, nodeA.SupportedActions);
        Assert.Equal(2, nodeA.ChuteMapping[DiverterSide.Left].Count);
        Assert.Contains("CHUTE_1", nodeA.ChuteMapping[DiverterSide.Left]);
        Assert.Contains("CHUTE_2", nodeA.ChuteMapping[DiverterSide.Left]);

        var nodeB = sorterTopology.Nodes[1];
        Assert.Equal("NODE_B", nodeB.NodeId);
        Assert.Equal("Node B", nodeB.NodeName);
        Assert.Contains(DiverterSide.Straight, nodeB.SupportedActions);
        Assert.Contains(DiverterSide.Right, nodeB.SupportedActions);
        Assert.Single(nodeB.ChuteMapping[DiverterSide.Right]);
        Assert.Contains("CHUTE_3", nodeB.ChuteMapping[DiverterSide.Right]);
    }

    [Fact]
    public void FromSorterTopology_ConvertsBackCorrectly()
    {
        // Arrange
        var nodeA = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Straight, DiverterSide.Left }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                [DiverterSide.Left] = new List<string> { "CHUTE_1", "CHUTE_2" }.AsReadOnly(),
                [DiverterSide.Straight] = new List<string>().AsReadOnly()
            }.AsReadOnly()
        };

        var sorterTopology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test Description",
            Nodes = new List<DiverterNode> { nodeA }.AsReadOnly()
        };

        // Act
        var lineTopology = TopologyConfigConverter.FromSorterTopology(sorterTopology);

        // Assert
        Assert.NotNull(lineTopology);
        Assert.Equal("TEST_TOPOLOGY", lineTopology.TopologyId);
        Assert.Equal("Test Description", lineTopology.Description);
        Assert.Single(lineTopology.WheelNodes);
        
        var wheelNode = lineTopology.WheelNodes[0];
        Assert.Equal("NODE_A", wheelNode.NodeId);
        Assert.Equal("Node A", wheelNode.NodeName);
        Assert.Equal(0, wheelNode.PositionIndex);
        Assert.True(wheelNode.HasLeftChute);
        Assert.False(wheelNode.HasRightChute);
        Assert.Equal(2, wheelNode.LeftChuteIds.Count);
        Assert.Contains("CHUTE_1", wheelNode.LeftChuteIds);
        Assert.Contains("CHUTE_2", wheelNode.LeftChuteIds);

        // Check chutes
        Assert.Equal(2, lineTopology.Chutes.Count);
        Assert.All(lineTopology.Chutes, c => Assert.Equal("NODE_A", c.BoundNodeId));
        Assert.All(lineTopology.Chutes, c => Assert.Equal("Left", c.BoundDirection));
    }

    [Fact]
    public void RoundTripConversion_PreservesTopologyStructure()
    {
        // Arrange
        var originalLineTopology = new LineTopologyConfig
        {
            TopologyId = "ROUNDTRIP_TEST",
            TopologyName = "Roundtrip Test",
            Description = "Test roundtrip conversion",
            WheelNodes = new List<WheelNodeConfig>
            {
                new WheelNodeConfig
                {
                    NodeId = "NODE_A",
                    NodeName = "Node A",
                    PositionIndex = 0,
                    HasLeftChute = true,
                    HasRightChute = false,
                    LeftChuteIds = new[] { "CHUTE_1" },
                    RightChuteIds = Array.Empty<string>(),
                    SupportedSides = new[] { DiverterSide.Straight, DiverterSide.Left }
                }
            },
            Chutes = new List<ChuteConfig>()
        };

        // Act
        var sorterTopology = TopologyConfigConverter.ToSorterTopology(originalLineTopology);
        var convertedLineTopology = TopologyConfigConverter.FromSorterTopology(sorterTopology);

        // Assert
        Assert.Equal(originalLineTopology.TopologyId, convertedLineTopology.TopologyId);
        Assert.Equal(originalLineTopology.Description, convertedLineTopology.Description);
        Assert.Equal(originalLineTopology.WheelNodes.Count, convertedLineTopology.WheelNodes.Count);
        
        var originalNode = originalLineTopology.WheelNodes[0];
        var convertedNode = convertedLineTopology.WheelNodes[0];
        Assert.Equal(originalNode.NodeId, convertedNode.NodeId);
        Assert.Equal(originalNode.NodeName, convertedNode.NodeName);
        Assert.Equal(originalNode.HasLeftChute, convertedNode.HasLeftChute);
        Assert.Equal(originalNode.HasRightChute, convertedNode.HasRightChute);
    }

    [Fact]
    public void ToSorterTopology_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            TopologyConfigConverter.ToSorterTopology(null!));
    }

    [Fact]
    public void FromSorterTopology_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            TopologyConfigConverter.FromSorterTopology(null!));
    }
}
