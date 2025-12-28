using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Events.Sensor;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Ingress;
using ZakYip.WheelDiverterSorter.Ingress.Configuration;
using ZakYip.WheelDiverterSorter.Ingress.Models;
using ZakYip.WheelDiverterSorter.Ingress.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Core.Abstractions.Configuration;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests;

public class ParcelDetectionServiceTests
{
    /// <summary>
    /// 创建带传感器配置的 Mock 服务
    /// </summary>
    private Mock<ISensorConfigService> CreateMockSensorService(long sensorId, int deduplicationWindowMs = 400)
    {
        var mockService = new Mock<ISensorConfigService>();
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
        mockService.Setup(s => s.GetSensorConfig()).Returns(sensorConfig);
        return mockService;
    }

    /// <summary>
    /// 创建带多个传感器配置的 Mock 服务
    /// </summary>
    private Mock<ISensorConfigService> CreateMockSensorService(Dictionary<long, int> sensorDeduplicationWindows)
    {
        var mockService = new Mock<ISensorConfigService>();
        var sensorConfig = new SensorConfiguration
        {
            Sensors = sensorDeduplicationWindows.Select((kvp, index) => new SensorIoEntry
            {
                SensorId = kvp.Key,
                SensorName = $"Test Sensor {kvp.Key}",
                IoType = SensorIoType.ParcelCreation,
                BitNumber = (int)kvp.Key,
                DeduplicationWindowMs = kvp.Value,
                IsEnabled = true
            }).ToList()
        };
        mockService.Setup(s => s.GetSensorConfig()).Returns(sensorConfig);
        return mockService;
    }

    /// <summary>
    /// 创建带传感器配置的 Mock 仓储（已弃用，使用 CreateMockSensorService 代替）
    /// </summary>
    [Obsolete("Use CreateMockSensorService instead. This helper uses a repository-based configuration which is deprecated in favor of ISensorConfigService mocking to avoid hot path DB access (Rule 5.2).")]
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

    /// <summary>
    /// 创建带多个传感器配置的 Mock 仓储（已弃用，使用 CreateMockSensorService 代替）
    /// </summary>
    [Obsolete("Use CreateMockSensorService instead. This helper uses a repository-based configuration which is deprecated in favor of ISensorConfigService mocking to avoid hot path DB access (Rule 5.2).")]
    private Mock<ISensorConfigurationRepository> CreateMockSensorRepository(Dictionary<long, int> sensorDeduplicationWindows)
    {
        var mockRepo = new Mock<ISensorConfigurationRepository>();
        var sensorConfig = new SensorConfiguration
        {
            Sensors = sensorDeduplicationWindows.Select((kvp, index) => new SensorIoEntry
            {
                SensorId = kvp.Key,
                SensorName = $"Test Sensor {kvp.Key}",
                IoType = SensorIoType.ParcelCreation,
                BitNumber = (int)kvp.Key,
                DeduplicationWindowMs = kvp.Value,
                IsEnabled = true
            }).ToList()
        };
        mockRepo.Setup(r => r.Get()).Returns(sensorConfig);
        return mockRepo;
    }

    [Fact]
    public async Task GenerateParcelId_ShouldBeUnique()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(sensors, options);

        var detectedParcelIds = new List<long>();
        service.ParcelDetected += (sender, e) => detectedParcelIds.Add(e.ParcelId);

        // Start the service to subscribe to sensor events
        await service.StartAsync();

