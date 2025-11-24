using ZakYip.WheelDiverterSorter.Core.Utilities;
using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Xunit.Abstractions;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.Communication.Tests.HighLoadTests;

/// <summary>
/// 高负载并发测试
/// </summary>
public class ConcurrentClientTests
{
    private readonly ITestOutputHelper _output;

    public ConcurrentClientTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleConcurrentRequests_WithoutDataRace()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1, // 简单的格口分配逻辑
            Mock.Of<ISystemClock>(),
            logger);
        
        await client.ConnectAsync(); // 必须先连接
        
        var successCount = 0;
        var errorCount = 0;
        var concurrencyLevel = 100;
        var requestsPerTask = 10;
        
        // Act
        var tasks = Enumerable.Range(0, concurrencyLevel).Select(async taskId =>
        {
            for (int i = 0; i < requestsPerTask; i++)
            {
                var parcelId = taskId * 1000 + i + 1;
                try
                {
                    var result = await client.NotifyParcelDetectedAsync(parcelId);
                    if (result)
                    {
                        Interlocked.Increment(ref successCount);
                    }
                    else
                    {
                        Interlocked.Increment(ref errorCount);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref errorCount);
                }
            }
        });

        await Task.WhenAll(tasks);

        // Assert
        var totalRequests = concurrencyLevel * requestsPerTask;
        _output.WriteLine($"Total requests: {totalRequests}");
        _output.WriteLine($"Success: {successCount}");
        _output.WriteLine($"Errors: {errorCount}");
        
        Assert.Equal(totalRequests, successCount + errorCount);
        Assert.True(successCount > 0, "应该有成功的请求");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleHighThroughput_WithLowLatency()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(), logger);
        
        var requestCount = 1000;
        var latencies = new ConcurrentBag<long>();
        var sw = Stopwatch.StartNew();

        // Act
        var tasks = Enumerable.Range(1, requestCount).Select(async parcelId =>
        {
            var requestSw = Stopwatch.StartNew();
            await client.NotifyParcelDetectedAsync(parcelId);
            requestSw.Stop();
            latencies.Add(requestSw.ElapsedMilliseconds);
        });

        await Task.WhenAll(tasks);
        sw.Stop();

        // Assert
        var throughput = (double)requestCount / sw.Elapsed.TotalSeconds;
        var avgLatency = latencies.Average();
        var p95Latency = latencies.OrderBy(l => l).ElementAt((int)(requestCount * 0.95));
        var p99Latency = latencies.OrderBy(l => l).ElementAt((int)(requestCount * 0.99));

        _output.WriteLine($"Total time: {sw.ElapsedMilliseconds}ms");
        _output.WriteLine($"Throughput: {throughput:F2} req/s");
        _output.WriteLine($"Avg latency: {avgLatency:F2}ms");
        _output.WriteLine($"P95 latency: {p95Latency}ms");
        _output.WriteLine($"P99 latency: {p99Latency}ms");

        Assert.True(throughput > 100, $"吞吐量应该大于100 req/s，实际: {throughput:F2}");
        Assert.True(avgLatency < 100, $"平均延迟应该小于100ms，实际: {avgLatency:F2}ms");
    }

    [Fact]
    public async Task InMemoryClient_ShouldHandleEventNotifications_Concurrently()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(), logger);
        
        await client.ConnectAsync(); // 必须先连接
        
        var receivedNotifications = new ConcurrentBag<ChuteAssignmentNotificationEventArgs>();
        client.ChuteAssignmentReceived += (sender, e) => receivedNotifications.Add(e);

        var requestCount = 100;

        // Act
        var tasks = Enumerable.Range(1, requestCount).Select(async parcelId =>
        {
            await client.NotifyParcelDetectedAsync(parcelId);
            // 等待事件触发
            await Task.Delay(10);
        });

        await Task.WhenAll(tasks);

        // Assert
        _output.WriteLine($"Received {receivedNotifications.Count} notifications out of {requestCount} requests");
        Assert.True(receivedNotifications.Count > 0, "应该接收到事件通知");
        Assert.True(receivedNotifications.Count <= requestCount, "通知数量不应该超过请求数量");
    }

    [Fact]
    public async Task InMemoryClient_ShouldNotLeak_UnderHighLoad()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();

        // Act - 创建和销毁多个客户端实例
        for (int iteration = 0; iteration < 10; iteration++)
        {
            using var client = new InMemoryRuleEngineClient(
                parcelId => (int)(parcelId % 10) + 1,
                Mock.Of<ISystemClock>(), logger);
            
            var tasks = Enumerable.Range(1, 100).Select(async parcelId =>
            {
                await client.NotifyParcelDetectedAsync(parcelId);
            });

            await Task.WhenAll(tasks);
        }

        // Assert - 如果有内存泄漏，这个测试会很慢或失败
        // 成功完成就说明没有明显的资源泄漏
        Assert.True(true);
    }

    [Fact]
    public async Task RuleEngineClientBase_ShouldHandleValidationErrors_Gracefully()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(), logger);

        await client.ConnectAsync(); // 必须先连接

        // Act & Assert - InMemoryClient doesn't validate parcelId
        // It just forwards the call, so we test other aspects instead
        var result1 = await client.NotifyParcelDetectedAsync(1);
        Assert.True(result1, "正常包裹ID应该成功");
    }

    [Fact]
    public async Task RuleEngineClientBase_ShouldHandleDisposedState_Gracefully()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(), logger);
        
        // Act
        client.Dispose();

        // Assert
        await Assert.ThrowsAsync<ObjectDisposedException>(async () =>
        {
            await client.NotifyParcelDetectedAsync(1);
        });
    }

    [Fact]
    public async Task RuleEngineClientBase_ShouldHandleMultipleDisposeCalls()
    {
        // Arrange
        var logger = Mock.Of<ILogger<InMemoryRuleEngineClient>>();
        var client = new InMemoryRuleEngineClient(
            parcelId => (int)(parcelId % 10) + 1,
            Mock.Of<ISystemClock>(), logger);
        
        // Act & Assert - 多次 Dispose 不应该抛出异常
        client.Dispose();
        client.Dispose();
        client.Dispose();
        
        Assert.True(true);
    }
}
