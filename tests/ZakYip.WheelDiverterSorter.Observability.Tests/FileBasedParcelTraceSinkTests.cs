using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Observability.Tracing;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// FileBasedParcelTraceSink 单元测试
/// </summary>
public class FileBasedParcelTraceSinkTests
{
    [Fact]
    public async Task WriteAsync_ValidEvent_LogsInformation()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var sink = new FileBasedParcelTraceSink(mockLogger.Object, Mock.Of<ISystemClock>());

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 12345,
            BarCode = "BC12345",
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = "Created",
            Source = "Ingress",
            Details = "Test details"
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("ParcelTrace")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WriteAsync_NullBarCode_LogsSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var sink = new FileBasedParcelTraceSink(mockLogger.Object, Mock.Of<ISystemClock>());

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 67890,
            BarCode = null,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = "RoutePlanned",
            Source = "Execution",
            Details = null
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert - 不应抛出异常，且应记录日志
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public async Task WriteAsync_LoggerThrowsException_DoesNotPropagate()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        
        // 设置 LogInformation 抛出异常，但 LogWarning 不抛异常
        mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Throws(new InvalidOperationException("Test exception"));

        var sink = new FileBasedParcelTraceSink(mockLogger.Object, Mock.Of<ISystemClock>());

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 11111,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = "Diverted",
            Source = "Execution"
        };

        // Act & Assert - 不应抛出异常
        var exception = await Record.ExceptionAsync(async () => await sink.WriteAsync(eventArgs));
        Assert.Null(exception);
    }

    [Fact]
    public async Task WriteAsync_LoggerThrowsException_LogsWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var infoCallCount = 0;
        
        mockLogger
            .Setup(x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()))
            .Callback(() => 
            {
                infoCallCount++;
                throw new InvalidOperationException("Logging failed");
            });

        var sink = new FileBasedParcelTraceSink(mockLogger.Object, Mock.Of<ISystemClock>());

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 22222,
            OccurredAt = DateTimeOffset.UtcNow,
            Stage = "ExceptionDiverted",
            Source = "Execution"
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert - 应尝试记录 Info，失败后应记录 Warning
        Assert.Equal(1, infoCallCount);
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("写入包裹追踪日志失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FileBasedParcelTraceSink(null!, Mock.Of<ISystemClock>()));
    }
}
