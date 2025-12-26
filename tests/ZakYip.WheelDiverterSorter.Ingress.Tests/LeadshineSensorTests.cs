using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.Hardware.Ports;
using ZakYip.WheelDiverterSorter.Core.Utilities;
using ZakYip.WheelDiverterSorter.Ingress.Sensors;
using ZakYip.WheelDiverterSorter.Observability.Utilities;
using Microsoft.Extensions.Logging;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests;

/// <summary>
/// Tests for LeadshineSensor state change ignore window functionality
/// </summary>
public class LeadshineSensorTests
{
    [Fact]
    public async Task StateChangeIgnoreWindow_ShouldIgnoreAllStateChangesWithinWindow()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockLogDeduplicator = new Mock<ILogDeduplicator>();
        var mockInputPort = new Mock<IInputPort>();
        var mockClock = new Mock<ISystemClock>();
        
        var baseTime = DateTimeOffset.Now;
        mockClock.Setup(c => c.LocalNowOffset).Returns(baseTime);
        
        var sensorId = 1L;
        var inputBit = 0;
        var stateChangeIgnoreWindowMs = 200;
        
        var sensor = new LeadshineSensor(
            mockLogger.Object,
            mockLogDeduplicator.Object,
            sensorId,
            SensorType.Photoelectric,
            mockInputPort.Object,
            inputBit,
            mockClock.Object,
            pollingIntervalMs: 10,
            stateChangeIgnoreWindowMs: stateChangeIgnoreWindowMs
        );
        
        var eventCount = 0;
        sensor.SensorTriggered += (sender, e) => eventCount++;
        
        // Setup input port to simulate hollow parcel scenario
        var readSequence = new Queue<bool>();
        readSequence.Enqueue(false); // Initial state
        readSequence.Enqueue(true);  // Rising edge 1 (T0)
        readSequence.Enqueue(false); // Falling edge (T0+50ms) - should be ignored
        readSequence.Enqueue(true);  // Rising edge 2 (T0+100ms) - should be ignored
        readSequence.Enqueue(false); // Falling edge (T0+150ms) - should be ignored
        readSequence.Enqueue(true);  // Rising edge 3 (T0+250ms) - outside window, should trigger
        
        var currentTime = baseTime;
        mockInputPort.Setup(p => p.ReadAsync(inputBit))
            .ReturnsAsync(() => readSequence.Count > 0 ? readSequence.Dequeue() : false)
            .Callback(() =>
            {
                // Advance time by 50ms for each read
                currentTime = currentTime.AddMilliseconds(50);
                mockClock.Setup(c => c.LocalNowOffset).Returns(currentTime);
            });
        
        // Act
        using var cts = new CancellationTokenSource();
        var startTask = sensor.StartAsync(cts.Token);
        
        // Wait for all reads to complete
        await Task.Delay(500);
        cts.Cancel();
        
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        await sensor.StopAsync();
        
