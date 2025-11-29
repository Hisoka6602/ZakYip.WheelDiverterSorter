using Moq;
using Microsoft.Extensions.Logging;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Sorting.Models;
using ZakYip.WheelDiverterSorter.Core.Sorting.Strategy;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Core.Hardware.Devices;
using ZakYip.WheelDiverterSorter.Core.Hardware.IoLinkage;
using ZakYip.WheelDiverterSorter.Core.Hardware.Mappings;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Hardware.Providers;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Execution;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Ingress;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Upstream;
using ZakYip.WheelDiverterSorter.Execution.Strategy;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;

namespace ZakYip.WheelDiverterSorter.Execution.Tests.Strategy;

/// <summary>
/// 正式分拣格口选择策略单元测试
/// </summary>
public class FormalChuteSelectionStrategyTests : IDisposable
{
    private readonly Mock<IUpstreamRoutingClient> _mockUpstreamClient;
    private readonly Mock<ISystemClock> _mockClock;
    private readonly Mock<ILogger<FormalChuteSelectionStrategy>> _mockLogger;
    private readonly FormalChuteSelectionStrategy _strategy;
    private readonly DateTimeOffset _testTime;

    public FormalChuteSelectionStrategyTests()
    {
        _mockUpstreamClient = new Mock<IUpstreamRoutingClient>();
        _mockClock = new Mock<ISystemClock>();
        _mockLogger = new Mock<ILogger<FormalChuteSelectionStrategy>>();

        _testTime = new DateTimeOffset(2025, 11, 22, 12, 0, 0, TimeSpan.Zero);
        _mockClock.Setup(c => c.LocalNow).Returns(_testTime.LocalDateTime);

        // 默认设置：已连接
        _mockUpstreamClient.Setup(c => c.IsConnected).Returns(true);
        _mockUpstreamClient.Setup(c => c.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // 使用 subscribeToEvents = true 以便策略可以自己处理事件
        _strategy = new FormalChuteSelectionStrategy(
            _mockUpstreamClient.Object,
            _mockClock.Object,
            _mockLogger.Object,
            timeoutCalculator: null,
            subscribeToEvents: true);
    }

    public void Dispose()
    {
        _strategy.Dispose();
    }

    [Fact]
    public async Task SelectChuteAsync_WithOverloadForced_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            IsOverloadForced = true
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("超载", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WhenNotConnected_ReturnsExceptionChute()
    {
        // Arrange
        _mockUpstreamClient.Setup(c => c.IsConnected).Returns(false);

        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("未连接", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WhenNotificationFails_ReturnsExceptionChute()
    {
        // Arrange
        _mockUpstreamClient.Setup(c => c.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999
        };

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("无法发送", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WhenUpstreamResponds_ReturnsAssignedChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            ExceptionRoutingPolicy = new ExceptionRoutingPolicy
            {
                ExceptionChuteId = 999,
                UpstreamTimeoutMs = 5000
            }
        };

        // 创建一个不订阅事件的新策略实例，以便我们可以手动控制
        using var strategy = new FormalChuteSelectionStrategy(
            _mockUpstreamClient.Object,
            _mockClock.Object,
            _mockLogger.Object,
            subscribeToEvents: false);

        // 模拟上游通知成功，并在后台线程中触发分配
        _mockUpstreamClient.Setup(c => c.NotifyParcelDetectedAsync(It.IsAny<long>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // 在后台触发格口分配
        var selectionTask = strategy.SelectChuteAsync(context, CancellationToken.None);
        
        // 短暂延迟后手动通知分配
        await Task.Delay(50);
        strategy.NotifyChuteAssignment(1001, 5);

        // Act
        var result = await selectionTask;

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.TargetChuteId);
        Assert.False(result.IsException);
    }

    [Fact]
    public async Task SelectChuteAsync_WhenTimeout_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            ExceptionRoutingPolicy = new ExceptionRoutingPolicy
            {
                ExceptionChuteId = 999,
                UpstreamTimeoutMs = 100 // 非常短的超时
            }
        };

        // 上游不响应

        // Act
        var result = await _strategy.SelectChuteAsync(context, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        Assert.Contains("超时", result.ExceptionReason);
    }

    [Fact]
    public async Task SelectChuteAsync_WhenCancelled_ReturnsExceptionChute()
    {
        // Arrange
        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            ExceptionRoutingPolicy = new ExceptionRoutingPolicy
            {
                ExceptionChuteId = 999,
                UpstreamTimeoutMs = 10000
            }
        };

        using var cts = new CancellationTokenSource(50); // 短时间后取消

        // Act
        var result = await _strategy.SelectChuteAsync(context, cts.Token);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(999, result.TargetChuteId);
        Assert.True(result.IsException);
        // 可能是超时或取消
        Assert.True(result.ExceptionReason!.Contains("超时") || result.ExceptionReason!.Contains("取消"));
    }

    [Fact]
    public void NotifyChuteAssignment_WithPendingAssignment_ReturnsTrue()
    {
        // Arrange - 使用不订阅事件的策略
        using var strategy = new FormalChuteSelectionStrategy(
            _mockUpstreamClient.Object,
            _mockClock.Object,
            _mockLogger.Object,
            subscribeToEvents: false);

        var context = new SortingContext
        {
            ParcelId = 1001,
            SortingMode = SortingMode.Formal,
            ExceptionChuteId = 999,
            ExceptionRoutingPolicy = new ExceptionRoutingPolicy
            {
                ExceptionChuteId = 999,
                UpstreamTimeoutMs = 5000
            }
        };

        // 启动选择任务
        var selectionTask = strategy.SelectChuteAsync(context, CancellationToken.None);

        // Act - 手动通知分配
        var notified = strategy.NotifyChuteAssignment(1001, 5);

        // Assert
        Assert.True(notified);
        
        // 等待任务完成
        var result = selectionTask.GetAwaiter().GetResult();
        Assert.True(result.IsSuccess);
        Assert.Equal(5, result.TargetChuteId);
    }

    [Fact]
    public void NotifyChuteAssignment_WithoutPendingAssignment_ReturnsFalse()
    {
        // Arrange - 使用不订阅事件的策略
        using var strategy = new FormalChuteSelectionStrategy(
            _mockUpstreamClient.Object,
            _mockClock.Object,
            _mockLogger.Object,
            subscribeToEvents: false);

        // Act - 通知一个不存在的分配
        var notified = strategy.NotifyChuteAssignment(9999, 5);

        // Assert
        Assert.False(notified);
    }
}
