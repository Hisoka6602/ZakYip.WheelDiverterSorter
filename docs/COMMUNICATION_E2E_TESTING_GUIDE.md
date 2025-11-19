# Communication Layer E2E Testing Guide

## Overview

This document provides guidance on implementing End-to-End (E2E) tests for the Communication layer protocol implementations.

## E2E Test Strategy

E2E tests validate the complete communication flow from the WheelDiverter system to RuleEngine and back, including:

1. **Parcel Detection** → **Notification** → **Push Reception** → **Sorting**
2. **Connection Lifecycle**: Connect → Communicate → Disconnect → Reconnect
3. **Failure Scenarios**: Timeouts, disconnections, retries, circuit breaker activation
4. **Performance**: Latency, throughput, concurrent operations

## Test Scenarios

### Scenario 1: Normal Push Flow (Happy Path)

**Objective**: Verify complete push model flow works correctly

**Test Steps**:
1. Start RuleEngine mock server
2. Create and connect RuleEngine client
3. Detect parcel (generate parcel ID)
4. Send notification via `NotifyParcelDetectedAsync`
5. Wait for push via `ChuteAssignmentReceived` event (with timeout)
6. Verify received chute assignment
7. Simulate sorting execution
8. Verify sorting completed successfully

**Expected Result**: Parcel sorted to correct chute within TTL timeout

### Scenario 2: TTL Timeout Handling

**Objective**: Verify system handles upstream timeout correctly

**Test Steps**:
1. Configure mock server to never push
2. Connect client
3. Send notification
4. Wait with TTL timeout (10 seconds)
5. Verify timeout exception is caught
6. Verify system uses exception chute as fallback
7. Verify parcel routed to exception chute

**Expected Result**: System gracefully handles timeout and routes to exception chute

### Scenario 3: Connection Loss and Reconnection

**Objective**: Verify system recovers from connection failures

**Test Steps**:
1. Start mock server and connect
2. Send several notifications successfully
3. Stop mock server (simulate disconnect)
4. Attempt to send notification (should fail)
5. Restart mock server
6. Reconnect client
7. Send notification again
8. Verify push received successfully

**Expected Result**: System reconnects automatically and resumes normal operation

### Scenario 4: High Concurrency Load

**Objective**: Verify system handles multiple concurrent notifications

**Test Steps**:
1. Start mock server
2. Connect client
3. Send 100 notifications concurrently
4. Collect all push responses
5. Verify all notifications received correct responses
6. Measure average latency
7. Verify no errors or dropped messages

**Expected Result**: All notifications processed successfully with acceptable latency

### Scenario 5: Circuit Breaker Activation

**Objective**: Verify circuit breaker protects system under failure

**Test Steps**:
1. Configure mock server to reject connections
2. Attempt multiple connections (exceed failure threshold)
3. Verify circuit breaker opens
4. Verify subsequent requests fail fast
5. Wait for circuit breaker timeout
6. Start mock server
7. Verify circuit breaker enters half-open state
8. Connect successfully
9. Verify circuit breaker closes

**Expected Result**: Circuit breaker prevents cascading failures and recovers automatically

## Implementation Example: TCP E2E Test

