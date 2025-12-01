using System.Text.Json;
using System.Text.Json.Serialization;
using ScenariosLib = ZakYip.WheelDiverterSorter.Simulation.Scenarios;

namespace ZakYip.WheelDiverterSorter.Simulation.Scenarios;

/// <summary>
/// 仿真场景序列化助手
/// Simulation Scenario Serializer - Supports JSON/YAML scenario definitions
/// </summary>
/// <remarks>
/// PR-RS14: 此类使用 Simulation.Scenarios 共享库中定义的 SimulationScenario 类型。
/// </remarks>
public static class SimulationScenarioSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    /// <summary>
    /// 将场景序列化为 JSON 字符串
    /// </summary>
    public static string SerializeToJson(ScenariosLib.SimulationScenario scenario)
    {
        return JsonSerializer.Serialize(scenario, JsonOptions);
    }

    /// <summary>
    /// 从 JSON 字符串反序列化场景
    /// </summary>
    public static ScenariosLib.SimulationScenario? DeserializeFromJson(string json)
    {
        return JsonSerializer.Deserialize<ScenariosLib.SimulationScenario>(json, JsonOptions);
    }

    /// <summary>
    /// 将场景保存到 JSON 文件
    /// </summary>
    public static async Task SaveToFileAsync(ScenariosLib.SimulationScenario scenario, string filePath)
    {
        var json = SerializeToJson(scenario);
        await File.WriteAllTextAsync(filePath, json);
    }

    /// <summary>
    /// 从 JSON 文件加载场景
    /// </summary>
    public static async Task<ScenariosLib.SimulationScenario?> LoadFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath);
        return DeserializeFromJson(json);
    }
}
