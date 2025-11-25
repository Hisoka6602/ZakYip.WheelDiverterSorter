using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel;

/// <summary>
/// 测试 ChuteAssignmentTimeoutCalculator 格口分配超时计算功能
/// </summary>
public class ChuteAssignmentTimeoutCalculatorTests
{
    private readonly Mock<ILineTopologyRepository> _mockTopologyRepository;
    private readonly Mock<ISystemConfigurationRepository> _mockSystemConfigRepository;
    private readonly Mock<ILogger<ChuteAssignmentTimeoutCalculator>> _mockLogger;
    private readonly ChuteAssignmentTimeoutCalculator _calculator;

    public ChuteAssignmentTimeoutCalculatorTests()
    {
        _mockTopologyRepository = new Mock<ILineTopologyRepository>();
        _mockSystemConfigRepository = new Mock<ISystemConfigurationRepository>();
        _mockLogger = new Mock<ILogger<ChuteAssignmentTimeoutCalculator>>();
        _calculator = new ChuteAssignmentTimeoutCalculator(
            _mockTopologyRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullTopologyRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ChuteAssignmentTimeoutCalculator(
                null!,
                _mockSystemConfigRepository.Object,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullSystemConfigRepository_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ChuteAssignmentTimeoutCalculator(
                _mockTopologyRepository.Object,
                null!,
                _mockLogger.Object));
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new ChuteAssignmentTimeoutCalculator(
                _mockTopologyRepository.Object,
                _mockSystemConfigRepository.Object,
                null!));
    }

    #endregion

    #region Normal Path Tests

