using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;


using ZakYip.WheelDiverterSorter.Core.LineModel.Chutes;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;namespace ZakYip.WheelDiverterSorter.Core.Tests;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

public class DefaultSwitchingPathGeneratorTests
{
    private readonly Mock<IRouteConfigurationRepository> _mockRepository;
    private readonly DefaultSwitchingPathGenerator _generator;

    public DefaultSwitchingPathGeneratorTests()
    {
        _mockRepository = new Mock<IRouteConfigurationRepository>();
        _generator = new DefaultSwitchingPathGenerator(_mockRepository.Object);
    }

    [Fact]
    public void GeneratePath_WithValidChuteId_ReturnsValidPath()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    SequenceNumber = 2
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(chuteId, result.TargetChuteId);
        Assert.Equal(2, result.Segments.Count);
        Assert.Equal(1, result.Segments[0].DiverterId);
        Assert.Equal(DiverterDirection.Straight, result.Segments[0].TargetDirection);
        Assert.Equal(1, result.Segments[0].SequenceNumber);
        Assert.Equal(2, result.Segments[1].DiverterId);
        Assert.Equal(DiverterDirection.Left, result.Segments[1].TargetDirection);
        Assert.Equal(2, result.Segments[1].SequenceNumber);
        Assert.Equal(WellKnownChuteIds.DefaultException, result.FallbackChuteId);
    }

    [Fact]
    public void GeneratePath_WithNullChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithEmptyChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WithWhitespaceChuteId_ReturnsNull()
    {
        // Act
        var result = _generator.GeneratePath(0);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void GeneratePath_WhenConfigNotFound_ReturnsNull()
    {
        // Arrange
        var chuteId = 999;
        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns((ChuteRouteConfiguration?)null);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.Null(result);
        _mockRepository.Verify(r => r.GetByChuteId(chuteId), Times.Once);
    }

    [Fact]
    public void GeneratePath_WhenConfigHasEmptyDiverters_ReturnsNull()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>()
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void GeneratePath_OrdersSegmentsBySequenceNumber()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 3,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 3
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Left,
                    SequenceNumber = 2
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Segments.Count);
        Assert.Equal(1, result.Segments[0].DiverterId);
        Assert.Equal(2, result.Segments[1].DiverterId);
        Assert.Equal(3, result.Segments[2].DiverterId);
    }

    [Fact]
    public void GeneratePath_CalculatesTtlDynamicallyBasedOnSegmentConfiguration()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1000.0,
                    SegmentToleranceTimeMs = 2000
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        // TTL = (5.0 / 1.0) * 1000 + 2000 = 7000ms
        Assert.All(result.Segments, segment => Assert.Equal(7000, segment.TtlMilliseconds));
    }

    [Fact]
    public void Constructor_WithNullRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultSwitchingPathGenerator((IRouteConfigurationRepository)null!));
    }

    [Fact]
    public void GeneratePath_SetsGeneratedAtToCurrentTime()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);
        var before = DateTimeOffset.Now;

        // Act
        var result = _generator.GeneratePath(chuteId);
        var after = DateTimeOffset.Now;

        // Assert
        Assert.NotNull(result);
        Assert.True(result.GeneratedAt >= before);
        Assert.True(result.GeneratedAt <= after);
    }

    [Theory]
    [InlineData(DiverterDirection.Straight)]
    [InlineData(DiverterDirection.Left)]
    [InlineData(DiverterDirection.Right)]
    public void GeneratePath_PreservesTargetDirection(DiverterDirection direction)
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = direction,
                    SequenceNumber = 1
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(direction, result.Segments[0].TargetDirection);
    }

    [Fact]
    public void GeneratePath_CalculatesTtlWithDifferentSegmentSpeeds()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 10000.0,
                    SegmentSpeedMmPerSecond = 2000.0,
                    SegmentToleranceTimeMs = 1000
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 2,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1500.0,
                    SegmentToleranceTimeMs = 1500
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.Segments.Count);
        // Segment 1: (10.0 / 2.0) * 1000 + 1000 = 6000ms
        Assert.Equal(6000, result.Segments[0].TtlMilliseconds);
        // Segment 2: (5.0 / 1.5) * 1000 + 1500 = 3333 + 1500 = 4834ms (rounded up)
        Assert.Equal(4834, result.Segments[1].TtlMilliseconds);
    }

    [Fact]
    public void GeneratePath_EnforcesMinimumTtl()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 100.0,  // Very short segment
                    SegmentSpeedMmPerSecond = 10000.0,  // Very fast
                    SegmentToleranceTimeMs = 0  // No tolerance
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        // Should enforce minimum TTL of 1000ms even though calculated value is much smaller
        Assert.All(result.Segments, segment => Assert.True(segment.TtlMilliseconds >= 1000));
    }

    [Fact]
    public void GeneratePath_CalculatesTtlForMultipleSegmentsIndependently()
    {
        // Arrange
        var chuteId = 1;
        var config = new ChuteRouteConfiguration
        {
            ChuteId = chuteId,
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 1,
                    SegmentLengthMm = 5000.0,
                    SegmentSpeedMmPerSecond = 1000.0,
                    SegmentToleranceTimeMs = 2000
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 2,
                    TargetDirection = DiverterDirection.Straight,
                    SequenceNumber = 2,
                    SegmentLengthMm = 8000.0,
                    SegmentSpeedMmPerSecond = 1500.0,
                    SegmentToleranceTimeMs = 2500
                },
                new DiverterConfigurationEntry
                {
                    DiverterId = 3,
                    TargetDirection = DiverterDirection.Right,
                    SequenceNumber = 3,
                    SegmentLengthMm = 6000.0,
                    SegmentSpeedMmPerSecond = 2000.0,
                    SegmentToleranceTimeMs = 1500
                }
            }
        };

        _mockRepository.Setup(r => r.GetByChuteId(chuteId)).Returns(config);

        // Act
        var result = _generator.GeneratePath(chuteId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.Segments.Count);
        // Segment 1: (5.0 / 1.0) * 1000 + 2000 = 7000ms
        Assert.Equal(7000, result.Segments[0].TtlMilliseconds);
        // Segment 2: (8.0 / 1.5) * 1000 + 2500 = 5333 + 2500 = 7834ms (rounded up)
        Assert.Equal(7834, result.Segments[1].TtlMilliseconds);
        // Segment 3: (6.0 / 2.0) * 1000 + 1500 = 4500ms
        Assert.Equal(4500, result.Segments[2].TtlMilliseconds);
    }

    [Theory]
    [InlineData(1000, 400, true)]  // Tolerance 400ms < 500ms (1000/2), should be valid
    [InlineData(1000, 500, false)] // Tolerance 500ms = 500ms (1000/2), should be invalid (not strictly less than)
    [InlineData(1000, 600, false)] // Tolerance 600ms > 500ms (1000/2), should be invalid
    [InlineData(2000, 999, true)]  // Tolerance 999ms < 1000ms (2000/2), should be valid
    [InlineData(2000, 1000, false)] // Tolerance 1000ms = 1000ms (2000/2), should be invalid
    [InlineData(500, 200, true)]   // Tolerance 200ms < 250ms (500/2), should be valid
    [InlineData(500, 250, false)]  // Tolerance 250ms = 250ms (500/2), should be invalid
    public void ValidateToleranceTime_WithVariousIntervals_ReturnsExpectedResult(
        int parcelIntervalMs, int toleranceMs, bool expectedValid)
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentLengthMm = 5000.0,
            SegmentSpeedMmPerSecond = 1000.0,
            SegmentToleranceTimeMs = toleranceMs
        };

        // Act
        var result = DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, parcelIntervalMs);

        // Assert
        Assert.Equal(expectedValid, result);
    }

    [Fact]
    public void ValidateToleranceTime_WithZeroInterval_ThrowsArgumentException()
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentToleranceTimeMs = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, 0));
    }

    [Fact]
    public void ValidateToleranceTime_WithNegativeInterval_ThrowsArgumentException()
    {
        // Arrange
        var segmentConfig = new DiverterConfigurationEntry
        {
            DiverterId = 1,
            TargetDirection = DiverterDirection.Straight,
            SequenceNumber = 1,
            SegmentToleranceTimeMs = 1000
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            DefaultSwitchingPathGenerator.ValidateToleranceTime(segmentConfig, -100));
    }

    #region PR-TOPO02: N 摆轮模型路径生成测试

    [Fact]
    public void GeneratePath_FromTopology_N1_LeftChute_GeneratesCorrectPath()
    {
        // Arrange - N=1 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(1);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求左侧格口
        var result = generator.GeneratePath(1); // 左侧格口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(1, result.TargetChuteId);
        Assert.Single(result.Segments);
        Assert.Equal(DiverterDirection.Left, result.Segments[0].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N1_RightChute_GeneratesCorrectPath()
    {
        // Arrange - N=1 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(1);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求右侧格口
        var result = generator.GeneratePath(2); // 右侧格口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(2, result.TargetChuteId);
        Assert.Single(result.Segments);
        Assert.Equal(DiverterDirection.Right, result.Segments[0].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N1_ExceptionChute_GeneratesAllStraightPath()
    {
        // Arrange - N=1 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(1);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求异常口
        var result = generator.GeneratePath(999); // 异常口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(999, result.TargetChuteId);
        Assert.Single(result.Segments);
        Assert.Equal(DiverterDirection.Straight, result.Segments[0].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N3_MiddleChute_GeneratesCorrectPath()
    {
        // Arrange - N=3 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(3);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求第2个摆轮的左侧格口
        var result = generator.GeneratePath(3); // D2的左侧格口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(3, result.TargetChuteId);
        Assert.Equal(3, result.Segments.Count);
        // D1: 直通
        Assert.Equal(DiverterDirection.Straight, result.Segments[0].TargetDirection);
        // D2: 左
        Assert.Equal(DiverterDirection.Left, result.Segments[1].TargetDirection);
        // D3: 直通（包裹已经被分走了）
        Assert.Equal(DiverterDirection.Straight, result.Segments[2].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N3_LastChute_GeneratesCorrectPath()
    {
        // Arrange - N=3 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(3);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求第3个摆轮的右侧格口
        var result = generator.GeneratePath(6); // D3的右侧格口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(6, result.TargetChuteId);
        Assert.Equal(3, result.Segments.Count);
        // D1: 直通
        Assert.Equal(DiverterDirection.Straight, result.Segments[0].TargetDirection);
        // D2: 直通
        Assert.Equal(DiverterDirection.Straight, result.Segments[1].TargetDirection);
        // D3: 右
        Assert.Equal(DiverterDirection.Right, result.Segments[2].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N4_PathSegmentsLinearlyIncreaseWithN()
    {
        // Arrange - N=4 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(4);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求第4个摆轮的格口
        var result = generator.GeneratePath(7); // D4的左侧格口

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Segments.Count); // N=4 时应该有4个段
        // D1-D3: 直通
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(DiverterDirection.Straight, result.Segments[i].TargetDirection);
        }
        // D4: 左
        Assert.Equal(DiverterDirection.Left, result.Segments[3].TargetDirection);
    }

    [Fact]
    public void GeneratePath_FromTopology_N4_ExceptionChute_AllStraight()
    {
        // Arrange - N=4 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(4);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求异常口
        var result = generator.GeneratePath(999);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(4, result.Segments.Count);
        Assert.All(result.Segments, s => Assert.Equal(DiverterDirection.Straight, s.TargetDirection));
    }

    [Fact]
    public void GeneratePath_FromTopology_WithUnknownChute_ReturnsNull()
    {
        // Arrange - N=3 摆轮配置
        var mockTopologyRepo = new Mock<IChutePathTopologyRepository>();
        var config = CreateTopologyConfig(3);
        mockTopologyRepo.Setup(r => r.Get()).Returns(config);
        
        var generator = new DefaultSwitchingPathGenerator(mockTopologyRepo.Object);

        // Act - 请求不存在的格口
        var result = generator.GeneratePath(12345);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void Constructor_WithTopologyRepository_ThrowsOnNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new DefaultSwitchingPathGenerator((IChutePathTopologyRepository)null!));
    }

    #region 测试辅助常量和方法

    /// <summary>
    /// 测试用传感器ID偏移量（与生产代码保持一致）
    /// </summary>
    private const int TestSensorIdOffset = 100;

    /// <summary>
    /// 测试用异常口ID
    /// </summary>
    private const long TestExceptionChuteId = 999;

    /// <summary>
    /// 计算左侧格口ID
    /// </summary>
    /// <param name="diverterIndex">摆轮索引（从1开始）</param>
    private static long CalculateLeftChuteId(int diverterIndex) => (diverterIndex - 1) * 2 + 1;

    /// <summary>
    /// 计算右侧格口ID
    /// </summary>
    /// <param name="diverterIndex">摆轮索引（从1开始）</param>
    private static long CalculateRightChuteId(int diverterIndex) => (diverterIndex - 1) * 2 + 2;

    /// <summary>
    /// 创建 N 摆轮拓扑配置用于测试
    /// </summary>
    /// <remarks>
    /// 格口编号规则：
    /// - 摆轮 i 的左侧格口 ID = (i-1)*2 + 1
    /// - 摆轮 i 的右侧格口 ID = (i-1)*2 + 2
    /// - 异常口 ID = 999
    /// </remarks>
    private static ChutePathTopologyConfig CreateTopologyConfig(int diverterCount)
    {
        var nodes = new List<DiverterPathNode>();
        for (int i = 1; i <= diverterCount; i++)
        {
            nodes.Add(new DiverterPathNode
            {
                DiverterId = i,
                DiverterName = $"摆轮D{i}",
                PositionIndex = i,
                SegmentId = i,
                FrontSensorId = i + TestSensorIdOffset,
                LeftChuteIds = new List<long> { CalculateLeftChuteId(i) },
                RightChuteIds = new List<long> { CalculateRightChuteId(i) }
            });
        }

        return new ChutePathTopologyConfig
        {
            TopologyId = $"test-n{diverterCount}",
            TopologyName = $"N={diverterCount}测试拓扑",
            EntrySensorId = 1,
            DiverterNodes = nodes,
            ExceptionChuteId = TestExceptionChuteId
        };
    }

    #endregion

    #endregion
}
