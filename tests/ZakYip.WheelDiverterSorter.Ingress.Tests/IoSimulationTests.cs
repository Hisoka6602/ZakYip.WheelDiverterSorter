using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using ZakYip.WheelDiverterSorter.Drivers.Abstractions;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Simulated;

namespace ZakYip.WheelDiverterSorter.Ingress.Tests;

/// <summary>
/// IO simulation tests covering sensor debouncing, configuration errors, and high-load scenarios
/// </summary>
public class IoSimulationTests
{
    [Fact]
    public async Task SensorDebounce_RapidFlickering_FilteredCorrectly()
    {
        // Arrange
        var mockInputPort = new Mock<IInputPort>();
        var readSequence = new Queue<bool>(new[] 
        { 
            true, false, true, false, true,  // Rapid flickering
            true, true, true, true, true      // Stable high
        });

        mockInputPort.Setup(p => p.ReadAsync(It.IsAny<int>()))
            .ReturnsAsync(() => readSequence.Count > 0 ? readSequence.Dequeue() : true);

        var debounceWindowMs = 50;
        var stableReadings = new List<bool>();

        // Act - Read with debounce logic
        for (int i = 0; i < 10; i++)
        {
            var reading = await mockInputPort.Object.ReadAsync(0);
            
            // Simple debounce: wait a bit and read again to confirm
            await Task.Delay(debounceWindowMs);
            var confirmReading = await mockInputPort.Object.ReadAsync(0);
            
            // Only accept if reading is stable
            if (reading == confirmReading)
            {
                stableReadings.Add(reading);
            }
        }

        // Assert
        Assert.NotEmpty(stableReadings);
        // Should have filtered out the flickering and only captured stable readings
        var trueCount = stableReadings.Count(r => r);
        var falseCount = stableReadings.Count(r => !r);
        
        // Most readings should be true (after flickering settled)
        Assert.True(trueCount >= falseCount, "Stable readings should dominate after debounce");
    }

    [Fact]
    public async Task IoConfigurationError_WrongPortMapping_DetectedDuringStartup()
    {
        // Arrange
        var configuredPorts = new Dictionary<string, int>
        {
            { "sensor_entry", 0 },
            { "sensor_exit", 1 },
            { "sensor_diverter", 2 }
        };

        var mockIoDriver = new Mock<IIoLinkageDriver>();
        
        // Simulate port 2 being disconnected/misconfigured
        mockIoDriver.Setup(d => d.ReadIoPointAsync(0)).ReturnsAsync(false);
        mockIoDriver.Setup(d => d.ReadIoPointAsync(1)).ReturnsAsync(false);
        mockIoDriver.Setup(d => d.ReadIoPointAsync(2))
            .ThrowsAsync(new InvalidOperationException("Port 2 not found"));

        var selfTestResults = new Dictionary<string, bool>();

        // Act - Self-test all configured ports
        foreach (var port in configuredPorts)
        {
            try
            {
                var result = await mockIoDriver.Object.ReadIoPointAsync(port.Value);
                selfTestResults[port.Key] = true; // Port accessible
            }
            catch (Exception)
            {
                selfTestResults[port.Key] = false; // Port error
            }
        }

        // Assert
        Assert.True(selfTestResults["sensor_entry"], "Port 0 should be accessible");
        Assert.True(selfTestResults["sensor_exit"], "Port 1 should be accessible");
        Assert.False(selfTestResults["sensor_diverter"], "Port 2 should fail due to misconfiguration");
        
        var errorCount = selfTestResults.Values.Count(v => !v);
        Assert.Equal(1, errorCount);
    }

