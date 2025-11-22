using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Orchestration;
using ZakYip.WheelDiverterSorter.Host.Services;
using ZakYip.WheelDiverterSorter.Observability;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 路由-拓扑一致性检查器测试
/// </summary>
public class RouteTopologyConsistencyCheckerTests
{
    private readonly Mock<IRouteConfigurationRepository> _mockRouteRepo;
    private readonly Mock<ILineTopologyRepository> _mockTopologyRepo;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly RouteTopologyConsistencyChecker _checker;

    public RouteTopologyConsistencyCheckerTests()
    {
        _mockRouteRepo = new Mock<IRouteConfigurationRepository>();
        _mockTopologyRepo = new Mock<ILineTopologyRepository>();
        _mockClock = new Mock<ISystemClock>();

        _mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2025, 11, 22, 10, 30, 0));

        _checker = new RouteTopologyConsistencyChecker(
            _mockRouteRepo.Object,
            _mockTopologyRepo.Object,
            _mockClock.Object,
            NullLogger<RouteTopologyConsistencyChecker>.Instance);
    }

    [Fact]
    public void Check_WhenRoutingAndTopologyAreConsistent_ReturnsConsistent()
    {
        // Arrange
        var routeConfigs = new List<ChuteRouteConfiguration>
        {
            new ChuteRouteConfiguration { ChuteId = 1, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 2, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 3, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true }
        };

        var topologyConfig = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = new[]
            {
                new ChuteConfig { ChuteId = "1", ChuteName = "Chute 1", BoundNodeId = "N1", BoundDirection = "Left", IsEnabled = true },
                new ChuteConfig { ChuteId = "2", ChuteName = "Chute 2", BoundNodeId = "N1", BoundDirection = "Right", IsEnabled = true },
                new ChuteConfig { ChuteId = "3", ChuteName = "Chute 3", BoundNodeId = "N2", BoundDirection = "Left", IsEnabled = true }
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _mockRouteRepo.Setup(r => r.GetAllEnabled()).Returns(routeConfigs);
        _mockTopologyRepo.Setup(t => t.Get()).Returns(topologyConfig);

        // Act
        var result = _checker.Check();

        // Assert
        Assert.True(result.IsConsistent);
        Assert.Equal(3, result.RoutingChuteIds.Count);
        Assert.Equal(3, result.TopologyChuteIds.Count);
        Assert.Empty(result.InvalidRoutingReferences);
        Assert.Empty(result.UnusedTopologyChutes);
    }

    [Fact]
    public void Check_WhenRoutingReferencesNonExistentChute_ReturnsInconsistent()
    {
        // Arrange
        var routeConfigs = new List<ChuteRouteConfiguration>
        {
            new ChuteRouteConfiguration { ChuteId = 1, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 2, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 99, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true } // Non-existent
        };

        var topologyConfig = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = new[]
            {
                new ChuteConfig { ChuteId = "1", ChuteName = "Chute 1", BoundNodeId = "N1", BoundDirection = "Left", IsEnabled = true },
                new ChuteConfig { ChuteId = "2", ChuteName = "Chute 2", BoundNodeId = "N1", BoundDirection = "Right", IsEnabled = true }
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _mockRouteRepo.Setup(r => r.GetAllEnabled()).Returns(routeConfigs);
        _mockTopologyRepo.Setup(t => t.Get()).Returns(topologyConfig);

        // Act
        var result = _checker.Check();

        // Assert
        Assert.False(result.IsConsistent);
        Assert.Equal(3, result.RoutingChuteIds.Count);
        Assert.Equal(2, result.TopologyChuteIds.Count);
        Assert.Single(result.InvalidRoutingReferences);
        Assert.Contains(99, result.InvalidRoutingReferences);
    }

    [Fact]
    public void Check_WhenTopologyHasUnusedChutes_ReturnsConsistentWithUnused()
    {
        // Arrange
        var routeConfigs = new List<ChuteRouteConfiguration>
        {
            new ChuteRouteConfiguration { ChuteId = 1, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 2, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true }
        };

        var topologyConfig = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = new[]
            {
                new ChuteConfig { ChuteId = "1", ChuteName = "Chute 1", BoundNodeId = "N1", BoundDirection = "Left", IsEnabled = true },
                new ChuteConfig { ChuteId = "2", ChuteName = "Chute 2", BoundNodeId = "N1", BoundDirection = "Right", IsEnabled = true },
                new ChuteConfig { ChuteId = "3", ChuteName = "Chute 3", BoundNodeId = "N2", BoundDirection = "Left", IsEnabled = true }, // Unused
                new ChuteConfig { ChuteId = "4", ChuteName = "Chute 4", BoundNodeId = "N2", BoundDirection = "Right", IsEnabled = true } // Unused
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _mockRouteRepo.Setup(r => r.GetAllEnabled()).Returns(routeConfigs);
        _mockTopologyRepo.Setup(t => t.Get()).Returns(topologyConfig);

        // Act
        var result = _checker.Check();

        // Assert
        Assert.True(result.IsConsistent); // Still consistent because all routing references are valid
        Assert.Equal(2, result.RoutingChuteIds.Count);
        Assert.Equal(4, result.TopologyChuteIds.Count);
        Assert.Empty(result.InvalidRoutingReferences);
        Assert.Equal(2, result.UnusedTopologyChutes.Count);
        Assert.Contains(3, result.UnusedTopologyChutes);
        Assert.Contains(4, result.UnusedTopologyChutes);
    }

    [Fact]
    public void Check_IgnoresDisabledChutes()
    {
        // Arrange
        var routeConfigs = new List<ChuteRouteConfiguration>
        {
            new ChuteRouteConfiguration { ChuteId = 1, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true },
            new ChuteRouteConfiguration { ChuteId = 2, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = false } // Disabled
        };

        var topologyConfig = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = new[]
            {
                new ChuteConfig { ChuteId = "1", ChuteName = "Chute 1", BoundNodeId = "N1", BoundDirection = "Left", IsEnabled = true },
                new ChuteConfig { ChuteId = "2", ChuteName = "Chute 2", BoundNodeId = "N1", BoundDirection = "Right", IsEnabled = false } // Disabled
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _mockRouteRepo.Setup(r => r.GetAllEnabled()).Returns(routeConfigs);
        _mockTopologyRepo.Setup(t => t.Get()).Returns(topologyConfig);

        // Act
        var result = _checker.Check();

        // Assert
        Assert.True(result.IsConsistent);
        Assert.Single(result.RoutingChuteIds); // Only enabled route
        Assert.Single(result.TopologyChuteIds); // Only enabled chute
        Assert.Contains(1, result.RoutingChuteIds);
        Assert.Contains(1, result.TopologyChuteIds);
    }

    [Fact]
    public void Check_HandlesNonNumericChuteIds()
    {
        // Arrange
        var routeConfigs = new List<ChuteRouteConfiguration>
        {
            new ChuteRouteConfiguration { ChuteId = 1, DiverterConfigurations = new List<DiverterConfigurationEntry>(), IsEnabled = true }
        };

        var topologyConfig = new LineTopologyConfig
        {
            TopologyId = "test",
            TopologyName = "Test Topology",
            WheelNodes = Array.Empty<WheelNodeConfig>(),
            Chutes = new[]
            {
                new ChuteConfig { ChuteId = "1", ChuteName = "Chute 1", BoundNodeId = "N1", BoundDirection = "Left", IsEnabled = true },
                new ChuteConfig { ChuteId = "EXCEPTION", ChuteName = "Exception Chute", BoundNodeId = "N2", BoundDirection = "Right", IsEnabled = true } // Non-numeric
            },
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };

        _mockRouteRepo.Setup(r => r.GetAllEnabled()).Returns(routeConfigs);
        _mockTopologyRepo.Setup(t => t.Get()).Returns(topologyConfig);

        // Act
        var result = _checker.Check();

        // Assert
        Assert.True(result.IsConsistent);
        Assert.Single(result.RoutingChuteIds);
        Assert.Single(result.TopologyChuteIds); // Only numeric ChuteId "1" is counted
        Assert.Empty(result.InvalidRoutingReferences);
    }
}
