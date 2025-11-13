using System.ComponentModel.DataAnnotations;

namespace ZakYip.WheelDiverterSorter.Host.Models.Config;

/// <summary>
/// 格口传感器配置请求模型
/// </summary>
public class ChuteSensorConfigRequest
{
    /// <summary>
    /// 传感器ID
    /// </summary>
    /// <remarks>
    /// 关联到传感器配置中的传感器标识符
    /// </remarks>
    /// <example>1</example>
    [Required(ErrorMessage = "传感器ID不能为空")]
    public required int SensorId { get; set; }

    /// <summary>
    /// 传感器类型 (Photoelectric/Laser)
    /// </summary>
    /// <example>Photoelectric</example>
    public string SensorType { get; set; } = "Photoelectric";

    /// <summary>
    /// IO输入位（仅硬件传感器需要）
    /// </summary>
    /// <remarks>
    /// 硬件传感器的输入端口位号，例如雷赛控制卡的输入位
    /// </remarks>
    /// <example>5</example>
    public int? InputBit { get; set; }

    /// <summary>
    /// 是否启用此传感器
    /// </summary>
    /// <example>true</example>
    public bool IsEnabled { get; set; } = true;

    /// <summary>
    /// 去抖动时间（毫秒）
    /// </summary>
    /// <remarks>
    /// 防止误触发的去抖动延迟时间，默认100ms
    /// </remarks>
    /// <example>100</example>
    public int DebounceTimeMs { get; set; } = 100;
}