    [Fact]
    public async Task IoHighLoad_MultipleConcurrentOperations_ThreadSafe()
    {
        // Arrange
        var ioDriver = new SimulatedIoLinkageDriver(NullLogger<SimulatedIoLinkageDriver>.Instance);
        var operationCount = 100;
        var tasks = new List<Task>();
        var results = new ConcurrentBag<bool>();

        // Act - Simulate high load with concurrent IO operations
        for (int i = 0; i < operationCount; i++)
        {
            var bitNumber = i % 10; // Use 10 different IO points
            
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    // Concurrent writes
                    await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                    {
                        BitNumber = bitNumber,
                        Level = Core.Enums.Sensors.TriggerLevel.ActiveHigh
                    });
                    
                    await Task.Delay(1); // Small delay to increase concurrency
                    
                    // Concurrent reads
                    var state = await ioDriver.ReadIoPointAsync(bitNumber);
                    results.Add(state);
                }
                catch (Exception)
                {
                    results.Add(false); // Mark as failed
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(operationCount, results.Count);
        Assert.True(results.All(r => r), "All concurrent operations should succeed without data corruption");
        
        // Verify final state is consistent
        var finalStates = ioDriver.GetAllIoStates();
        Assert.NotEmpty(finalStates);
    }

    [Fact]
    public async Task SensorJitter_ShortBursts_HandledWithoutFalseDetection()
    {
        // Arrange
        var mockSensor = new Mock<IInputPort>();
        var jitterPattern = new Queue<bool>(new[]
        {
            false, false, false,          // Stable low
            true, false, true, false,     // Jitter
            false, false, false,          // Stable low again
            true, true, true, true        // Real detection
        });

        mockSensor.Setup(s => s.ReadAsync(It.IsAny<int>()))
            .ReturnsAsync(() => jitterPattern.Count > 0 ? jitterPattern.Dequeue() : false);

        var detectionThreshold = 3; // Need 3 consecutive readings
        var detections = new List<bool>();

        // Act - Read with consecutive threshold logic
        var consecutiveCount = 0;
        while (jitterPattern.Count > 0 || consecutiveCount > 0)
        {
            var reading = await mockSensor.Object.ReadAsync(0);
            
            if (reading)
            {
                consecutiveCount++;
                if (consecutiveCount >= detectionThreshold)
                {
                    detections.Add(true);
                    consecutiveCount = 0; // Reset after detection
                }
            }
            else
            {
                consecutiveCount = 0;
            }
            
            await Task.Delay(10);
        }

        // Assert
        Assert.Single(detections); // Should only detect once (the real 4 consecutive trues)
    }

