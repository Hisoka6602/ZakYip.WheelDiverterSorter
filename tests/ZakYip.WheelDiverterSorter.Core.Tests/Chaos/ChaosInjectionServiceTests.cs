using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Chaos;
using ZakYip.WheelDiverterSorter.Core.Enums.Communication;
using ZakYip.WheelDiverterSorter.Core.Enums.Conveyor;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.Enums.IoBinding;
using ZakYip.WheelDiverterSorter.Core.Enums.Routing;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.Enums.Sorting;
using ZakYip.WheelDiverterSorter.Core.Enums.System;

namespace ZakYip.WheelDiverterSorter.Core.Tests.Chaos;

/// <summary>
/// PR-41: 混沌注入服务测试
/// Tests for ChaosInjectionService
/// </summary>
public class ChaosInjectionServiceTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() => 
            new ChaosInjectionService(null!, ChaosProfiles.Disabled));
    }

    [Fact]
    public void Constructor_WithDisabledOptions_IsNotEnabled()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = ChaosProfiles.Disabled;

        // Act
        var service = new ChaosInjectionService(logger, options);

        // Assert
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void Constructor_WithMildOptions_IsEnabled()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = ChaosProfiles.Mild;

        // Act
        var service = new ChaosInjectionService(logger, options);

        // Assert
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void Enable_WhenCalled_SetsIsEnabledToTrue()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);
        Assert.False(service.IsEnabled);

        // Act
        service.Enable();

        // Assert
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public void Disable_WhenCalled_SetsIsEnabledToFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Mild);
        Assert.True(service.IsEnabled);

        // Act
        service.Disable();

        // Assert
        Assert.False(service.IsEnabled);
    }

    [Fact]
    public void Configure_WithNullOptions_ThrowsArgumentNullException()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => service.Configure(null!));
    }

    [Fact]
    public void Configure_WithEnabledOptions_UpdatesIsEnabled()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);
        Assert.False(service.IsEnabled);

        // Act
        service.Configure(ChaosProfiles.Moderate);

        // Assert
        Assert.True(service.IsEnabled);
    }

    [Fact]
    public async Task ShouldInjectCommunicationChaosAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var result = await service.ShouldInjectCommunicationChaosAsync("TestOp");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldInjectDriverChaosAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var result = await service.ShouldInjectDriverChaosAsync("TestDriver");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task ShouldInjectIoChaosAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var result = await service.ShouldInjectIoChaosAsync("Sensor-01");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetCommunicationDelayAsync_WhenDisabled_ReturnsZero()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var delay = await service.GetCommunicationDelayAsync();

        // Assert
        Assert.Equal(0, delay);
    }

    [Fact]
    public async Task GetDriverDelayAsync_WhenDisabled_ReturnsZero()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var delay = await service.GetDriverDelayAsync();

        // Assert
        Assert.Equal(0, delay);
    }

    [Fact]
    public async Task ShouldInjectCommunicationChaosAsync_WithZeroProbability_AlwaysReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = new ChaosInjectionOptions
        {
            Enabled = true,
            Communication = new ChaosLayerOptions
            {
                ExceptionProbability = 0.0
            },
            Seed = 42
        };
        var service = new ChaosInjectionService(logger, options);

        // Act - Try many times
        var results = new List<bool>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(await service.ShouldInjectCommunicationChaosAsync("TestOp"));
        }

        // Assert - All should be false
        Assert.All(results, r => Assert.False(r));
    }

    [Fact]
    public async Task ShouldInjectCommunicationChaosAsync_WithOneProbability_AlwaysReturnsTrue()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = new ChaosInjectionOptions
        {
            Enabled = true,
            Communication = new ChaosLayerOptions
            {
                ExceptionProbability = 1.0
            },
            Seed = 42
        };
        var service = new ChaosInjectionService(logger, options);

        // Act - Try many times
        var results = new List<bool>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(await service.ShouldInjectCommunicationChaosAsync("TestOp"));
        }

        // Assert - All should be true
        Assert.All(results, r => Assert.True(r));
    }

    [Fact]
    public async Task GetCommunicationDelayAsync_WhenEnabled_ReturnsDelayInRange()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = new ChaosInjectionOptions
        {
            Enabled = true,
            Communication = new ChaosLayerOptions
            {
                DelayProbability = 1.0, // Always inject
                MinDelayMs = 100,
                MaxDelayMs = 500
            },
            Seed = 42
        };
        var service = new ChaosInjectionService(logger, options);

        // Act
        var delays = new List<int>();
        for (int i = 0; i < 50; i++)
        {
            delays.Add(await service.GetCommunicationDelayAsync());
        }

        // Assert - All delays should be in range
        Assert.All(delays, d => 
        {
            Assert.InRange(d, 100, 500);
        });
    }

    [Fact]
    public async Task GetDriverDelayAsync_WhenEnabled_ReturnsDelayInRange()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = new ChaosInjectionOptions
        {
            Enabled = true,
            Driver = new ChaosLayerOptions
            {
                DelayProbability = 1.0, // Always inject
                MinDelayMs = 50,
                MaxDelayMs = 300
            },
            Seed = 42
        };
        var service = new ChaosInjectionService(logger, options);

        // Act
        var delays = new List<int>();
        for (int i = 0; i < 50; i++)
        {
            delays.Add(await service.GetDriverDelayAsync());
        }

        // Assert - All delays should be in range
        Assert.All(delays, d => 
        {
            Assert.InRange(d, 50, 300);
        });
    }

    [Fact]
    public void ChaosProfiles_Mild_HasCorrectConfiguration()
    {
        // Arrange & Act
        var profile = ChaosProfiles.Mild;

        // Assert
        Assert.True(profile.Enabled);
        Assert.Equal(0.01, profile.Communication.ExceptionProbability);
        Assert.Equal(0.05, profile.Communication.DelayProbability);
        Assert.Equal(0.005, profile.Communication.DisconnectProbability);
        Assert.Equal(0.01, profile.Driver.ExceptionProbability);
        Assert.Equal(0.01, profile.Io.DropoutProbability);
    }

    [Fact]
    public void ChaosProfiles_Moderate_HasCorrectConfiguration()
    {
        // Arrange & Act
        var profile = ChaosProfiles.Moderate;

        // Assert
        Assert.True(profile.Enabled);
        Assert.Equal(0.05, profile.Communication.ExceptionProbability);
        Assert.Equal(0.1, profile.Communication.DelayProbability);
        Assert.Equal(0.02, profile.Communication.DisconnectProbability);
        Assert.Equal(0.05, profile.Driver.ExceptionProbability);
        Assert.Equal(0.03, profile.Io.DropoutProbability);
    }

    [Fact]
    public void ChaosProfiles_Heavy_HasCorrectConfiguration()
    {
        // Arrange & Act
        var profile = ChaosProfiles.Heavy;

        // Assert
        Assert.True(profile.Enabled);
        Assert.Equal(0.1, profile.Communication.ExceptionProbability);
        Assert.Equal(0.2, profile.Communication.DelayProbability);
        Assert.Equal(0.05, profile.Communication.DisconnectProbability);
        Assert.Equal(0.1, profile.Driver.ExceptionProbability);
        Assert.Equal(0.08, profile.Io.DropoutProbability);
    }

    [Fact]
    public void ChaosProfiles_Disabled_IsNotEnabled()
    {
        // Arrange & Act
        var profile = ChaosProfiles.Disabled;

        // Assert
        Assert.False(profile.Enabled);
    }

    [Fact]
    public void ChaosInjectedException_HasCorrectProperties()
    {
        // Arrange & Act
        var exception = new ChaosInjectedException("communication", "delay", "Test message");

        // Assert
        Assert.Equal("communication", exception.Layer);
        Assert.Equal("delay", exception.Type);
        Assert.Contains("[CHAOS-COMMUNICATION]", exception.Message);
        Assert.Contains("Test message", exception.Message);
    }

    [Fact]
    public void ChaosInjectedException_WithInnerException_PreservesInner()
    {
        // Arrange
        var inner = new InvalidOperationException("Inner");

        // Act
        var exception = new ChaosInjectedException("driver", "exception", "Test", inner);

        // Assert
        Assert.Equal("driver", exception.Layer);
        Assert.Equal("exception", exception.Type);
        Assert.Same(inner, exception.InnerException);
    }

    [Fact]
    public void ShouldInjectDisconnect_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Disabled);

        // Act
        var result = service.ShouldInjectDisconnect("communication");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ShouldInjectDisconnect_WithZeroProbability_AlwaysReturnsFalse()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var options = new ChaosInjectionOptions
        {
            Enabled = true,
            Communication = new ChaosLayerOptions
            {
                DisconnectProbability = 0.0
            },
            Seed = 42
        };
        var service = new ChaosInjectionService(logger, options);

        // Act - Try many times
        var results = new List<bool>();
        for (int i = 0; i < 100; i++)
        {
            results.Add(service.ShouldInjectDisconnect("communication"));
        }

        // Assert - All should be false
        Assert.All(results, r => Assert.False(r));
    }

    [Fact]
    public void ThreadSafety_ConcurrentAccess_DoesNotThrow()
    {
        // Arrange
        var logger = NullLogger<ChaosInjectionService>.Instance;
        var service = new ChaosInjectionService(logger, ChaosProfiles.Moderate);

        // Act & Assert - Should not throw
        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            await service.ShouldInjectCommunicationChaosAsync("Op" + i);
            await service.GetCommunicationDelayAsync();
            
            if (i % 10 == 0)
            {
                service.Enable();
            }
            else if (i % 10 == 5)
            {
                service.Disable();
            }
        });

        // Should complete without throwing
        Task.WaitAll(tasks.ToArray());
    }
}
