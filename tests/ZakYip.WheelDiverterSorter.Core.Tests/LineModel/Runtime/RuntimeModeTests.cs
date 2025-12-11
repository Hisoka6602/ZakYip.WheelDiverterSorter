using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using ZakYip.WheelDiverterSorter.Core.Enums.System;
using Xunit;

namespace ZakYip.WheelDiverterSorter.Core.Tests.LineModel.Runtime;

/// <summary>
/// 运行模式枚举测试
/// Tests for RuntimeMode enum
/// </summary>
public class RuntimeModeTests
{
    [Fact]
    public void RuntimeMode_Production_HasCorrectValue()
    {
        // Assert
        Assert.Equal(0, (int)RuntimeMode.Production);
    }

    [Fact]
    public void RuntimeMode_Simulation_HasCorrectValue()
    {
        // Assert
        Assert.Equal(1, (int)RuntimeMode.Simulation);
    }

    [Fact]
    public void RuntimeMode_PerformanceTest_HasCorrectValue()
    {
        // Assert
        Assert.Equal(2, (int)RuntimeMode.PerformanceTest);
    }

    [Theory]
    [InlineData("Production", RuntimeMode.Production)]
    [InlineData("Simulation", RuntimeMode.Simulation)]
    [InlineData("PerformanceTest", RuntimeMode.PerformanceTest)]
    [InlineData("production", RuntimeMode.Production)]
    [InlineData("SIMULATION", RuntimeMode.Simulation)]
    public void RuntimeMode_CanBeParsedFromString(string input, RuntimeMode expected)
    {
        // Act
        var result = Enum.Parse<RuntimeMode>(input, ignoreCase: true);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RuntimeMode_HasAllExpectedValues()
    {
        // Assert
        var values = Enum.GetValues<RuntimeMode>();
        Assert.Equal(3, values.Length);
        Assert.Contains(RuntimeMode.Production, values);
        Assert.Contains(RuntimeMode.Simulation, values);
        Assert.Contains(RuntimeMode.PerformanceTest, values);
    }
}
