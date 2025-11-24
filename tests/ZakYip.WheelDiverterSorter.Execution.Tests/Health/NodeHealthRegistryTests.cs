using FluentAssertions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Execution.Health;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Health;

/// <summary>
/// Tests for NodeHealthRegistry
/// </summary>
public class NodeHealthRegistryTests
{
    [Fact]
    public void UpdateNodeHealth_AddsNewNode()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        var nodeStatus = new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        };

        // Act
        registry.UpdateNodeHealth(nodeStatus);
        var result = registry.GetNodeHealth(1);

        // Assert
        result.Should().NotBeNull();
        result!.Value.NodeId.Should().Be(1);
        result.Value.IsHealthy.Should().BeTrue();
    }

    [Fact]
    public void UpdateNodeHealth_UpdatesExistingNode()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        var initialStatus = new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        };
        
        registry.UpdateNodeHealth(initialStatus);

        var updatedStatus = new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = false,
            ErrorCode = "TEST_ERROR",
            ErrorMessage = "测试错误",
            CheckedAt = DateTimeOffset.UtcNow
        };

        // Act
        registry.UpdateNodeHealth(updatedStatus);
        var result = registry.GetNodeHealth(1);

        // Assert
        result.Should().NotBeNull();
        result!.Value.IsHealthy.Should().BeFalse();
        result.Value.ErrorCode.Should().Be("TEST_ERROR");
    }

    [Fact]
    public void GetNodeHealth_ReturnsNullForNonexistentNode()
    {
        // Arrange
        var registry = new NodeHealthRegistry();

        // Act
        var result = registry.GetNodeHealth(999);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void IsNodeHealthy_ReturnsTrueForNonexistentNode()
    {
        // Arrange
        var registry = new NodeHealthRegistry();

        // Act
        var isHealthy = registry.IsNodeHealthy(999);

        // Assert
        isHealthy.Should().BeTrue("未注册的节点默认视为健康");
    }

    [Fact]
    public void IsNodeHealthy_ReturnsFalseForUnhealthyNode()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        var nodeStatus = new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = false,
            CheckedAt = DateTimeOffset.UtcNow
        };
        
        registry.UpdateNodeHealth(nodeStatus);

        // Act
        var isHealthy = registry.IsNodeHealthy(1);

        // Assert
        isHealthy.Should().BeFalse();
    }

    [Fact]
    public void GetUnhealthyNodes_ReturnsOnlyUnhealthyNodes()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        });
        
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 2,
            IsHealthy = false,
            CheckedAt = DateTimeOffset.UtcNow
        });
        
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 3,
            IsHealthy = false,
            CheckedAt = DateTimeOffset.UtcNow
        });

        // Act
        var unhealthyNodes = registry.GetUnhealthyNodes();

        // Assert
        unhealthyNodes.Should().HaveCount(2);
        unhealthyNodes.Should().Contain(n => n.NodeId == 2);
        unhealthyNodes.Should().Contain(n => n.NodeId == 3);
    }

    [Fact]
    public void GetDegradationMode_ReturnsNoneWhenAllNodesHealthy()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        });

        // Act
        var mode = registry.GetDegradationMode();

        // Assert
        mode.Should().Be(DegradationMode.None);
    }

    [Fact]
    public void GetDegradationMode_ReturnsNodeDegradedWhenFewNodesUnhealthy()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        
        // Add 10 healthy nodes
        for (int i = 1; i <= 10; i++)
        {
            registry.UpdateNodeHealth(new NodeHealthStatus
            {
                NodeId = i,
                IsHealthy = true,
                CheckedAt = DateTimeOffset.UtcNow
            });
        }
        
        // Add 2 unhealthy nodes (20% < 30% threshold)
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 11,
            IsHealthy = false,
            CheckedAt = DateTimeOffset.UtcNow
        });
        
        registry.UpdateNodeHealth(new NodeHealthStatus
        {
            NodeId = 12,
            IsHealthy = false,
            CheckedAt = DateTimeOffset.UtcNow
        });

        // Act
        var mode = registry.GetDegradationMode();

        // Assert
        mode.Should().Be(DegradationMode.NodeDegraded);
    }

    [Fact]
    public void GetDegradationMode_ReturnsLineDegradedWhenManyNodesUnhealthy()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        
        // Add 7 healthy nodes
        for (int i = 1; i <= 7; i++)
        {
            registry.UpdateNodeHealth(new NodeHealthStatus
            {
                NodeId = i,
                IsHealthy = true,
                CheckedAt = DateTimeOffset.UtcNow
            });
        }
        
        // Add 3 unhealthy nodes (30% threshold)
        for (int i = 8; i <= 10; i++)
        {
            registry.UpdateNodeHealth(new NodeHealthStatus
            {
                NodeId = i,
                IsHealthy = false,
                CheckedAt = DateTimeOffset.UtcNow
            });
        }

        // Act
        var mode = registry.GetDegradationMode();

        // Assert
        mode.Should().Be(DegradationMode.LineDegraded);
    }

    [Fact]
    public void NodeHealthChanged_FiresEventOnHealthStatusChange()
    {
        // Arrange
        var registry = new NodeHealthRegistry();
        NodeHealthChangedEventArgs? eventArgs = null;
        registry.NodeHealthChanged += (sender, args) => eventArgs = args;

        var nodeStatus = new NodeHealthStatus
        {
            NodeId = 1,
            IsHealthy = true,
            CheckedAt = DateTimeOffset.UtcNow
        };

        // Act
        registry.UpdateNodeHealth(nodeStatus);

        // Assert
        eventArgs.Should().NotBeNull();
        eventArgs!.NodeId.Should().Be(1);
        eventArgs.NewStatus.IsHealthy.Should().BeTrue();
    }
}
