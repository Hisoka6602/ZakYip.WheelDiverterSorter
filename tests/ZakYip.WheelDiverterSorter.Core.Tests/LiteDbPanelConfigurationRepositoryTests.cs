using Xunit;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Models;
using ZakYip.WheelDiverterSorter.Core.LineModel.Configuration.Repositories.Interfaces;
using ZakYip.WheelDiverterSorter.Configuration.Persistence.Repositories.LiteDb;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Core.Tests;

/// <summary>
/// 测试面板配置仓储的持久化和检索功能
/// </summary>
public class LiteDbPanelConfigurationRepositoryTests : IDisposable
{
    private readonly string _testDatabasePath;
    private readonly LiteDbPanelConfigurationRepository _repository;

    public LiteDbPanelConfigurationRepositoryTests()
    {
        _testDatabasePath = Path.Combine(Path.GetTempPath(), $"test_panel_config_{Guid.NewGuid()}.db");
        _repository = new LiteDbPanelConfigurationRepository(_testDatabasePath);
    }

    public void Dispose()
    {
        _repository.Dispose();
        
        // Clean up test database
        if (File.Exists(_testDatabasePath))
        {
            File.Delete(_testDatabasePath);
        }
    }

    [Fact]
    public void InitializeDefault_CreatesDefaultConfiguration()
    {
        // Act
        _repository.InitializeDefault();
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("panel", config.ConfigName);
        Assert.False(config.Enabled);
        Assert.True(config.UseSimulation);
        Assert.Equal(100, config.PollingIntervalMs);
        Assert.Equal(50, config.DebounceMs);
        Assert.Equal(TriggerLevel.ActiveHigh, config.StartButtonTriggerLevel);
    }

    [Fact]
    public void InitializeDefault_CalledTwice_DoesNotDuplicate()
    {
        // Act
        _repository.InitializeDefault();
        _repository.InitializeDefault();
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal(1, config.Version);
    }

    [Fact]
    public void Get_WithoutInitialization_ReturnsDefaultConfig()
    {
        // Act
        var config = _repository.Get();

        // Assert
        Assert.NotNull(config);
        Assert.Equal("panel", config.ConfigName);
        Assert.False(config.Enabled);
        Assert.True(config.UseSimulation);
    }

    [Fact]
    public void Update_WithValidConfiguration_UpdatesSuccessfully()
    {
        // Arrange
        _repository.InitializeDefault();
        var newConfig = PanelConfiguration.GetDefault() with
        {
            Enabled = true,
            UseSimulation = false,
            PollingIntervalMs = 200,
            DebounceMs = 100,
            StartButtonInputBit = 5,
            StartButtonTriggerLevel = TriggerLevel.ActiveLow
        };

        // Act
        _repository.Update(newConfig);
        var updated = _repository.Get();

        // Assert
        Assert.True(updated.Enabled);
        Assert.False(updated.UseSimulation);
        Assert.Equal(200, updated.PollingIntervalMs);
        Assert.Equal(100, updated.DebounceMs);
        Assert.Equal(5, updated.StartButtonInputBit);
        Assert.Equal(TriggerLevel.ActiveLow, updated.StartButtonTriggerLevel);
    }

    [Fact]
    public void Update_WithInvalidPollingInterval_ThrowsArgumentException()
    {
        // Arrange - polling interval too small
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            PollingIntervalMs = 5 // Invalid: must be >= 10
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
        Assert.Contains("轮询间隔必须在 10-5000 毫秒之间", ex.Message);
    }

    [Fact]
    public void Update_WithInvalidDebounceTime_ThrowsArgumentException()
    {
        // Arrange - debounce time too large
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            DebounceMs = 6000 // Invalid: must be <= 5000
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
        Assert.Contains("防抖时间必须在 10-5000 毫秒之间", ex.Message);
    }

    [Fact]
    public void Update_WithDebounceGreaterThanPolling_ThrowsArgumentException()
    {
        // Arrange - debounce > polling (now allowed to be equal)
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            PollingIntervalMs = 100,
            DebounceMs = 150 // Invalid: debounce must be <= polling
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
        Assert.Contains("防抖时间不能大于轮询间隔", ex.Message);
    }

    [Fact]
    public void Update_WithDebounceEqualToPolling_UpdatesSuccessfully()
    {
        // Arrange - debounce can now equal polling (new requirement)
        var validConfig = PanelConfiguration.GetDefault() with
        {
            PollingIntervalMs = 100,
            DebounceMs = 100 // Valid: debounce can be equal to polling
        };

        // Act
        _repository.Update(validConfig);
        var updated = _repository.Get();

        // Assert
        Assert.Equal(100, updated.PollingIntervalMs);
        Assert.Equal(100, updated.DebounceMs);
    }

    [Fact]
    public void Update_WithInvalidIoBit_ThrowsArgumentException()
    {
        // Arrange - IO bit out of range
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            StartButtonInputBit = 1024 // Invalid: must be 0-1023
        };

