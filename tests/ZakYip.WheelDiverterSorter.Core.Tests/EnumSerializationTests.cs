// 本文件使用向后兼容API进行测试，抑制废弃警告
#pragma warning disable CS0618 // Type or member is obsolete

using Xunit;
using LiteDB;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;
using ZakYip.WheelDiverterSorter.Core.Enums;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 测试枚举在LiteDB中的序列化格式
/// </summary>
public class EnumSerializationTests : IDisposable
{
    private readonly string _testDbPath;

    public EnumSerializationTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_enum_{Guid.NewGuid()}.db");
    }

    public void Dispose()
    {
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public void LiteDB_StoresDriverVendorType_AsString()
    {
        // Arrange
        var repository = new LiteDbDriverConfigurationRepository(_testDbPath);
        var config = DriverConfiguration.GetDefault();
        config.VendorType = DriverVendorType.Siemens;

        // Act - Save configuration
        repository.Update(config);

        // Assert - Read raw BSON to verify enum is stored as string
        using var db = new LiteDatabase($"Filename={_testDbPath};Connection=shared", LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection("DriverConfiguration");
        var doc = collection.FindAll().FirstOrDefault();
        
        Assert.NotNull(doc);
        var vendorTypeValue = doc["VendorType"];
        Assert.Equal(BsonType.String, vendorTypeValue.Type);
        Assert.Equal("Siemens", vendorTypeValue.AsString);
    }

    [Fact]
    public void LiteDB_StoresSensorVendorType_AsString()
    {
        // Arrange
        var repository = new LiteDbSensorConfigurationRepository(_testDbPath);
        var config = SensorConfiguration.GetDefault();
        config.VendorType = SensorVendorType.Omron;

        // Act - Save configuration
        repository.Update(config);

        // Assert - Read raw BSON to verify enum is stored as string
        using var db = new LiteDatabase($"Filename={_testDbPath};Connection=shared", LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db.GetCollection("SensorConfiguration");
        var doc = collection.FindAll().FirstOrDefault();
        
        Assert.NotNull(doc);
        var vendorTypeValue = doc["VendorType"];
        Assert.Equal(BsonType.String, vendorTypeValue.Type);
        Assert.Equal("Omron", vendorTypeValue.AsString);
    }

    [Fact]
    public void LiteDB_StoresCommunicationMode_AsString()
    {
        // Arrange
        var repository = new LiteDbCommunicationConfigurationRepository(_testDbPath);
        var config = CommunicationConfiguration.GetDefault();
        config.Mode = CommunicationMode.Mqtt;

        // Act - Save configuration
        repository.Update(config);

        // Assert - Read raw BSON to verify enum is stored as string
        using var db2 = new LiteDatabase($"Filename={_testDbPath};Connection=shared", LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db2.GetCollection("CommunicationConfiguration");
        var doc = collection.FindAll().FirstOrDefault();
        
        Assert.NotNull(doc);
        var modeValue = doc["Mode"];
        Assert.Equal(BsonType.String, modeValue.Type);
        Assert.Equal("Mqtt", modeValue.AsString);
    }

    [Fact]
    public void LiteDB_StoresDiverterDirection_AsString()
    {
        // Arrange
        var repository = new LiteDbRouteConfigurationRepository(_testDbPath);
        var config = new ChuteRouteConfiguration
        {
            ChuteId = 100,
            ChuteName = "Test Chute",
            DiverterConfigurations = new List<DiverterConfigurationEntry>
            {
                new DiverterConfigurationEntry
                {
                    DiverterId = 1,
                    DiverterName = "D1",
                    TargetDirection = DiverterDirection.Left,
                    SequenceNumber = 1
                }
            },
            BeltSpeedMmPerSecond = 1000.0,
            BeltLengthMm = 5000.0,
            ToleranceTimeMs = 2000,
            IsEnabled = true
        };

        // Act - Save configuration
        repository.Upsert(config);

        // Assert - Read raw BSON to verify enum is stored as string
        using var db3 = new LiteDatabase($"Filename={_testDbPath};Connection=shared", LiteDbMapperConfig.CreateConfiguredMapper());
        var collection = db3.GetCollection("ChuteRoutes");
        var doc = collection.FindAll().FirstOrDefault();
        
        Assert.NotNull(doc);
        var diverterConfigs = doc["DiverterConfigurations"].AsArray;
        Assert.NotEmpty(diverterConfigs);
        
        var firstDiverter = diverterConfigs[0].AsDocument;
        var targetDirection = firstDiverter["TargetDirection"];
        Assert.Equal(BsonType.String, targetDirection.Type);
        Assert.Equal("Left", targetDirection.AsString);
    }

    [Fact]
    public void DriverConfiguration_CanReadAndWrite_WithEnumTypes()
    {
        // Arrange
        var repository = new LiteDbDriverConfigurationRepository(_testDbPath);
        var config = DriverConfiguration.GetDefault();
        config.VendorType = DriverVendorType.Mitsubishi;

        // Act
        repository.Update(config);
        var retrieved = repository.Get();

        // Assert
        Assert.Equal(DriverVendorType.Mitsubishi, retrieved.VendorType);
    }

    [Fact]
    public void SensorConfiguration_CanReadAndWrite_WithEnumTypes()
    {
        // Arrange
        var repository = new LiteDbSensorConfigurationRepository(_testDbPath);
        var config = SensorConfiguration.GetDefault();
        config.VendorType = SensorVendorType.Siemens;

        // Act
        repository.Update(config);
        var retrieved = repository.Get();

        // Assert
        Assert.Equal(SensorVendorType.Siemens, retrieved.VendorType);
    }

    [Fact]
    public void CommunicationConfiguration_CanReadAndWrite_WithEnumTypes()
    {
        // Arrange
        var repository = new LiteDbCommunicationConfigurationRepository(_testDbPath);
        var config = CommunicationConfiguration.GetDefault();
        config.Mode = CommunicationMode.SignalR;

        // Act
        repository.Update(config);
        var retrieved = repository.Get();

        // Assert
        Assert.Equal(CommunicationMode.SignalR, retrieved.Mode);
    }
}
