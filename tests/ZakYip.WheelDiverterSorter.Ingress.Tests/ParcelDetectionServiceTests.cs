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

namespace ZakYip.WheelDiverterSorter.Ingress.Tests;

public class ParcelDetectionServiceTests
{
    [Fact]
    public async Task GenerateParcelId_ShouldBeUnique()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
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
                SensorId = "SENSOR_01",
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
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            DeduplicationWindowMs = 500 // 500ms window
        });
        var service = new ParcelDetectionService(sensors, options);

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
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(300), // Within 500ms window
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Should detect both parcels, second one flagged as duplicate
        Assert.Equal(2, detectedCount);
        Assert.Equal(1, duplicateCount);
    }

    [Fact]
    public async Task DeduplicationWindow_ShouldAllowTriggersOutsideWindow()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            DeduplicationWindowMs = 500 // 500ms window
        });
        var service = new ParcelDetectionService(sensors, options);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger twice outside deduplication window
        var sensorEvent1 = new SensorEvent
        {
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = "SENSOR_01",
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
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
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
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = triggerTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent);

        // Assert
        Assert.NotNull(detectedArgs);
        Assert.True(detectedArgs.ParcelId > 0); // ParcelId should be a positive timestamp
        Assert.Equal(triggerTime, detectedArgs.DetectedAt);
        Assert.Equal("SENSOR_01", detectedArgs.SensorId);
        Assert.Equal(SensorType.Photoelectric, detectedArgs.SensorType);
        Assert.Equal("SENSOR_01", detectedArgs.Position); // Position should match SensorId
    }

    [Fact]
    public async Task OnlyTriggeredEvents_ShouldBeProcessed()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
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
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = DateTimeOffset.Now,
            IsTriggered = false // Not triggered
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        // Act - Trigger with IsTriggered = true
        var sensorEvent2 = new SensorEvent
        {
            SensorId = "SENSOR_01",
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
        mockSensor1.Setup(s => s.SensorId).Returns("SENSOR_01");
        mockSensor1.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor1.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mockSensor2 = new Mock<ISensor>();
        mockSensor2.Setup(s => s.SensorId).Returns("SENSOR_02");
        mockSensor2.Setup(s => s.Type).Returns(SensorType.Laser);
        mockSensor2.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor1.Object, mockSensor2.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            DeduplicationWindowMs = 1000
        });
        var service = new ParcelDetectionService(sensors, options);

        var detectedCount = 0;
        service.ParcelDetected += (sender, e) => detectedCount++;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger both sensors at the same time
        var sensorEvent1 = new SensorEvent
        {
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor1.Raise(s => s.SensorTriggered += null, mockSensor1.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = "SENSOR_02",
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
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            DeduplicationWindowMs = 500 // 500ms window
        });
        var service = new ParcelDetectionService(sensors, options);

        ZakYip.WheelDiverterSorter.Core.Events.Sensor.DuplicateTriggerEventArgs? duplicateArgs = null;
        service.DuplicateTriggerDetected += (sender, e) => duplicateArgs = e;

        // Start the service
        await service.StartAsync();

        var baseTime = DateTimeOffset.Now;

        // Act - Trigger twice within deduplication window
        var sensorEvent1 = new SensorEvent
        {
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime,
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent1);

        var sensorEvent2 = new SensorEvent
        {
            SensorId = "SENSOR_01",
            SensorType = SensorType.Photoelectric,
            TriggerTime = baseTime.AddMilliseconds(300), // Within 500ms window
            IsTriggered = true
        };
        mockSensor.Raise(s => s.SensorTriggered += null, mockSensor.Object, sensorEvent2);

        // Assert - Duplicate event should be triggered with correct information
        Assert.NotNull(duplicateArgs);
        Assert.True(duplicateArgs.ParcelId > 0);
        Assert.Equal("SENSOR_01", duplicateArgs.SensorId);
        Assert.Equal(SensorType.Photoelectric, duplicateArgs.SensorType);
        Assert.Equal(baseTime.AddMilliseconds(300), duplicateArgs.DetectedAt);
        Assert.True(duplicateArgs.TimeSinceLastTriggerMs >= 299 && duplicateArgs.TimeSinceLastTriggerMs <= 301); // Allow small tolerance
        Assert.Contains("去重窗口", duplicateArgs.Reason);
    }

    [Fact]
    public async Task ParcelIdHistory_ShouldLimitSize()
    {
        // Arrange
        var mockSensor = new Mock<ISensor>();
        mockSensor.Setup(s => s.SensorId).Returns("SENSOR_01");
        mockSensor.Setup(s => s.Type).Returns(SensorType.Photoelectric);
        mockSensor.Setup(s => s.StartAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var sensors = new[] { mockSensor.Object };
        var options = Options.Create(new ParcelDetectionOptions
        {
            ParcelIdHistorySize = 10, // Small history size for testing
            DeduplicationWindowMs = 100 // Small window to allow rapid triggering
        });
        var service = new ParcelDetectionService(sensors, options);

        var detectedParcelIds = new List<long>();
        service.ParcelDetected += (sender, e) => detectedParcelIds.Add(e.ParcelId);

        // Start the service
        await service.StartAsync();

        // Act - Generate more parcels than history size
        for (int i = 0; i < 15; i++)
        {
            var sensorEvent = new SensorEvent
            {
                SensorId = "SENSOR_01",
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
}
