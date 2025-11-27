using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Topology;
using ZakYip.WheelDiverterSorter.Core.Topology.Services;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Topology;

/// <summary>
/// TopologyNode 记录的单元测试
/// </summary>
public class TopologyNodeTests
{
    [Fact]
    public void TopologyNode_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var node = new TopologyNode
        {
            NodeId = "N001",
            NodeType = TopologyNodeType.WheelDiverter,
            DisplayName = "摆轮节点1"
        };

        // Assert
        Assert.NotNull(node);
        Assert.Equal("N001", node.NodeId);
        Assert.Equal(TopologyNodeType.WheelDiverter, node.NodeType);
        Assert.Equal("摆轮节点1", node.DisplayName);
    }

    [Fact]
    public void TopologyNode_DisplayName_ShouldBeOptional()
    {
        // Arrange & Act
        var node = new TopologyNode
        {
            NodeId = "N002",
            NodeType = TopologyNodeType.Chute
        };

        // Assert
        Assert.NotNull(node);
        Assert.Equal("N002", node.NodeId);
        Assert.Null(node.DisplayName);
    }

    [Theory]
    [InlineData(TopologyNodeType.Induction)]
    [InlineData(TopologyNodeType.WheelDiverter)]
    [InlineData(TopologyNodeType.ConveyorSegment)]
    [InlineData(TopologyNodeType.Chute)]
    [InlineData(TopologyNodeType.Sensor)]
    public void TopologyNode_ShouldSupportAllNodeTypes(TopologyNodeType nodeType)
    {
        // Arrange & Act
        var node = new TopologyNode
        {
            NodeId = $"NODE_{nodeType}",
            NodeType = nodeType
        };

        // Assert
        Assert.Equal(nodeType, node.NodeType);
    }
}

/// <summary>
/// TopologyEdge 记录的单元测试
/// </summary>
public class TopologyEdgeTests
{
    [Fact]
    public void TopologyEdge_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var edge = new TopologyEdge
        {
            FromNodeId = "N001",
            ToNodeId = "N002"
        };

        // Assert
        Assert.NotNull(edge);
        Assert.Equal("N001", edge.FromNodeId);
        Assert.Equal("N002", edge.ToNodeId);
    }

    [Fact]
    public void TopologyEdge_ShouldSupportSelfLoop()
    {
        // Arrange & Act
        var edge = new TopologyEdge
        {
            FromNodeId = "N001",
            ToNodeId = "N001"
        };

        // Assert
        Assert.Equal(edge.FromNodeId, edge.ToNodeId);
    }
}

/// <summary>
/// DeviceBinding 记录的单元测试
/// </summary>
public class DeviceBindingTests
{
    [Fact]
    public void DeviceBinding_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var binding = new DeviceBinding
        {
            NodeId = "N001"
        };

        // Assert
        Assert.NotNull(binding);
        Assert.Equal("N001", binding.NodeId);
        Assert.Null(binding.IoGroupName);
        Assert.Null(binding.IoPortNumber);
        Assert.Null(binding.WheelDeviceId);
        Assert.Null(binding.ConveyorSegmentId);
    }

    [Fact]
    public void DeviceBinding_ShouldCreateWithAllProperties()
    {
        // Arrange & Act
        var binding = new DeviceBinding
        {
            NodeId = "D1",
            IoGroupName = "MainIO",
            IoPortNumber = 10,
            WheelDeviceId = 1001L,
            ConveyorSegmentId = 2001L
        };

        // Assert
        Assert.Equal("D1", binding.NodeId);
        Assert.Equal("MainIO", binding.IoGroupName);
        Assert.Equal(10, binding.IoPortNumber);
        Assert.Equal(1001L, binding.WheelDeviceId);
        Assert.Equal(2001L, binding.ConveyorSegmentId);
    }

    [Fact]
    public void DeviceBinding_WheelDeviceId_ShouldUseLongType()
    {
        // Arrange
        long largeDeviceId = long.MaxValue;

        // Act
        var binding = new DeviceBinding
        {
            NodeId = "D1",
            WheelDeviceId = largeDeviceId
        };

        // Assert
        Assert.Equal(largeDeviceId, binding.WheelDeviceId);
    }

    [Fact]
    public void DeviceBinding_ConveyorSegmentId_ShouldUseLongType()
    {
        // Arrange
        long largeSegmentId = long.MaxValue;

        // Act
        var binding = new DeviceBinding
        {
            NodeId = "SEG1",
            ConveyorSegmentId = largeSegmentId
        };

        // Assert
        Assert.Equal(largeSegmentId, binding.ConveyorSegmentId);
    }
}

