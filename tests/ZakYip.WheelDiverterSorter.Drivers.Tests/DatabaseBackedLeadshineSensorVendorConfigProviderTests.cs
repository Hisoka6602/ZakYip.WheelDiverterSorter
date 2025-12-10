using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Moq;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Drivers.Vendors.Leadshine.Configuration;

namespace ZakYip.WheelDiverterSorter.Drivers.Tests;

/// <summary>
/// Tests for DatabaseBackedLeadshineSensorVendorConfigProvider
/// </summary>
public class DatabaseBackedLeadshineSensorVendorConfigProviderTests
{
    [Fact]
    public void GetSensorConfigs_ShouldReturnEmptyList_WhenNoSensorsConfigured()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>()
        });

        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act
        var result = provider.GetSensorConfigs();

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public void GetSensorConfigs_ShouldReturnOnlyEnabledSensors()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() { SensorId = 1, IoType = SensorIoType.ParcelCreation, BitNumber = 8, IsEnabled = true },
                new() { SensorId = 2, IoType = SensorIoType.WheelFront, BitNumber = 9, IsEnabled = false },
                new() { SensorId = 3, IoType = SensorIoType.ChuteLock, BitNumber = 0, IsEnabled = true }
            }
        });

        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act
        var result = provider.GetSensorConfigs();

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains(result, c => c.SensorId == 1);
        Assert.Contains(result, c => c.SensorId == 3);
    }

    [Fact]
    public void GetSensorConfigs_ShouldMapSensorIoTypeToPhotoelectricType()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() { SensorId = 1, IoType = SensorIoType.ParcelCreation, BitNumber = 8, IsEnabled = true },
                new() { SensorId = 2, IoType = SensorIoType.WheelFront, BitNumber = 9, IsEnabled = true },
                new() { SensorId = 3, IoType = SensorIoType.ChuteLock, BitNumber = 0, IsEnabled = true }
            }
        });

        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act
        var result = provider.GetSensorConfigs();

        // Assert
        Assert.All(result, config =>
        {
            Assert.Equal("Photoelectric", config.SensorTypeName);
        });
    }

    [Fact]
    public void GetSensorConfigs_ShouldMapBitNumberToInputBit()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() { SensorId = 1, IoType = SensorIoType.ParcelCreation, BitNumber = 8, IsEnabled = true },
                new() { SensorId = 2, IoType = SensorIoType.WheelFront, BitNumber = 9, IsEnabled = true }
            }
        });

        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act
        var result = provider.GetSensorConfigs();

        // Assert
        Assert.Contains(result, c => c.SensorId == 1 && c.InputBit == 8);
        Assert.Contains(result, c => c.SensorId == 2 && c.InputBit == 9);
    }

    [Fact]
    public void GetSensorConfigs_ShouldPreservePollingIntervalMs()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>
            {
                new() { SensorId = 1, IoType = SensorIoType.ParcelCreation, BitNumber = 8, IsEnabled = true, PollingIntervalMs = 20 },
                new() { SensorId = 2, IoType = SensorIoType.WheelFront, BitNumber = 9, IsEnabled = true, PollingIntervalMs = null }
            }
        });

        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act
        var result = provider.GetSensorConfigs();

        // Assert
        Assert.Contains(result, c => c.SensorId == 1 && c.PollingIntervalMs == 20);
        Assert.Contains(result, c => c.SensorId == 2 && c.PollingIntervalMs == null);
    }

    [Fact]
    public void VendorTypeName_ShouldReturnLeadshine()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            0);

        // Act & Assert
        Assert.Equal("Leadshine", provider.VendorTypeName);
    }

    [Fact]
    public void CardNo_ShouldReturnConfiguredValue()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            NullLogger<DatabaseBackedLeadshineSensorVendorConfigProvider>.Instance,
            5);

        // Act & Assert
        Assert.Equal((ushort)5, provider.CardNo);
    }

    [Fact]
    public void GetSensorConfigs_ShouldReturnEmptyListAndLogError_WhenRepositoryThrowsException()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Throws(new InvalidOperationException("Database error"));
        
        var logger = new Mock<ILogger<DatabaseBackedLeadshineSensorVendorConfigProvider>>();
        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            logger.Object,
            0);
        
        // Act
        var result = provider.GetSensorConfigs();
        
        // Assert
        Assert.Empty(result);
        logger.Verify(
            x => x.Log(
                LogLevel.Error,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("从数据库加载传感器配置失败")),
                It.IsAny<Exception>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }

    [Fact]
    public void GetSensorConfigs_ShouldLogWarning_WhenNoSensorsConfigured()
    {
        // Arrange
        var repository = new Mock<ISensorConfigurationRepository>();
        repository.Setup(r => r.Get()).Returns(new SensorConfiguration
        {
            Sensors = new List<SensorIoEntry>()
        });
        
        var logger = new Mock<ILogger<DatabaseBackedLeadshineSensorVendorConfigProvider>>();
        var provider = new DatabaseBackedLeadshineSensorVendorConfigProvider(
            repository.Object,
            logger.Object,
            0);
        
        // Act
        var result = provider.GetSensorConfigs();
        
        // Assert
        Assert.Empty(result);
        logger.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("传感器配置为空或未找到")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
