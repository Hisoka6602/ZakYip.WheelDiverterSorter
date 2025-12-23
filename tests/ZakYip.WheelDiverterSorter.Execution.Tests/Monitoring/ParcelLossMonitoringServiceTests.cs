using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Execution.Monitoring;
using ZakYip.WheelDiverterSorter.Execution.Queues;
using ZakYip.WheelDiverterSorter.Execution.Tracking;
using ZakYip.WheelDiverterSorter.Observability.Utilities;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Monitoring;

/// <summary>
/// 包裹丢失监控服务测试
/// </summary>
public class ParcelLossMonitoringServiceTests
{
    private readonly Mock<IPositionIndexQueueManager> _mockQueueManager;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ILogger<ParcelLossMonitoringService>> _mockLogger;
    private readonly Mock<ISafeExecutionService> _mockSafeExecutor;
    private readonly Mock<IPositionIntervalTracker> _mockIntervalTracker;
    private readonly Mock<IParcelLossDetectionConfigurationRepository> _mockConfigRepository;
    private readonly PositionIntervalTrackerOptions _trackerOptions;

    public ParcelLossMonitoringServiceTests()
    {
        _mockQueueManager = new Mock<IPositionIndexQueueManager>();
        _mockClock = new Mock<ISystemClock>();
        _mockLogger = new Mock<ILogger<ParcelLossMonitoringService>>();
        _mockSafeExecutor = new Mock<ISafeExecutionService>();
        _mockIntervalTracker = new Mock<IPositionIntervalTracker>();
        _mockConfigRepository = new Mock<IParcelLossDetectionConfigurationRepository>();
        
        _trackerOptions = new PositionIntervalTrackerOptions
        {
            MonitoringIntervalMs = 60,
            LostDetectionMultiplier = 1.5,
            TimeoutMultiplier = 3.0,
            WindowSize = 10
        };
    }

