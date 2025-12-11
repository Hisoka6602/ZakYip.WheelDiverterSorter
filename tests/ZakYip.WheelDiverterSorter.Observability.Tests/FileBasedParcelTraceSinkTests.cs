using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Events.Monitoring;
using ZakYip.WheelDiverterSorter.Core.LineModel.Tracing;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Observability.Tracing;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// FileBasedParcelTraceSink 单元测试
/// </summary>
public class FileBasedParcelTraceSinkTests : IDisposable
{
    private readonly string _testLogDirectory;

    public FileBasedParcelTraceSinkTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"test_logs_{Guid.NewGuid():N}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogDirectory))
        {
            Directory.Delete(_testLogDirectory, true);
        }
    }

    [Fact]
    public async Task WriteAsync_ValidEvent_WritesJsonToFile()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2024, 1, 15, 10, 30, 0));
        
        var sink = new FileBasedParcelTraceSink(mockLogger.Object, mockClock.Object, _testLogDirectory);

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 12345,
            BarCode = "BC12345",
            OccurredAt = DateTimeOffset.Now,
            Stage = "Created",
            Source = "Ingress",
            Details = "Test details"
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert - 验证文件是否被创建
        var expectedFile = Path.Combine(_testLogDirectory, "parcel-trace-20240115.log");
        Assert.True(File.Exists(expectedFile), "日志文件应该被创建");
        
        var content = await File.ReadAllTextAsync(expectedFile);
        Assert.Contains("12345", content);
        Assert.Contains("BC12345", content);
        Assert.Contains("Created", content);
    }

    [Fact]
    public async Task WriteAsync_NullBarCode_WritesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2024, 1, 15, 10, 30, 0));
        
        var sink = new FileBasedParcelTraceSink(mockLogger.Object, mockClock.Object, _testLogDirectory);

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 67890,
            BarCode = null,
            OccurredAt = DateTimeOffset.Now,
            Stage = "RoutePlanned",
            Source = "Execution",
            Details = null
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert - 不应抛出异常，文件应该被创建
        var expectedFile = Path.Combine(_testLogDirectory, "parcel-trace-20240115.log");
        Assert.True(File.Exists(expectedFile), "日志文件应该被创建");
        
        var content = await File.ReadAllTextAsync(expectedFile);
        Assert.Contains("67890", content);
        Assert.Contains("RoutePlanned", content);
    }

    [Fact]
    public async Task WriteAsync_FileWriteFailure_LogsWarning()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<FileBasedParcelTraceSink>>();
        var mockClock = new Mock<ISystemClock>();
        mockClock.Setup(c => c.LocalNow).Returns(new DateTime(2024, 1, 15, 10, 30, 0));
        
        // 使用只读目录导致写入失败
        var readonlyPath = Path.Combine(Path.GetTempPath(), "readonly_test_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(readonlyPath);
        
        // 创建sink后，将目录设置为只读
        var sink = new FileBasedParcelTraceSink(mockLogger.Object, mockClock.Object, readonlyPath);
        
        // 在Linux上设置目录为只读
        if (OperatingSystem.IsLinux())
        {
            System.IO.File.SetUnixFileMode(readonlyPath, UnixFileMode.UserRead | UnixFileMode.GroupRead | UnixFileMode.OtherRead);
        }
        else
        {
            // Windows上设置只读属性
            var dirInfo = new DirectoryInfo(readonlyPath);
            dirInfo.Attributes = FileAttributes.ReadOnly;
        }

        var eventArgs = new ParcelTraceEventArgs
        {
            ItemId = 22222,
            OccurredAt = DateTimeOffset.Now,
            Stage = "ExceptionDiverted",
            Source = "Execution"
        };

        // Act
        await sink.WriteAsync(eventArgs);

        // Assert - 应记录 Warning（写入失败），但不抛出异常
        mockLogger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("写入包裹追踪日志失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.AtLeastOnce);
        
        // Cleanup - restore permissions and delete directory
        try
        {
            if (OperatingSystem.IsLinux())
            {
                System.IO.File.SetUnixFileMode(readonlyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }
            else
            {
                var dirInfo = new DirectoryInfo(readonlyPath);
                dirInfo.Attributes = FileAttributes.Normal;
            }
            Directory.Delete(readonlyPath, true);
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new FileBasedParcelTraceSink(null!, Mock.Of<ISystemClock>()));
    }
}
