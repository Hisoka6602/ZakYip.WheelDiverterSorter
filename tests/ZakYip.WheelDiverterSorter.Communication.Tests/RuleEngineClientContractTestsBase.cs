using ZakYip.WheelDiverterSorter.Communication.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Tests;

/// <summary>
/// 所有RuleEngine客户端的契约测试基类
/// </summary>
/// <remarks>
/// 此基类定义了所有协议实现必须通过的契约测试。
/// 每个协议实现都应该继承此类并实现CreateClient方法。
/// </remarks>
public abstract class RuleEngineClientContractTestsBase : IDisposable
{
    /// <summary>
    /// 创建待测试的客户端实例
    /// </summary>
    /// <returns>客户端实例</returns>
    protected abstract IRuleEngineClient CreateClient();

    /// <summary>
    /// 启动模拟服务器（如果需要）
    /// </summary>
    protected abstract Task StartMockServerAsync();

    /// <summary>
    /// 停止模拟服务器（如果需要）
    /// </summary>
    protected abstract Task StopMockServerAsync();

    /// <summary>
    /// 配置模拟服务器的行为
    /// </summary>
    /// <param name="behavior">服务器行为配置</param>
    protected abstract Task ConfigureMockServerBehaviorAsync(MockServerBehavior behavior);

    [Fact]
    public async Task Contract_ConnectAsync_ShouldSucceed_WhenServerIsAvailable()
    {
        // Arrange
        await StartMockServerAsync();
        using var client = CreateClient();

        // Act
        var result = await client.ConnectAsync();

        // Assert
        Assert.True(result);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Contract_ConnectAsync_ShouldFail_WhenServerIsUnavailable()
    {
        // Arrange
        // Don't start mock server
        using var client = CreateClient();

        // Act
        var result = await client.ConnectAsync();

        // Assert
        Assert.False(result);
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Contract_NotifyParcelDetectedAsync_ShouldReceivePushNotification_WhenServerPushes()
    {
        // Arrange
        await StartMockServerAsync();
        await ConfigureMockServerBehaviorAsync(MockServerBehavior.PushNormally);
        
        using var client = CreateClient();
        await client.ConnectAsync();

        var tcs = new TaskCompletionSource<ChuteAssignmentNotificationEventArgs>();
        client.ChuteAssignmentReceived += (sender, args) =>
        {
            tcs.TrySetResult(args);
        };

        const long testParcelId = 1234567890L;

        // Act
        var notified = await client.NotifyParcelDetectedAsync(testParcelId);
        
        // Wait for push with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var notification = await tcs.Task.WaitAsync(cts.Token);

        // Assert
        Assert.True(notified);
        Assert.NotNull(notification);
        Assert.Equal(testParcelId, notification.ParcelId);
        Assert.True(notification.ChuteId > 0);
    }

    [Fact]
    public async Task Contract_NotifyParcelDetectedAsync_ShouldTimeout_WhenServerDoesNotPush()
    {
        // Arrange
        await StartMockServerAsync();
        await ConfigureMockServerBehaviorAsync(MockServerBehavior.NeverPush);
        
        using var client = CreateClient();
        await client.ConnectAsync();

        var tcs = new TaskCompletionSource<ChuteAssignmentNotificationEventArgs>();
        client.ChuteAssignmentReceived += (sender, args) =>
        {
            tcs.TrySetResult(args);
        };

        const long testParcelId = 1234567890L;

        // Act
        var notified = await client.NotifyParcelDetectedAsync(testParcelId);
        
        // Wait for push with short timeout (should timeout)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));

        // Assert
        Assert.True(notified); // Notification sent successfully
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await tcs.Task.WaitAsync(cts.Token);
        });
    }

    [Fact]
    public async Task Contract_Connection_ShouldReconnect_WhenConnectionIsLost()
    {
        // Arrange
        await StartMockServerAsync();
        using var client = CreateClient();
        await client.ConnectAsync();

        // Act - Simulate connection loss
        await StopMockServerAsync();
        await Task.Delay(100); // Give time for disconnect detection
        
        // Restart server and reconnect
        await StartMockServerAsync();
        var reconnected = await client.ConnectAsync();

        // Assert
        Assert.True(reconnected);
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task Contract_DisconnectAsync_ShouldCleanupResources()
    {
        // Arrange
        await StartMockServerAsync();
        using var client = CreateClient();
        await client.ConnectAsync();

        // Act
        await client.DisconnectAsync();

        // Assert
        Assert.False(client.IsConnected);
    }

    [Fact]
    public async Task Contract_MultipleNotifications_ShouldAllBeReceived()
    {
        // Arrange
        await StartMockServerAsync();
        await ConfigureMockServerBehaviorAsync(MockServerBehavior.PushNormally);
        
        using var client = CreateClient();
        await client.ConnectAsync();

        var notifications = new List<ChuteAssignmentNotificationEventArgs>();
        client.ChuteAssignmentReceived += (sender, args) =>
        {
            notifications.Add(args);
        };

        const int notificationCount = 5;

        // Act
        for (int i = 0; i < notificationCount; i++)
        {
            var parcelId = 1000L + i;
            await client.NotifyParcelDetectedAsync(parcelId);
        }

        // Wait for all notifications
        await Task.Delay(TimeSpan.FromSeconds(3));

        // Assert
        Assert.Equal(notificationCount, notifications.Count);
        for (int i = 0; i < notificationCount; i++)
        {
            var expectedParcelId = 1000L + i;
            Assert.Contains(notifications, n => n.ParcelId == expectedParcelId);
        }
    }

    public virtual void Dispose()
    {
        StopMockServerAsync().GetAwaiter().GetResult();
    }
}

/// <summary>
/// 模拟服务器行为配置
/// </summary>
public enum MockServerBehavior
{
    /// <summary>
    /// 正常推送格口分配
    /// </summary>
    PushNormally,

    /// <summary>
    /// 从不推送（模拟超时）
    /// </summary>
    NeverPush,

    /// <summary>
    /// 延迟推送
    /// </summary>
    DelayedPush,

    /// <summary>
    /// 随机断开连接
    /// </summary>
    RandomDisconnect
}