        // Act - Trigger multiple times
        for (int i = 0; i < 5; i++)
        {
            var sensorEvent = new SensorEvent
            {
                SensorId = 1,
                SensorType = SensorType.Photoelectric,
                TriggerTime = DateTimeOffset.Now.AddMilliseconds(i * 1500),
                IsTriggered = true
            };

            mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);
            Thread.Sleep(10); // Small delay to ensure different timestamps
        }

        // Assert
        Assert.Equal(5, detectedParcelIds.Count);
        Assert.Equal(detectedParcelIds.Count, detectedParcelIds.Distinct().Count()); // All IDs should be unique
    }

    [Fact]
    public async Task DeduplicationWindow_ShouldDetectDuplicateTriggersWithinWindow()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorService(1, deduplicationWindowMs: 500);
        var service = new ParcelDetectionService(sensors, options, sensorConfigService: mockSensorRepo.Object);

        var detectedCount = 0;
        var duplicateCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;
        service.DuplicateTriggerDetected += (sender, e) => duplicateCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger twice within deduplication window
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(300), // Within 500ms window
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Only first trigger creates parcel, second trigger is completely ignored
        Assert.Equal(1, detectedCount); // Changed from 2 to 1 - second trigger is ignored
        Assert.Equal(1, duplicateCount); // Duplicate event is still raised for monitoring
    }

    [Fact]
    public async Task DeduplicationWindow_ShouldAllowTriggersOutsideWindow()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorService(1, deduplicationWindowMs: 500);
        var service = new ParcelDetectionService(sensors, options, sensorConfigService: mockSensorRepo.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger twice outside deduplication window
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(600), // Outside 500ms window
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Should detect both
        Assert.Equal(2, detectedCount);
    }

    [Fact]
    public async Task ParcelDetectedEventArgs_ShouldContainCorrectInformation()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(sensors, options);

        ParcelDetectedEventArgs? detectedArgs = null;
        service.ParcelDetected += (sender, e) => detectedArgs = e;

        // Start the service
        await service.StartAsync();

        var triggerTime = DateTimeOffset.Now;

        // Act
        var sensorEvent = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = triggerTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);

        // Assert
        Assert.NotNull(detectedArgs);
        Assert.True(detectedArgs.ParcelId > 0); // ParcelId should be a positive timestamp
        Assert.Equal(triggerTime, detectedArgs.DetectedAt);
        Assert.Equal(1, detectedArgs.SensorId);
        Assert.Equal(SensorType.Photoelectric, detectedArgs.SensorType);
        Assert.Equal("1", detectedArgs.Position); // Position should match SensorId
    }

    [Fact]
    public async Task OnlyTriggeredEvents_ShouldBeProcessed()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(sensors, options);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        // Act - Trigger with IsTriggered = false
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = DateTimeOffset.Now,
            IsTriggered = false // Not triggered
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        // Act - Trigger with IsTriggered = true
        var sensorEvent2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = DateTimeOffset.Now.AddSeconds(1),
            IsTriggered = true // Triggered
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Should only detect the triggered event
        Assert.Equal(1, detectedCount);
    }

    [Fact]
    public async Task MultipleSensors_ShouldTrackSeparately()
    {
        // Arrange
        var mockSensor1 = new Mock<ISensor>();
        mockSensor1.Setup(s => s.SensorId).Returns(1);
        mockSensor1.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor1.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mockSensor2 = new Mock<ISensor>();
        mockSensor2.Setup(s => s.SensorId).Returns(2);
        mockSensor2.Setup(s => s.Type).Returns(SensorType.Laser);
        mockSensor2.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor1.Object, mockSensor2.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorService(new Dictionary<long, int>
        {
            { 1, 1000 },
            { 2, 1000 }
        });
        var service = new ParcelDetectionService(sensors, options, sensorConfigService: mockSensorRepo.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger both sensors at the same time
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor1.Raise(s => s.SensorTriggered += null, mockSensor1.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = 2,
            SensorType = SensorType.Laser,
            TriggerTime = baseTime.AddMilliseconds(100),
            IsTriggered = true
        };
        mockSensor2.Raise(s => s.SensorTriggered += null, mockSensor2.Object, sensorEvent2);

        // Assert - Both should be detected (different sensors)
        Assert.Equal(2, detectedCount);
    }

    [Fact]
    public async Task DuplicateTriggerEvent_ShouldContainCorrectInformation()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions());
        var mockSensorRepo = CreateMockSensorService(1, deduplicationWindowMs: 500);
        var service = new ParcelDetectionService(sensors, options, sensorConfigService: mockSensorRepo.Object);

        ZakYip.WheelDiverterSorter.Core.Events.Sensor.DuplicateTriggerEventArgs? duplicateArgs = null;
        service.DuplicateTriggerDetected += (sender, e) => duplicateArgs = e;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger twice within deduplication window
        var sensorEvent1 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = 1,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(300), // Within 500ms window
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Duplicate event should be triggered with correct information
        Assert.NotNull(duplicateArgs);
        Assert.Equal(0, duplicateArgs.ParcelId); // Changed: ParcelId is 0 because duplicate triggers don't create parcels
        Assert.Equal(1, duplicateArgs.SensorId);
        Assert.Equal(SensorType.Photoelectric, duplicateArgs.SensorType);
        Assert.Equal(baseTime.AddMilliseconds(300), duplicateArgs.DetectedAt);
        Assert.True(duplicateArgs.TimeSinceLastTriggerMs >= 299 && duplicateArgs.TimeSinceLastTriggerMs <= 301); // Allow small tolerance
        Assert.Contains("去重窗口", duplicateArgs.Reason);
        Assert.Contains("已忽略", duplicateArgs.Reason); // Verify the reason says it was ignored
    }

    [Fact]
    public async Task ParcelIdHistory_ShouldLimitSize()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(1);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            ParcelIdHistorySize = 10 // Small history size for testing
        });
        var mockSensorRepo = CreateMockSensorService(1, deduplicationWindowMs: 100); // Small window to allow rapid triggering
        var service = new ParcelDetectionService(sensors, options, sensorConfigService: mockSensorRepo.Object);

        var detectedParcelIds = new List<long>();
        service.ParcelDetected += (sender, e) => detectedParcelIds.Add(e.ParcelId);

        // Start the service
        await service.StartAsync();

        // Act - Generate more parcels than history size
        for (int i = 0; i < 15; i++)
        {
            var sensorEvent = new SensorEvent
            {
                SensorId = 1,
                SensorType = SensorType.Photoelectric,
                TriggerTime = DateTimeOffset.Now.AddMilliseconds(i * 150),
                IsTriggered = true
            };
            mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);
            Thread.Sleep(10); // Small delay
        }

        // Assert - Should have generated 15 parcels
        Assert.Equal(15, detectedParcelIds.Count);
        // All should still be unique (within the test scope)
        Assert.Equal(detectedParcelIds.Count, detectedParcelIds.Distinct().Count());
    }

    [Fact]
    public async Task UnknownSensorType_ShouldNotTriggerParcelCreation()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(999);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        
        // Mock repository that returns a sensor with Unknown type
        var mockConfig = new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() 
                { 
                    SensorId = 999, 
                    IoType = SensorIoType.Unknown,
                    BitNumber = 0,
                    IsEnabled = true 
                }
            }
        };
        
        var mockRepository = new Mock<ISensorConfigService>();
        mockRepository.Setup(s => s.GetSensorConfig()).Returns(mockConfig);
        
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(
            sensors, 
            options,
            sensorConfigService: mockRepository.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger unknown sensor
        var sensorEvent = new SensorEvent
        {
            SensorId = 999,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);

        // Assert - Should NOT create parcel (count should be 0)
        Assert.Equal(0, detectedCount);
    }

    [Fact]
    public async Task UnconfiguredSensor_ShouldReturnUnknownType()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(888);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        
        // Mock repository that returns empty configuration (sensor not found)
        var mockConfig = new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>()
        };
        
        var mockRepository = new Mock<ISensorConfigService>();
        mockRepository.Setup(s => s.GetSensorConfig()).Returns(mockConfig);
        
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(
            sensors, 
            options,
            sensorConfigService: mockRepository.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger unconfigured sensor
        var sensorEvent = new SensorEvent
        {
            SensorId = 888,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);

        // Assert - Should NOT create parcel (sensor not configured -> Unknown type)
        Assert.Equal(0, detectedCount);
    }

    [Fact]
    public async Task DisabledSensor_ShouldReturnUnknownType()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns(777);
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        
        // Mock repository that returns a disabled sensor
        var mockConfig = new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() 
                { 
                    SensorId = 777, 
                    IoType = SensorIoType.ParcelCreation,
                    BitNumber = 0,
                    IsEnabled = false  // Disabled
                }
            }
        };
        
        var mockRepository = new Mock<ISensorConfigService>();
        mockRepository.Setup(s => s.GetSensorConfig()).Returns(mockConfig);
        
        var options = Options.Create(new ParcelDetectionOptions());
        var service = new ParcelDetectionService(
            sensors, 
            options,
            sensorConfigService: mockRepository.Object);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger disabled sensor
        var sensorEvent = new SensorEvent
        {
            SensorId = 777,
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);

        // Assert - Should NOT create parcel (disabled sensor -> Unknown type)
        Assert.Equal(0, detectedCount);
    }
}
