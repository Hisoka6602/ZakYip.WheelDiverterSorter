using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 测试 RouteTimingEstimator 路径时间预估功能
/// </summary>
public class RouteTimingEstimatorTests
{
    private readonly Mock<ILineTopologyRepository> _mockRepository;
    private readonly RouteTimingEstimator _estimator;

    public RouteTimingEstimatorTests()
    {
        _mockRepository = new Mock<ILineTopologyRepository>();
        _estimator = new RouteTimingEstimator(_mockRepository.Object);
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new RouteTimingEstimator(null!));
    }

    [Fact]
    public void EstimateArrivalTime_WithNullChuteId_ReturnsFailureResult()
    {
        // Act
        var result = _estimator.EstimateArrivalTime(null!);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("格口ID不能为空", result.ErrorMessage);
        Assert.Equal(string.Empty, result.ChuteId);
        Assert.Equal(0, result.TotalDistanceMm);
        Assert.Equal(0, result.EstimatedArrivalTimeMs);
        Assert.Equal(0, result.SegmentCount);
    }

    [Fact]
    public void EstimateArrivalTime_WithEmptyChuteId_ReturnsFailureResult()
    {
        // Act
        var result = _estimator.EstimateArrivalTime("");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("格口ID不能为空", result.ErrorMessage);
    }

    [Fact]
    public void EstimateArrivalTime_WithWhitespaceChuteId_ReturnsFailureResult()
    {
        // Act
        var result = _estimator.EstimateArrivalTime("   ");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("格口ID不能为空", result.ErrorMessage);
    }

    [Fact]
    public void EstimateArrivalTime_WithNonExistentChute_ReturnsFailureResult()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("NON_EXISTENT_CHUTE");

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("无法找到格口", result.ErrorMessage);
        Assert.Contains("NON_EXISTENT_CHUTE", result.ErrorMessage);
        Assert.Equal("NON_EXISTENT_CHUTE", result.ChuteId);
        Assert.Equal(0, result.TotalDistanceMm);
        Assert.Equal(0, result.SegmentCount);
    }

    [Fact]
    public void EstimateArrivalTime_WithValidChute_ReturnsSuccessResult()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("CHUTE_1");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorMessage);
        Assert.Equal("CHUTE_1", result.ChuteId);
        Assert.Equal(1000, result.TotalDistanceMm); // 1000mm segment
        Assert.Equal(500.0, result.SpeedMmPerSec);
        Assert.Equal(1, result.SegmentCount);
        Assert.Equal(2000.0, result.EstimatedArrivalTimeMs); // 1000mm / 500mm/s * 1000ms/s = 2000ms
    }

    [Fact]
    public void EstimateArrivalTime_WithCustomSpeed_UsesCustomSpeed()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act - Use 1000 mm/s instead of default 500 mm/s
        var result = _estimator.EstimateArrivalTime("CHUTE_1", speedMmPerSec: 1000.0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("CHUTE_1", result.ChuteId);
        Assert.Equal(1000, result.TotalDistanceMm);
        Assert.Equal(1000.0, result.SpeedMmPerSec);
        Assert.Equal(1000.0, result.EstimatedArrivalTimeMs); // 1000mm / 1000mm/s * 1000ms/s = 1000ms
    }

    [Fact]
    public void EstimateArrivalTime_WithZeroSpeed_ReturnsFailureResult()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("CHUTE_1", speedMmPerSec: 0);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("线速必须大于0", result.ErrorMessage);
    }

    [Fact]
    public void EstimateArrivalTime_WithNegativeSpeed_ReturnsFailureResult()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("CHUTE_1", speedMmPerSec: -100);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Contains("线速必须大于0", result.ErrorMessage);
    }

    [Fact]
    public void EstimateArrivalTime_WithMultipleSegments_CalculatesCorrectTime()
    {
        // Arrange
        var topology = CreateMultiSegmentTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("CHUTE_2");

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal("CHUTE_2", result.ChuteId);
        Assert.Equal(2, result.SegmentCount);
        // Segment 1: 1000mm, Segment 2: 1500mm = 2500mm total
        Assert.Equal(2500, result.TotalDistanceMm);
        // Segment 1: 1000mm/500mm/s = 2s, Segment 2: 1500mm/500mm/s = 3s = 5s total = 5000ms
        Assert.Equal(5000.0, result.EstimatedArrivalTimeMs);
    }

    [Fact]
    public void EstimateArrivalTime_WithDropOffset_IncludesDropOffsetInDistance()
    {
        // Arrange
        var topology = CreateTopologyWithDropOffset();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var result = _estimator.EstimateArrivalTime("CHUTE_1");

        // Assert
        Assert.True(result.IsSuccess);
        // Segment: 1000mm + DropOffset: 200mm = 1200mm
        Assert.Equal(1200, result.TotalDistanceMm);
    }

    [Fact]
    public void EstimateArrivalTime_WithDropOffsetAndCustomSpeed_IncludesDropOffsetTime()
    {
        // Arrange
        var topology = CreateTopologyWithDropOffset();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act - Use custom speed
        var result = _estimator.EstimateArrivalTime("CHUTE_1", speedMmPerSec: 1000.0);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(1200, result.TotalDistanceMm);
        // Segment: 1000mm/1000mm/s = 1s = 1000ms
        // DropOffset: 200mm/1000mm/s = 0.2s = 200ms
        // Total: 1200ms
        Assert.Equal(1200.0, result.EstimatedArrivalTimeMs);
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithValidChute_ReturnsThreshold()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act - Default tolerance factor is 1.5
        var threshold = _estimator.CalculateTimeoutThreshold("CHUTE_1");

        // Assert
        Assert.NotNull(threshold);
        // Estimated time: 2000ms, tolerance: 1.5 = 3000ms
        Assert.Equal(3000.0, threshold.Value);
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithCustomToleranceFactor_UsesCustomFactor()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act - Use tolerance factor of 2.0
        var threshold = _estimator.CalculateTimeoutThreshold("CHUTE_1", toleranceFactor: 2.0);

        // Assert
        Assert.NotNull(threshold);
        // Estimated time: 2000ms, tolerance: 2.0 = 4000ms
        Assert.Equal(4000.0, threshold.Value);
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithZeroToleranceFactor_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _estimator.CalculateTimeoutThreshold("CHUTE_1", toleranceFactor: 0));
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithNegativeToleranceFactor_ThrowsArgumentException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            _estimator.CalculateTimeoutThreshold("CHUTE_1", toleranceFactor: -0.5));
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithNonExistentChute_ReturnsNull()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act
        var threshold = _estimator.CalculateTimeoutThreshold("NON_EXISTENT_CHUTE");

        // Assert
        Assert.Null(threshold);
    }

    [Fact]
    public void CalculateTimeoutThreshold_WithCustomSpeed_UsesCustomSpeed()
    {
        // Arrange
        var topology = CreateSimpleTopology();
        _mockRepository.Setup(r => r.Get()).Returns(topology);

        // Act - Use custom speed of 1000 mm/s
        var threshold = _estimator.CalculateTimeoutThreshold("CHUTE_1", speedMmPerSec: 1000.0);

        // Assert
        Assert.NotNull(threshold);
        // Estimated time with 1000mm/s: 1000ms, tolerance: 1.5 = 1500ms
        Assert.Equal(1500.0, threshold.Value);
    }

    // Helper methods to create test topologies

    private LineTopologyConfig CreateSimpleTopology()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_1",
                NodeName = "Node 1",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to EndIoId of first segment
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Chute 1",
                BoundNodeId = "NODE_1",
                BoundDirection = "Left",
                IsExceptionChute = false,
                DropOffsetMm = 0
            }
        };

        var segments = new List<LineSegmentConfig>
        {
            new LineSegmentConfig
            {
                SegmentId = "SEG_ENTRY_NODE1",
                StartIoId = 1, // Entry IO
                EndIoId = 2, // First node IO
                LengthMm = 1000,
                SpeedMmPerSec = 500.0
            }
        };

        return new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = chutes,
            LineSegments = segments,
            DefaultLineSpeedMmps = 500m
        };
    }

    private LineTopologyConfig CreateMultiSegmentTopology()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_1",
                NodeName = "Node 1",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to EndIoId of first segment
                HasLeftChute = false,
                SupportedSides = new[] { DiverterSide.Straight }
            },
            new WheelNodeConfig
            {
                NodeId = "NODE_2",
                NodeName = "Node 2",
                PositionIndex = 1,
                FrontIoId = 3, // Reference to EndIoId of second segment
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_2" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_2",
                ChuteName = "Chute 2",
                BoundNodeId = "NODE_2",
                BoundDirection = "Left",
                IsExceptionChute = false,
                DropOffsetMm = 0
            }
        };

        var segments = new List<LineSegmentConfig>
        {
            new LineSegmentConfig
            {
                SegmentId = "SEG_ENTRY_NODE1",
                StartIoId = 1, // Entry IO
                EndIoId = 2, // First node IO
                LengthMm = 1000,
                SpeedMmPerSec = 500.0
            },
            new LineSegmentConfig
            {
                SegmentId = "SEG_NODE1_NODE2",
                StartIoId = 2, // First node IO
                EndIoId = 3, // Second node IO
                LengthMm = 1500,
                SpeedMmPerSec = 500.0
            }
        };

        return new LineTopologyConfig
        {
            TopologyId = "MULTI_SEGMENT_TOPOLOGY",
            TopologyName = "Multi Segment Topology",
            WheelNodes = wheelNodes,
            Chutes = chutes,
            LineSegments = segments,
            DefaultLineSpeedMmps = 500m
        };
    }

    private LineTopologyConfig CreateTopologyWithDropOffset()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "NODE_1",
                NodeName = "Node 1",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to EndIoId of first segment
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        var chutes = new List<ChuteConfig>
        {
            new ChuteConfig
            {
                ChuteId = "CHUTE_1",
                ChuteName = "Chute 1",
                BoundNodeId = "NODE_1",
                BoundDirection = "Left",
                IsExceptionChute = false,
                DropOffsetMm = 200 // 200mm drop offset
            }
        };

        var segments = new List<LineSegmentConfig>
        {
            new LineSegmentConfig
            {
                SegmentId = "SEG_ENTRY_NODE1",
                StartIoId = 1, // Entry IO
                EndIoId = 2, // First node IO
                LengthMm = 1000,
                SpeedMmPerSec = 500.0
            }
        };

        return new LineTopologyConfig
        {
            TopologyId = "DROP_OFFSET_TOPOLOGY",
            TopologyName = "Drop Offset Topology",
            WheelNodes = wheelNodes,
            Chutes = chutes,
            LineSegments = segments,
            DefaultLineSpeedMmps = 500m
        };
    }
}
