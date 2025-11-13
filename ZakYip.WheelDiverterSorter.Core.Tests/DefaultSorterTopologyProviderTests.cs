using Xunit;
using ZakYip.WheelDiverterSorter.Core;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class DefaultSorterTopologyProviderTests
{
    [Fact]
    public void GetDefaultTopology_ReturnsValidTopology()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

        // Assert
        Assert.NotNull(topology);
        Assert.Equal("DEFAULT_LINEAR_TOPOLOGY", topology.TopologyId);
        Assert.NotEmpty(topology.Description);
        Assert.NotNull(topology.Nodes);
    }

    [Fact]
    public void GetDefaultTopology_ReturnsThreeNodes()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

        // Assert
        Assert.Equal(3, topology.Nodes.Count);
        Assert.Equal("DIVERTER_A", topology.Nodes[0].NodeId);
        Assert.Equal("DIVERTER_B", topology.Nodes[1].NodeId);
        Assert.Equal("DIVERTER_C", topology.Nodes[2].NodeId);
    }

    [Fact]
    public void GetDefaultTopology_NodeA_HasCorrectConfiguration()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();
        var nodeA = topology.Nodes[0];

        // Assert
        Assert.Equal("DIVERTER_A", nodeA.NodeId);
        Assert.Equal("摆轮节点A", nodeA.NodeName);
        Assert.Contains(DiverterSide.Straight, nodeA.SupportedActions);
        Assert.Contains(DiverterSide.Left, nodeA.SupportedActions);
        Assert.DoesNotContain(DiverterSide.Right, nodeA.SupportedActions);
        
        // Check chute mapping
        Assert.True(nodeA.ChuteMapping.ContainsKey(DiverterSide.Left));
        Assert.Contains("CHUTE_A1", nodeA.ChuteMapping[DiverterSide.Left]);
        Assert.Contains("CHUTE_A2", nodeA.ChuteMapping[DiverterSide.Left]);
    }

    [Fact]
    public void GetDefaultTopology_NodeB_HasCorrectConfiguration()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();
        var nodeB = topology.Nodes[1];

        // Assert
        Assert.Equal("DIVERTER_B", nodeB.NodeId);
        Assert.Equal("摆轮节点B", nodeB.NodeName);
        Assert.Contains(DiverterSide.Straight, nodeB.SupportedActions);
        Assert.Contains(DiverterSide.Right, nodeB.SupportedActions);
        Assert.DoesNotContain(DiverterSide.Left, nodeB.SupportedActions);
        
        // Check chute mapping
        Assert.True(nodeB.ChuteMapping.ContainsKey(DiverterSide.Right));
        Assert.Contains("CHUTE_B1", nodeB.ChuteMapping[DiverterSide.Right]);
    }

    [Fact]
    public void GetDefaultTopology_NodeC_HasCorrectConfiguration()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();
        var nodeC = topology.Nodes[2];

        // Assert
        Assert.Equal("DIVERTER_C", nodeC.NodeId);
        Assert.Equal("摆轮节点C", nodeC.NodeName);
        Assert.Contains(DiverterSide.Straight, nodeC.SupportedActions);
        Assert.Contains(DiverterSide.Left, nodeC.SupportedActions);
        Assert.Contains(DiverterSide.Right, nodeC.SupportedActions);
        
        // Check chute mapping
        Assert.True(nodeC.ChuteMapping.ContainsKey(DiverterSide.Left));
        Assert.Contains("CHUTE_C1", nodeC.ChuteMapping[DiverterSide.Left]);
        
        Assert.True(nodeC.ChuteMapping.ContainsKey(DiverterSide.Right));
        Assert.Contains("CHUTE_C2", nodeC.ChuteMapping[DiverterSide.Right]);
        Assert.Contains("CHUTE_C3", nodeC.ChuteMapping[DiverterSide.Right]);
        
        Assert.True(nodeC.ChuteMapping.ContainsKey(DiverterSide.Straight));
        Assert.Contains("CHUTE_END", nodeC.ChuteMapping[DiverterSide.Straight]);
    }

    [Fact]
    public void GetDefaultTopology_CanFindRoutesToAllChutes()
    {
        // Act
        var topology = DefaultSorterTopologyProvider.GetDefaultTopology();

        // Assert - Verify routes can be found for all configured chutes
        var routesA1 = topology.FindRoutesToChute("CHUTE_A1");
        Assert.NotEmpty(routesA1);
        
        var routesA2 = topology.FindRoutesToChute("CHUTE_A2");
        Assert.NotEmpty(routesA2);
        
        var routesB1 = topology.FindRoutesToChute("CHUTE_B1");
        Assert.NotEmpty(routesB1);
        
        var routesC1 = topology.FindRoutesToChute("CHUTE_C1");
        Assert.NotEmpty(routesC1);
        
        var routesC2 = topology.FindRoutesToChute("CHUTE_C2");
        Assert.NotEmpty(routesC2);
        
        var routesC3 = topology.FindRoutesToChute("CHUTE_C3");
        Assert.NotEmpty(routesC3);
        
        var routesEnd = topology.FindRoutesToChute("CHUTE_END");
        Assert.NotEmpty(routesEnd);
    }
}