```csharp
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using ZakYip.WheelDiverterSorter.Communication.Clients;
using ZakYip.WheelDiverterSorter.Communication.Configuration;
using ZakYip.WheelDiverterSorter.Communication.Models;

namespace ZakYip.WheelDiverterSorter.E2ETests.Communication;

/// <summary>
/// TCP协议端到端测试
/// </summary>
public class TcpCommunicationE2ETests : IDisposable
{
    private readonly TcpMockServer _mockServer;
    private readonly TcpRuleEngineClient _client;
    private const int TestPort = 9876;

    public TcpCommunicationE2ETests()
    {
        _mockServer = new TcpMockServer(TestPort);
        
        var options = new RuleEngineConnectionOptions
        {
            TcpServer = $"localhost:{TestPort}",
            TimeoutMs = 5000,
            RetryCount = 3
        };

        var logger = new Mock<ILogger<TcpRuleEngineClient>>().Object;
        _client = new TcpRuleEngineClient(logger, options);
    }

    [Fact]
    public async Task E2E_NormalPushFlow_ShouldCompleteSuccessfully()
    {
        // Arrange
        await _mockServer.StartAsync();
        _mockServer.Behavior = MockServerBehavior.PushNormally;
        
        var connected = await _client.ConnectAsync();
        Assert.True(connected);

        var tcs = new TaskCompletionSource<ChuteAssignmentNotificationEventArgs>();
        _client.ChuteAssignmentReceived += (sender, args) =>
        {
            tcs.TrySetResult(args);
        };

        var parcelId = DateTime.Now.Ticks;

        // Act - Send notification
        var notified = await _client.NotifyParcelDetectedAsync(parcelId);
        Assert.True(notified);

        // Act - Wait for push with timeout
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var notification = await tcs.Task.WaitAsync(cts.Token);

        // Assert
        Assert.NotNull(notification);
        Assert.Equal(parcelId, notification.ParcelId);
        Assert.True(notification.ChuteId > 0);
    }

    [Fact]
    public async Task E2E_TimeoutHandling_ShouldUseExceptionChute()
    {
        // Arrange
        await _mockServer.StartAsync();
        _mockServer.Behavior = MockServerBehavior.NeverPush;
        
        await _client.ConnectAsync();

        var tcs = new TaskCompletionSource<ChuteAssignmentNotificationEventArgs>();
        _client.ChuteAssignmentReceived += (sender, args) =>
        {
            tcs.TrySetResult(args);
        };

        var parcelId = DateTime.Now.Ticks;

        // Act
        await _client.NotifyParcelDetectedAsync(parcelId);
        
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        
        // Assert - Should timeout
        await Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await tcs.Task.WaitAsync(cts.Token);
        });

        // Verify system would route to exception chute
        // (This would be tested in integration with sorting logic)
    }

    [Fact]
    public async Task E2E_ConnectionLossAndRecovery_ShouldReconnect()
    {
        // Arrange
        await _mockServer.StartAsync();
        await _client.ConnectAsync();

        // Act - Simulate connection loss
        await _mockServer.StopAsync();
        await Task.Delay(500);

        Assert.False(_client.IsConnected);

        // Act - Restart and reconnect
        await _mockServer.StartAsync();
        var reconnected = await _client.ConnectAsync();

        // Assert
        Assert.True(reconnected);
        Assert.True(_client.IsConnected);
    }

    [Fact]
    public async Task E2E_HighConcurrency_ShouldHandleMultipleNotifications()
    {
        // Arrange
        await _mockServer.StartAsync();
        _mockServer.Behavior = MockServerBehavior.PushNormally;
        await _client.ConnectAsync();

        var notifications = new ConcurrentBag<ChuteAssignmentNotificationEventArgs>();
        _client.ChuteAssignmentReceived += (sender, args) =>
        {
            notifications.Add(args);
        };

        const int notificationCount = 50;
        var tasks = new List<Task>();

        // Act - Send multiple notifications concurrently
        for (int i = 0; i < notificationCount; i++)
        {
            var parcelId = DateTime.Now.Ticks + i;
            tasks.Add(_client.NotifyParcelDetectedAsync(parcelId));
        }

        await Task.WhenAll(tasks);

        // Wait for all responses
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(notificationCount, notifications.Count);
    }

    public void Dispose()
    {
        _client?.Dispose();
        _mockServer?.Dispose();
    }
}
```

## Mock Server Requirements

For realistic E2E testing, mock servers should:

1. **Support Multiple Behaviors**: Normal, timeout, error, delayed response
2. **Thread-Safe**: Handle concurrent connections
3. **Configurable**: Adjustable timeouts, failure rates
4. **Observable**: Provide metrics (requests received, responses sent)

## Running E2E Tests

### Local Development