    /// <summary>
    /// 测试：当 IsEnabled = false 时，不应执行包裹丢失检测逻辑
    /// </summary>
    [Fact]
    public async Task MonitorQueues_WhenIsEnabledIsFalse_ShouldNotExecuteDetectionLogic()
    {
        // Arrange
        var config = new ParcelLossDetectionConfiguration
        {
            IsEnabled = false,
            MonitoringIntervalMs = 60,
            AutoClearMedianIntervalMs = 300000,
            LostDetectionMultiplier = 1.5,
            TimeoutMultiplier = 3.0,
            WindowSize = 10
        };
        
        _mockConfigRepository
            .Setup(r => r.Get())
            .Returns(config);

        var service = new ParcelLossMonitoringService(
            _mockQueueManager.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockSafeExecutor.Object,
            Options.Create(_trackerOptions),
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

        // Use reflection to invoke the private MonitorQueuesForLostParcels method
        var method = typeof(ParcelLossMonitoringService).GetMethod(
            "MonitorQueuesForLostParcels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        if (method?.Invoke(service, null) is Task monitorTask)
        {
            await monitorTask;
        }

        // Assert
        // 验证没有调用队列管理器的 GetAllQueueStatuses 方法（检测逻辑的入口）
        _mockQueueManager.Verify(
            q => q.GetAllQueueStatuses(),
            Times.Never,
            "当 IsEnabled=false 时，不应调用 GetAllQueueStatuses 执行检测逻辑");
        
        // 验证没有调用自动清空中位数统计数据
        _mockIntervalTracker.Verify(
            t => t.ClearAllStatistics(),
            Times.Never,
            "当 IsEnabled=false 时，不应执行自动清空中位数逻辑");
        
        // 验证没有调用自动清空任务队列
        _mockQueueManager.Verify(
            q => q.ClearAllQueues(),
            Times.Never,
            "当 IsEnabled=false 时，不应执行自动清空队列逻辑");
    }

    /// <summary>
    /// 测试：当配置仓储为 null 时，不应执行包裹丢失检测逻辑
    /// </summary>
    [Fact]
    public async Task MonitorQueues_WhenConfigRepositoryIsNull_ShouldNotExecuteDetectionLogic()
    {
        // Arrange
        var service = new ParcelLossMonitoringService(
            _mockQueueManager.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockSafeExecutor.Object,
            Options.Create(_trackerOptions),
            _mockIntervalTracker.Object,
            configRepository: null); // 配置仓储为 null

        // Use reflection to invoke the private MonitorQueuesForLostParcels method
        var method = typeof(ParcelLossMonitoringService).GetMethod(
            "MonitorQueuesForLostParcels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        if (method?.Invoke(service, null) is Task monitorTask)
        {
            await monitorTask;
        }

        // Assert
        // 验证没有调用队列管理器的 GetAllQueueStatuses 方法
        _mockQueueManager.Verify(
            q => q.GetAllQueueStatuses(),
            Times.Never,
            "当配置仓储为 null 时，不应调用 GetAllQueueStatuses 执行检测逻辑");
    }

    /// <summary>
    /// 测试：当读取配置失败时，不应执行包裹丢失检测逻辑
    /// </summary>
    [Fact]
    public async Task MonitorQueues_WhenConfigReadFails_ShouldNotExecuteDetectionLogic()
    {
        // Arrange
        _mockConfigRepository
            .Setup(r => r.Get())
            .Throws(new Exception("配置读取失败"));

        var service = new ParcelLossMonitoringService(
            _mockQueueManager.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockSafeExecutor.Object,
            Options.Create(_trackerOptions),
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

        // Use reflection to invoke the private MonitorQueuesForLostParcels method
        var method = typeof(ParcelLossMonitoringService).GetMethod(
            "MonitorQueuesForLostParcels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        if (method?.Invoke(service, null) is Task monitorTask)
        {
            await monitorTask;
        }

        // Assert
        // 验证没有调用队列管理器的 GetAllQueueStatuses 方法
        _mockQueueManager.Verify(
            q => q.GetAllQueueStatuses(),
            Times.Never,
            "当配置读取失败时，不应调用 GetAllQueueStatuses 执行检测逻辑");
        
        // 验证记录了警告日志
        _mockLogger.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("读取包裹丢失检测配置失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once,
            "当配置读取失败时，应记录警告日志");
    }

    /// <summary>
    /// 测试：当 IsEnabled = true 时，应执行包裹丢失检测逻辑
    /// </summary>
    [Fact]
    public async Task MonitorQueues_WhenIsEnabledIsTrue_ShouldExecuteDetectionLogic()
    {
        // Arrange
        var config = new ParcelLossDetectionConfiguration
        {
            IsEnabled = true,
            MonitoringIntervalMs = 60,
            AutoClearMedianIntervalMs = 300000,
            AutoClearQueueIntervalSeconds = 30,
            LostDetectionMultiplier = 1.5,
            TimeoutMultiplier = 3.0,
            WindowSize = 10
        };
        
        _mockConfigRepository
            .Setup(r => r.Get())
            .Returns(config);
        
        _mockClock
            .Setup(c => c.LocalNow)
            .Returns(DateTime.Now);

        _mockQueueManager
            .Setup(q => q.GetAllQueueStatuses())
            .Returns(new Dictionary<int, QueueStatus>());

        var service = new ParcelLossMonitoringService(
            _mockQueueManager.Object,
            _mockClock.Object,
            _mockLogger.Object,
            _mockSafeExecutor.Object,
            Options.Create(_trackerOptions),
            _mockIntervalTracker.Object,
            _mockConfigRepository.Object);

        // Use reflection to invoke the private MonitorQueuesForLostParcels method
        var method = typeof(ParcelLossMonitoringService).GetMethod(
            "MonitorQueuesForLostParcels",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        // Act
        if (method?.Invoke(service, null) is Task monitorTask)
        {
            await monitorTask;
        }

        // Assert
        // 验证调用了队列管理器的 GetAllQueueStatuses 方法
        _mockQueueManager.Verify(
            q => q.GetAllQueueStatuses(),
            Times.Once,
            "当 IsEnabled=true 时，应调用 GetAllQueueStatuses 执行检测逻辑");
    }
}
