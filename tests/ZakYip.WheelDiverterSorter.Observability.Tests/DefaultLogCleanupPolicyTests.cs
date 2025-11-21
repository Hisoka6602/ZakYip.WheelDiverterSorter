using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Observability.Tracing;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Observability.Tests;

/// <summary>
/// DefaultLogCleanupPolicy 单元测试
/// </summary>
public class DefaultLogCleanupPolicyTests : IDisposable
{
    private readonly string _testLogDirectory;

    public DefaultLogCleanupPolicyTests()
    {
        _testLogDirectory = Path.Combine(Path.GetTempPath(), $"test_logs_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testLogDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testLogDirectory))
        {
            Directory.Delete(_testLogDirectory, true);
        }
    }

    [Fact]
    public async Task CleanupAsync_NoLogDirectory_NoException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();
        var options = Options.Create(new LogCleanupOptions
        {
            LogDirectory = Path.Combine(_testLogDirectory, "nonexistent")
        });

        var policy = new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), options);

        // Act & Assert - 不应抛出异常
        var exception = await Record.ExceptionAsync(() => policy.CleanupAsync());
        Assert.Null(exception);
    }

    [Fact]
    public async Task CleanupAsync_DeletesOldFiles()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();
        var options = Options.Create(new LogCleanupOptions
        {
            LogDirectory = _testLogDirectory,
            RetentionDays = 7,
            MaxTotalSizeMb = 100
        });

        // 创建一些测试日志文件
        var oldFile = Path.Combine(_testLogDirectory, "old-2020-01-01.log");
        var newFile = Path.Combine(_testLogDirectory, "new-2099-12-31.log");
        
        File.WriteAllText(oldFile, "old log content");
        File.WriteAllText(newFile, "new log content");
        
        // 设置旧文件的修改时间为 100 天前
        File.SetLastWriteTimeUtc(oldFile, DateTime.UtcNow.AddDays(-100));

        var policy = new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), options);

        // Act
        await policy.CleanupAsync();

        // Assert
        Assert.False(File.Exists(oldFile), "旧文件应该被删除");
        Assert.True(File.Exists(newFile), "新文件应该保留");
    }

    [Fact]
    public async Task CleanupAsync_DeletesFilesWhenExceedsSizeLimit()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();
        var options = Options.Create(new LogCleanupOptions
        {
            LogDirectory = _testLogDirectory,
            RetentionDays = 365, // 保留很长时间，但要测试大小限制
            MaxTotalSizeMb = 1 // 1 MB 上限
        });

        // 创建多个大文件（每个 0.5 MB）
        var file1 = Path.Combine(_testLogDirectory, "file1.log");
        var file2 = Path.Combine(_testLogDirectory, "file2.log");
        var file3 = Path.Combine(_testLogDirectory, "file3.log");

        var content = new string('X', 512 * 1024); // 0.5 MB
        File.WriteAllText(file1, content);
        File.WriteAllText(file2, content);
        File.WriteAllText(file3, content);

        // 设置不同的修改时间
        File.SetLastWriteTimeUtc(file1, DateTime.UtcNow.AddDays(-3));
        File.SetLastWriteTimeUtc(file2, DateTime.UtcNow.AddDays(-2));
        File.SetLastWriteTimeUtc(file3, DateTime.UtcNow.AddDays(-1));

        var policy = new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), options);

        // Act
        await policy.CleanupAsync();

        // Assert - 最旧的文件应该被删除以保持在大小限制内
        Assert.False(File.Exists(file1), "最旧的文件应该被删除");
        Assert.True(File.Exists(file2) || File.Exists(file3), "至少一个较新的文件应该保留");
    }

    [Fact]
    public async Task CleanupAsync_NoFilesToDelete_CompletesSuccessfully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();
        var options = Options.Create(new LogCleanupOptions
        {
            LogDirectory = _testLogDirectory,
            RetentionDays = 7,
            MaxTotalSizeMb = 1000
        });

        // 创建一个最近的小文件
        var recentFile = Path.Combine(_testLogDirectory, "recent.log");
        File.WriteAllText(recentFile, "small content");

        var policy = new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), options);

        // Act
        await policy.CleanupAsync();

        // Assert
        Assert.True(File.Exists(recentFile), "最近的文件应该保留");
    }

    [Fact]
    public async Task CleanupAsync_CancellationRequested_StopsGracefully()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();
        var options = Options.Create(new LogCleanupOptions
        {
            LogDirectory = _testLogDirectory,
            RetentionDays = 1,
            MaxTotalSizeMb = 1
        });

        // 创建多个旧文件
        for (int i = 0; i < 100; i++)
        {
            var file = Path.Combine(_testLogDirectory, $"old-{i}.log");
            File.WriteAllText(file, new string('X', 10000));
            File.SetLastWriteTimeUtc(file, DateTime.UtcNow.AddDays(-100));
        }

        var policy = new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), options);
        var cts = new CancellationTokenSource();
        cts.Cancel(); // 立即取消

        // Act & Assert - 不应抛出异常
        var exception = await Record.ExceptionAsync(() => policy.CleanupAsync(cts.Token));
        Assert.Null(exception);
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        // Arrange
        var options = Options.Create(new LogCleanupOptions());

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultLogCleanupPolicy(null!, Mock.Of<ISystemClock>(), options));
    }

    [Fact]
    public void Constructor_NullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var mockLogger = new Mock<ILogger<DefaultLogCleanupPolicy>>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new DefaultLogCleanupPolicy(mockLogger.Object, Mock.Of<ISystemClock>(), null!));
    }
}