        // Act & Assert
        var ex = Assert.Throws<ArgumentException>(() => _repository.Update(invalidConfig));
        Assert.Contains("IO位", ex.Message);
        Assert.Contains("1024", ex.Message);
    }

    [Fact]
    public void Update_WithAllIoBindings_UpdatesSuccessfully()
    {
        // Arrange - comprehensive IO configuration
        var fullConfig = PanelConfiguration.GetDefault() with
        {
            Enabled = true,
            UseSimulation = false,
            PollingIntervalMs = 150,
            DebounceMs = 75,
            
            // Button inputs
            StartButtonInputBit = 0,
            StartButtonTriggerLevel = TriggerLevel.ActiveHigh,
            StopButtonInputBit = 1,
            StopButtonTriggerLevel = TriggerLevel.ActiveLow,
            EmergencyStopButtonInputBit = 2,
            EmergencyStopButtonTriggerLevel = TriggerLevel.ActiveLow,
            
            // Light outputs
            StartLightOutputBit = 10,
            StartLightOutputLevel = TriggerLevel.ActiveHigh,
            StopLightOutputBit = 11,
            StopLightOutputLevel = TriggerLevel.ActiveHigh,
            ConnectionLightOutputBit = 12,
            ConnectionLightOutputLevel = TriggerLevel.ActiveHigh,
            
            // Signal tower outputs
            SignalTowerRedOutputBit = 20,
            SignalTowerRedOutputLevel = TriggerLevel.ActiveHigh,
            SignalTowerYellowOutputBit = 21,
            SignalTowerYellowOutputLevel = TriggerLevel.ActiveHigh,
            SignalTowerGreenOutputBit = 22,
            SignalTowerGreenOutputLevel = TriggerLevel.ActiveHigh
        };

        // Act
        _repository.Update(fullConfig);
        var updated = _repository.Get();

        // Assert - verify all fields
        Assert.True(updated.Enabled);
        Assert.False(updated.UseSimulation);
        Assert.Equal(150, updated.PollingIntervalMs);
        Assert.Equal(75, updated.DebounceMs);
        
        // Button inputs
        Assert.Equal(0, updated.StartButtonInputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.StartButtonTriggerLevel);
        Assert.Equal(1, updated.StopButtonInputBit);
        Assert.Equal(TriggerLevel.ActiveLow, updated.StopButtonTriggerLevel);
        Assert.Equal(2, updated.EmergencyStopButtonInputBit);
        Assert.Equal(TriggerLevel.ActiveLow, updated.EmergencyStopButtonTriggerLevel);
        
        // Light outputs
        Assert.Equal(10, updated.StartLightOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.StartLightOutputLevel);
        Assert.Equal(11, updated.StopLightOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.StopLightOutputLevel);
        Assert.Equal(12, updated.ConnectionLightOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.ConnectionLightOutputLevel);
        
        // Signal tower outputs
        Assert.Equal(20, updated.SignalTowerRedOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.SignalTowerRedOutputLevel);
        Assert.Equal(21, updated.SignalTowerYellowOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.SignalTowerYellowOutputLevel);
        Assert.Equal(22, updated.SignalTowerGreenOutputBit);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.SignalTowerGreenOutputLevel);
    }

    [Fact]
    public void Update_WithMixedTriggerLevels_UpdatesCorrectly()
    {
        // Arrange - mix of ActiveHigh and ActiveLow
        var mixedConfig = PanelConfiguration.GetDefault() with
        {
            StartButtonInputBit = 0,
            StartButtonTriggerLevel = TriggerLevel.ActiveHigh,
            StopButtonInputBit = 1,
            StopButtonTriggerLevel = TriggerLevel.ActiveLow,
            EmergencyStopButtonInputBit = 2,
            EmergencyStopButtonTriggerLevel = TriggerLevel.ActiveLow,
            
            StartLightOutputBit = 10,
            StartLightOutputLevel = TriggerLevel.ActiveLow,
            StopLightOutputBit = 11,
            StopLightOutputLevel = TriggerLevel.ActiveHigh
        };

        // Act
        _repository.Update(mixedConfig);
        var updated = _repository.Get();

        // Assert - verify trigger levels are preserved
        Assert.Equal(TriggerLevel.ActiveHigh, updated.StartButtonTriggerLevel);
        Assert.Equal(TriggerLevel.ActiveLow, updated.StopButtonTriggerLevel);
        Assert.Equal(TriggerLevel.ActiveLow, updated.EmergencyStopButtonTriggerLevel);
        Assert.Equal(TriggerLevel.ActiveLow, updated.StartLightOutputLevel);
        Assert.Equal(TriggerLevel.ActiveHigh, updated.StopLightOutputLevel);
    }

    [Fact]
    public void Update_WithNullIoBits_UpdatesSuccessfully()
    {
        // Arrange - some IO bindings are null (not configured)
        var partialConfig = PanelConfiguration.GetDefault() with
        {
            StartButtonInputBit = 0,
            StopButtonInputBit = null, // Not configured
            EmergencyStopButtonInputBit = 2
        };

        // Act
        _repository.Update(partialConfig);
        var updated = _repository.Get();

        // Assert
        Assert.Equal(0, updated.StartButtonInputBit);
        Assert.Null(updated.StopButtonInputBit);
        Assert.Equal(2, updated.EmergencyStopButtonInputBit);
    }

    [Fact]
    public void PanelConfiguration_Validate_WithValidConfig_ReturnsTrue()
    {
        // Arrange
        var validConfig = PanelConfiguration.GetDefault() with
        {
            PollingIntervalMs = 100,
            DebounceMs = 50
        };

        // Act
        var (isValid, errorMessage) = validConfig.Validate();

        // Assert
        Assert.True(isValid);
        Assert.Null(errorMessage);
    }

    [Fact]
    public void PanelConfiguration_Validate_WithInvalidPolling_ReturnsFalse()
    {
        // Arrange
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            PollingIntervalMs = 6000 // Too large (must be <= 5000)
        };

        // Act
        var (isValid, errorMessage) = invalidConfig.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("轮询间隔", errorMessage);
    }

    [Fact]
    public void PanelConfiguration_Validate_WithInvalidDebounce_ReturnsFalse()
    {
        // Arrange
        var invalidConfig = PanelConfiguration.GetDefault() with
        {
            DebounceMs = 5 // Too small
        };

        // Act
        var (isValid, errorMessage) = invalidConfig.Validate();

        // Assert
        Assert.False(isValid);
        Assert.Contains("防抖时间", errorMessage);
    }
}
