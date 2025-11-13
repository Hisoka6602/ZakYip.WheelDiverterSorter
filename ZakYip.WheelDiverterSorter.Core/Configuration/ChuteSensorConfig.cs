namespace ZakYip.WheelDiverterSorter.Core.Configuration;

/// <summary>
/// 格口传感器IO配置
/// </summary>
/// <remarks>
/// 每个格口前面都有一个触发传感器，用于检测包裹到达此格口
/// </remarks>
public class ChuteSensorConfig
{
    /// <summary>
    /// 传感器ID（数字ID，与硬件设备对应）
    /// </summary>
    /// <remarks>
    /// 关联到传感器配置中的传感器标识符
    /// </remarks>
    public required int SensorId { get; set; }

    /// <summary>
    /// 传感器名称（可选）- Sensor Name (Optional)
    /// </summary>
    /// <remarks>
    /// 用于显示的友好名称，例如 "SENSOR_PE_01"、"格口1前传感器"
    /// </remarks>
    public string? SensorName { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    public string SensorType { get; set; } = "Photoelectric";

    /// <summary>
    /// IO输入位（仅硬件传感器需要）
    /// </summary>
    /// <remarks>
    /// 硬件传感器的输入端口位号，例如雷赛控制卡的输入位
    /// </remarks>
    public int? InputBit { get; set; }

    /// <summary>
    /// 是否启用此传感器
    /// </summary>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 去抖动时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止误触发的去抖动延迟时间，默认100ms
    /// </remarks>
    public int DebounceTimeMs { get; set; } = 100;
}