/// <summary>
/// JsonLineTopologyService 的单元测试
/// </summary>
public class JsonLineTopologyServiceTests
{
    [Fact]
    public void CreateDefault_ShouldReturnValidTopology()
    {
        // Act
        var service = JsonLineTopologyService.CreateDefault();

        // Assert
        Assert.NotNull(service);
        Assert.NotEmpty(service.Nodes);
        Assert.NotEmpty(service.Edges);
    }

    [Fact]
    public void Nodes_ShouldReturnAllNodes()
    {
        // Arrange
        var nodes = new List<TopologyNode>
        {
            new TopologyNode { NodeId = "N1", NodeType = TopologyNodeType.Induction },
            new TopologyNode { NodeId = "N2", NodeType = TopologyNodeType.WheelDiverter },
            new TopologyNode { NodeId = "N3", NodeType = TopologyNodeType.Chute }
        };
        var service = new JsonLineTopologyService(nodes);

        // Assert
        Assert.Equal(3, service.Nodes.Count);
    }

    [Fact]
    public void Edges_ShouldReturnAllEdges()
    {
        // Arrange
        var edges = new List<TopologyEdge>
        {
            new TopologyEdge { FromNodeId = "N1", ToNodeId = "N2" },
            new TopologyEdge { FromNodeId = "N2", ToNodeId = "N3" }
        };
        var service = new JsonLineTopologyService(edges: edges);

        // Assert
        Assert.Equal(2, service.Edges.Count);
    }

    [Fact]
    public void FindNode_WithValidId_ShouldReturnNode()
    {
        // Arrange
        var nodes = new List<TopologyNode>
        {
            new TopologyNode { NodeId = "D1", NodeType = TopologyNodeType.WheelDiverter, DisplayName = "摆轮D1" }
        };
        var service = new JsonLineTopologyService(nodes);

        // Act
        var node = service.FindNode("D1");

        // Assert
        Assert.NotNull(node);
        Assert.Equal("D1", node.NodeId);
        Assert.Equal("摆轮D1", node.DisplayName);
    }

    [Fact]
    public void FindNode_WithInvalidId_ShouldReturnNull()
    {
        // Arrange
        var service = new JsonLineTopologyService();

        // Act
        var node = service.FindNode("NONEXISTENT");

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void FindNode_WithNullOrEmptyId_ShouldReturnNull()
    {
        // Arrange
        var service = JsonLineTopologyService.CreateDefault();

        // Act & Assert
        Assert.Null(service.FindNode(null!));
        Assert.Null(service.FindNode(""));
    }

    [Fact]
    public void GetSuccessors_ShouldReturnCorrectSuccessors()
    {
        // Arrange
        var nodes = new List<TopologyNode>
        {
            new TopologyNode { NodeId = "N1", NodeType = TopologyNodeType.Induction },
            new TopologyNode { NodeId = "N2", NodeType = TopologyNodeType.WheelDiverter },
            new TopologyNode { NodeId = "N3", NodeType = TopologyNodeType.Chute },
            new TopologyNode { NodeId = "N4", NodeType = TopologyNodeType.Chute }
        };
        var edges = new List<TopologyEdge>
        {
            new TopologyEdge { FromNodeId = "N1", ToNodeId = "N2" },
            new TopologyEdge { FromNodeId = "N2", ToNodeId = "N3" },
            new TopologyEdge { FromNodeId = "N2", ToNodeId = "N4" }
        };
        var service = new JsonLineTopologyService(nodes, edges);

        // Act
        var successors = service.GetSuccessors("N2");

        // Assert
        Assert.Equal(2, successors.Count);
        Assert.Contains(successors, n => n.NodeId == "N3");
        Assert.Contains(successors, n => n.NodeId == "N4");
    }

    [Fact]
    public void GetSuccessors_WithNoSuccessors_ShouldReturnEmptyList()
    {
        // Arrange
        var nodes = new List<TopologyNode>
        {
            new TopologyNode { NodeId = "N1", NodeType = TopologyNodeType.Chute }
        };
        var service = new JsonLineTopologyService(nodes);

        // Act
        var successors = service.GetSuccessors("N1");

        // Assert
        Assert.Empty(successors);
    }

    [Fact]
    public void GetSuccessors_WithNullOrEmptyId_ShouldReturnEmptyList()
    {
        // Arrange
        var service = JsonLineTopologyService.CreateDefault();

        // Act & Assert
        Assert.Empty(service.GetSuccessors(null!));
        Assert.Empty(service.GetSuccessors(""));
    }

    [Fact]
    public void EmptyService_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var service = new JsonLineTopologyService();

        // Assert
        Assert.Empty(service.Nodes);
        Assert.Empty(service.Edges);
        Assert.Null(service.FindNode("ANY"));
        Assert.Empty(service.GetSuccessors("ANY"));
    }
}