    [Fact]
    public async Task IoOverload_RapidFireCommands_NoDeadlock()
    {
        // Arrange
        var ioDriver = new SimulatedIoLinkageDriver(NullLogger<SimulatedIoLinkageDriver>.Instance);
        var commandCount = 1000;
        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10)); // Timeout to prevent infinite hang

        // Act - Fire rapid commands
        var tasks = new List<Task>();
        for (int i = 0; i < commandCount; i++)
        {
            var bitNum = i % 20;
            tasks.Add(Task.Run(async () =>
            {
                await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                {
                    BitNumber = bitNum,
                    Level = (i % 2 == 0) 
                        ? Core.Enums.Sensors.TriggerLevel.ActiveHigh 
                        : Core.Enums.Sensors.TriggerLevel.ActiveLow
                });
            }, cts.Token));
        }

        // Wait for all to complete or timeout
        var completed = await Task.WhenAny(
            Task.WhenAll(tasks),
            Task.Delay(TimeSpan.FromSeconds(10), cts.Token)
        );

        // Assert
        Assert.Equal(Task.WhenAll(tasks), completed); // Should complete before timeout
        Assert.False(cts.Token.IsCancellationRequested, "Should complete without timeout/deadlock");
    }

    [Fact]
    public async Task IoErrorRecovery_TransientFailure_SystemContinues()
    {
        // Arrange
        var mockIoDriver = new Mock<IIoLinkageDriver>();
        var callCount = 0;
        
        mockIoDriver.Setup(d => d.ReadIoPointAsync(It.IsAny<int>()))
            .ReturnsAsync(() =>
            {
                callCount++;
                // Fail on attempts 2-4 (transient error)
                if (callCount >= 2 && callCount <= 4)
                {
                    throw new IOException("Transient IO error");
                }
                return true;
            });

        var successfulReads = 0;
        var failedReads = 0;

        // Act - Attempt reads with retry logic
        for (int i = 0; i < 10; i++)
        {
            try
            {
                await mockIoDriver.Object.ReadIoPointAsync(0);
                successfulReads++;
            }
            catch (IOException)
            {
                failedReads++;
                await Task.Delay(50); // Small backoff before retry
            }
        }

        // Assert
        Assert.Equal(3, failedReads); // Failed on attempts 2, 3, 4
        Assert.Equal(7, successfulReads); // Succeeded on 1, 5, 6, 7, 8, 9, 10
        Assert.True(successfulReads > failedReads, "Should recover and have more successes");
    }

    [Fact]
    public async Task MultiDiverter_SimultaneousIoControl_NoInterference()
    {
        // Arrange
        var ioDriver = new SimulatedIoLinkageDriver(NullLogger<SimulatedIoLinkageDriver>.Instance);
        var diverter1Bits = new[] { 0, 1, 2 };
        var diverter2Bits = new[] { 3, 4, 5 };
        var diverter3Bits = new[] { 6, 7, 8 };

        // Act - Control multiple diverters simultaneously
        var tasks = new List<Task>();
        
        // Diverter 1 operations
        tasks.Add(Task.Run(async () =>
        {
            foreach (var bit in diverter1Bits)
            {
                await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                {
                    BitNumber = bit,
                    Level = Core.Enums.Sensors.TriggerLevel.ActiveHigh
                });
            }
        }));

        // Diverter 2 operations
        tasks.Add(Task.Run(async () =>
        {
            foreach (var bit in diverter2Bits)
            {
                await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                {
                    BitNumber = bit,
                    Level = Core.Enums.Sensors.TriggerLevel.ActiveLow
                });
            }
        }));

        // Diverter 3 operations
        tasks.Add(Task.Run(async () =>
        {
            foreach (var bit in diverter3Bits)
            {
                await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                {
                    BitNumber = bit,
                    Level = Core.Enums.Sensors.TriggerLevel.ActiveHigh
                });
            }
        }));

        await Task.WhenAll(tasks);

        // Assert - Verify each diverter's bits are set correctly with no interference
        foreach (var bit in diverter1Bits)
        {
            Assert.True(await ioDriver.ReadIoPointAsync(bit), $"Diverter 1 bit {bit} should be high");
        }
        
        foreach (var bit in diverter2Bits)
        {
            Assert.False(await ioDriver.ReadIoPointAsync(bit), $"Diverter 2 bit {bit} should be low");
        }
        
        foreach (var bit in diverter3Bits)
        {
            Assert.True(await ioDriver.ReadIoPointAsync(bit), $"Diverter 3 bit {bit} should be high");
        }
    }

    [Fact]
    public async Task IoStateConsistency_ConcurrentReadWrite_MaintainsDataIntegrity()
    {
        // Arrange
        var ioDriver = new SimulatedIoLinkageDriver(NullLogger<SimulatedIoLinkageDriver>.Instance);
        var testBit = 5;
        var iterationCount = 100;
        var readResults = new ConcurrentBag<bool>();

        // Act - Concurrent reads and writes to same bit
        var tasks = new List<Task>();
        
        // Writer task - toggles bit rapidly
        tasks.Add(Task.Run(async () =>
        {
            for (int i = 0; i < iterationCount; i++)
            {
                await ioDriver.SetIoPointAsync(new Core.LineModel.Configuration.IoLinkagePoint
                {
                    BitNumber = testBit,
                    Level = (i % 2 == 0) 
                        ? Core.Enums.Sensors.TriggerLevel.ActiveHigh 
                        : Core.Enums.Sensors.TriggerLevel.ActiveLow
                });
                await Task.Yield(); // Yield to allow concurrent access
            }
        }));

        // Reader tasks - read the bit
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                for (int j = 0; j < iterationCount; j++)
                {
                    var value = await ioDriver.ReadIoPointAsync(testBit);
                    readResults.Add(value);
                    await Task.Yield();
                }
            }));
        }

        await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(5 * iterationCount, readResults.Count);
        // Should have mix of true and false, showing state changes were captured
        Assert.Contains(true, readResults);
        Assert.Contains(false, readResults);
        // No exceptions or data corruption occurred
    }
}