    [Fact]
    public void CalculateTimeoutSeconds_WithSingleSegmentPath_ReturnsCorrectTimeout()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：1000mm / 500mm/s = 2秒 * 0.9 = 1.8秒
        Assert.Equal(1.8m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithMultipleSegments_CalculatesOnlyToFirstWheel()
    {
        // Arrange
        var topology = CreateMultiSegmentTopology();
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 注意：计算器只计算到第一个摆轮的路径，不包括第二个摆轮
        // 只有段1：1000mm / 500mm/s = 2秒 * 0.9 = 1.8秒
        Assert.Equal(1.8m, timeout);
    }

    #endregion

    #region Safety Factor Boundary Tests

    [Fact]
    public void CalculateTimeoutSeconds_WithSafetyFactorAtMinimum_ClampsTo0Point1()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.05m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 安全系数0.05应该被Clamp到0.1
        // 计算：1000mm / 500mm/s = 2秒 * 0.1 = 0.2秒
        Assert.Equal(0.2m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithSafetyFactorExactly0Point1_UsesValue()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.1m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：1000mm / 500mm/s = 2秒 * 0.1 = 0.2秒
        Assert.Equal(0.2m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithSafetyFactorExactly1Point0_UsesValue()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 1.0m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：1000mm / 500mm/s = 2秒 * 1.0 = 2.0秒
        Assert.Equal(2.0m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithSafetyFactorAboveMaximum_ClampsTo1Point0()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 1.5m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 安全系数1.5应该被Clamp到1.0
        // 计算：1000mm / 500mm/s = 2秒 * 1.0 = 2.0秒
        Assert.Equal(2.0m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithSafetyFactorZero_ClampsTo0Point1()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 安全系数0应该被Clamp到0.1
        // 计算：1000mm / 500mm/s = 2秒 * 0.1 = 0.2秒
        Assert.Equal(0.2m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithNegativeSafetyFactor_ClampsTo0Point1()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: -0.5m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 负数安全系数应该被Clamp到0.1
        // 计算：1000mm / 500mm/s = 2秒 * 0.1 = 0.2秒
        Assert.Equal(0.2m, timeout);
    }

    #endregion

    #region Fallback Scenarios Tests

    [Fact]
    public void CalculateTimeoutSeconds_WithNullTopology_ReturnsFallbackTimeout()
    {
        // Arrange
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        LineTopologyConfig? nullTopology = null;
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(nullTopology!);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithNoWheelNodes_ReturnsFallbackTimeout()
    {
        // Arrange
        var topology = CreateTopologyWithoutWheelNodes();
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithNoPathToFirstWheel_ReturnsFallbackTimeout()
    {
        // Arrange
        var topology = CreateTopologyWithMissingSegment();
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithZeroSegmentSpeed_ReturnsFallbackTimeout()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 0);
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithNegativeSegmentSpeed_ReturnsFallbackTimeout()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: -500.0);
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithNullSystemConfig_UsesDefaultFallbackTimeout()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        SystemConfiguration? nullConfig = null;
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(nullConfig!);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 没有系统配置时会创建默认的 ChuteAssignmentTimeoutOptions
        // 由于拓扑配置正常，应该能够正常计算：1000mm / 500mm/s = 2秒 * 0.9 = 1.8秒
        Assert.Equal(1.8m, timeout);
    }

    #endregion

    #region Calculation Precision Tests

    [Fact]
    public void CalculateTimeoutSeconds_WithVerySmallTimeout_ReturnsCalculatedValue()
    {
        // Arrange - 极小的长度和极大的速度，导致计算结果非常小但大于0
        var topology = CreateSimpleTopology(segmentLength: 0.001, segmentSpeed: 1000000.0);
        var systemConfig = CreateSystemConfig(fallbackTimeout: 5m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.1m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：0.001mm / 1000000mm/s * 0.1 = 0.0000000001秒（非常小但大于0）
        // 由于结果大于0，不会触发降级逻辑
        Assert.True(timeout > 0);
        Assert.True(timeout < 0.001m); // 验证是一个非常小的值
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithPreciseCalculation_ReturnsAccurateResult()
    {
        // Arrange - 验证计算精度
        var topology = CreateSimpleTopology(segmentLength: 2345, segmentSpeed: 678.9);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.75m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：2345mm / 678.9mm/s ≈ 3.4539秒 * 0.75 ≈ 2.5904秒
        var expected = (decimal)(2345.0 / 678.9) * 0.75m;
        Assert.Equal(expected, timeout, 4); // 精确到4位小数
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithVeryLongPath_HandlesLargeValues()
    {
        // Arrange - 测试大数值处理
        var topology = CreateSimpleTopology(segmentLength: 100000, segmentSpeed: 100.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：100000mm / 100mm/s = 1000秒 * 0.9 = 900秒
        Assert.Equal(900m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithVeryHighSpeed_HandlesSmallResult()
    {
        // Arrange - 测试高速场景
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 5000.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 计算：1000mm / 5000mm/s = 0.2秒 * 0.9 = 0.18秒
        Assert.Equal(0.18m, timeout);
    }

    #endregion

    #region Exception Handling Tests

    [Fact]
    public void CalculateTimeoutSeconds_WhenTopologyRepositoryThrows_ReturnsHardcodedFallback()
    {
        // Arrange
        _mockTopologyRepository.Setup(r => r.Get()).Throws(new InvalidOperationException("Database error"));
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(CreateSystemConfig());

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 异常时应该返回硬编码的降级值 5 秒
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WhenSystemConfigRepositoryThrows_ReturnsHardcodedFallback()
    {
        // Arrange
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Throws(new InvalidOperationException("Config error"));

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        // 系统配置Repository异常时应该返回硬编码的降级值 5 秒
        Assert.Equal(5m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WhenBothRepositoriesThrow_ReturnsHardcodedFallback()
    {
        // Arrange
        _mockTopologyRepository.Setup(r => r.Get()).Throws(new InvalidOperationException("Topology error"));
        _mockSystemConfigRepository.Setup(r => r.Get()).Throws(new InvalidOperationException("Config error"));

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(5m, timeout);
    }

    #endregion

    #region Custom Configuration Tests

    [Fact]
    public void CalculateTimeoutSeconds_WithCustomFallbackTimeout_UsesCustomValue()
    {
        // Arrange
        var topology = CreateTopologyWithoutWheelNodes(); // 触发降级
        var systemConfig = CreateSystemConfig(fallbackTimeout: 10m);
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 1, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(10m, timeout);
    }

    [Fact]
    public void CalculateTimeoutSeconds_WithDifferentLineId_CalculatesCorrectly()
    {
        // Arrange - LineId 不影响计算，但要验证能正确处理
        var topology = CreateSimpleTopology(segmentLength: 1000, segmentSpeed: 500.0);
        var systemConfig = CreateSystemConfig();
        
        _mockTopologyRepository.Setup(r => r.Get()).Returns(topology);
        _mockSystemConfigRepository.Setup(r => r.Get()).Returns(systemConfig);

        var context = new ChuteAssignmentTimeoutContext(LineId: 999, SafetyFactor: 0.9m);

        // Act
        var timeout = _calculator.CalculateTimeoutSeconds(context);

        // Assert
        Assert.Equal(1.8m, timeout);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// 创建简单的单段拓扑
    /// </summary>
    private LineTopologyConfig CreateSimpleTopology(double segmentLength, double segmentSpeed)
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "WHEEL_1",
                NodeName = "First Wheel",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to EndIoId of first segment
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        var segments = new List<LineSegmentConfig>
        {
            new LineSegmentConfig
            {
                SegmentId = 1,
                StartIoId = 1, // Entry IO
                EndIoId = 2, // First wheel IO
                LengthMm = segmentLength,
                SpeedMmPerSec = segmentSpeed
            }
        };

        return new LineTopologyConfig
        {
            TopologyId = "TEST_TOPOLOGY",
            TopologyName = "Test Topology",
            WheelNodes = wheelNodes,
            Chutes = new List<ChuteConfig>(),
            LineSegments = segments,
            DefaultLineSpeedMmps = 500m
        };
    }

    /// <summary>
    /// 创建多段路径拓扑
    /// </summary>
    private LineTopologyConfig CreateMultiSegmentTopology()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "WHEEL_1",
                NodeName = "First Wheel",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to EndIoId of first segment
                HasLeftChute = false,
                SupportedSides = new[] { DiverterSide.Straight }
            },
            new WheelNodeConfig
            {
                NodeId = "WHEEL_2",
                NodeName = "Second Wheel",
                PositionIndex = 1,
                FrontIoId = 3, // Reference to EndIoId of second segment
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_2" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        var segments = new List<LineSegmentConfig>
        {
            new LineSegmentConfig
            {
                SegmentId = 1,
                StartIoId = 1, // Entry IO
                EndIoId = 2, // First wheel IO
                LengthMm = 1000,
                SpeedMmPerSec = 500.0
            },
            new LineSegmentConfig
            {
                SegmentId = 2,
                StartIoId = 2, // First wheel IO
                EndIoId = 3, // Second wheel IO
                LengthMm = 1500,
                SpeedMmPerSec = 500.0
            }
        };

        return new LineTopologyConfig
        {
            TopologyId = "MULTI_SEGMENT_TOPOLOGY",
            TopologyName = "Multi Segment Topology",
            WheelNodes = wheelNodes,
            Chutes = new List<ChuteConfig>(),
            LineSegments = segments,
            DefaultLineSpeedMmps = 500m
        };
    }

    /// <summary>
    /// 创建没有摆轮节点的拓扑
    /// </summary>
    private LineTopologyConfig CreateTopologyWithoutWheelNodes()
    {
        return new LineTopologyConfig
        {
            TopologyId = "NO_WHEEL_TOPOLOGY",
            TopologyName = "No Wheel Topology",
            WheelNodes = new List<WheelNodeConfig>(),
            Chutes = new List<ChuteConfig>(),
            LineSegments = new List<LineSegmentConfig>(),
            DefaultLineSpeedMmps = 500m
        };
    }

    /// <summary>
    /// 创建缺少路径段的拓扑
    /// </summary>
    private LineTopologyConfig CreateTopologyWithMissingSegment()
    {
        var wheelNodes = new List<WheelNodeConfig>
        {
            new WheelNodeConfig
            {
                NodeId = "WHEEL_1",
                NodeName = "First Wheel",
                PositionIndex = 0,
                FrontIoId = 2, // Reference to a wheel IO
                HasLeftChute = true,
                LeftChuteIds = new[] { "CHUTE_1" },
                SupportedSides = new[] { DiverterSide.Left, DiverterSide.Straight }
            }
        };

        // 故意不添加从 ENTRY 到 WHEEL_1 的段
        return new LineTopologyConfig
        {
            TopologyId = "MISSING_SEGMENT_TOPOLOGY",
            TopologyName = "Missing Segment Topology",
            WheelNodes = wheelNodes,
            Chutes = new List<ChuteConfig>(),
            LineSegments = new List<LineSegmentConfig>(), // 空的段列表
            DefaultLineSpeedMmps = 500m
        };
    }

    /// <summary>
    /// 创建系统配置
    /// </summary>
    private SystemConfiguration CreateSystemConfig(decimal fallbackTimeout = 5m)
    {
        return new SystemConfiguration
        {
            ConfigName = "system",
            ChuteAssignmentTimeout = new ChuteAssignmentTimeoutOptions
            {
                SafetyFactor = 0.9m,
                FallbackTimeoutSeconds = fallbackTimeout
            }
        };
    }

    #endregion
}
