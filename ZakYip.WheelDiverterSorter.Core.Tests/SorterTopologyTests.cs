using Xunit;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class SorterTopologyTests
{
    [Fact]
    public void GetNodeIndex_WithExistingNode_ReturnsCorrectIndex()
    {
        // Arrange
        var nodeA = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Straight }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>().AsReadOnly()
        };

        var nodeB = new DiverterNode
        {
            NodeId = "NODE_B",
            NodeName = "Node B",
            SupportedActions = new List<DiverterSide> { DiverterSide.Left }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>().AsReadOnly()
        };

        var topology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test topology",
            Nodes = new List<DiverterNode> { nodeA, nodeB }.AsReadOnly()
        };

        // Act
        var indexA = topology.GetNodeIndex("NODE_A");
        var indexB = topology.GetNodeIndex("NODE_B");

        // Assert
        Assert.Equal(0, indexA);
        Assert.Equal(1, indexB);
    }

    [Fact]
    public void GetNodeIndex_WithNonExistentNode_ReturnsMinusOne()
    {
        // Arrange
        var node = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Straight }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>().AsReadOnly()
        };

        var topology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test topology",
            Nodes = new List<DiverterNode> { node }.AsReadOnly()
        };

        // Act
        var index = topology.GetNodeIndex("NON_EXISTENT");

        // Assert
        Assert.Equal(-1, index);
    }

    [Fact]
    public void FindRoutesToChute_WithExistingChute_ReturnsCorrectRoutes()
    {
        // Arrange
        var node = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Left, DiverterSide.Right }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                [DiverterSide.Left] = new List<string> { "CHUTE_1", "CHUTE_2" }.AsReadOnly(),
                [DiverterSide.Right] = new List<string> { "CHUTE_3" }.AsReadOnly()
            }.AsReadOnly()
        };

        var topology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test topology",
            Nodes = new List<DiverterNode> { node }.AsReadOnly()
        };

        // Act
        var routes = topology.FindRoutesToChute("CHUTE_1");

        // Assert
        Assert.Single(routes);
        Assert.Equal("NODE_A", routes[0].Node.NodeId);
        Assert.Equal(DiverterSide.Left, routes[0].Side);
    }

    [Fact]
    public void FindRoutesToChute_WithNonExistentChute_ReturnsEmptyList()
    {
        // Arrange
        var node = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Left }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                [DiverterSide.Left] = new List<string> { "CHUTE_1" }.AsReadOnly()
            }.AsReadOnly()
        };

        var topology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test topology",
            Nodes = new List<DiverterNode> { node }.AsReadOnly()
        };

        // Act
        var routes = topology.FindRoutesToChute("NON_EXISTENT");

        // Assert
        Assert.Empty(routes);
    }

    [Fact]
    public void FindRoutesToChute_WithMultipleNodes_ReturnsAllMatchingRoutes()
    {
        // Arrange
        var nodeA = new DiverterNode
        {
            NodeId = "NODE_A",
            NodeName = "Node A",
            SupportedActions = new List<DiverterSide> { DiverterSide.Left }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                [DiverterSide.Left] = new List<string> { "CHUTE_1" }.AsReadOnly()
            }.AsReadOnly()
        };

        var nodeB = new DiverterNode
        {
            NodeId = "NODE_B",
            NodeName = "Node B",
            SupportedActions = new List<DiverterSide> { DiverterSide.Right }.AsReadOnly(),
            ChuteMapping = new Dictionary<DiverterSide, IReadOnlyList<string>>
            {
                [DiverterSide.Right] = new List<string> { "CHUTE_1" }.AsReadOnly()
            }.AsReadOnly()
        };

        var topology = new SorterTopology
        {
            TopologyId = "TEST_TOPOLOGY",
            Description = "Test topology",
            Nodes = new List<DiverterNode> { nodeA, nodeB }.AsReadOnly()
        };

        // Act
        var routes = topology.FindRoutesToChute("CHUTE_1");

        // Assert
        Assert.Equal(2, routes.Count);
        Assert.Contains(routes, r => r.Node.NodeId == "NODE_A" && r.Side == DiverterSide.Left);
        Assert.Contains(routes, r => r.Node.NodeId == "NODE_B" && r.Side == DiverterSide.Right);
    }
}
