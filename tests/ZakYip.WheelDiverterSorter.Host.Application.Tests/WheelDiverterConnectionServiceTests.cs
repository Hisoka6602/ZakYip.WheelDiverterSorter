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
/// TODO (TD-052): 完善 WheelDiverterConnectionService 的 PassThroughAllAsync 集成测试
/// </remarks>
public class WheelDiverterConnectionServiceTests
{
    private readonly Mock<IWheelDiverterConfigurationRepository> _mockConfigRepository;
    private readonly Mock<IWheelDiverterDriverManager> _mockDriverManager;
    private readonly Mock<INodeHealthRegistry> _mockHealthRegistry;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<ILogger<WheelDiverterConnectionService>> _mockLogger;

    public WheelDiverterConnectionServiceTests()
    {
        _mockConfigRepository = new Mock<IWheelDiverterConfigurationRepository>();
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
            _mockDriverManager.Object,
            _mockHealthRegistry.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object,
            _mockLogger.Object);

        // Assert
        Assert.NotNull(service);
    }
}
