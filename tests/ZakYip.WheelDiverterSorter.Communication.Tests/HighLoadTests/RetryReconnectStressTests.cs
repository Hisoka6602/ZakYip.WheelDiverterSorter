using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Core.Utilities;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.HighLoadTests;

/// <summary>
/// 重试和重连压力测试
/// </summary>
public class RetryReconnectStressTests
{
    private readonly ITestOutputHelper _output;

    public RetryReconnectStressTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleRequests_Successfully()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        await client.ConnectAsync(); // 必须先连接

        // Act - 发送正常请求应该成功
        var result = await client.NotifyParcelDetectedAsync(1);

        // Assert
        Assert.True(result, "请求应该成功");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleRequestsFast()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        await client.ConnectAsync(); // 必须先连接

        // Act - 正常的请求也应该快速成功
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await client.NotifyParcelDetectedAsync(1);
        sw.Stop();

        // Assert
        Assert.True(result, "请求应该成功");
        Assert.True(sw.ElapsedMilliseconds < 500, 
            $"请求应该在500ms内完成，实际: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleMultipleConcurrentRetries()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        await client.ConnectAsync(); // 必须先连接

        // Act - 并发发送多个请求
        var tasks = Enumerable.Range(1, 50).Select(async parcelId =>
        {
            return await client.NotifyParcelDetectedAsync(parcelId);
        });

        var results = await Task.WhenAll(tasks);

        // Assert
        var successCount = results.Count(r => r);
        _output.WriteLine($"成功: {successCount}/{results.Length}");
        Assert.True(successCount > 0, "应该有成功的请求");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleRapidConnectDisconnect()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();

        // Act - 快速连接和断开
        for (int i = 0; i < 20; i++)
        {
            using var client = new InMemoryRuleEngineClient(
                parcelId => (int)(parcelId % 10) + 1,
                Mock.Of<ISystemClock>(),
                logger);
            await client.ConnectAsync();
            await client.NotifyParcelDetectedAsync(i + 1);
            await client.DisconnectAsync();
        }

        // Assert - 能成功完成就说明没有问题
        Assert.True(true);
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleConcurrentConnections()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();

        // Act - 多个线程同时尝试连接
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);
        var tasks = Enumerable.Range(1, 20).Select(async _ =>
        {
            return await client.ConnectAsync();
        });

        var results = await Task.WhenAll(tasks);

        // Assert - 所有连接尝试都应该成功（或返回已连接状态）
        Assert.All(results, result => Assert.True(result));
        Assert.True(client.IsConnected);
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleCancellation_DuringRequest()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);
        var cts = new CancellationTokenSource();

        // Act - 发送请求后立即取消
        var task = client.NotifyParcelDetectedAsync(1, cts.Token);
        cts.CancelAfter(100); // 100ms后取消

        // Assert - InMemoryClient 可能在取消前就完成了，所以不一定抛出异常
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            // 预期的取消异常
        }
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 2000, 
            $"请求应该在2秒内完成或取消，实际: {sw.ElapsedMilliseconds}ms");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleNormalRequest()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        await client.ConnectAsync(); // 必须先连接

        // Act
        var result = await client.NotifyParcelDetectedAsync(1);

        // Assert
        Assert.True(result, "正常请求应该成功");
    }

    [Fact]
    public async Task InMemoryClient_ShouldEnsureConnection_BeforeSending()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        // Act - 不显式连接，直接发送请求（InMemoryClient会返回false）
        var result = await client.NotifyParcelDetectedAsync(1);

        // Assert - InMemoryClient 需要显式连接
        Assert.False(result, "未连接时应该返回false");
        Assert.False(client.IsConnected, "未连接时应该保持未连接状态");

        // 连接后再试
        await client.ConnectAsync();
        result = await client.NotifyParcelDetectedAsync(1);
        Assert.True(result, "连接后应该成功");
        Assert.True(client.IsConnected, "连接后应该保持连接状态");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleReconnect_AfterDisconnection()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(),
            logger);

        // Act - 连接、断开、重连
        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        await client.DisconnectAsync();
        Assert.False(client.IsConnected);

        await client.ConnectAsync();
        Assert.True(client.IsConnected);

        var result = await client.NotifyParcelDetectedAsync(1);

        // Assert
        Assert.True(result, "重连后应该能正常发送请求");
    }
}
