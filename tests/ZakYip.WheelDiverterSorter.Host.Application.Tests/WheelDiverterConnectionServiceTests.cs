using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Application.Services.WheelDiverter;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.LineModel.Runtime.Health;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Host.Application.Tests;

/// <summary>
/// 摆轮连接服务单元测试
/// </summary>
/// <remarks>
/// 测试 WheelDiverterConnectionService 的摆轮管理功能：
/// - PassThroughAllAsync 方法
/// - RunAllAsync 方法
/// - StopAllAsync 方法
/// - 错误处理和健康状态更新
/// 
/// TD-052: 已完善 PassThroughAllAsync 的集成测试（当前 PR）
/// </remarks>
public class WheelDiverterConnectionServiceTests
{
    private readonly Mock<IWheelDiverterConfigurationRepository> _mockConfigRepository;
    private readonly Mock<ISystemConfigurationRepository> _mockSystemConfigRepository;
    private readonly Mock<IWheelDiverterDriverManager> _mockDriverManager;
    private readonly Mock<INodeHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<WheelDiverterConnectionService>> _mockLogger;

    public WheelDiverterConnectionServiceTests()
    {
        _mockConfigRepository = new Mock<IWheelDiverterConfigurationRepository>();
        _mockSystemConfigRepository = new Mock<ISystemConfigurationRepository>();
        _mockDriverManager = new Mock<IWheelDiverterDriverManager>();
        _mockHealthRegistry = new Mock<INodeHealthRegistry>();
        _mockClock = new Mock<ISystemClock>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _mockLogger = new Mock<ILogger<WheelDiverterConnectionService>>();
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenConfigRepositoryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            null!,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenDriverManagerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            null!,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenHealthRegistryIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            null!,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenClockIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            null!,
            _mockSafeExecutor.Object,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenSafeExecutorIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            null!,
            _mockLogger.Object));
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentNullException_WhenLoggerIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            null!));
    }

    [Fact]
    public void Constructor_ShouldSucceed_WithValidParameters()
    {
        // Act
        var service = new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }

    /// <summary>
    /// 测试 PassThroughAllAsync - 所有摆轮成功接收 PassThrough 命令
    /// TD-052: 完善 PassThroughAllAsync 集成测试
    /// </summary>
    [Fact]
    public async Task PassThroughAllAsync_ShouldSucceed_WhenAllDriversSucceed()
    {
        // Arrange
        var mockDriver1 = new Mock<IWheelDiverterDriver>();
        var mockDriver2 = new Mock<IWheelDiverterDriver>();
        
        mockDriver1.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockDriver2.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var activeDrivers = new Dictionary<string, IWheelDiverterDriver>
        {
            { "WD001", mockDriver1.Object },
            { "WD002", mockDriver2.Object }
        };

        _mockDriverManager.Setup(dm => dm.GetActiveDrivers()).Returns(activeDrivers);

        // 模拟 SafeExecutionService 直接执行委托
        _mockSafeExecutor
            .Setup(se => se.ExecuteAsync(
                It.IsAny<Func<Task<WheelDiverterOperationResult>>>(),
                It.IsAny<string>(),
                It.IsAny<WheelDiverterOperationResult>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<WheelDiverterOperationResult>>, string, WheelDiverterOperationResult, CancellationToken>(
                async (func, _, _, ct) => await func());

        var service = new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.PassThroughAllAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, "所有摆轮成功时应返回 IsSuccess=true");
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(2, result.TotalCount);
        Assert.Empty(result.FailedDriverIds);
        Assert.Null(result.ErrorMessage);

        // 验证所有驱动都被调用
        mockDriver1.Verify(d => d.PassThroughAsync(It.IsAny<CancellationToken>()), Times.Once);
        mockDriver2.Verify(d => d.PassThroughAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 测试 PassThroughAllAsync - 部分摆轮失败场景
    /// TD-052: 完善部分失败场景测试
    /// </summary>
    [Fact]
    public async Task PassThroughAllAsync_ShouldReportPartialFailure_WhenSomeDriversFail()
    {
        // Arrange
        var mockDriver1 = new Mock<IWheelDiverterDriver>();
        var mockDriver2 = new Mock<IWheelDiverterDriver>();
        var mockDriver3 = new Mock<IWheelDiverterDriver>();
        
        mockDriver1.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockDriver2.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(false); // 失败
        mockDriver3.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var activeDrivers = new Dictionary<string, IWheelDiverterDriver>
        {
            { "WD001", mockDriver1.Object },
            { "WD002", mockDriver2.Object },
            { "WD003", mockDriver3.Object }
        };

        _mockDriverManager.Setup(dm => dm.GetActiveDrivers()).Returns(activeDrivers);

        _mockSafeExecutor
            .Setup(se => se.ExecuteAsync(
                It.IsAny<Func<Task<WheelDiverterOperationResult>>>(),
                It.IsAny<string>(),
                It.IsAny<WheelDiverterOperationResult>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<WheelDiverterOperationResult>>, string, WheelDiverterOperationResult, CancellationToken>(
                async (func, _, _, ct) => await func());

        var service = new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.PassThroughAllAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess, "部分失败时应返回 IsSuccess=false");
        Assert.Equal(2, result.SuccessCount);
        Assert.Equal(3, result.TotalCount);
        Assert.Single(result.FailedDriverIds);
        Assert.Contains("WD002", result.FailedDriverIds);
        Assert.NotNull(result.ErrorMessage);
        Assert.Contains("WD002", result.ErrorMessage);
    }

    /// <summary>
    /// 测试 PassThroughAllAsync - 异常处理
    /// TD-052: 完善异常场景测试
    /// </summary>
    [Fact]
    public async Task PassThroughAllAsync_ShouldHandleException_WhenDriverThrows()
    {
        // Arrange
        var mockDriver1 = new Mock<IWheelDiverterDriver>();
        var mockDriver2 = new Mock<IWheelDiverterDriver>();
        
        mockDriver1.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mockDriver2.Setup(d => d.PassThroughAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("驱动器连接失败"));

        var activeDrivers = new Dictionary<string, IWheelDiverterDriver>
        {
            { "WD001", mockDriver1.Object },
            { "WD002", mockDriver2.Object }
        };

        _mockDriverManager.Setup(dm => dm.GetActiveDrivers()).Returns(activeDrivers);

        _mockSafeExecutor
            .Setup(se => se.ExecuteAsync(
                It.IsAny<Func<Task<WheelDiverterOperationResult>>>(),
                It.IsAny<string>(),
                It.IsAny<WheelDiverterOperationResult>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<WheelDiverterOperationResult>>, string, WheelDiverterOperationResult, CancellationToken>(
                async (func, _, _, ct) => await func());

        var service = new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.PassThroughAllAsync(CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess, "异常时应返回 IsSuccess=false");
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(2, result.TotalCount);
        Assert.Single(result.FailedDriverIds);
        Assert.Contains("WD002", result.FailedDriverIds);
        
        // 验证健康状态更新被调用（通过日志验证）
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((o, t) => o.ToString()!.Contains("WD002")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce,
            "异常时应记录错误日志");
    }

    /// <summary>
    /// 测试 PassThroughAllAsync - 无活动驱动场景
    /// TD-052: 完善边界场景测试
    /// </summary>
    [Fact]
    public async Task PassThroughAllAsync_ShouldReturnSuccess_WhenNoActiveDrivers()
    {
        // Arrange
        _mockDriverManager.Setup(dm => dm.GetActiveDrivers())
            .Returns(new Dictionary<string, IWheelDiverterDriver>());

        _mockSafeExecutor
            .Setup(se => se.ExecuteAsync(
                It.IsAny<Func<Task<WheelDiverterOperationResult>>>(),
                It.IsAny<string>(),
                It.IsAny<WheelDiverterOperationResult>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task<WheelDiverterOperationResult>>, string, WheelDiverterOperationResult, CancellationToken>(
                async (func, _, _, ct) => await func());

        var service = new WheelDiverterConnectionService(
            _mockConfigRepository.Object,
            _mockSystemConfigRepository.Object,
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Act
        var result = await service.PassThroughAllAsync(CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess, "无活动驱动时应返回 IsSuccess=true（无操作成功）");
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.FailedDriverIds);
    }
}
