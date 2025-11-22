using System;
using System.IO;
using Xunit;
using ZakYip.WheelDiverterSorter.Core.Enums.Sensors;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// LiteDbIoLinkageConfigurationRepository 单元测试
/// </summary>
public class LiteDbIoLinkageConfigurationRepositoryTests : IDisposable
{
    private readonly string _testDbPath;
    private readonly LiteDbIoLinkageConfigurationRepository _repository;

    public LiteDbIoLinkageConfigurationRepositoryTests()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"test_io_linkage_{Guid.NewGuid()}.db");
        _repository = new LiteDbIoLinkageConfigurationRepository(_testDbPath);
    }

    public void Dispose()
    {
        _repository?.Dispose();
        if (File.Exists(_testDbPath))
        {
            File.Delete(_testDbPath);
        }
    }

    [Fact]
    public void Get_ShouldReturnDefaultConfiguration_WhenNoConfigurationExists()
    {
        // Act
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("io_linkage", config.ConfigName);
        Assert.Equal(1, config.Version);
        Assert.True(config.Enabled);
        Assert.Empty(config.RunningStateIos);
        Assert.Empty(config.StoppedStateIos);
    }

    [Fact]
    public void Update_ShouldPersistConfiguration()
    {
        // Arrange
        var config = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveLow },
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveHigh }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        _repository.Update(config);
        var retrieved = _repository.Get();

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal("io_linkage", retrieved.ConfigName);
        Assert.True(retrieved.Enabled);
        Assert.Equal(2, retrieved.RunningStateIos.Count);
        Assert.Equal(2, retrieved.StoppedStateIos.Count);
        Assert.Equal(3, retrieved.RunningStateIos[0].BitNumber);
        Assert.Equal(TriggerLevel.ActiveLow, retrieved.RunningStateIos[0].Level);
    }

    [Fact]
    public void Update_ShouldPreserveId_WhenUpdatingExistingConfiguration()
    {
        // Arrange
        var initialConfig = IoLinkageConfiguration.GetDefault();
        _repository.Update(initialConfig);
        var firstRetrieved = _repository.Get();
        var originalId = firstRetrieved.Id;

        var updatedConfig = firstRetrieved with
        {
            Enabled = false,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 10, Level = TriggerLevel.ActiveHigh }
            }
        };

        // Act
        _repository.Update(updatedConfig);
        var secondRetrieved = _repository.Get();

        // Assert
        Assert.Equal(originalId, secondRetrieved.Id);
        Assert.False(secondRetrieved.Enabled);
        Assert.Single(secondRetrieved.RunningStateIos);
    }

    [Fact]
    public void Update_ShouldThrowArgumentException_WhenConfigurationIsInvalid()
    {
        // Arrange
        var invalidConfig = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 2000, Level = TriggerLevel.ActiveHigh } // Invalid bit number
            },
            StoppedStateIos = new(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
    }

    [Fact]
    public void Update_ShouldThrowArgumentNullException_WhenConfigurationIsNull()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => _repository.Update(null!));
    }

    [Fact]
    public void InitializeDefault_ShouldCreateDefaultConfiguration_WhenNoConfigurationExists()
    {
        // Arrange
        var testTime = new DateTime(2025, 11, 22, 10, 0, 0);

        // Act
        _repository.InitializeDefault(testTime);
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("io_linkage", config.ConfigName);
        Assert.Equal(testTime, config.CreatedAt);
        Assert.Equal(testTime, config.UpdatedAt);
    }

    [Fact]
    public void InitializeDefault_ShouldNotOverwriteExistingConfiguration()
    {
        // Arrange
        var existingConfig = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = false,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 100, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _repository.Update(existingConfig);

        // Act
        _repository.InitializeDefault();
        var config = _repository.Get();

        // Assert
        Assert.False(config.Enabled); // Should keep existing config
        Assert.Single(config.RunningStateIos);
    }

    [Fact]
    public void Configuration_ShouldPersistAcrossRepositoryInstances()
    {
        // Arrange
        var config = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 7, Level = TriggerLevel.ActiveHigh }
            },
            StoppedStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 7, Level = TriggerLevel.ActiveLow }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _repository.Update(config);
        _repository.Dispose();

        // Act - Create new repository instance
        using var newRepository = new LiteDbIoLinkageConfigurationRepository(_testDbPath);
        var retrieved = newRepository.Get();

        // Assert
        Assert.NotNull(retrieved);
        Assert.True(retrieved.Enabled);
        Assert.Single(retrieved.RunningStateIos);
        Assert.Equal(7, retrieved.RunningStateIos[0].BitNumber);
        Assert.Equal(TriggerLevel.ActiveHigh, retrieved.RunningStateIos[0].Level);
    }

    [Fact]
    public void Validate_ShouldFailForDuplicateBitNumbers()
    {
        // Arrange
        var config = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveHigh },
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveLow } // Duplicate
            },
            StoppedStateIos = new(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("重复", errorMessage);
    }

    [Fact]
    public void Validate_ShouldPassForValidConfiguration()
    {
        // Arrange
        var config = new IoLinkageConfiguration
        {
            ConfigName = "io_linkage",
            Version = 1,
            Enabled = true,
            RunningStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveLow },
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveLow }
            },
            StoppedStateIos = new()
            {
                new IoLinkagePoint { BitNumber = 3, Level = TriggerLevel.ActiveHigh },
                new IoLinkagePoint { BitNumber = 5, Level = TriggerLevel.ActiveHigh }
            },
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Act
        var (isValid, errorMessage) = config.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }
}
