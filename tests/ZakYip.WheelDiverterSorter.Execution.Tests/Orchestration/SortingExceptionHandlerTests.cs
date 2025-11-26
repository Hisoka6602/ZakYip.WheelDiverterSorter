using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.LineModel.Topology;
using ZakYip.WheelDiverterSorter.Core.Sorting.Orchestration;
using ZakYip.WheelDiverterSorter.Execution;
using ZakYip.WheelDiverterSorter.Execution.Orchestration;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Orchestration;

/// <summary>
/// 分拣异常处理器单元测试
/// </summary>
/// <remarks>
/// PR-1: 测试 SortingExceptionHandler 的异常处理逻辑
/// 覆盖场景：
/// 1. 成功生成异常格口路径
/// 2. 异常格口路径生成失败
/// 3. 创建路径生成失败结果
/// </remarks>
public class SortingExceptionHandlerTests
{
    private readonly Mock<ISwitchingPathGenerator> _mockPathGenerator;
    private readonly Mock<ILogger<SortingExceptionHandler>> _mockLogger;
    private readonly SortingExceptionHandler _handler;

    public SortingExceptionHandlerTests()
    {
        _mockPathGenerator = new Mock<ISwitchingPathGenerator>();
        _mockLogger = new Mock<ILogger<SortingExceptionHandler>>();
        _handler = new SortingExceptionHandler(_mockPathGenerator.Object, _mockLogger.Object);
    }

    #region GenerateExceptionPath Tests

    /// <summary>
    /// 测试成功生成异常格口路径
    /// </summary>
    [Fact]
    public void GenerateExceptionPath_WhenPathGeneratorSucceeds_ReturnsPath()
    {
        // Arrange
        long exceptionChuteId = 99;
        long parcelId = 12345;
        string reason = "路由超时";

        var expectedPath = new SwitchingPath
        {
            TargetChuteId = exceptionChuteId,
            Segments = new List<SwitchingPathSegment>
            {
                new() { DiverterId = 99, TargetDirection = DiverterDirection.Straight, SequenceNumber = 1, TtlMilliseconds = 300 }
            }.AsReadOnly(),
            GeneratedAt = DateTimeOffset.Now,
            FallbackChuteId = exceptionChuteId
        };

        _mockPathGenerator.Setup(g => g.GeneratePath(exceptionChuteId)).Returns(expectedPath);

        // Act
        var result = _handler.GenerateExceptionPath(exceptionChuteId, parcelId, reason);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(expectedPath);
        result!.TargetChuteId.Should().Be(exceptionChuteId);

        _mockPathGenerator.Verify(g => g.GeneratePath(exceptionChuteId), Times.Once);
    }

    /// <summary>
    /// 测试异常格口路径生成失败
    /// </summary>
    [Fact]
    public void GenerateExceptionPath_WhenPathGeneratorFails_ReturnsNull()
    {
        // Arrange
        long exceptionChuteId = 99;
        long parcelId = 12345;
        string reason = "无效格口";

        _mockPathGenerator.Setup(g => g.GeneratePath(exceptionChuteId)).Returns((SwitchingPath?)null);

        // Act
        var result = _handler.GenerateExceptionPath(exceptionChuteId, parcelId, reason);

        // Assert
        result.Should().BeNull();
        _mockPathGenerator.Verify(g => g.GeneratePath(exceptionChuteId), Times.Once);
    }

    /// <summary>
    /// 测试使用不同的异常原因
    /// </summary>
    [Theory]
    [InlineData("路由超时")]
    [InlineData("拓扑不存在")]
    [InlineData("路径健康检查失败")]
    [InlineData("超载处置")]
    public void GenerateExceptionPath_WithDifferentReasons_GeneratesPath(string reason)
    {
        // Arrange
        long exceptionChuteId = 99;
        long parcelId = 12345;

        var expectedPath = new SwitchingPath
        {
            TargetChuteId = exceptionChuteId,
            Segments = new List<SwitchingPathSegment>().AsReadOnly(),
            GeneratedAt = DateTimeOffset.Now,
            FallbackChuteId = exceptionChuteId
        };

        _mockPathGenerator.Setup(g => g.GeneratePath(exceptionChuteId)).Returns(expectedPath);

        // Act
        var result = _handler.GenerateExceptionPath(exceptionChuteId, parcelId, reason);

        // Assert
        result.Should().NotBeNull();
        _mockPathGenerator.Verify(g => g.GeneratePath(exceptionChuteId), Times.Once);
    }

    #endregion

    #region CreatePathGenerationFailureResult Tests

    /// <summary>
    /// 测试创建路径生成失败结果
    /// </summary>
    [Fact]
    public void CreatePathGenerationFailureResult_ReturnsFailureResult()
    {
        // Arrange
        long parcelId = 12345;
        long targetChuteId = 5;
        long exceptionChuteId = 99;
        string reason = "拓扑配置错误";

        // Act
        var result = _handler.CreatePathGenerationFailureResult(parcelId, targetChuteId, exceptionChuteId, reason);

        // Assert
        result.Should().NotBeNull();
        result.IsSuccess.Should().BeFalse();
        result.ParcelId.Should().Be(parcelId.ToString());
        result.TargetChuteId.Should().Be(targetChuteId);
        result.ActualChuteId.Should().Be(0);
        result.ExecutionTimeMs.Should().Be(0);
        result.FailureReason.Should().Contain("路径生成失败");
        result.FailureReason.Should().Contain(reason);
        result.FailureReason.Should().Contain("连异常格口路径都无法生成");
    }

    /// <summary>
    /// 测试不同原因的失败结果
    /// </summary>
    [Theory]
    [InlineData("格口不存在")]
    [InlineData("拓扑无法到达")]
    [InlineData("异常格口配置错误")]
    public void CreatePathGenerationFailureResult_IncludesReasonInMessage(string reason)
    {
        // Arrange
        long parcelId = 99999;
        long targetChuteId = 10;
        long exceptionChuteId = 99;

        // Act
        var result = _handler.CreatePathGenerationFailureResult(parcelId, targetChuteId, exceptionChuteId, reason);

        // Assert
        result.FailureReason.Should().Contain(reason);
    }

    /// <summary>
    /// 测试结果包含正确的ID
    /// </summary>
    [Fact]
    public void CreatePathGenerationFailureResult_CorrectIds()
    {
        // Arrange
        long parcelId = 54321;
        long targetChuteId = 7;
        long exceptionChuteId = 88;
        string reason = "测试原因";

        // Act
        var result = _handler.CreatePathGenerationFailureResult(parcelId, targetChuteId, exceptionChuteId, reason);

        // Assert
        result.ParcelId.Should().Be("54321");
        result.TargetChuteId.Should().Be(7);
        result.ActualChuteId.Should().Be(0);
        result.IsOverloadException.Should().BeFalse();
    }

    #endregion

    #region Constructor Tests

    /// <summary>
    /// 测试构造函数参数校验 - 空路径生成器
    /// </summary>
    [Fact]
    public void Constructor_WithNullPathGenerator_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SortingExceptionHandler(null!, _mockLogger.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("pathGenerator");
    }

    /// <summary>
    /// 测试构造函数参数校验 - 空日志器
    /// </summary>
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new SortingExceptionHandler(_mockPathGenerator.Object, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    #endregion
}
