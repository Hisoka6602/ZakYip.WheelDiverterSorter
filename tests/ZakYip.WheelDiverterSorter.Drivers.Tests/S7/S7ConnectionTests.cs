using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Siemens.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests.S7;

public class S7ConnectionTests
{
    private readonly Mock<ILogger<S7Connection>> _mockLogger;
    private readonly Mock<IOptionsMonitor<S7Options>> _mockOptionsMonitor;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly S7Options _options;

    public S7ConnectionTests()
    {
        _mockLogger = new Mock<ILogger<S7Connection>>();
        _mockOptionsMonitor = new Mock<IOptionsMonitor<S7Options>>();
        _mockClock = new Mock<ISystemClock>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        
        _options = new S7Options
        {
            IpAddress = "192.168.0.100",
            Rack = 0,
            Slot = 1,
            CpuType = S7CpuType.S71200,
            ConnectionTimeout = 5000,
            ReadWriteTimeout = 2000,
            MaxReconnectAttempts = 3,
            ReconnectDelay = 100, // Short delay for testing
            EnableHealthCheck = false,  // 禁用健康检查以简化测试
            EnablePerformanceMetrics = true
        };
        _mockOptionsMonitor.Setup(x => x.CurrentStatealue).Returns(_options);
        _mockClock.Setup(x => x.LocalNow).Returns(DateTime.Now);
        
        // Setup SafeExecutionService to execute the action immediately
        _mockSafeExecutor
            .Setup(x => x.ExecuteAsync(
                It.IsAny<Func<Task>>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns<Func<Task>, string, CancellationToken>(async (action, _, __) =>
            {
                try
                {
                    await action();
                    return true;
                }
                catch
                {
                    return false;
                }
            });
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Act
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Assert
        Assert.NotNull(connection);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7Connection(
                null!, 
                _mockOptionsMonitor.Object,
                _mockClock.Object,
                _mockSafeExecutor.Object));
    }

    [Fact]
    public void Constructor_WithNullOptionsMonitor_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new S7Connection(
                _mockLogger.Object, 
                null!,
                _mockClock.Object,
                _mockSafeExecutor.Object));
    }

    [Fact]
    public async Task ConnectAsync_WithInvalidIpAddress_ReturnsFalse()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act
        var result = await connection.ConnectAsync();

        // Assert
        Assert.False(result);
        Assert.False(connection.IsConnected);
    }

    [Fact]
    public void Disconnect_WhenNotConnected_DoesNotThrow()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act & Assert
        connection.Disconnect(); // Should not throw
    }

    [Fact]
    public async Task ReadBitAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.ReadBitAsync("DB1", 0, 0));
    }

    [Fact]
    public async Task WriteBitAsync_WhenNotConnected_ThrowsInvalidOperationException()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            connection.WriteBitAsync("DB1", 0, 0, true));
    }

    [Fact]
    public void GetHealth_ReturnsHealthInfo()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act
        var health = connection.GetHealth();

        // Assert
        Assert.NotNull(health);
        Assert.False(health.IsConnected);
        Assert.Equal(0, health.ConsecutiveFailures);
    }

    [Fact]
    public void GetMetrics_ReturnsMetricsInfo()
    {
        // Arrange
        var connection = new S7Connection(
            _mockLogger.Object, 
            _mockOptionsMonitor.Object,
            _mockClock.Object,
            _mockSafeExecutor.Object);

        // Act
        var metrics = connection.GetMetrics();

        // Assert
        Assert.NotNull(metrics);
        Assert.Equal(0, metrics.TotalReads);
        Assert.Equal(0, metrics.TotalWrites);
        Assert.Equal(100.0, metrics.ReadSuccessRate);
        Assert.Equal(100.0, metrics.WriteSuccessRate);
    }
}
