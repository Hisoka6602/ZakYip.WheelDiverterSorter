using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 格口路径拓扑配置测试
/// </summary>
public class ChutePathTopologyConfigTests
{
    [Fact]
    public void ChutePathTopologyConfig_ShouldCreateWithRequiredProperties()
    {
        // Arrange & Act
        var config = CreateSampleTopologyConfig();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("default", config.TopologyId);
        Assert.Equal("测试拓扑", config.TopologyName);
        Assert.Equal(1, config.EntrySensorId);
        Assert.Equal(999, config.ExceptionChuteId);
        Assert.Equal(3, config.DiverterNodes.Count);
    }

    [Fact]
    public void FindNodeByDiverterId_WithExistingId_ShouldReturnCorrectNode()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var node = config.FindNodeByDiverterId(2);

        // Assert
        Assert.NotNull(node);
        Assert.Equal(2, node.DiverterId);
        Assert.Equal("摆轮D2", node.DiverterName);
        Assert.Equal(2, node.PositionIndex);
    }

    [Fact]
    public void FindNodeByDiverterId_WithNonExistingId_ShouldReturnNull()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var node = config.FindNodeByDiverterId(999);

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void FindNodeByChuteId_WithLeftChuteId_ShouldReturnCorrectNode()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var node = config.FindNodeByChuteId(2); // Left chute of diverter 1

        // Assert
        Assert.NotNull(node);
        Assert.Equal(1, node.DiverterId);
    }

    [Fact]
    public void FindNodeByChuteId_WithRightChuteId_ShouldReturnCorrectNode()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var node = config.FindNodeByChuteId(1); // Right chute of diverter 1

        // Assert
        Assert.NotNull(node);
        Assert.Equal(1, node.DiverterId);
    }

    [Fact]
    public void FindNodeByChuteId_WithNonExistingChuteId_ShouldReturnNull()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var node = config.FindNodeByChuteId(100);

        // Assert
        Assert.Null(node);
    }

    [Fact]
    public void GetPathToChute_WithValidChuteId_ShouldReturnCorrectPath()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act - Get path to chute 4 (on diverter 2)
        var path = config.GetPathToChute(4);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(2, path.Count); // Should include diverter 1 and 2
        Assert.Equal(1, path[0].DiverterId);
        Assert.Equal(2, path[1].DiverterId);
    }

    [Fact]
    public void GetPathToChute_WithChuteOnFirstDiverter_ShouldReturnSingleNodePath()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act - Get path to chute 1 (on diverter 1)
        var path = config.GetPathToChute(1);

        // Assert
        Assert.NotNull(path);
        Assert.Single(path);
        Assert.Equal(1, path[0].DiverterId);
    }

    [Fact]
    public void GetPathToChute_WithNonExistingChuteId_ShouldReturnNull()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var path = config.GetPathToChute(100);

        // Assert
        Assert.Null(path);
    }

    [Fact]
    public void GetChuteDirection_WithLeftChute_ShouldReturnLeft()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var direction = config.GetChuteDirection(2); // Left chute of diverter 1

        // Assert
        Assert.NotNull(direction);
        Assert.Equal(DiverterDirection.Left, direction);
    }

    [Fact]
    public void GetChuteDirection_WithRightChute_ShouldReturnRight()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var direction = config.GetChuteDirection(1); // Right chute of diverter 1

        // Assert
        Assert.NotNull(direction);
        Assert.Equal(DiverterDirection.Right, direction);
    }

    [Fact]
    public void GetChuteDirection_WithNonExistingChuteId_ShouldReturnNull()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var direction = config.GetChuteDirection(100);

        // Assert
        Assert.Null(direction);
    }

    [Fact]
    public void TotalChuteCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act
        var count = config.TotalChuteCount;

        // Assert
        // Diverter 1: 1 left + 1 right = 2
        // Diverter 2: 1 left + 1 right = 2
        // Diverter 3: 1 left + 1 right = 2
        Assert.Equal(6, count);
    }

    [Fact]
    public void DiverterPathNode_HasLeftChute_ShouldReturnTrueWhenLeftChutesExist()
    {
        // Arrange
        var node = new DiverterPathNode
        {
            DiverterId = 1,
            PositionIndex = 1,
            SegmentId = 1,
            FrontSensorId = 2,
            LeftChuteIds = new List<long> { 1, 2 }
        };

        // Assert
        Assert.True(node.HasLeftChute);
    }

    [Fact]
    public void DiverterPathNode_HasLeftChute_ShouldReturnFalseWhenNoLeftChutes()
    {
        // Arrange
        var node = new DiverterPathNode
        {
            DiverterId = 1,
            PositionIndex = 1,
            SegmentId = 1,
            FrontSensorId = 2,
            LeftChuteIds = Array.Empty<long>()
        };

        // Assert
        Assert.False(node.HasLeftChute);
    }

    [Fact]
    public void DiverterPathNode_HasRightChute_ShouldReturnTrueWhenRightChutesExist()
    {
        // Arrange
        var node = new DiverterPathNode
        {
            DiverterId = 1,
            PositionIndex = 1,
            SegmentId = 1,
            FrontSensorId = 2,
            RightChuteIds = new List<long> { 1, 2 }
        };

        // Assert
        Assert.True(node.HasRightChute);
    }

    [Fact]
    public void DiverterPathNode_HasRightChute_ShouldReturnFalseWhenNoRightChutes()
    {
        // Arrange
        var node = new DiverterPathNode
        {
            DiverterId = 1,
            PositionIndex = 1,
            SegmentId = 1,
            FrontSensorId = 2,
            RightChuteIds = Array.Empty<long>()
        };

        // Assert
        Assert.False(node.HasRightChute);
    }

    [Fact]
    public void GetPathToChute_WithChuteOnLastDiverter_ShouldReturnAllNodes()
    {
        // Arrange
        var config = CreateSampleTopologyConfig();

        // Act - Get path to chute 5 (on diverter 3, the last one)
        var path = config.GetPathToChute(5);

        // Assert
        Assert.NotNull(path);
        Assert.Equal(3, path.Count); // Should include all 3 diverters
        Assert.Equal(1, path[0].PositionIndex);
        Assert.Equal(2, path[1].PositionIndex);
        Assert.Equal(3, path[2].PositionIndex);
    }

    private static ChutePathTopologyConfig CreateSampleTopologyConfig()
    {
        return new ChutePathTopologyConfig
        {
            TopologyId = "default",
            TopologyName = "测试拓扑",
            Description = "用于单元测试的拓扑配置",
            EntrySensorId = 1,
            DiverterNodes = new List<DiverterPathNode>
            {
                new()
                {
                    DiverterId = 1,
                    DiverterName = "摆轮D1",
                    PositionIndex = 1,
                    SegmentId = 1,
                    FrontSensorId = 2,
                    LeftChuteIds = new List<long> { 2 },
                    RightChuteIds = new List<long> { 1 },
                    Remarks = "第一个摆轮"
                },
                new()
                {
                    DiverterId = 2,
                    DiverterName = "摆轮D2",
                    PositionIndex = 2,
                    SegmentId = 2,
                    FrontSensorId = 3,
                    LeftChuteIds = new List<long> { 4 },
                    RightChuteIds = new List<long> { 3 },
                    Remarks = "第二个摆轮"
                },
                new()
                {
                    DiverterId = 3,
                    DiverterName = "摆轮D3",
                    PositionIndex = 3,
                    SegmentId = 3,
                    FrontSensorId = 4,
                    LeftChuteIds = new List<long> { 6 },
                    RightChuteIds = new List<long> { 5 },
                    Remarks = "第三个摆轮"
                }
            },
            ExceptionChuteId = 999,
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
    }
}