```bash
# Run all E2E tests
dotnet test --filter "Category=E2E"

# Run specific protocol E2E tests
dotnet test --filter "FullyQualifiedName~TcpCommunicationE2ETests"

# Run with verbose output
dotnet test --filter "Category=E2E" --logger "console;verbosity=detailed"
```

### CI/CD Pipeline

```yaml
# .github/workflows/e2e-tests.yml
name: E2E Tests

on:
  pull_request:
    paths:
      - 'src/Infrastructure/ZakYip.WheelDiverterSorter.Communication/**'
      - 'tests/ZakYip.WheelDiverterSorter.E2ETests/**'

jobs:
  e2e-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'
      
      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E" --logger trx
      
      - name: Upload Test Results
        if: always()
        uses: actions/upload-artifact@v3
        with:
          name: e2e-test-results
          path: '**/TestResults/*.trx'
```

## Performance Benchmarks

Use BenchmarkDotNet for performance testing:

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;

[MemoryDiagnoser]
[SimpleJob(warmupCount: 3, targetCount: 10)]
public class CommunicationBenchmarks
{
    private TcpRuleEngineClient? _client;

    [GlobalSetup]
    public void Setup()
    {
        // Initialize client
    }

    [Benchmark]
    public async Task NotifyParcelDetected()
    {
        await _client!.NotifyParcelDetectedAsync(DateTime.Now.Ticks);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _client?.Dispose();
    }
}
```

Run benchmarks:

```bash
dotnet run -c Release --project tests/ZakYip.WheelDiverterSorter.Benchmarks
```

## Monitoring and Observability

### Logging

Enable detailed logging during E2E tests:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "ZakYip.WheelDiverterSorter.Communication": "Trace"
    }
  }
}
```

### Metrics

Track key metrics during E2E tests:

- **Request Latency**: Time from notification to push reception
- **Success Rate**: Percentage of successful notifications
- **Timeout Rate**: Percentage of TTL timeouts
- **Reconnection Count**: Number of reconnections during test

### Example Metrics Collection

```csharp
public class MetricsCollector
{
    public int TotalRequests { get; private set; }
    public int SuccessfulRequests { get; private set; }
    public int TimeoutRequests { get; private set; }
    public List<TimeSpan> Latencies { get; } = new();

    public void RecordSuccess(TimeSpan latency)
    {
        TotalRequests++;
        SuccessfulRequests++;
        Latencies.Add(latency);
    }

    public void RecordTimeout()
    {
        TotalRequests++;
        TimeoutRequests++;
    }

    public double SuccessRate => TotalRequests > 0 
        ? (double)SuccessfulRequests / TotalRequests * 100 
        : 0;

    public TimeSpan AverageLatency => Latencies.Any() 
        ? TimeSpan.FromTicks((long)Latencies.Average(l => l.Ticks)) 
        : TimeSpan.Zero;

    public TimeSpan P95Latency => Latencies.Any() 
        ? Latencies.OrderBy(l => l).ElementAt((int)(Latencies.Count * 0.95)) 
        : TimeSpan.Zero;
}
```

## Best Practices

1. **Isolated Tests**: Each test should be independent and not affect others
2. **Cleanup**: Always dispose resources in finally blocks or IDisposable
3. **Timeouts**: Use reasonable timeouts to prevent hanging tests
4. **Assertions**: Clear, specific assertions with meaningful messages
5. **Retry Logic**: Don't retry in tests - let failures surface issues
6. **Documentation**: Document test purpose, setup, and expected behavior

## Related Documents

- [COMMUNICATION_DEVELOPER_GUIDE.md](../COMMUNICATION_DEVELOPER_GUIDE.md) - Developer guide
- [COMMUNICATION_INTEGRATION.md](../COMMUNICATION_INTEGRATION.md) - Integration guide
- [TESTING.md](../TESTING.md) - General testing guide

## Summary

E2E tests provide confidence that the Communication layer works correctly in realistic scenarios. By following this guide, developers can create comprehensive test suites that validate protocol implementations, error handling, and performance characteristics.