        // Assert
        // Should have 2 events: first rising edge + rising edge after window expires
        Assert.Equal(2, eventCount);
    }
    
    [Fact]
    public async Task StateChangeIgnoreWindow_Disabled_ShouldDetectAllStateChanges()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockLogDeduplicator = new Mock<ILogDeduplicator>();
        var mockInputPort = new Mock<IInputPort>();
        var mockClock = new Mock<ISystemClock>();
        
        var baseTime = DateTimeOffset.Now;
        mockClock.Setup(c => c.LocalNowOffset).Returns(baseTime);
        
        var sensorId = 1L;
        var inputBit = 0;
        var stateChangeIgnoreWindowMs = 0; // Disabled
        
        var sensor = new LeadshineSensor(
            mockLogger.Object,
            mockLogDeduplicator.Object,
            sensorId,
            SensorType.Photoelectric,
            mockInputPort.Object,
            inputBit,
            mockClock.Object,
            pollingIntervalMs: 10,
            stateChangeIgnoreWindowMs: stateChangeIgnoreWindowMs
        );
        
        var eventCount = 0;
        sensor.SensorTriggered += (sender, e) => eventCount++;
        
        // Setup input port to return alternating states
        var readSequence = new Queue<bool>(new[] { false, true, false, true, false });
        
        mockInputPort.Setup(p => p.ReadAsync(inputBit))
            .ReturnsAsync(() => readSequence.Count > 0 ? readSequence.Dequeue() : false);
        
        // Act
        using var cts = new CancellationTokenSource();
        var startTask = sensor.StartAsync(cts.Token);
        
        await Task.Delay(300);
        cts.Cancel();
        
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        await sensor.StopAsync();
        
        // Assert
        // All 4 state changes should be detected when window is disabled
        Assert.Equal(4, eventCount);
    }
    
    [Fact]
    public async Task StateChangeIgnoreWindow_ShouldUpdateLastStateEvenWhenIgnoring()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockLogDeduplicator = new Mock<ILogDeduplicator>();
        var mockInputPort = new Mock<IInputPort>();
        var mockClock = new Mock<ISystemClock>();
        
        var baseTime = DateTimeOffset.Now;
        var currentTime = baseTime;
        mockClock.Setup(c => c.LocalNowOffset).Returns(() => currentTime);
        
        var sensorId = 1L;
        var inputBit = 0;
        var stateChangeIgnoreWindowMs = 200;
        
        var sensor = new LeadshineSensor(
            mockLogger.Object,
            mockLogDeduplicator.Object,
            sensorId,
            SensorType.Photoelectric,
            mockInputPort.Object,
            inputBit,
            mockClock.Object,
            pollingIntervalMs: 10,
            stateChangeIgnoreWindowMs: stateChangeIgnoreWindowMs
        );
        
        var events = new List<SensorEvent>();
        sensor.SensorTriggered += (sender, e) => events.Add(e);
        
        // Setup to simulate: false -> true (T0) -> false (T50, ignored) -> false (T60, should not re-detect)
        var readCalls = 0;
        mockInputPort.Setup(p => p.ReadAsync(inputBit))
            .ReturnsAsync(() =>
            {
                readCalls++;
                return readCalls switch
                {
                    1 => false, // Initial
                    2 => true,  // Rising edge at T0
                    3 => false, // Falling edge at T50 (ignored)
                    _ => false  // Stay false (should not trigger again)
                };
            })
            .Callback(() =>
            {
                currentTime = currentTime.AddMilliseconds(10);
            });
        
        // Act
        using var cts = new CancellationTokenSource();
        var startTask = sensor.StartAsync(cts.Token);
        
        await Task.Delay(500);
        cts.Cancel();
        
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        await sensor.StopAsync();
        
        // Assert
        // Should only have 1 event (the first rising edge)
        // The falling edge is ignored, but _lastState should be updated to prevent re-detection
        Assert.Single(events);
        Assert.True(events[0].IsTriggered);
    }
    
    [Fact]
    public async Task StateChangeIgnoreWindow_OutsideWindow_ShouldDetectStateChange()
    {
        // Arrange
        var mockLogger = new Mock<ILogger>();
        var mockLogDeduplicator = new Mock<ILogDeduplicator>();
        var mockInputPort = new Mock<IInputPort>();
        var mockClock = new Mock<ISystemClock>();
        
        var baseTime = DateTimeOffset.Now;
        var currentTime = baseTime;
        mockClock.Setup(c => c.LocalNowOffset).Returns(() => currentTime);
        
        var sensorId = 1L;
        var inputBit = 0;
        var stateChangeIgnoreWindowMs = 100;
        
        var sensor = new LeadshineSensor(
            mockLogger.Object,
            mockLogDeduplicator.Object,
            sensorId,
            SensorType.Photoelectric,
            mockInputPort.Object,
            inputBit,
            mockClock.Object,
            pollingIntervalMs: 10,
            stateChangeIgnoreWindowMs: stateChangeIgnoreWindowMs
        );
        
        var events = new List<SensorEvent>();
        sensor.SensorTriggered += (sender, e) => events.Add(e);
        
        // Setup: false -> true (T0) -> false (T150, outside window)
        var readCalls = 0;
        mockInputPort.Setup(p => p.ReadAsync(inputBit))
            .ReturnsAsync(() =>
            {
                readCalls++;
                if (readCalls == 1) return false; // Initial
                if (readCalls == 2) return true;  // Rising edge at T0
                if (readCalls <= 15) return true; // Stay true until T150
                return false; // Falling edge at T150 (outside window)
            })
            .Callback(() =>
            {
                currentTime = currentTime.AddMilliseconds(10);
            });
        
        // Act
        using var cts = new CancellationTokenSource();
        var startTask = sensor.StartAsync(cts.Token);
        
        await Task.Delay(400);
        cts.Cancel();
        
        try
        {
            await startTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }
        
        await sensor.StopAsync();
        
        // Assert
        // Should have 2 events: rising edge + falling edge (outside window)
        Assert.Equal(2, events.Count);
        Assert.True(events[0].IsTriggered);
        Assert.False(events[1].IsTriggered);
    }
}
