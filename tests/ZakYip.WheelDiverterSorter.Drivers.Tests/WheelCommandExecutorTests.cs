using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Results;
using ZakYip.WheelDiverterSorter.Drivers;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests;

/// <summary>
/// WheelCommandExecutor 单元测试
/// </summary>
/// <remarks>
/// 测试覆盖三类典型场景：正常执行、超时、设备错误
/// </remarks>
public class WheelCommandExecutorTests
{
    private readonly Mock<IWheelDiverterDriverManager> _mockDriverManager;
    private readonly Mock<IWheelDiverterDriver> _mockDriver;
    private readonly Mock<ILogger<WheelCommandExecutor>> _mockLogger;
    private readonly WheelCommandExecutor _executor;

    public WheelCommandExecutorTests()
    {
        _mockDriverManager = new Mock<IWheelDiverterDriverManager>();
        _mockDriver = new Mock<IWheelDiverterDriver>();
        _mockLogger = new Mock<ILogger<WheelCommandExecutor>>();
        
        _mockDriver.Setup(d => d.DiverterId).Returns("D001");
        
        _executor = new WheelCommandExecutor(
            _mockDriverManager.Object,
            _mockLogger.Object);
    }

    #region 正常执行场景

    [Fact(DisplayName = "左转命令执行成功应返回成功结果")]
    public async Task ExecuteAsync_TurnLeft_Success_ReturnsSuccessResult()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        _mockDriver.Verify(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "右转命令执行成功应返回成功结果")]
    public async Task ExecuteAsync_TurnRight_Success_ReturnsSuccessResult()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnRightAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = WheelCommand.TurnRight("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        _mockDriver.Verify(d => d.TurnRightAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact(DisplayName = "直通命令执行成功应返回成功结果")]
    public async Task ExecuteAsync_PassThrough_Success_ReturnsSuccessResult()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var command = WheelCommand.PassThrough("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.True(result.IsSuccess);
        _mockDriver.Verify(d => d.PassThroughAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    #endregion

    #region 设备错误场景

    [Fact(DisplayName = "摆轮未找到应返回WheelNotFound错误")]
    public async Task ExecuteAsync_DriverNotFound_ReturnsWheelNotFoundError()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D999")).Returns((IWheelDiverterDriver?)null);

        var command = WheelCommand.TurnLeft("D999", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelNotFound, result.ErrorCode);
        Assert.Contains("D999", result.ErrorMessage);
    }

    [Fact(DisplayName = "驱动器返回失败应返回WheelCommandFailed错误")]
    public async Task ExecuteAsync_DriverReturnsFalse_ReturnsWheelCommandFailedError()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelCommandFailed, result.ErrorCode);
    }

    [Fact(DisplayName = "驱动器抛出WheelDriverException应正确转换为OperationResult")]
    public async Task ExecuteAsync_DriverThrowsWheelDriverException_ReturnsCorrectErrorCode()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(WheelDriverException.CommunicationError("D001", new IOException("网络断开")));

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelCommunicationError, result.ErrorCode);
    }

    [Fact(DisplayName = "驱动器抛出普通异常应返回WheelCommunicationError")]
    public async Task ExecuteAsync_DriverThrowsException_ReturnsWheelCommunicationError()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("设备状态异常"));

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromSeconds(1));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelCommunicationError, result.ErrorCode);
        Assert.Contains("设备状态异常", result.ErrorMessage);
    }

    #endregion

    #region 超时场景

    [Fact(DisplayName = "命令执行超时应返回WheelCommandTimeout错误")]
    public async Task ExecuteAsync_Timeout_ReturnsWheelCommandTimeoutError()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                // 模拟长时间执行，超过超时时间
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return true;
            });

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromMilliseconds(100));

        // Act
        var result = await _executor.ExecuteAsync(command);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.WheelCommandTimeout, result.ErrorCode);
    }

    [Fact(DisplayName = "外部取消应向上传递OperationCanceledException")]
    public async Task ExecuteAsync_ExternalCancellation_ThrowsOperationCanceledException()
    {
        // Arrange
        _mockDriverManager.Setup(m => m.GetDriver("D001")).Returns(_mockDriver.Object);
        _mockDriver.Setup(d => d.TurnLeftAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken ct) =>
            {
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
                return true;
            });

        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromSeconds(10));
        using var cts = new CancellationTokenSource();

        // Act & Assert
        cts.CancelAfter(TimeSpan.FromMilliseconds(50));
        // TaskCanceledException 继承自 OperationCanceledException
        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await _executor.ExecuteAsync(command, cts.Token));
    }

    #endregion

    #region 参数验证

    [Fact(DisplayName = "空命令应抛出ArgumentNullException")]
    public async Task ExecuteAsync_NullCommand_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(
            async () => await _executor.ExecuteAsync(null!));
    }

    [Fact(DisplayName = "构造函数空driverManager应抛出ArgumentNullException")]
    public void Constructor_NullDriverManager_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new WheelCommandExecutor(null!, _mockLogger.Object));
    }

    [Fact(DisplayName = "构造函数空logger应抛出ArgumentNullException")]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => new WheelCommandExecutor(_mockDriverManager.Object, null!));
    }

    #endregion

    #region WheelCommand 静态工厂方法测试

    [Fact(DisplayName = "WheelCommand.TurnLeft应创建正确的左转命令")]
    public void WheelCommand_TurnLeft_CreatesCorrectCommand()
    {
        // Arrange & Act
        var command = WheelCommand.TurnLeft("D001", TimeSpan.FromMilliseconds(500), 1);

        // Assert
        Assert.Equal("D001", command.DiverterId);
        Assert.Equal(DiverterDirection.Left, command.Direction);
        Assert.Equal(TimeSpan.FromMilliseconds(500), command.Timeout);
        Assert.Equal(1, command.SequenceNumber);
    }

    [Fact(DisplayName = "WheelCommand.TurnRight应创建正确的右转命令")]
    public void WheelCommand_TurnRight_CreatesCorrectCommand()
    {
        // Arrange & Act
        var command = WheelCommand.TurnRight("D002", TimeSpan.FromSeconds(1));

        // Assert
        Assert.Equal("D002", command.DiverterId);
        Assert.Equal(DiverterDirection.Right, command.Direction);
        Assert.Equal(TimeSpan.FromSeconds(1), command.Timeout);
        Assert.Null(command.SequenceNumber);
    }

    [Fact(DisplayName = "WheelCommand.PassThrough应创建正确的直通命令")]
    public void WheelCommand_PassThrough_CreatesCorrectCommand()
    {
        // Arrange & Act
        var command = WheelCommand.PassThrough("D003", TimeSpan.FromMilliseconds(200), 3);

        // Assert
        Assert.Equal("D003", command.DiverterId);
        Assert.Equal(DiverterDirection.Straight, command.Direction);
        Assert.Equal(TimeSpan.FromMilliseconds(200), command.Timeout);
        Assert.Equal(3, command.SequenceNumber);
    }

    #endregion
}
