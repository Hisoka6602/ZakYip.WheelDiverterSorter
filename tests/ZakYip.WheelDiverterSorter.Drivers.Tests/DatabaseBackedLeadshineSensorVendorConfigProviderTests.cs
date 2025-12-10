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
        Assert.Contains(result, c => c.SensorId == "1");
        Assert.Contains(result, c => c.SensorId == "3");
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
        Assert.Contains(result, c => c.SensorId == "1" && c.InputBit == 8);
        Assert.Contains(result, c => c.SensorId == "2" && c.InputBit == 9);
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
        Assert.Contains(result, c => c.SensorId == "1" && c.PollingIntervalMs == 20);
        Assert.Contains(result, c => c.SensorId == "2" && c.PollingIntervalMs == null);
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
}