/// <summary>
/// JsonDeviceBindingService 的单元测试
/// </summary>
public class JsonDeviceBindingServiceTests
{
    [Fact]
    public void CreateDefault_ShouldReturnValidService()
    {
        // Act
        var service = JsonDeviceBindingService.CreateDefault();

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void GetBinding_WithValidNodeId_ShouldReturnBinding()
    {
        // Arrange
        var bindings = new List<DeviceBinding>
        {
            new DeviceBinding 
            { 
                NodeId = "D1", 
                WheelDeviceId = 1001L,
                IoGroupName = "MainIO",
                IoPortNumber = 1
            }
        };
        var service = new JsonDeviceBindingService(bindings);

        // Act
        var binding = service.GetBinding("D1");

        // Assert
        Assert.NotNull(binding);
        Assert.Equal("D1", binding.NodeId);
        Assert.Equal(1001L, binding.WheelDeviceId);
    }

    [Fact]
    public void GetBinding_WithInvalidNodeId_ShouldReturnNull()
    {
        // Arrange
        var service = new JsonDeviceBindingService();

        // Act
        var binding = service.GetBinding("NONEXISTENT");

        // Assert
        Assert.Null(binding);
    }

    [Fact]
    public void GetBinding_WithNullOrEmptyId_ShouldReturnNull()
    {
        // Arrange
        var service = JsonDeviceBindingService.CreateDefault();

        // Act & Assert
        Assert.Null(service.GetBinding(null!));
        Assert.Null(service.GetBinding(""));
    }

    [Fact]
    public void GetWheelDevice_WithNoBinding_ShouldReturnNull()
    {
        // Arrange
        var service = new JsonDeviceBindingService();

        // Act
        var device = service.GetWheelDevice("D1");

        // Assert
        Assert.Null(device);
    }

    [Fact]
    public void GetConveyorDevice_WithNoBinding_ShouldReturnNull()
    {
        // Arrange
        var service = new JsonDeviceBindingService();

        // Act
        var device = service.GetConveyorDevice("SEG1");

        // Assert
        Assert.Null(device);
    }

    [Fact]
    public void GetIoPort_WithNoBinding_ShouldReturnNull()
    {
        // Arrange
        var service = new JsonDeviceBindingService();

        // Act
        var port = service.GetIoPort("SENSOR1");

        // Assert
        Assert.Null(port);
    }

    [Fact]
    public void GetIoPortKey_ShouldReturnCorrectFormat()
    {
        // Act
        var key = JsonDeviceBindingService.GetIoPortKey("MainIO", 10);

        // Assert
        Assert.Equal("MainIO:10", key);
    }

    [Fact]
    public void EmptyService_ShouldWorkCorrectly()
    {
        // Arrange & Act
        var service = new JsonDeviceBindingService();

        // Assert
        Assert.Null(service.GetBinding("ANY"));
        Assert.Null(service.GetWheelDevice("ANY"));
        Assert.Null(service.GetConveyorDevice("ANY"));
        Assert.Null(service.GetIoPort("ANY"));
    }
}
