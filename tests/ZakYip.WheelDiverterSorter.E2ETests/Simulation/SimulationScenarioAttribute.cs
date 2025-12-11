using ZakYip.WheelDiverterSorter.Core.LineModel.Services;
using System;

namespace ZakYip.WheelDiverterSorter.E2ETests.Simulation;

/// <summary>
/// PR-42: 仿真场景标记属性
/// 用于将测试方法与仿真场景 ID 关联
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class SimulationScenarioAttribute : Attribute
{
    /// <summary>
    /// 仿真场景唯一标识符
    /// </summary>
    public string ScenarioId { get; }

    public SimulationScenarioAttribute(string scenarioId)
    {
        if (string.IsNullOrWhiteSpace(scenarioId))
        {
            throw new ArgumentException("Scenario ID cannot be null or empty", nameof(scenarioId));
        }

        ScenarioId = scenarioId;
    }
}
