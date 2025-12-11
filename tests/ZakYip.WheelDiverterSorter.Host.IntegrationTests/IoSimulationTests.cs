using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using Xunit;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios.Configuration;
using ZakYip.WheelDiverterSorter.Simulation.Scenarios;
using ZakYip.WheelDiverterSorter.Core.Enums;
using ZakYip.WheelDiverterSorter.Core.Enums.Hardware;

namespace ZakYip.WheelDiverterSorter.Host.IntegrationTests;

/// <summary>
/// PR-40: IO 高复杂度仿真测试
/// </summary>
/// <remarks>
/// 测试 IO 层的各种复杂场景，包括传感器抖动、混沌模式、压力测试和配置错误
/// </remarks>
public class IoSimulationTests
{
    [Fact]
    public void SensorJitterScenario_WithDebouncingEnabled_IsWellDefined()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoSensorJitter(parcelCount: 30, enableDebouncing: true);

        // Assert
        Assert.NotNull(scenario);
        Assert.Contains("传感器抖动", scenario.ScenarioName);
        Assert.Contains("启用", scenario.ScenarioName);
        Assert.NotNull(scenario.Options);
        Assert.Equal(30, scenario.Options.ParcelCount);

        // 验证传感器抖动配置
        Assert.NotNull(scenario.Options.SensorFault);
        Assert.True(scenario.Options.SensorFault.IsEnableSensorJitter);
        Assert.Equal(5, scenario.Options.SensorFault.JitterTriggerCount);
        Assert.Equal(80, scenario.Options.SensorFault.JitterIntervalMs);
        Assert.Equal(0.5m, scenario.Options.SensorFault.JitterProbability);
        Assert.True(scenario.Options.SensorFault.IsEnableDebouncing);
        Assert.Equal(100, scenario.Options.SensorFault.DebounceWindowMs);
    }

    [Fact]
    public void SensorJitterScenario_WithDebouncingDisabled_IsWellDefined()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoSensorJitter(parcelCount: 30, enableDebouncing: false);

        // Assert
        Assert.NotNull(scenario);
        Assert.Contains("禁用", scenario.ScenarioName);
        Assert.NotNull(scenario.Options.SensorFault);
        Assert.False(scenario.Options.SensorFault.IsEnableDebouncing);
    }

    [Fact]
    public void ChaosModeScenario_IsWellDefined()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoChaosMode(parcelCount: 50);

        // Assert
        Assert.NotNull(scenario);
        Assert.Equal("IO-ChaosMode-混沌模式仿真", scenario.ScenarioName);
        Assert.NotNull(scenario.Options);
        Assert.Equal(50, scenario.Options.ParcelCount);

        // 验证混沌模式配置
        Assert.NotNull(scenario.Options.SensorFault);
        Assert.Equal(IoBehaviorMode.Chaos, scenario.Options.SensorFault.BehaviorMode);
        Assert.True(scenario.Options.SensorFault.IsEnableSensorJitter);
        Assert.NotNull(scenario.Options.SensorFault.SensorDelayRangeMs);
        Assert.True(scenario.Options.SensorFault.SensorLossProbability > 0);
    }

    [Fact]
    public void StressTestScenario_IsWellDefined()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoStressTest(parcelCount: 200);

        // Assert
        Assert.NotNull(scenario);
        Assert.Equal("IO-StressTest-IO压力测试", scenario.ScenarioName);
        Assert.NotNull(scenario.Options);
        Assert.Equal(200, scenario.Options.ParcelCount);

        // 验证压力测试配置
        Assert.Equal(150, scenario.Options.ParcelInterval.TotalMilliseconds); // 高密度间隔
        Assert.NotNull(scenario.Topology);
        Assert.Equal(10, scenario.Topology.DiverterCount);
        Assert.Equal(10, scenario.Topology.ChuteCount);
    }

    [Fact]
    public void ConfigErrorScenario_IsWellDefined()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoConfigError(parcelCount: 20);

        // Assert
        Assert.NotNull(scenario);
        Assert.Equal("IO-ConfigError-IO配置错误仿真", scenario.ScenarioName);
        Assert.NotNull(scenario.Options);
        Assert.Equal(20, scenario.Options.ParcelCount);

        // 验证配置错误场景
        Assert.NotNull(scenario.FaultInjection);
        Assert.True(scenario.FaultInjection.InjectSensorFailure);
        Assert.True(scenario.FaultInjection.SensorFailureProbability > 0);
    }

    [Fact]
    public void SensorFaultOptions_IdealMode_HasExpectedDefaults()
    {
        // Arrange & Act
        var options = new SensorFaultOptions
        {
            BehaviorMode = IoBehaviorMode.Ideal
        };

        // Assert
        Assert.Equal(IoBehaviorMode.Ideal, options.BehaviorMode);
        Assert.False(options.IsEnableSensorJitter);
        Assert.Equal(0m, options.SensorLossProbability);
        Assert.Null(options.SensorDelayRangeMs);
    }

    [Fact]
    public void SensorFaultOptions_ChaosMode_CanBeConfigured()
    {
        // Arrange & Act
        var options = new SensorFaultOptions
        {
            BehaviorMode = IoBehaviorMode.Chaos,
            IsEnableSensorJitter = true,
            JitterTriggerCount = 4,
            JitterIntervalMs = 60,
            JitterProbability = 0.3m,
            SensorDelayRangeMs = (20, 150),
            SensorLossProbability = 0.1m,
            IsEnableDebouncing = true,
            DebounceWindowMs = 120
        };

        // Assert
        Assert.Equal(IoBehaviorMode.Chaos, options.BehaviorMode);
        Assert.True(options.IsEnableSensorJitter);
        Assert.Equal(4, options.JitterTriggerCount);
        Assert.Equal(60, options.JitterIntervalMs);
        Assert.Equal(0.3m, options.JitterProbability);
        Assert.NotNull(options.SensorDelayRangeMs);
        Assert.Equal((20, 150), options.SensorDelayRangeMs.Value);
        Assert.Equal(0.1m, options.SensorLossProbability);
        Assert.True(options.IsEnableDebouncing);
        Assert.Equal(120, options.DebounceWindowMs);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SensorJitterScenario_SupportsDebounceToggle(bool enableDebouncing)
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoSensorJitter(30, enableDebouncing);

        // Assert
        Assert.NotNull(scenario.Options.SensorFault);
        Assert.Equal(enableDebouncing, scenario.Options.SensorFault.IsEnableDebouncing);
    }

    [Fact]
    public void IoScenarios_HaveUniqueNames()
    {
        // Arrange
        var scenarios = new[]
        {
            ScenarioDefinitions.CreateIoSensorJitter(30, true),
            ScenarioDefinitions.CreateIoSensorJitter(30, false),
            ScenarioDefinitions.CreateIoChaosMode(50),
            ScenarioDefinitions.CreateIoStressTest(200),
            ScenarioDefinitions.CreateIoConfigError(20)
        };

        // Act
        var names = scenarios.Select(s => s.ScenarioName).ToList();

        // Assert
        Assert.Equal(scenarios.Length, names.Distinct().Count());
    }

    [Fact]
    public void IoScenarios_AllHaveDescriptions()
    {
        // Arrange
        var scenarios = new[]
        {
            ScenarioDefinitions.CreateIoSensorJitter(30, true),
            ScenarioDefinitions.CreateIoChaosMode(50),
            ScenarioDefinitions.CreateIoStressTest(200),
            ScenarioDefinitions.CreateIoConfigError(20)
        };

        // Act & Assert
        foreach (var scenario in scenarios)
        {
            Assert.NotNull(scenario.Description);
            Assert.NotEmpty(scenario.Description);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public void ChaosModeScenario_SupportsVariousParcelCounts(int parcelCount)
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoChaosMode(parcelCount);

        // Assert
        Assert.Equal(parcelCount, scenario.Options.ParcelCount);
    }

    [Theory]
    [InlineData(100)]
    [InlineData(200)]
    [InlineData(500)]
    public void StressTestScenario_SupportsHighParcelCounts(int parcelCount)
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoStressTest(parcelCount);

        // Assert
        Assert.Equal(parcelCount, scenario.Options.ParcelCount);
        Assert.True(scenario.Options.ParcelInterval.TotalMilliseconds <= 200, 
            "压力测试应使用较短的包裹间隔");
    }

    [Fact]
    public void SensorFaultOptions_DefaultDebounceSettings_AreReasonable()
    {
        // Arrange & Act
        var options = new SensorFaultOptions
        {
            IsEnableDebouncing = true
        };

        // Assert
        Assert.True(options.IsEnableDebouncing);
        Assert.Equal(100, options.DebounceWindowMs);
        Assert.True(options.DebounceWindowMs > 0, "去抖窗口应该为正值");
        Assert.True(options.DebounceWindowMs < 1000, "去抖窗口不应过长");
    }

    [Fact]
    public void ChaosModeScenario_HasRealisticJitterProbability()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoChaosMode(50);

        // Assert
        Assert.NotNull(scenario.Options.SensorFault);
        var jitterProb = scenario.Options.SensorFault.JitterProbability;
        Assert.True(jitterProb >= 0m && jitterProb <= 1m, "抖动概率应在 0-1 之间");
        Assert.True(jitterProb > 0m, "混沌模式应该有非零的抖动概率");
    }

    [Fact]
    public void ChaosModeScenario_HasRealisticLossProbability()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoChaosMode(50);

        // Assert
        Assert.NotNull(scenario.Options.SensorFault);
        var lossProb = scenario.Options.SensorFault.SensorLossProbability;
        Assert.True(lossProb >= 0m && lossProb <= 1m, "丢失概率应在 0-1 之间");
        Assert.True(lossProb < 0.2m, "丢失概率不应过高，以免影响测试可观察性");
    }

    [Fact]
    public void StressTestScenario_UsesMultipleChutes()
    {
        // Arrange & Act
        var scenario = ScenarioDefinitions.CreateIoStressTest(200);

        // Assert
        Assert.NotNull(scenario.Options.FixedChuteIds);
        Assert.True(scenario.Options.FixedChuteIds.Count >= 5, 
            "压力测试应使用多个格口以增加 IO 复杂度");
    }
}
