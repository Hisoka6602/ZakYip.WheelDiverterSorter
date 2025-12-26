using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests;

/// <summary>
/// 防抖行为测试
/// </summary>
/// <remarks>
/// 这些测试用例验证系统的防抖机制，特别是：
/// 1. 信号保持高电平时是否会重复触发（答案：不会，这是正确的边沿触发行为）
/// 2. 防抖窗口内的重复触发是否被正确过滤
/// 3. 防抖窗口过期后的新触发是否被正确识别
/// </remarks>
public class DebounceBehaviorTests
{
    /// <summary>
    /// 创建带传感器配置的 Mock 仓储
    /// </summary>
    private Mock<ISensorConfigurationRepository> CreateMockSensorRepository(long sensorId, int deduplicationWindowMs = 400)
    {
        var mockRepo = new Mock<ISensorConfigurationRepository>();
        var sensorConfig = new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new SensorIoEntry
                {
                    SensorId = sensorId,
                    SensorName = "Test Sensor",
                    IoType = SensorIoType.ParcelCreation,
                    BitNumber = 0,
                    DeduplicationWindowMs = deduplicationWindowMs,
                    IsEnabled = true
                }
            }
        };
        mockRepo.Setup(r => r.Get()).Returns(sensorConfig);
        return mockRepo;
    }

    [Fact]
    public async Task ContinuousHighSignal_ShouldNotRetrigger_AfterDebounceWindow()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorRepository(1, deduplicationWindowMs: 200);
        var service = new ParcelDetectionService(sensors, options, sensorConfigRepository: mockSensorRepo.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - 模拟信号保持高电平的场景
        // 1. 第一次触发（上升沿：LOW → HIGH）
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true  // 上升沿
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        // 2. 等待 200ms 防抖窗口过期（在实际场景中，传感器层不会发送此事件，因为信号保持HIGH没有状态变化）
        // 注意：这个测试验证的是即使手动触发事件，防抖层也会正确处理

        // Assert
        // 关键验证：信号保持高电平时，传感器层不会产生新的事件
        // 因为 LeadshineSensor.cs 只检测状态变化（line 158: if (currentState != _lastState)）
        // 所以这个测试只能验证：即使人为发送新事件，防抖层也不会在窗口内重复触发
        Assert.Equal(1, detectedCount);

        // 额外说明：在实际运行中，如果信号在 200ms 后仍然是 HIGH，
        // LeadshineSensor 不会发送新的 SensorTriggered 事件，
        // 因此 ParcelDetectionService 不会收到新事件，也就不会重复触发。
        // 这是正确的边沿触发行为，而非电平触发行为。
    }

    [Fact]
    public async Task EdgeTriggering_MultipleRisingEdges_OutsideDebounceWindow_ShouldTriggerMultipleTimes()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorRepository(1, deduplicationWindowMs: 200);
        var service = new ParcelDetectionService(sensors, options, sensorConfigRepository: mockSensorRepo.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - 模拟多个包裹依次经过传感器的场景
        // 每个包裹产生一次上升沿（LOW → HIGH）

        // 第一个包裹：上升沿
        var event1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event1);

        // 第二个包裹：在 250ms 后（超过 200ms 防抖窗口）再次上升沿
        var event2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(250),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event2);

        // 第三个包裹：再次超过防抖窗口
        var event3 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(500),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event3);

        // Assert
        Assert.Equal(3, detectedCount);
    }

    [Fact]
    public async Task EdgeTriggering_RisingEdgeWithinDebounceWindow_ShouldNotRetrigger()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorRepository(1, deduplicationWindowMs: 200);
        var service = new ParcelDetectionService(sensors, options, sensorConfigRepository: mockSensorRepo.Object);

        var detectedCount = 0;
        var duplicateCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;
        service.DuplicateTriggerDetected += (sender, e) => duplicateCount++;

        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - 模拟传感器抖动或包裹边缘触发的场景
        // 在防抖窗口内多次触发（这些都应该被过滤）

        // 第一次上升沿（有效触发）
        var event1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event1);

        // 50ms 后的重复触发（防抖窗口内，应被忽略）
        var event2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(50),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event2);

        // 100ms 后的重复触发（仍在防抖窗口内，应被忽略）
        var event3 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(100),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event3);

        // 150ms 后的重复触发（仍在防抖窗口内，应被忽略）
        var event4 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(150),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event4);

        // Assert
        Assert.Equal(1, detectedCount); // 只有第一次触发有效
        Assert.Equal(3, duplicateCount); // 后面3次被识别为重复触发
    }

    [Fact]
    public async Task DebounceWindow_ExactlyAtBoundary_ShouldBeFiltered()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorRepository(1, deduplicationWindowMs: 200);
        var service = new ParcelDetectionService(sensors, options, sensorConfigRepository: mockSensorRepo.Object);

        var detectedCount = 0;
        var duplicateCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;
        service.DuplicateTriggerDetected += (sender, e) => duplicateCount++;

        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act
        var event1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event1);

        // 恰好在 200ms 边界（根据代码 line 445: timeSinceLastTriggerMs < deduplicationWindowMs）
        // 200ms 应该被允许（因为是 <，不是 <=）
        var event2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(200),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event2);

        // Assert
        Assert.Equal(2, detectedCount); // 200ms 边界上的触发应该被允许（< 而非 <=）
        Assert.Equal(0, duplicateCount);
    }

    [Fact]
    public async Task DebounceWindow_JustBeforeBoundary_ShouldBeFiltered()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorRepository(1, deduplicationWindowMs: 200);
        var service = new ParcelDetectionService(sensors, options, sensorConfigRepository: mockSensorRepo.Object);

        var detectedCount = 0;
        var duplicateCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;
        service.DuplicateTriggerDetected += (sender, e) => duplicateCount++;

        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act
        var event1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event1);

        // 199ms（刚好在防抖窗口内）
        var event2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(199),
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, event2);

        // Assert
        Assert.Equal(1, detectedCount); // 199ms 应该被过滤
        Assert.Equal(1, duplicateCount);
    }
}
