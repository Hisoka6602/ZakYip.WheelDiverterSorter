using Xunit;
using ZakYip.WheelDiverterSorter.Core.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

public class DefaultLineTopologyConfigProviderTests
{
    [Fact]
    public async Task GetTopologyAsync_ReturnsDefaultTopology()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act
        var topology = await provider.GetTopologyAsync();

        // Assert
        Assert.NotNull(topology);
        Assert.Equal("DEFAULT_LINEAR_TOPOLOGY", topology.TopologyId);
        Assert.Equal("默认直线摆轮分拣拓扑", topology.TopologyName);
    }

    [Fact]
    public async Task GetTopologyAsync_ReturnsThreeNodes()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act
        var topology = await provider.GetTopologyAsync();

        // Assert
        Assert.NotNull(topology.WheelNodes);
        Assert.Equal(3, topology.WheelNodes.Count);
        Assert.Equal("DIVERTER_A", topology.WheelNodes[0].NodeId);
        Assert.Equal("DIVERTER_B", topology.WheelNodes[1].NodeId);
        Assert.Equal("DIVERTER_C", topology.WheelNodes[2].NodeId);
    }

    [Fact]
    public async Task GetTopologyAsync_ReturnsSevenChutes()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act
        var topology = await provider.GetTopologyAsync();

        // Assert
        Assert.NotNull(topology.Chutes);
        Assert.Equal(7, topology.Chutes.Count);
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_A1");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_A2");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_B1");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_C1");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_C2");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_C3");
        Assert.Contains(topology.Chutes, c => c.ChuteId == "CHUTE_END" && c.IsExceptionChute);
    }

    [Fact]
    public async Task GetTopologyAsync_HasExceptionChute()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act
        var topology = await provider.GetTopologyAsync();
        var exceptionChute = topology.GetExceptionChute();

        // Assert
        Assert.NotNull(exceptionChute);
        Assert.Equal("CHUTE_END", exceptionChute.ChuteId);
        Assert.True(exceptionChute.IsExceptionChute);
    }

    [Fact]
    public async Task GetTopologyAsync_CalledMultipleTimes_ReturnsSameInstance()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act
        var topology1 = await provider.GetTopologyAsync();
        var topology2 = await provider.GetTopologyAsync();

        // Assert
        Assert.Same(topology1, topology2);
    }

    [Fact]
    public async Task RefreshAsync_DoesNotThrow()
    {
        // Arrange
        var provider = new DefaultLineTopologyConfigProvider();

        // Act & Assert
        await provider.RefreshAsync();
        var topology = await provider.GetTopologyAsync();
        Assert.NotNull(topology);
    }
}
